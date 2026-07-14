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
from ray.rllib.algorithms.ppo.ppo_catalog import PPOCatalog
from ray.rllib.algorithms.ppo.torch.ppo_torch_rl_module import PPOTorchRLModule
from ray.rllib.core import (
    COMPONENT_LEARNER,
    COMPONENT_LEARNER_GROUP,
    COMPONENT_RL_MODULE,
    DEFAULT_MODULE_ID,
)
from ray.rllib.core.rl_module.default_model_config import DefaultModelConfig
from ray.rllib.core.rl_module.multi_rl_module import MultiRLModuleSpec
from ray.rllib.core.rl_module.rl_module import RLModuleSpec
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

NUM_AGENTS = 4
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

# per team policy
policies = {
    "learner_policy": (None, obs_space, act_space, {}),
    "opponent_policy": (None, obs_space, act_space, {}),
}

def policy_mapping_fn(agent_id, *args, **kwargs):
    idx = int(agent_id.split("_")[1])
    if idx == 0:
        return "learner_policy"
    return "opponent_policy"
    
    
parser = argparse.ArgumentParser()
parser.add_argument(
    "--checkpoint",
    type=str,
    default=None,
    help="Seed both team policies from a single-policy checkpoint",
)
parser.add_argument(
    "--paramCount",
    action="store_true",
)
args = parser.parse_args()

checkpoint_path = None
module_checkpoint_path = None
if args.checkpoint:
    checkpoint_path = os.path.abspath(args.checkpoint)
    module_base_path = os.path.join(
        checkpoint_path,
        COMPONENT_LEARNER_GROUP,
        COMPONENT_LEARNER,
        COMPONENT_RL_MODULE,
    )
    module_checkpoint_path = os.path.join(module_base_path, DEFAULT_MODULE_ID)
    if not os.path.isdir(module_checkpoint_path):
        # Fall back to the first available module directory if the default
        # module name differs from the checkpoint (e.g. shared_policy).
        module_names = [
            name
            for name in os.listdir(module_base_path)
            if os.path.isdir(os.path.join(module_base_path, name))
        ]
        if not module_names:
            raise FileNotFoundError(
                f"Could not find module checkpoint under: {module_base_path}"
            )
        module_checkpoint_path = os.path.join(module_base_path, module_names[0])
    print(f"Using module checkpoint path: {module_checkpoint_path}")
else:
    print("Starting training from scratch")

module_specs = {
    "learner_policy": RLModuleSpec(
        module_class=PPOTorchRLModule,
        observation_space=obs_space,
        action_space=act_space,
        model_config=DefaultModelConfig(
            conv_filters=[
                [32, 5, 2],
                [64, 3, 2],
                [128, 3, 2],
                [128, 3, 2],
            ],
            conv_activation="tanh",
            head_fcnet_hiddens=[256],
        ),
        catalog_class=PPOCatalog,
        load_state_path=module_checkpoint_path,
    ),
    "opponent_policy": RLModuleSpec(
        module_class=PPOTorchRLModule,
        observation_space=obs_space,
        action_space=act_space,
        model_config=DefaultModelConfig(
            conv_filters=[
                [32, 5, 2],
                [64, 3, 2],
                [128, 3, 2],
                [128, 3, 2],
            ],
            conv_activation="tanh",
            head_fcnet_hiddens=[256],
        ),
        catalog_class=PPOCatalog,
        load_state_path=module_checkpoint_path,
    ),
}

multi_rl_module_spec = MultiRLModuleSpec(rl_module_specs=module_specs)

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
    .training(
        lr=1.5e-4,
        train_batch_size=4096, #this is not a batch size but env steps in some cases, so for 8 cars the batch size is
        gamma=0.99,
        use_gae=True,
        lambda_=0.95,
        clip_param=0.2,
        vf_clip_param=10.0,
        grad_clip=0.5,
        vf_loss_coeff=0.5,
        entropy_coeff=0.01,
    )
    .rl_module(rl_module_spec=multi_rl_module_spec)
    .multi_agent(
        policies=policies,
        policy_mapping_fn=policy_mapping_fn,
        policies_to_train=["learner_policy"],
    )
    .resources(
        num_gpus=1
    )
)

algo = config.build_algo()

learner = algo.get_module("learner_policy")
opponent = algo.get_module("opponent_policy")

opponent.set_state(learner.get_state())


if args.paramCount:
    module = algo.get_module("learner_policy")

    print("===== learner_policy =====")
    print("Total params:", sum(p.numel() for p in module.parameters()))

    for name, p in module.named_parameters():
        print(f"{name:70} {list(p.shape)} {p.numel():,}")


for i in range(1000000):

    print(f"iter: {i}")

    result = algo.train()

    learner = result["learners"]["learner_policy"]

    def log(name, value):
        if value is not None:
            tb_writer.add_scalar(name, value, i)

    log("loss/total", learner.get("total_loss"))
    log("loss/policy", learner.get("policy_loss"))
    log("loss/value", learner.get("vf_loss"))
    log("loss/value_unclipped", learner.get("vf_loss_unclipped"))

    log("train/entropy", learner.get("entropy"))
    log("train/kl", learner.get("mean_kl_loss"))
    log("train/grad_norm",
        learner.get("gradients_default_optimizer_global_norm"))
    log("train/lr",
        learner.get("default_optimizer_learning_rate"))
    log("train/kl_coeff",
        learner.get("curr_kl_coeff"))
        
    if (i + 1) % 10 == 0:
        weights = algo.learner_group.get_weights()

        algo.set_weights({
            "opponent_policy": weights["learner_policy"],
        })

        print(f"Updated opponent at iteration {i + 1}")
        
        # learner = algo.get_module("learner_policy")
        # opponent = algo.get_module("opponent_policy")

        # l = next(learner.parameters()).detach()
        # o = next(opponent.parameters()).detach()

        # print(l)
        # print(o)
        # print(torch.allclose(l, o))

    if i % 20 == 0:
        algo.save(f"{MODEL_DIR}/checkpoint_{i}")