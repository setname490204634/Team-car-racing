import os
import warnings
os.environ["KMP_DUPLICATE_LIB_OK"] = "TRUE"
os.environ["RAY_DEDUP_LOGS"] = "1"
os.environ["RAY_DISABLE_IMPORT_WARNING"] = "1"
os.environ["RAY_SILENCE_LOGS"] = "1"
os.environ["PYTHONWARNINGS"] = "ignore::DeprecationWarning"
warnings.filterwarnings("ignore")

import gymnasium as gym
import numpy as np
from ray.tune.registry import register_env
from ray.rllib.algorithms.ppo import PPOConfig
from unityEnv.UnitySingleCarEnv2 import UnityCarEnv
from ray.rllib.core.rl_module.default_model_config import DefaultModelConfig


MODEL_DIR = os.path.abspath("./pythonSide/models")
os.makedirs(MODEL_DIR, exist_ok=True)

def make_env(config):
    env = UnityCarEnv(
        run_headless=True,
        debug=False
    )
    return env

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
        num_env_runners=2
        )
    .training(
        lr=1e-4,
        train_batch_size=2048,
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
    .resources(
        num_gpus=1
    )
)

algo = config.build_algo()

for i in range(1000000):

    result = algo.train()

    print(f"Iter {i}")

    if i % 50 == 0:
        path = algo.save(f"{MODEL_DIR}/checkpoint_{i}")
        print(f"Checkpoint saved to {path}")