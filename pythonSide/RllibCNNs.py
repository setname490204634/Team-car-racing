import torch
import torch.nn as nn
import gymnasium as gym
from typing import Optional
from ray.rllib.core.rl_module.rl_module import RLModule
from ray.rllib.core.columns import Columns
from ray.rllib.utils.typing import TensorType
from ray.rllib.models.torch.torch_action_dist import TorchCategorical, TorchDiagGaussian


class SmallCNNRLModule(RLModule, nn.Module):
    def __init__(
        self,
        *,
        observation_space: gym.Space,
        action_space: gym.Space,
        model_config: Optional[dict] = None,
        inference_only: bool = False,
        **kwargs,
    ):
        RLModule.__init__(
            self,
            observation_space=observation_space,
            action_space=action_space,
            inference_only=inference_only,
            model_config=model_config,
        )
        nn.Module.__init__(self)

        self.framework = "torch"

        # -------- Feature extractor --------
        self.conv1 = nn.Conv2d(12, 32, kernel_size=5, stride=2)
        self.conv2 = nn.Conv2d(32, 64, kernel_size=3, stride=2)
        self.conv3 = nn.Conv2d(64, 64, kernel_size=3, stride=1)
        self.relu = nn.ReLU()

        self.fc = nn.Linear(64 * 12 * 28, 512)

        # -------- Policy + value heads --------
        action_dim = action_space.shape[0]

        self.policy_mean = nn.Linear(512, action_dim)
        self.policy_log_std = nn.Parameter(torch.zeros(action_dim))

        self.value_head = nn.Linear(512, 1)

    # ---------------------------------------------------
    # Shared feature extraction
    # ---------------------------------------------------
    def _features(self, obs: TensorType) -> TensorType:
        # obs: [B, 64, 128, 12] → [B, 12, 64, 128]
        # x = obs.permute(0, 3, 1, 2)
        x = obs
        x = self.relu(self.conv1(x))
        x = self.relu(self.conv2(x))
        x = self.relu(self.conv3(x))

        x = x.flatten(start_dim=1)
        return self.relu(self.fc(x))

    # ---------------------------------------------------
    # PPO-required forward passes
    # ---------------------------------------------------
    def forward_exploration(self, batch, **kwargs):
        features = self._features(batch[Columns.OBS])  # already implemented
        mean = self.actor_mean(features)
        log_std = self.actor_log_std.expand_as(mean)
        return {
            Columns.ACTION_DIST_INPUTS: torch.cat([mean, log_std], dim=-1),
            Columns.OBS: batch[Columns.OBS]
        }

    def get_train_action_dist_cls(self):
        if isinstance(self.action_space, gym.spaces.Discrete):
            return TorchCategorical
        else:
            return TorchDiagGaussian


    def forward_inference(self, batch, **kwargs):
        features = self._features(batch[Columns.OBS])
        mean = self.policy_mean(features)

        return {
            Columns.ACTIONS: mean,
            Columns.VF_PREDS: self.value_head(features).squeeze(-1),
        }


    def forward_train(self, batch, **kwargs):
        features = self._features(batch[Columns.OBS])
        mean = self.policy_mean(features)
        std = torch.exp(self.policy_log_std)

        return {
            Columns.ACTION_DIST_INPUTS: torch.cat([mean, std], dim=-1),
            Columns.VF_PREDS: self.value_head(features).squeeze(-1),
        }


    def get_value(self, batch, **kwargs):
        features = self._features(batch[Columns.OBS])
        return self.value_head(features).squeeze(-1)