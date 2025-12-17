import os
from stable_baselines3.common.callbacks import  BaseCallback
import sender
from CommandConstants import CommandCode

class SaveVecNormalizeCallback(BaseCallback):
    def __init__(self, save_path, save_freq):
        super().__init__()
        self.save_path = save_path
        self.save_freq = save_freq
        os.makedirs(save_path, exist_ok=True)

    def _on_step(self):
        if self.n_calls % self.save_freq == 0:
            model_file = os.path.join(self.save_path, f"model_{self.n_calls}.zip")
            vec_file   = os.path.join(self.save_path, f"vecnormalize_{self.n_calls}.pkl")

            print(f"[AutoSave] Saving model → {model_file}")
            print(f"[AutoSave] Saving VecNormalize → {vec_file}")

            self.model.save(model_file)
            self.model.get_env().save(vec_file)

        return True
    
class FreezeCarDuringPPO(BaseCallback):

    def __init__(self):
        super().__init__()
        self.paused = False

    def _on_rollout_end(self):
        env = self.model.env.envs[0]
        print("Pausing Unity simulation...")
        sender.send_command(CommandCode.StopSimulation, 0, env.control_port)
        self.paused = True
        return True

    def _on_rollout_start(self):
        if self.paused:
            env = self.model.env.envs[0]
            print("Unpausing Unity simulation...")
            sender.send_command(CommandCode.ContinueSimulation, 0, env.control_port)
            self.paused = False
        return True
    
    def _on_step(self) -> bool:
        return True
    
class RewardLogCallback(BaseCallback):
    def __init__(self, verbose=0):
        super().__init__(verbose)

    def _on_step(self) -> bool:

        info = self.locals["infos"][0]
        if "episode" in info:

            episode_data = info["episode"]

            # Log total reward + length
            self.logger.record("episode/total_reward", episode_data["r"])
            self.logger.record("episode/length", episode_data["l"])

            # Log every reward component
            if "rewards" in episode_data:
                reward_details = episode_data["rewards"]

                for key, value in reward_details.items():
                    self.logger.record(f"rewards/{key}", value)

        return True