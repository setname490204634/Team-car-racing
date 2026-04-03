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
from ray.rllib.core.rl_module.default_model_config import DefaultModelConfig
from unityEnv.UnityMultiCarEnv import UnityMultiCarEnv

MODEL_DIR = os.path.abspath("./pythonSide/models")
os.makedirs(MODEL_DIR, exist_ok=True)

NUM_AGENTS = 2

def make_env(config):
    return UnityMultiCarEnv(
        number_of_agents=NUM_AGENTS,
        run_headless=True,
        debug=True
    )

register_env("UnityMultiCarEnv-v0", make_env)

obs_space = gym.spaces.Box(low=0.0, high=1.0, shape=(12, 64, 128), dtype=np.float32)
act_space = gym.spaces.Box(low=-1.0, high=1.0, shape=(2,), dtype=np.float32)

#one policy to rule them all
policies = {
    "shared_policy": (None, obs_space, act_space, {})
}

def policy_mapping_fn(agent_id, *args, **kwargs):
    return "shared_policy"

#per team policy
# policies = {
#     "team_0_policy": (None, obs_space, act_space, {}),
#     "team_1_policy": (None, obs_space, act_space, {}),
#     "team_2_policy": (None, obs_space, act_space, {}),
#     "team_3_policy": (None, obs_space, act_space, {}),
# }

# # Mapping agents to team policies
# def policy_mapping_fn(agent_id, *args, **kwargs):
#     idx = int(agent_id.split("_")[1])
#     if idx in [0,1]:
#         return "team_0_policy"
#     elif idx in [2,3]:
#         return "team_1_policy"
#     elif idx in [4,5]:
#         return "team_2_policy"
#     else:  # idx in [6,7]
#         return "team_3_policy"

config = (
    PPOConfig()
    .framework("torch")
    .environment(
        "UnityMultiCarEnv-v0",
        env_config={}
    )
    .env_runners(
        num_env_runners=2, rollout_fragment_length=32
    )
    .training(
        lr=1e-4,
        train_batch_size=1024, #fore some reason unkown to mankind this is not a batch size but env steps, so for 8 cars the batch size is 8 times this number
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
            conv_activation="relu",
            head_fcnet_hiddens=[256],
        )
    )
    .multi_agent(
        policies=policies,
        policy_mapping_fn=policy_mapping_fn,
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