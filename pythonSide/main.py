import os
from stable_baselines3 import PPO
from stable_baselines3.common.vec_env import DummyVecEnv, VecTransposeImage
from stable_baselines3.common.callbacks import CheckpointCallback
from UnitySingleCarEnv import UnityCarEnv  # your environment class

os.makedirs("models", exist_ok=True)
os.makedirs("logs", exist_ok=True)

unity_env = UnityCarEnv()
env = DummyVecEnv([lambda: unity_env])  # single instance
env = VecTransposeImage(env)  # transpose images if needed for CNN

model = PPO(
     policy="MultiInputPolicy",
    env=env,
    verbose=1,
    batch_size=64,
    n_steps=2048,
    learning_rate=3e-4,
    gamma=0.99,
    tensorboard_log="./logs/"
)

checkpoint_callback = CheckpointCallback(
    save_freq=10000,
    save_path="./models/",
    name_prefix="ppo_unity_car"
)

total_timesteps = 5000000
model.learn(total_timesteps=total_timesteps, callback=checkpoint_callback)

model.save("./models/ppo_unity_car_final")
env.close()

print("Training complete. Final model saved.")
