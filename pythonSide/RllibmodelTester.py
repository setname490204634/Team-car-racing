import os
import warnings
os.environ["KMP_DUPLICATE_LIB_OK"] = "TRUE"
os.environ["RAY_DEDUP_LOGS"] = "1"
os.environ["RAY_DISABLE_IMPORT_WARNING"] = "1"
os.environ["RAY_SILENCE_LOGS"] = "1"
PYTHONWARNINGS="ignore::DeprecationWarning"
warnings.filterwarnings("ignore")

from ray.tune.registry import register_env
from ray.rllib.algorithms.ppo import PPO
from unityEnv.UnitySingleCarEnv import UnityCarEnv
import ray
ray.init(object_store_memory=200*1024*1024)

def make_env(config):
    return UnityCarEnv(run_headless=False, debug=True)

register_env("UnityCarEnv-v0", make_env)

algo = PPO.from_checkpoint(
    os.path.abspath("./pythonSide/models/checkpoint_450")
)

# RLlib will automatically create the env
obs = algo.get_policy().model.get_initial_state()  # or use algo.compute_single_action directly

while True:
    action = algo.compute_single_action()  # RLlib handles env internally