from ray.rllib.algorithms.ppo import PPO
from unityEnv.UnitySingleCarEnv import UnityCarEnv

algo = PPO.from_checkpoint("./pythonSide/models")

env = UnityCarEnv(run_headless=False, debug=True)

obs, _ = env.reset()

while True:
    action = algo.compute_single_action(obs, explore=False)
    obs, reward, terminated, truncated, _ = env.step(action)

    if terminated or truncated:
        obs, _ = env.reset()