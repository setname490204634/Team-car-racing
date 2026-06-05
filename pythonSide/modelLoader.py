import os
import gymnasium as gym
import numpy as np
import torch
from ray.tune.registry import register_env
from ray.rllib.algorithms.ppo import PPOConfig
from ray.rllib.core.rl_module.default_model_config import DefaultModelConfig
from unityEnv.UnityMultiCarEnv import UnityMultiCarEnv

NUM_AGENTS = 2
GRAY_SCALE_OBS_HISTORY = True

def make_env(config):
    return UnityMultiCarEnv(
        number_of_agents=NUM_AGENTS,
        run_headless=True,
        debug=True,
        grayScaleHisotry=GRAY_SCALE_OBS_HISTORY
    )

register_env("UnityMultiCarEnv-v0", make_env)

if GRAY_SCALE_OBS_HISTORY:
    obs_shape = (6, 64, 128)
else:
    obs_shape = (12, 64, 128)

obs_space = gym.spaces.Box(low=0.0, high=1.0, shape=obs_shape, dtype=np.float32)
act_space = gym.spaces.Box(low=-1.0, high=1.0, shape=(2,), dtype=np.float32)

policies = {
    "shared_policy": (None, obs_space, act_space, {})
}

def policy_mapping_fn(agent_id, *args, **kwargs):
    return "shared_policy"

config = (
    PPOConfig()
    .framework("torch")
    .environment("UnityMultiCarEnv-v0", env_config={})
    .env_runners(num_env_runners=1, rollout_fragment_length=512)
    .training(
        lr=1e-4,
        train_batch_size=8192,
        gamma=0.97,
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
            conv_activation="tanh",
            head_fcnet_hiddens=[256],
        )
    )
    .multi_agent(
        policies=policies,
        policy_mapping_fn=policy_mapping_fn,
    )
    .resources(num_gpus=1)
)

algo = config.build_algo()

CHECKPOINT_PATH = os.path.abspath("./pythonSide/models/checkpoint_continued_250")
algo.restore(CHECKPOINT_PATH)

print("Checkpoint loaded successfully!")

for i in range(1000000):
    result = algo.train()
    print(f"Iter {i}")

    if i % 10 == 0:
        path = algo.save(os.path.abspath(f"./pythonSide/models/checkpoint_continued_{i}"))
        print(f"Checkpoint saved to {path}")