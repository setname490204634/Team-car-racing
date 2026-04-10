import numpy as np
import cv2
from .rewards import Rewards
from torch.utils.tensorboard import SummaryWriter
import os
import matplotlib.pyplot as plt

REWARDDEBUG = False
REWARDDEBUGWEIGHTED = False

class agent:
    def __init__(
        self,
        id: str,
        unity_car_id: int,
        logdir,
        maxSteps: int,
        stack_size: int = 4,
        img_shape = (64, 128, 3),
        debug: bool = False,
        rewardMul: Rewards = Rewards.defaultWeights(),
        fatalCollision: bool = False,
        grayScaleHisotry: bool = True
    ):
        # names
        self.agent_id = id
        self.unity_car_id = unity_car_id
        self.log_file_name = f"carLog_{unity_car_id}"
        self.logdir = os.path.join(logdir, self.log_file_name)

        # config
        self.stack_size = stack_size
        self.image_shape = img_shape
        self.debug = debug
        self.showCouter = 0
        self.grayScaleHisotry = grayScaleHisotry

        # frame stack
        self.frame_buffer = np.zeros(
            (stack_size, *img_shape),
            dtype=np.uint8
        )

        # rewards
        self.episode_rewards_per_category = Rewards()
        self.weighted_rewards = Rewards()
        self.episode_reward = 0.0
        self.rewardMul = rewardMul

        # step tracking
        self.current_step = 0
        self.max_steps = maxSteps

        # termination state
        self.fatalCollision = fatalCollision
        self.terminated = False
        self.truncated = False

        # debug plotting
        self.plot_rewards = [
            "progressReward",
            "speedRewardV",
            "collisionPenalty",
            "grassPenalty"
        ]

        self.reward_history = {name: [] for name in self.plot_rewards}
        self.time_steps = []

        self.plot_initialized = False
        
    def encode_action(self, action):
        steer_cmd = int((np.clip(action[0], -1, 1) + 1) * 127.5)
        throttle_cmd = int((np.clip(action[1], -1, 1) + 1) * 127.5)

        return steer_cmd, throttle_cmd
    
    def update_frame_stack(self, frame):
        self.frame_buffer = np.roll(self.frame_buffer, -1, axis=0)
        self.frame_buffer[-1] = frame
        
    def initFrameStack(self, rgb):
        for i in range(self.stack_size):
            self.frame_buffer[i] = rgb
        
    def _build_observation(self):
        stacked = np.concatenate(self.frame_buffer, axis=2)
        stacked = np.transpose(stacked, (2, 0, 1))
        obs = stacked.astype(np.float32) / 255.0

        return obs
    
    def _buildObservationGrayscaleStack(self):
        frames = []

        # newest RGB
        frames.append(self.frame_buffer[-1])

        # grayscale
        for i in range(self.stack_size - 2, -1, -1):
            gray = cv2.cvtColor(self.frame_buffer[i], cv2.COLOR_RGB2GRAY)
            gray = np.expand_dims(gray, axis=2)
            frames.append(gray)

        stacked = np.concatenate(frames, axis=2)
        stacked = np.transpose(stacked, (2, 0, 1))
        return stacked.astype(np.float32) / 255.0
            
    def get_observation(self):
        if self.grayScaleHisotry:
            return self._buildObservationGrayscaleStack()
        else:
            return self._build_observation()
    
    def compute_reward(self, rewards_packet):
        rewards_packet.collisionPenalty

        reward = float(
            np.dot(
                rewards_packet.as_vector(),
                self.rewardMul.as_vector()
            )
        )

        for field in vars(rewards_packet):

            current_value = getattr(rewards_packet, field)
            prev_sum = getattr(self.episode_rewards_per_category, field)

            # accumulate raw rewards
            setattr(
                self.episode_rewards_per_category,
                field,
                prev_sum + current_value
            )

            # accumulate weighted rewards
            weight = getattr(self.rewardMul, field)
            prev_weighted = getattr(self.weighted_rewards, field)

            setattr(
                self.weighted_rewards,
                field,
                prev_weighted + current_value * weight
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

        if rewards_packet.collisionPenalty != 0.0 and self.fatalCollision:
            self.terminated = True

        return self.terminated, self.truncated
    
    def update_from_packet(self, obs_packet):

        rgb = obs_packet.image

        self.update_frame_stack(rgb)

        reward = self.compute_reward(obs_packet.rewards)
        
        #debug
        self.showCouter += 1
        self.showCouter %= 5
        if self.debug and self.agent_id == "agent_0" and self.showCouter == 0:
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
            
            if REWARDDEBUG:
                self._update_plot(obs_packet.rewards)

        terminated, truncated = self.update_termination(obs_packet.rewards)

        obs = self.get_observation()

        return obs, reward, terminated, truncated
    
    def build_episode_info(self):
        info = {
            "episode_reward": self.episode_reward,
            "episode_length": self.current_step,
            "raw_rewards": self.episode_rewards_per_category,
            "weighted_rewards": self.weighted_rewards
        }
        return info
        
    def logEpisode(self, step):
        info = self.build_episode_info()

        # Initialize TensorBoard writer if not yet done
        if not hasattr(self, "tb_writer"):
            self.tb_writer = SummaryWriter(log_dir=self.logdir)

        # Log total episode reward
        self.tb_writer.add_scalar("episode/total_reward", info["episode_reward"], step)
        # Log episode length
        self.tb_writer.add_scalar("episode/length", info["episode_length"], step)

        # Log weighted rewards
        for field in vars(info["weighted_rewards"]):
            value = getattr(info["weighted_rewards"], field)
            if getattr(self.rewardMul, field) != 0:
                self.tb_writer.add_scalar(f"rewardWeighted/{field}", value, step)
                
        # Log raw rewards
        for field in vars(info["raw_rewards"]):
            value = getattr(info["raw_rewards"], field)
            self.tb_writer.add_scalar(f"rewardRaw/{field}", value, step)


        # Reset counters for next episode
        self.episode_reward = 0.0
        self.episode_rewards_per_category = Rewards()
        self.weighted_rewards = Rewards()
        self.current_step = 0
        
        #reset plot
        # self.reward_history = {name: [] for name in self.plot_rewards}
        # self.time_steps = []

        # if self.plot_initialized:
        #     self.ax.cla()
        #     self.plot_initialized = False
        
    def _init_plot(self):
        plt.ion()  # interactive mode
        self.fig, self.ax = plt.subplots()
        self.lines = {}

        for name in self.plot_rewards:
            line, = self.ax.plot([], [], label=name)
            self.lines[name] = line

        self.ax.legend()
        self.ax.set_title("Live Reward Components")
        self.ax.set_xlabel("Step")
        self.ax.set_ylabel("Value")

        self.plot_initialized = True
        
    def _update_plot(self, rewards_packet):
        if not self.plot_initialized:
            self._init_plot()

        self.time_steps.append(self.current_step)

        for name in self.plot_rewards:
            raw_value = getattr(rewards_packet, name)
            weight = getattr(self.rewardMul, name)
            
            if REWARDDEBUGWEIGHTED:
                value = raw_value * weight
            else:
                value = raw_value

            self.reward_history[name].append(value)
            self.lines[name].set_data(self.time_steps, self.reward_history[name])

        self.ax.relim()
        self.ax.autoscale_view()

        self.fig.canvas.draw()
        self.fig.canvas.flush_events()
        
    def close(self):
        if hasattr(self, "tb_writer") and self.tb_writer is not None:
            self.tb_writer.flush()
            self.tb_writer.close()
            self.tb_writer = None