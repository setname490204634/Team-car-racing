# start_training.py
import os
from stable_baselines3 import PPO
from stable_baselines3.common.vec_env import DummyVecEnv, VecTransposeImage
from stable_baselines3.common.callbacks import CheckpointCallback

from UnitySingleCarEnv import UnityCarEnv
from simplecCNN import SmallRacingCNN

os.makedirs("./pythonSide/models", exist_ok=True)
os.makedirs("./pythonSide/logs", exist_ok=True)

def make_env():
    return UnityCarEnv(run_headless=True)
env = DummyVecEnv([make_env])

policy_kwargs = dict(
    features_extractor_class=SmallRacingCNN,
    features_extractor_kwargs=dict(features_dim=256),

    # MLP for non-image inputs (speed, prev_action)
    net_arch=dict(
        pi=[256, 128],
        vf=[256, 128]
    )
)

model = PPO(
    policy="MultiInputPolicy",
    env=env,
    policy_kwargs=policy_kwargs,
    verbose=1,

    learning_rate=3e-4,
    batch_size=64,
    n_steps=2048,
    gamma=0.995,
    ent_coef=0.0003,
    clip_range=0.2,
    gae_lambda=0.95,

    tensorboard_log="./pythonSide/logs/"
)

checkpoint_callback = CheckpointCallback(
    save_freq=20000,
    save_path="./pythonSide/models/",
    name_prefix="ppo_unity_car"
)

total_timesteps = 5_000_000
model.learn(total_timesteps=total_timesteps, callback=checkpoint_callback)

final_model_path = "./pythonSide/models/ppo_unity_car_final"
model.save(final_model_path)
env.close()

print(f"Training complete. Final model saved to {final_model_path}")
