# start_training.py
import os
from stable_baselines3 import PPO
from stable_baselines3.common.vec_env import DummyVecEnv, VecTransposeImage
from stable_baselines3.common.callbacks import CheckpointCallback
from UnitySingleCarEnv import UnityCarEnv  # your environment class

os.makedirs("./pythonSide/models", exist_ok=True)
os.makedirs("./pythonSide/logs", exist_ok=True)

unity_env = UnityCarEnv(run_headless = True)
env = DummyVecEnv([lambda: unity_env])
env = VecTransposeImage(env)  # transpose images if needed for CNN

model = PPO(
    policy="MultiInputPolicy",
    env=env,
    verbose=1,
    batch_size=64,
    n_steps=2048,
    learning_rate=3e-4,
    gamma=0.99,
    tensorboard_log="./pythonSide/logs/"
)

checkpoint_callback = CheckpointCallback(
    save_freq=10000,
    save_path="./pythonSide/models/",
    name_prefix="ppo_unity_car"
)

total_timesteps = 5000000
model.learn(total_timesteps=total_timesteps, callback=checkpoint_callback)

final_model_path = "./pythonSide/models/ppo_unity_car_final"
model.save(final_model_path)
env.close()

print(f"Training complete. Final model saved to {final_model_path}")
