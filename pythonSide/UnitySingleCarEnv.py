import gymnasium as gym
from gymnasium import spaces
import numpy as np
import time
import sender
from reciever import ObservationReceiver
import subprocess
import socket
import os
import sys
from rewards import *


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


class UnityCarEnv(gym.Env):
    """Unity Car environment for PPO training and inference."""

    def __init__(self,
                 unity_exe_path: str = r"TeamRacing\builds\TeamRacing.exe",
                 control_port: int = 5005,
                 car_instr_port: int = 5006,
                 obs_port: int = 5007,
                 run_headless: bool = False):
        super().__init__()

        # --- Observation Space ---
        self.observation_space = spaces.Dict({
            "image": spaces.Box(low=0, high=255, shape=(64, 128, 3), dtype=np.uint8),
            "speed": spaces.Box(low=0.0, high=1.0, shape=(1,), dtype=np.float32),
            "prev_action": spaces.Box(low=-1.0, high=1.0, shape=(2,), dtype=np.float32)
        })

        # --- Action Space ---
        self.action_space = spaces.Box(low=-1.0, high=1.0, shape=(2,), dtype=np.float32)

        self.control_port = control_port
        self.car_instr_port = car_instr_port
        self.obs_port = obs_port
        self.unity_exe_path = unity_exe_path
        self.unity_process = None
        self.run_headless = run_headless

        # --- Start Unity headless executable ---
        self._launch_unity()

        # --- Wait until Unity starts and opens its ports ---
        wait_for_port("127.0.0.1", self.control_port)
        wait_for_port("127.0.0.1", self.car_instr_port)

        # --- Start the receiver ---
        self.obs_receiver = ObservationReceiver(host="0.0.0.0", port=self.obs_port)
        self.obs_receiver.start()

        # --- Initialize communication ---
        sender.send_command(31, 0, self.control_port)  # simulation speed: unlimited

        # --- Internal state ---
        self.current_step = 0
        self.max_steps = 500
        self.episode_reward = 0.0
        self.prev_action = np.zeros(self.action_space.shape, dtype=np.float32)
        self.filtered_action = np.zeros(self.action_space.shape, dtype=np.float32)
        self.filter_alpha = 1.0

    def _launch_unity(self):
        # return #uncoment for manual unity launch
        """Launch Unity executable with port arguments."""
        if not os.path.exists(self.unity_exe_path):
            raise FileNotFoundError(f"Unity executable not found: {self.unity_exe_path}")

        # Pass ports as command-line args
        args = [
            self.unity_exe_path,
            str(self.control_port),
            str(self.car_instr_port),
            str(self.obs_port)
        ]
        if (self.run_headless):
            args.append("-batchmode")# headless mode (no graphics)
            # args.append("-nographics")# no graphics rendering)

        print(f"Launching Unity: {' '.join(args)}")
        self.unity_process = subprocess.Popen(
            args,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            creationflags=subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0
        )

    def _build_observation(self, obs_packet):
        """Convert Unity observation packet to Gym-style observation dict."""
        obs = {
            "image": obs_packet.image,
            "speed": np.array([obs_packet.speed / 255.0], dtype=np.float32),
            "prev_action": self.prev_action.astype(np.float32)
        }
        return obs

    def reset(self, **kwargs):
        """Reset Unity and return initial observation."""
        sender.send_command(11, 0, self.control_port)  # stop
        sender.send_command(0, 0, self.control_port)   # reset cars
        sender.send_command(10, 0, self.control_port)  # start

        while not self.obs_receiver.has_min_observations(1):
            time.sleep(0.0001)

        obs_packet = self.obs_receiver.collect_observations()[-1]
        self.current_step = 0
        self.episode_reward = 0.0
        self.prev_action[:] = 0.0
        self.filtered_action[:] = 0.0

        return self._build_observation(obs_packet), {}

    def step(self, action):
        """Send action to Unity and receive next observation."""
        # Smooth control
        self.filtered_action = (
            self.filter_alpha * action + (1 - self.filter_alpha) * self.filtered_action
        )

        steer_cmd = int((np.clip(self.filtered_action[0], -1, 1) + 1) * 127.5)
        throttle_cmd = int((np.clip(self.filtered_action[1], -1, 1) + 1) * 127.5)
        sender.send_car_instruction(0, steer_cmd, throttle_cmd, self.car_instr_port)

        # Wait for observation
        while not self.obs_receiver.has_min_observations(1):
            time.sleep(0.0001)

        obs_packet = self.obs_receiver.collect_observations()[-1]

        # --- Handle reward vector ---
        reward = float(np.dot(obs_packet.rewards.as_vector(), RewardWeights().as_vector()))
        # print(obs_packet.rewards)
        reward = np.clip(reward, -100, 100)
        print(reward)

        # --- Update state ---
        self.prev_action = np.clip(action, -1.0, 1.0)
        self.current_step += 1
        self.episode_reward += reward

        truncated = False
        done = self.current_step >= self.max_steps

        # crashed
        if obs_packet.rewards.collision_penalty == -1:
            truncated = True
            done = True

        info = {}
        if done:
            print(self.episode_reward)
            info = {"episode": {"r": self.episode_reward, "l": self.current_step}}
            self.current_step = 0
            self.episode_reward = 0.0

        obs = self._build_observation(obs_packet)
        return obs, reward, done, truncated, info

    # ------------------------------------------------------------------
    def close(self):
        """Clean up Unity process and receiver."""

        if self.unity_process:
            print("Closing Unity process...")
            self.unity_process.terminate()
            try:
                self.unity_process.wait(timeout=5)
            except subprocess.TimeoutExpired:
                self.unity_process.kill()
            self.unity_process = None

        if self.obs_receiver:
            self.obs_receiver.stop()
            self.obs_receiver = None
            
    def __del__(self):
        self.close()
