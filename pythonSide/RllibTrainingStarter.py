import os
os.environ["KMP_DUPLICATE_LIB_OK"] = "TRUE"
import gymnasium as gym
import numpy as np
from ray.tune.registry import register_env
from ray.rllib.core.rl_module.rl_module import RLModuleSpec
from ray.rllib.connectors.common.frame_stacking import FrameStacking
from ray.rllib.algorithms.ppo import PPOConfig

# Import your existing CNN and Unity environment
from RllibCNNs import SmallCNNRLModule
from unityEnv.UnitySingleCarEnv import UnityCarEnv

# ---------------------------
# 1. Environment registration
# ---------------------------
def make_env(config):
    return UnityCarEnv(run_headless=True)

register_env("UnityCarEnv-v0", make_env)

# ---------------------------
# 2. RLlib PPO config with frame stacking
# ---------------------------
N_FRAMES = 4  # stack 4 frames → 12 channels (4*RGB)

# Define the observation space for the RLModule
obs_space = gym.spaces.Box(
    low=0.0,
    high=1.0,
    shape=(64, 128, 3 * N_FRAMES),  # 4 stacked RGB frames
    dtype=np.float32
)

act_space = gym.spaces.Box(
    low=-1.0,
    high=1.0,
    shape=(2,),
    dtype=np.float32
)

# RLModule spec
module_spec = RLModuleSpec(
    module_class=SmallCNNRLModule,
    observation_space=obs_space,
    action_space=act_space,
)

# PPO config
config = (
    PPOConfig()
    .framework("torch")
    .environment(
        "UnityCarEnv-v0",
        env_config={},  # put any UnityCarEnv kwargs here
    )
    .env_runners(
        num_env_runners=1
        )
    .rl_module(
        rl_module_spec=RLModuleSpec(
            module_class=SmallCNNRLModule,
            observation_space=gym.spaces.Box(
                low=0.0,
                high=1.0,
                shape=(64, 128, 12),  # 4 stacked RGB frames
                dtype=np.float32,
            ),
            action_space=gym.spaces.Box(
                low=-1.0,
                high=1.0,
                shape=(2,),
                dtype=np.float32,
            ),
        )
    )
)

algo = config.build()

for i in range(1000):
    result = algo.train()
    print(
        f"Iter {i} | "
        f"reward_mean = {result['episode_reward_mean']:.2f}"
    )