# continue_or_infer.py
import os
os.environ["KMP_DUPLICATE_LIB_OK"] = "TRUE"
from stable_baselines3 import PPO
from stable_baselines3.common.vec_env import DummyVecEnv, VecNormalize

from UnitySingleCarEnv import UnityCarEnv
from CNNs import SmallCNN2, VGG16StackedFramesExtractor
from callbacks import SaveVecNormalizeCallback, FreezeCarDuringPPO, RewardLogCallback

CONTINUE_TRAINING = False
HEADLESS_MODE = False

# CONTINUE_TRAINING = True
# HEADLESS_MODE = True



FEATURE_EXTRACTOR = SmallCNN2  # must match training
TOTAL_TIMESTEPS = 5_000_000


# Paths
MODEL_PATH = "./pythonSide/models/model_600000.zip"
VECNORM_PATH = "./pythonSide/models/vecnormalize_600000.pkl"
LOG_DIR = "./pythonSide/logs/"

def make_env():
    return UnityCarEnv(run_headless=HEADLESS_MODE)


# Always create a fresh Unity environment
raw_env = DummyVecEnv([make_env])

if CONTINUE_TRAINING:
    if os.path.exists(VECNORM_PATH):
        print("Loading VecNormalize stats for CONTINUED TRAINING...")
        env = VecNormalize.load(VECNORM_PATH, raw_env)
        env.training = True
        env.norm_reward = True
    else:
        print("No VecNormalize found → creating new one.")
        env = VecNormalize(raw_env, norm_obs=True, norm_reward=True, clip_obs=10.)
else:
    print("Running in INFERENCE mode...")
    if not os.path.exists(VECNORM_PATH):
        raise FileNotFoundError("VecNormalize stats missing! Cannot run inference.")
    env = VecNormalize.load(VECNORM_PATH, raw_env)
    env.training = False
    env.norm_reward = False

if not os.path.exists(MODEL_PATH):
    raise FileNotFoundError(f"Model path does not exist: {MODEL_PATH}")

print(f"Loading PPO model from: {MODEL_PATH}")

model = PPO.load(
    MODEL_PATH,
    env=env,
    tensorboard_log=LOG_DIR,
    custom_objects={
        "features_extractor_class": FEATURE_EXTRACTOR,
        "features_dim": 256,
    }
)

if CONTINUE_TRAINING:
    print("Continuing training...")

    callbacks = [
        SaveVecNormalizeCallback("./pythonSide/models/", save_freq=50000),
        FreezeCarDuringPPO(),
        RewardLogCallback(),
    ]

    model.learn(
        total_timesteps=TOTAL_TIMESTEPS,
        callback=callbacks,
    )

    print("Saving final model + VecNormalize...")
    model.save("./pythonSide/models/ppo_unity_car_final")
    env.save(VECNORM_PATH)

    print("Training finished.")

else:
    print("Inference mode running... Press CTRL+C to quit.")

    obs = env.reset()

    while True:
        action, _ = model.predict(obs, deterministic=True)
        obs, reward, done, info = env.step(action)
        if done:
            obs = env.reset()


env.close()
print("Program finished.")
