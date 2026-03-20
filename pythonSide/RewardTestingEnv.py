from unityEnv.UnitySingleCarEnv2 import UnityCarEnv
import numpy as np
import keyboard
import time

class HumanWSADAgent:
    def __init__(self, steer_scale=1.0, throttle_scale=1.0):
        self.steer_scale = steer_scale
        self.throttle_scale = throttle_scale

    def get_action(self):
        # Default to zero
        steer = 0.0
        throttle = 0.0

        # Throttle
        if keyboard.is_pressed("w"):
            throttle += self.throttle_scale
        if keyboard.is_pressed("s"):
            throttle -= self.throttle_scale

        # Steering
        if keyboard.is_pressed("a"):
            steer -= self.steer_scale
        if keyboard.is_pressed("d"):
            steer += self.steer_scale

        # Clip to [-1,1]
        steer = np.clip(steer, -1, 1)
        throttle = np.clip(throttle, -1, 1)

        # Quit
        if keyboard.is_pressed("q"):
            print("Exiting human agent...")
            exit()

        return np.array([steer, throttle], dtype=np.float32)


env = UnityCarEnv(run_headless=False, debug=True)
human_agent = HumanWSADAgent()

obs, _ = env.reset()

try:
    while True:
        action = human_agent.get_action()
        obs, reward, terminated, truncated, info = env.step(action)
        if terminated or truncated:
            obs, _ = env.reset()
finally:
    env.close()