import gymnasium as gym
from gymnasium import spaces
import numpy as np
import time
from . import sender
from .reciever import ObservationReceiver
import subprocess
import socket
import os
from .rewards import *
import cv2
import random
from .CommandConstants import CommandCode
from torch.utils.tensorboard import SummaryWriter
from datetime import datetime


def wait_for_port(host: str, port: int, timeout=20):
    start = time.time()
    while time.time() - start < timeout:
        try:
            with socket.create_connection((host, port), timeout=1):
                print(f"Unity is ready on port {port}")
                return True
        except (ConnectionRefusedError, OSError):
            time.sleep(0.5)
    print(f"Timeout: Unity did not open port {port} in {timeout} seconds.")
    return False


def get_os_assigned_port():
    s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    s.bind(("127.0.0.1", 0))
    port = s.getsockname()[1]
    return s, port


class MultiAgentUnityCarEnv(gym.Env):
    metadata = {"render_modes": ["human"]}

    def __init__(self, num_agents=2,
                 unity_exe_path=r"TeamRacing\builds\TeamRacing.exe",
                 log_dir=r"pythonSide\env_logs",
                 debug=False,
                 run_headless=False):
        super().__init__()

        self.num_agents = num_agents
        self.agents = [f"agent_{i}" for i in range(num_agents)]

        # Each agent observation and action
        self.observation_space = spaces.Box(
            low=0.0, high=1.0,
            shape=(12, 64, 128),
            dtype=np.float32
        )
        self.action_space = spaces.Box(low=-1.0, high=1.0, shape=(2,), dtype=np.float32)

        # Ports for Unity communication
        self._control_sock, self.control_port = get_os_assigned_port()
        self._car_sock, self.car_instr_port = get_os_assigned_port()
        self._obs_sock, self.obs_port = get_os_assigned_port()

        # Unity process
        self.unity_exe_path = unity_exe_path
        self.unity_process = None
        self.run_headless = run_headless

        # Multi-agent state
        self.stack_size = 4
        self.frame_buffer = {agent: np.zeros((self.stack_size, 64, 128, 3), dtype=np.uint8) 
                             for agent in self.agents}
        self.episode_rewards_per_agent = {agent: Rewards() for agent in self.agents}
        self.current_step_per_agent = {agent: 0 for agent in self.agents}
        self.episode_reward_per_agent = {agent: 0.0 for agent in self.agents}
        self.max_steps = 400
        self.episodeCount = 0

        # Logging
        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        self.tb_writer = SummaryWriter(
            log_dir=os.path.join(log_dir, f"env_{timestamp}")
        )

        self.debug = debug

        self._launch_unity()
        wait_for_port("127.0.0.1", self.control_port)
        wait_for_port("127.0.0.1", self.car_instr_port)

        self.obs_receiver = ObservationReceiver(host="0.0.0.0", port=self.obs_port)
        self.obs_receiver.start()

        self.sendCommandToUnity(CommandCode.ChangeMap, 4)
        if run_headless:
            self.sendCommandToUnity(CommandCode.UnlimitedSpeed)
        self.sendCommandToUnity(CommandCode.SetMaxSteeringChange, 6)

    def _update_frame_stack(self, agent, new_frame):
        np.roll(self.frame_buffer[agent], -1, axis=0)
        self.frame_buffer[agent][-1] = new_frame

    def sendCommandToUnity(self, command, value=0):
        sender.send_command(command, value, self.control_port)

    def _launch_unity(self):
        if not os.path.exists(self.unity_exe_path):
            raise FileNotFoundError(f"Unity executable not found: {self.unity_exe_path}")

        args = [
            self.unity_exe_path,
            "--controlPort", str(self.control_port),
            "--carInstructionsPort", str(self.car_instr_port),
            "--observationPort", str(self.obs_port)
        ]
        if self.run_headless:
            args.append("-batchmode")

        print(f"Launching Unity: {' '.join(args)}")
        self.unity_process = subprocess.Popen(
            args,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            creationflags=subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0
        )

    def _build_observation(self, agent):
        stacked = self.frame_buffer[agent].reshape(64, 128, 12)
        stacked = np.transpose(stacked, (2, 0, 1))
        obs = stacked.astype(np.float32) / 255.0
        return obs

    def reset(self, **kwargs):
        self.sendCommandToUnity(CommandCode.StopSimulation)
        time.sleep(0.03)
        self.sendCommandToUnity(CommandCode.ResetCarToRandomStartLocation)
        self.sendCommandToUnity(CommandCode.ChangeCarColoursRandomly)
        self.sendCommandToUnity(CommandCode.StartSimulation)

        # Wait for at least 1 observation per agent
        while not self.obs_receiver.has_min_observations(self.num_agents):
            time.sleep(0.001)

        obs_packets = self.obs_receiver.collect_observations()[-self.num_agents:]

        obs_dict = {}
        for i, agent in enumerate(self.agents):
            rgb = obs_packets[i].image
            rgb = cv2.flip(rgb, 0)
            # fill stack
            for j in range(self.stack_size):
                self.frame_buffer[agent][j] = rgb
            obs_dict[agent] = self._build_observation(agent)
            self.episode_rewards_per_agent[agent] = Rewards()
            self.current_step_per_agent[agent] = 0
            self.episode_reward_per_agent[agent] = 0.0

        return obs_dict, {}

    def step(self, action_dict):
        obs_dict, rewards, dones, infos = {}, {}, {}, {}

        # send actions to Unity
        for i, agent in enumerate(self.agents):
            action = action_dict[agent]
            steer_cmd = int((np.clip(action[0], -1, 1) + 1) * 127.5)
            throttle_cmd = int((np.clip(action[1], -1, 1) + 1) * 127.5)
            sender.send_car_instruction(i, steer_cmd, throttle_cmd, self.car_instr_port)

        # Wait for observation
        while not self.obs_receiver.has_min_observations(self.num_agents):
            time.sleep(0.001)

        obs_packets = self.obs_receiver.collect_observations()[-self.num_agents:]

        for i, agent in enumerate(self.agents):
            obs_packet = obs_packets[i]
            rgb = obs_packet.image

            if self.debug:
                bgr = cv2.cvtColor(rgb, cv2.COLOR_RGB2BGR)
                bgr = cv2.flip(bgr, 0)
                bgr = cv2.resize(bgr, None, fx=4, fy=4, interpolation=cv2.INTER_LINEAR)
                cv2.imshow(f"Unity Observation {agent}", bgr)
                cv2.waitKey(1)

            self._update_frame_stack(agent, rgb)

            reward = float(np.dot(obs_packet.rewards.as_vector(), Rewards.defaultWeights().as_vector()))
            # Update per-agent sums
            for field in vars(obs_packet.rewards):
                current_value = getattr(obs_packet.rewards, field)
                prev_sum = getattr(self.episode_rewards_per_agent[agent], field)
                setattr(self.episode_rewards_per_agent[agent], field, prev_sum + current_value)

            self.current_step_per_agent[agent] += 1
            self.episode_reward_per_agent[agent] += reward

            truncated = self.current_step_per_agent[agent] >= self.max_steps
            terminated = obs_packet.rewards.outOfBoundsPenalty < -0.5 or obs_packet.rewards.collisionPenalty < -0.5

            obs_dict[agent] = self._build_observation(agent)
            rewards[agent] = reward
            dones[agent] = terminated or truncated
            infos[agent] = {"episode": {
                "r": self.episode_reward_per_agent[agent],
                "l": self.current_step_per_agent[agent],
                "rewards": vars(self.episode_rewards_per_agent[agent]).copy()
            }} if dones[agent] else {}

        dones["__all__"] = all(dones.values())
        return obs_dict, rewards, dones, infos

    def close(self):
        if self.unity_process:
            self.unity_process.terminate()
            try:
                self.unity_process.wait(timeout=5)
            except subprocess.TimeoutExpired:
                self.unity_process.kill()
            self.unity_process = None

        if hasattr(self, "obs_receiver") and self.obs_receiver:
            self.obs_receiver.stop()
            self.obs_receiver = None

        if hasattr(self, "tb_writer"):
            self.tb_writer.flush()
            self.tb_writer.close()

    def __del__(self):
        self.close()