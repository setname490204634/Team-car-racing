# continue_training.py
import os
from stable_baselines3 import PPO
from stable_baselines3.common.vec_env import DummyVecEnv, VecTransposeImage
from stable_baselines3.common.callbacks import CheckpointCallback
from UnitySingleCarEnv import UnityCarEnv  # your environment class
import sender
from stable_baselines3.common.callbacks import BaseCallback

# --- Directories ---
os.makedirs("./pythonSide/models", exist_ok=True)
os.makedirs("./pythonSide/logs", exist_ok=True)

class FreezeCarDuringPPO(BaseCallback):

    def __init__(self):
        super().__init__()
        self.paused = False

    def _on_rollout_end(self):
        env = self.model.env.envs[0]
        print("Pausing Unity simulation...")
        sender.send_command(11, 0, env.control_port)  # PAUSE
        self.paused = True
        return True

    def _on_rollout_start(self):
        if self.paused:
            env = self.model.env.envs[0]
            print("Unpausing Unity simulation...")
            sender.send_command(12, 0, env.control_port)  # UNPAUSE
            self.paused = False
        return True
    
    def _on_step(self) -> bool:
        return True

# --- Environment ---
def make_env():
    return UnityCarEnv(run_headless=True)

env = DummyVecEnv([make_env])
# env = VecTransposeImage(env)

# --- Load existing model ---
model_path = "./pythonSide/models/head2.zip"
if not os.path.exists(model_path):
    raise FileNotFoundError(f"Model file {model_path} not found. Train a model first.")

print(f"Loading model from {model_path} to continue training...")
model = PPO.load(model_path, env=env, tensorboard_log="./pythonSide/logs/")

checkpoint_callback = CheckpointCallback(
    save_freq=20000,
    save_path="./pythonSide/models/",
    name_prefix="ppo_unity_car"
)

callbacks = [
    checkpoint_callback,
    FreezeCarDuringPPO()
]

total_timesteps = 5_000_000
model.learn(total_timesteps=total_timesteps, callback=callbacks)

# --- Save final model ---
final_model_path = "./pythonSide/models/ppo_unity_car_final"
model.save(final_model_path)
env.close()

print(f"Training resumed and completed. Final model saved to {final_model_path}")
