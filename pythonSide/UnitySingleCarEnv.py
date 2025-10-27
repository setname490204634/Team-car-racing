import gymnasium as gym
from gymnasium import spaces
import numpy as np
import time
import sender
from reciever import ObservationReceiver


class UnityCarEnv(gym.Env):
    """Unity Car environment for PPO training and inference."""

    def __init__(self):
        super().__init__()

        # --- Observation Space: image + speed + previous action ---
        self.observation_space = spaces.Dict({
            "image": spaces.Box(low=0, high=255, shape=(64, 128, 3), dtype=np.uint8),
            "speed": spaces.Box(low=0.0, high=1.0, shape=(1,), dtype=np.float32),
            "prev_action": spaces.Box(low=-1.0, high=1.0, shape=(2,), dtype=np.float32)
        })

        # --- Action Space: [steer, throttle] ---
        self.action_space = spaces.Box(low=-1.0, high=1.0, shape=(2,), dtype=np.float32)

        # --- Unity communication ---
        self.obs_receiver = ObservationReceiver()
        self.obs_receiver.start()

        sender.send_command(31, 0)  # simulation speed: unlimited
        sender.send_command(21, 5)  # send observations every frame

        # --- Internal state ---
        self.current_step = 0
        self.max_steps = 1200
        self.episode_reward = 0.0
        self.prev_action = np.zeros(self.action_space.shape, dtype=np.float32)
        self.filtered_action = np.zeros(self.action_space.shape, dtype=np.float32)
        self.filter_alpha = 0.6  # action smoothing coefficient (0 = no smoothing)


    def _build_observation(self, obs_packet):
        """Convert Unity observation packet to Gym-style observation dict."""
        return {
            "image": obs_packet.image,
            "speed": np.array([obs_packet.speed / 255.0], dtype=np.float32),
            "prev_action": self.prev_action.astype(np.float32)
        }


    def reset(self, **kwargs):
        """Reset the Unity environment and return the first observation."""
        sender.send_command(11, 0)  # stop
        sender.send_command(0, 0)   # reset cars
        sender.send_command(10, 0)  # start

        while not self.obs_receiver.has_min_observations(1):
            time.sleep(0.01)

        obs_packet = self.obs_receiver.collect_observations()[-1]
        self.current_step = 0
        self.episode_reward = 0.0
        self.prev_action[:] = 0.0
        self.filtered_action[:] = 0.0
        return self._build_observation(obs_packet), {}


    def step(self, action):
        """Send action to Unity, get next observation, reward, and done flag."""

        # --- Smooth the control signal (low-pass filter) ---
        self.filtered_action = (
            self.filter_alpha * action + (1 - self.filter_alpha) * self.filtered_action
        )
        steer_cmd = int((np.clip(self.filtered_action[0], -1, 1) + 1) * 127.5)
        throttle_cmd = int((np.clip(self.filtered_action[1], -1, 1) + 1) * 127.5)
        sender.send_car_instruction(0, steer_cmd, throttle_cmd)

        # --- Wait for next observation ---
        while not self.obs_receiver.has_min_observations(1):
            time.sleep(0.01)

        obs_packet = self.obs_receiver.collect_observations()[-1]

        # --- Reward shaping ---
        speed = obs_packet.speed / 255.0
        delta_action = np.sum(np.abs(action - self.prev_action))

        # Positive reward for moving fast, small penalty for jerky input
        reward = (speed * 1.0) - (delta_action * 0.05)
        reward = float(np.clip(reward, -5.0, 5.0))  # stabilize PPO training

        # --- Update step counters ---
        self.prev_action = np.clip(action, -1.0, 1.0)
        self.current_step += 1
        self.episode_reward += reward

        done = self.current_step >= self.max_steps
        info = {}
        if done:
            info = {"episode": {"r": self.episode_reward, "l": self.current_step}}
            self.current_step = 0
            self.episode_reward = 0.0

        obs = self._build_observation(obs_packet)
        return obs, reward, done, False, info  # truncated=False

    def close(self):
        if self.obs_receiver is not None:
            self.obs_receiver.stop()
            self.obs_receiver = None
    def __del__(self):
        self.close()