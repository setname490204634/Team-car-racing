# start_training.py
import os
from stable_baselines3 import PPO
from stable_baselines3.common.vec_env import DummyVecEnv, VecTransposeImage
from stable_baselines3.common.callbacks import CheckpointCallback
from stable_baselines3.common.callbacks import BaseCallback
import sender

from UnitySingleCarEnv import UnityCarEnv
from simplecCNN import SmallRacingCNN

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

    learning_rate=lambda f: 3e-4 * f,

    n_steps=4096,
    batch_size=512,
    n_epochs=10,

    gamma=0.99,
    gae_lambda=0.95,
    clip_range=0.1,

    ent_coef=0.0001,
    vf_coef=1.0,
    max_grad_norm=0.5,

    tensorboard_log="./pythonSide/logs/"
)


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

final_model_path = "./pythonSide/models/ppo_unity_car_final"
model.save(final_model_path)
env.close()

print(f"Training complete. Final model saved to {final_model_path}")
