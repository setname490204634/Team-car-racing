import numpy as np
import cv2
from .rewards import Rewards

class agent:
    def __init__(
        self,
        id: str,
        unity_car_id: int,
        stack_size: int = 4,
        img_shape = (64, 128, 3),
        debug: bool = False,
        rewardMul: Rewards = Rewards.defaultWeights()
    ):
        # names
        self.agent_id = id
        self.unity_car_id = unity_car_id

        # config
        self.stack_size = stack_size
        self.image_shape = img_shape
        self.debug = debug

        # frame stack
        self.frame_buffer = np.zeros(
            (stack_size, *img_shape),
            dtype=np.uint8
        )

        # rewards
        self.episode_rewards_per_category = Rewards()
        self.episode_reward = 0.0
        self.rewardMul = rewardMul

        # step tracking
        self.current_step = 0

        # termination state
        self.terminated = False
        self.truncated = False
        
    def encode_action(self, action):
        steer_cmd = int((np.clip(action[0], -1, 1) + 1) * 127.5)
        throttle_cmd = int((np.clip(action[1], -1, 1) + 1) * 127.5)

        return steer_cmd, throttle_cmd
    
    def update_frame_stack(self, frame):
        np.roll(self.frame_buffer, -1, axis=0)
        self.frame_buffer[-1] = frame
        
    def initFrameStack(self, rgb):
        for i in range(self.stack_size):
            self.frame_buffer[i] = rgb
        
    def _build_observation(self):
        stacked = self.frame_buffer.reshape(64, 128, 12)
        stacked = np.transpose(stacked, (2, 0, 1))
        obs = stacked.astype(np.float32) / 255.0

        return obs
    
    def get_observation(self):
        return self._build_observation()
    
    def compute_reward(self, rewards_packet):

        speed = rewards_packet.speedReward
        rewards_packet.collisionPenalty *= speed

        reward = float(
            np.dot(
                rewards_packet.as_vector(),
                self.rewardMul.as_vector()
            )
        )

        for field in vars(rewards_packet):

            current_value = getattr(rewards_packet, field)
            prev_sum = getattr(self.episode_rewards_per_category, field)

            setattr(
                self.episode_rewards_per_category,
                field,
                prev_sum + current_value
            )

        self.episode_reward += reward
        return reward
    
    def update_termination(self, rewards_packet):

        self.current_step += 1

        self.terminated = False
        self.truncated = False

        if self.current_step >= self.max_steps:
            self.truncated = True

        if rewards_packet.outOfBoundsPenalty < -0.5:
            self.terminated = True

        if rewards_packet.collisionPenalty != 0.0:
            ...
            # self.terminated = True

        return self.terminated, self.truncated
    
    def update_from_packet(self, obs_packet):

        rgb = obs_packet.image

        if self.debug:
            bgr = cv2.cvtColor(rgb, cv2.COLOR_RGB2BGR)
            bgr = cv2.flip(bgr, 0)
            bgr = cv2.resize(
                bgr,
                None,
                fx=4,
                fy=4,
                interpolation=cv2.INTER_LINEAR
            )

            cv2.imshow(f"Unity Observation {self.agent_id}", bgr)
            cv2.waitKey(1)

        self.update_frame_stack(rgb)

        reward = self.compute_reward(obs_packet.rewards)

        terminated, truncated = self.update_termination(obs_packet.rewards)

        obs = self.get_observation()

        return obs, reward, terminated, truncated
    
    def build_episode_info(self):

        return {
            "r": self.episode_reward,
            "l": self.current_step,
            "rewards": vars(self.episode_rewards_per_category).copy()
        }

    def __repr__(self):

        return (
            f"Agent(id={self.agent_id}, "
            f"step={self.current_step}, "
            f"reward={self.episode_reward:.3f})"
        )