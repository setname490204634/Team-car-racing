# continue_training.py
import os
from stable_baselines3 import PPO
from stable_baselines3.common.vec_env import DummyVecEnv, VecTransposeImage
from stable_baselines3.common.callbacks import CheckpointCallback
from UnitySingleCarEnv import UnityCarEnv  # your environment class

# --- Directories ---
os.makedirs("./pythonSide/models", exist_ok=True)
os.makedirs("./pythonSide/logs", exist_ok=True)

# --- Environment ---
def make_env():
    return UnityCarEnv(run_headless=False)

env = DummyVecEnv([make_env])
# env = VecTransposeImage(env)

# --- Load existing model ---
model_path = "./pythonSide/models/ppo_unity_car_300000_steps.zip"
if not os.path.exists(model_path):
    raise FileNotFoundError(f"Model file {model_path} not found. Train a model first.")

print(f"Loading model from {model_path} to continue training...")
model = PPO.load(model_path, env=env, tensorboard_log="./pythonSide/logs/")

# --- Checkpoint callback ---
checkpoint_callback = CheckpointCallback(
    save_freq=10000,
    save_path="./pythonSide/models/",
    name_prefix="ppo_unity_car"
)

# --- Continue training ---
additional_timesteps = 5_000_000  # adjust as needed
model.learn(total_timesteps=additional_timesteps, callback=checkpoint_callback)

# --- Save final model ---
final_model_path = "./pythonSide/models/ppo_unity_car_final"
model.save(final_model_path)
env.close()

print(f"Training resumed and completed. Final model saved to {final_model_path}")
