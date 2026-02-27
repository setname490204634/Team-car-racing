import os
import warnings
os.environ["KMP_DUPLICATE_LIB_OK"] = "TRUE"
os.environ["RAY_DEDUP_LOGS"] = "1"
os.environ["RAY_DISABLE_IMPORT_WARNING"] = "1"
os.environ["RAY_SILENCE_LOGS"] = "1"
PYTHONWARNINGS="ignore::DeprecationWarning"
warnings.filterwarnings("ignore")

import gymnasium as gym
import numpy as np
from ray.tune.registry import register_env
from ray.rllib.algorithms.ppo import PPOConfig
from unityEnv.UnitySingleCarEnv import UnityCarEnv
from ray.rllib.core.rl_module.default_model_config import DefaultModelConfig

from ray.rllib.algorithms.callbacks import DefaultCallbacks

class EpisodeInfoCallback(DefaultCallbacks):
    def on_episode_end(self, *, episode, **kwargs):
        # Get last info from env
        last_info = episode.last_info_for()
        if last_info and "episode" in last_info:
            episode_data = last_info["episode"]

            # Print total reward and length
            print(f"Episode {episode.episode_id} | reward={episode_data['r']:.2f} | length={episode_data['l']}")

            # Print detailed rewards per category
            if "rewards" in episode_data:
                for k, v in episode_data["rewards"].items():
                    print(f"  {k}: {v}")

def make_env(config):
    return UnityCarEnv(run_headless=True)

register_env("UnityCarEnv-v0", make_env)

obs_space = gym.spaces.Box(low=0.0, high=1.0, shape=(12, 64, 128), dtype=np.float32)
act_space = gym.spaces.Box(low=-1.0, high=1.0, shape=(2,), dtype=np.float32)

config = (
    PPOConfig()
    .framework("torch")
    .environment(
        "UnityCarEnv-v0",
        env_config={},
    )
    .env_runners(
        num_env_runners=1
        )
    .training(
        lr=3e-4,
        train_batch_size=128,
        gamma=0.99,
        use_gae=True,
        lambda_=0.95,
        clip_param=0.2,
        vf_clip_param=10.0,
        grad_clip=0.5,
        vf_loss_coeff=0.5,
        entropy_coeff=0.01,
    )
    .rl_module(
        model_config=DefaultModelConfig(
            conv_filters=[
                [32, 5, 2],
                [64, 3, 2],
                [64, 3, 1],
            ],
            conv_activation="relu",
            head_fcnet_hiddens=[256],
        )
    )
    .callbacks(EpisodeInfoCallback)
)

algo = config.build()

for i in range(10):
    result = algo.train()
    print(f"Iter {i} | num_env_steps_sampled={result['env_runners']['num_env_steps_sampled']}")