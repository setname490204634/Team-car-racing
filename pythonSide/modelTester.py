# continue_training.py
import os
from stable_baselines3 import PPO
from stable_baselines3.common.vec_env import DummyVecEnv, VecNormalize
from UnitySingleCarEnv import UnityCarEnv
from callbacks import SaveVecNormalizeCallback
from callbacks import FreezeCarDuringPPO


CONTINUE_TRAINING = False
HEADLESS_MODE = False

MODEL_PATH = "./pythonSide/models/modelHead6.zip"
VECNORM_PATH = "./pythonSide/models/vecnormalizeHead6.pkl"

TOTAL_TIMESTEPS = 5_000_000

def make_env():
    return UnityCarEnv(run_headless=HEADLESS_MODE)

# Always create a fresh Unity environment
raw_env = DummyVecEnv([make_env])

# Training: need VecNormalize in training mode
# Inference: load VecNormalize stats in eval mode
if CONTINUE_TRAINING:
    if os.path.exists(VECNORM_PATH):
        print("Loading VecNormalize stats...")
        env = VecNormalize.load(VECNORM_PATH, raw_env)
        env.training = True
        env.norm_reward = True
    else:
        print("No VecNormalize file found, creating new normalization wrapper...")
        env = VecNormalize(raw_env, norm_obs=True, norm_reward=True, clip_obs=10.)
else:
    print("Running in INFERENCE mode...")
    if not os.path.exists(VECNORM_PATH):
        raise FileNotFoundError("VecNormalize stats missing! Cannot run inference.")
    env = VecNormalize.load(VECNORM_PATH, raw_env)
    env.training = False
    env.norm_reward = False


if not os.path.exists(MODEL_PATH):
    raise FileNotFoundError(f"Model file {MODEL_PATH} not found!")

print(f"Loading PPO model from: {MODEL_PATH}")
model = PPO.load(MODEL_PATH, env=env, tensorboard_log="./pythonSide/logs/")


if CONTINUE_TRAINING:

    print("Continuing PPO training...")
    model.learn(
        total_timesteps=TOTAL_TIMESTEPS,
        callback=[  SaveVecNormalizeCallback("./pythonSide/models/", 20000),
                    FreezeCarDuringPPO()]
    )

    print("Saving final model + normalization...")
    model.save("./pythonSide/models/ppo_unity_car_final")
    env.save(VECNORM_PATH)

else:
    print("Inference mode: model will drive around...")

    obs = env.reset()
    while True:
        action, _ = model.predict(obs, deterministic=True)
        obs, reward, done, info = env.step(action)
        if done:
            obs = env.reset()


env.close()
print("Program finished.")
