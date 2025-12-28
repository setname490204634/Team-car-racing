import os
os.environ["KMP_DUPLICATE_LIB_OK"] = "TRUE"

from stable_baselines3 import PPO
from stable_baselines3.common.vec_env import DummyVecEnv, VecNormalize

from callbacks import SaveVecNormalizeCallback, FreezeCarDuringPPO, RewardLogCallback
from UnitySingleCarEnv import UnityCarEnv
from CNNs import VGG16StackedFramesExtractor

# Directories
os.makedirs("./pythonSide/models", exist_ok=True)
os.makedirs("./pythonSide/logs", exist_ok=True)

# ------------------------
# Environment
# ------------------------
def make_env():
    return UnityCarEnv(run_headless=True)

env = DummyVecEnv([make_env])

# IMPORTANT: do NOT normalize observations for VGG
env = VecNormalize(
    env,
    norm_obs=False,
    norm_reward=True,
    clip_obs=10.0
)

# ------------------------
# Policy configuration
# ------------------------
policy_kwargs = dict(
    features_extractor_class=VGG16StackedFramesExtractor,
    features_extractor_kwargs=dict(features_dim=256),
)

# ------------------------
# PPO model
# ------------------------
model = PPO(
    policy="CnnPolicy",
    env=env,
    policy_kwargs=policy_kwargs,
    verbose=1,

    learning_rate=1e-4,
    n_steps=512,
    batch_size=128,
    n_epochs=5,

    gamma=0.99,
    gae_lambda=0.95,

    ent_coef=0.03,
    vf_coef=1.0,
    max_grad_norm=0.5,

    tensorboard_log="./pythonSide/logs/"
)

# ------------------------
# Callbacks
# ------------------------
callbacks = [
    SaveVecNormalizeCallback("./pythonSide/models/", 50_000),
    FreezeCarDuringPPO(),
    RewardLogCallback()
]

# ------------------------
# Training
# ------------------------
total_timesteps = 5_000_000
model.learn(total_timesteps=total_timesteps, callback=callbacks)

# ------------------------
# Save
# ------------------------
final_model_path = "./pythonSide/models/ppo_unity_car_final"
model.save(final_model_path)
env.save("./pythonSide/models/vecnormalize_final.pkl")
env.close()

print(f"Training complete. Final model saved to {final_model_path}")
