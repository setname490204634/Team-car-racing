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

def wait_for_port(host: str, port: int, timeout=20):
    """Wait until a TCP port is open."""
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

def is_port_free(port: int) -> bool:
    """Check if a TCP port is available."""
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
        s.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        try:
            s.bind(("127.0.0.1", port))
        except OSError:
            return False
    return True


class UnityCarEnv(gym.Env):
    def __init__(self,
                 unity_exe_path: str = r"TeamRacing\builds\TeamRacing.exe",
                 control_port: int = 5005,
                 car_instr_port: int = 5006,
                 obs_port: int = 5007,
                 run_headless: bool = False):
        super().__init__()

        self.observation_space = spaces.Box(
            low=0.0, high=1.0,
            shape=(12, 64, 128),
            dtype=np.float32
        )

        self.action_space = spaces.Box(low=-1.0, high=1.0, shape=(2,), dtype=np.float32)

        #ports
        self.control_port = control_port
        self.car_instr_port = car_instr_port
        self.obs_port = obs_port
        #unity
        self.unity_exe_path = unity_exe_path
        self.unity_process = None
        self.run_headless = run_headless
        
        self.episode_rewards_per_category = Rewards()
        self.mapSwitchCount = 0
        #will change map every x resets
        self.changeMapEvery = 10
        #limits the pool of maps. easy maps have lower index
        self.maxMapIndex = 6
        
        self.stack_size = 4
        self.frame_buffer = np.zeros((self.stack_size, 64, 128, 3), dtype=np.uint8)
        
        self.episodeCount = 0
        self.current_step = 0
        self.max_steps = 400
        self.episode_reward = 0.0

        self._launch_unity()

        wait_for_port("127.0.0.1", self.control_port)
        wait_for_port("127.0.0.1", self.car_instr_port)

        self.obs_receiver = ObservationReceiver(host="0.0.0.0", port=self.obs_port)
        self.obs_receiver.start()

        self.sendCommandToUnity(CommandCode.ChangeMap)
        if run_headless:
            self.sendCommandToUnity(CommandCode.UnlimitedSpeed)
        self.sendCommandToUnity(CommandCode.SetMaxSteeringChange, 6)

        
    def _update_frame_stack(self, new_frame):
        self.frame_buffer[:-1] = self.frame_buffer[1:]
        self.frame_buffer[-1] = new_frame
        
    def sendCommandToUnity(self, command, value =0 ):
        sender.send_command(command, value, self.control_port)

    def _launch_unity(self):
        #return #uncomment for manual unity launch, then default ports are expected
        if not os.path.exists(self.unity_exe_path):
            raise FileNotFoundError(f"Unity executable not found: {self.unity_exe_path}")

        while True:
            free_control = is_port_free(self.control_port)
            free_car_instr = is_port_free(self.car_instr_port)
            free_obs = is_port_free(self.obs_port)
 
            if free_control and free_car_instr and free_obs:
                break  # all good

            print(f"Ports [{self.control_port}, {self.car_instr_port}, {self.obs_port}] "
                f"in use. Shifting +3 and retrying...")

            self.control_port += 3
            self.car_instr_port += 3
            self.obs_port += 3
            
        args = [
            self.unity_exe_path,
            str(self.control_port),
            str(self.car_instr_port),
            str(self.obs_port)
        ]
        if (self.run_headless):
            args.append("-batchmode")
        print(f"Launching Unity: {' '.join(args)}")
        self.unity_process = subprocess.Popen(
            args,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            creationflags=subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0
        )

    def _build_observation(self, obs_packet):
        stacked = self.frame_buffer.reshape(64, 128, 12)
        stacked = np.transpose(stacked, (2, 0, 1))
        obs = stacked.astype(np.float32) / 255.0
        return obs

    def reset(self, **kwargs):
        """Reset Unity and return initial observation."""
        self.mapSwitchCount += 1
        self.sendCommandToUnity(CommandCode.StopSimulation)

        if self.mapSwitchCount % self.changeMapEvery == 0:
            self.sendCommandToUnity(CommandCode.ChangeMap, random.randint(0, self.maxMapIndex))

        time.sleep(0.03)
        self.sendCommandToUnity(CommandCode.ResetCarToRandomStartLocation)
        self.sendCommandToUnity(CommandCode.ChangeCarColoursRandomly)
        self.sendCommandToUnity(CommandCode.StartSimulation)

        while not self.obs_receiver.has_min_observations(1):
            time.sleep(0.0001)

        self.episode_rewards_per_category = Rewards()  # reset at the start of the episode
        
        obs_packet = self.obs_receiver.collect_observations()[-1]
        self.current_step = 0
        self.episode_reward = 0.0
        
        rgb = obs_packet.image  # shape (64,128,3)
        rgb = cv2.flip(rgb, 0)

        # fill stack with the first frame
        for i in range(self.stack_size):
            self.frame_buffer[i] = rgb

        return self._build_observation(obs_packet), {}

    def step(self, action):

        steer_cmd = int((np.clip(action[0], -1, 1) + 1) * 127.5)
        throttle_cmd = int((np.clip(action[1], -1, 1) + 1) * 127.5)
        sender.send_car_instruction(0, steer_cmd, throttle_cmd, self.car_instr_port)

        # Wait for observation
        while not self.obs_receiver.has_min_observations(1):
            time.sleep(0.0001)

        obs_packet = self.obs_receiver.collect_observations()[-1]
        
        rgb = obs_packet.image
        
        # observation check
        # bgr = cv2.cvtColor(rgb, cv2.COLOR_RGB2BGR)
        # bgr = cv2.flip(bgr, 0)
        # bgr = cv2.resize(
        #     bgr,
        #     None,
        #     fx=4,
        #     fy=4,
        #     interpolation=cv2.INTER_LINEAR
        # )
        # cv2.imshow("Unity Observation", bgr)
        # cv2.waitKey(1)
        
                
        self._update_frame_stack(rgb)

        reward = float(np.dot(obs_packet.rewards.as_vector(), Rewards.defaultWeights().as_vector()))
        # Update the per-category sums (unweighted)
        for field in vars(obs_packet.rewards):
            current_value = getattr(obs_packet.rewards, field)
            prev_sum = getattr(self.episode_rewards_per_category, field)
            setattr(self.episode_rewards_per_category, field, prev_sum + current_value)

        self.current_step += 1

        truncated = False
        terminated = False
        
        # episode too long
        if self.current_step >= self.max_steps:
            truncated = True
            
        #bug prevention
        if obs_packet.rewards.outOfBoundsPenalty < -0.5:
            terminated = True
            reward = 0
            
        if obs_packet.rewards.collisionPenalty < -0.5:
            terminated = True
            
        self.episode_reward += reward

        info = {}
        
        if terminated or truncated:
            print(self.episode_reward)

            info = {
                "episode": {
                    "r": self.episode_reward,
                    "l": self.current_step,
                    "rewards": vars(self.episode_rewards_per_category).copy()
                }
            }

            self.current_step = 0
            self.episode_reward = 0.0
            self.episode_rewards_per_category = Rewards()
            self.episodeCount += 1

        obs = self._build_observation(obs_packet)
        return obs, reward, terminated, truncated, info

    def close(self):
        if self.unity_process:
            print("Closing Unity process...")
            self.unity_process.terminate()
            try:
                self.unity_process.wait(timeout=5)
            except subprocess.TimeoutExpired:
                self.unity_process.kill()
            self.unity_process = None

        obs_receiver = getattr(self, "obs_receiver", None)
        if obs_receiver:
            obs_receiver.stop()
            self.obs_receiver = None
            
    def __del__(self):
        self.close()
