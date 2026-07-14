import os
import warnings
import sys
import argparse

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
from unityEnv.envUtils import get_next_env_folder #the function is more general so its used here too
from torch.utils.tensorboard import SummaryWriter
import torch


MODEL_DIR = os.path.abspath("./pythonSide/models")
LOG_DIR = os.path.abspath("./pythonSide/training_logs")
log_dir = get_next_env_folder(LOG_DIR)
tb_writer = SummaryWriter(log_dir=log_dir)

os.makedirs(LOG_DIR, exist_ok=True)
os.makedirs(MODEL_DIR, exist_ok=True)

NUM_AGENTS = 1
GRAY_SCALE_OBS_HISTORY = True


torch.backends.cuda.matmul.allow_tf32 = True
torch.backends.cudnn.allow_tf32 = True


def make_env(config):
    return UnityMultiCarEnv(
        number_of_agents=NUM_AGENTS,
        run_headless=True,
        debug=True,
        grayScaleHisotry=GRAY_SCALE_OBS_HISTORY
    )

register_env("UnityMultiCarEnv-v0", make_env)

if GRAY_SCALE_OBS_HISTORY:
    obs_shape = (64, 128, 6)
else:
    obs_shape = (64, 128, 12)
    
obs_space = gym.spaces.Box(low=0.0, high=1.0, shape=obs_shape, dtype=np.float32)
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
        num_env_runners=1,
        rollout_fragment_length=512,
    )
    # 8192
    # 4096
    # 2048
    .training(
        lr=1.5e-4,
        train_batch_size=4096, #this is not a batch size but env steps in some cases
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
                [128, 3, 2],
                [128, 3, 2],
            ],
            conv_activation="tanh",
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

# Load checkpoint if provided as command-line argument
parser = argparse.ArgumentParser()
parser.add_argument("--checkpoint", type=str, default=None, help="Path to checkpoint to load")
parser.add_argument("--paramCount", type=str, default=None, help="will print out param count")
args = parser.parse_args()

if args.checkpoint:
    checkpoint_path = os.path.abspath(args.checkpoint)
    algo.restore(checkpoint_path)
    print(f"Checkpoint loaded from: {checkpoint_path}")
else:
    print("Starting training from scratch")

#show param count
if args.paramCount:
    module = algo.get_module("shared_policy")

    print("Total params:",
        sum(p.numel() for p in module.parameters()))

    for name, p in module.named_parameters():
        print(f"{name:70} {list(p.shape)} {p.numel():,}")
        
    module = algo.get_module("shared_policy")

    print("\n===== MODEL STRUCTURE =====")
    print(module)

    print("\n===== NAMED MODULES =====")
    for name, layer in module.named_modules():
        print(name, ":", layer)

    print("\n===== PARAMETERS =====")
    for name, p in module.named_parameters():
        print(f"{name:80} {list(p.shape)}")

for i in range(1000000):
    print(f"iter: {i}")
    result = algo.train()

    learners = result.get("learners", {}).get("shared_policy", {})
    env = result.get("env_runners", {})

    def log(name, value):
        if value is not None:
            tb_writer.add_scalar(name, value, i)

    log("loss/total", learners.get("total_loss"))
    log("loss/policy", learners.get("policy_loss"))
    log("loss/value", learners.get("vf_loss"))
    log("loss/value_unclipped", learners.get("vf_loss_unclipped"))

    log("train/entropy", learners.get("entropy"))
    log("train/kl", learners.get("mean_kl_loss"))
    log("train/grad_norm", learners.get("gradients_default_optimizer_global_norm"))
    log("train/lr", learners.get("default_optimizer_learning_rate"))
    log("train/clip_param", learners.get("curr_kl_coeff"))

    if i % 50 == 0:
        path = algo.save(f"{MODEL_DIR}/checkpoint_{i}")