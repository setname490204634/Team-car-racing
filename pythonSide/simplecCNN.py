import torch as th
import torch.nn as nn
from stable_baselines3.common.torch_layers import BaseFeaturesExtractor
from typing import Dict


class SmallRacingCNN(BaseFeaturesExtractor):
    """
    Custom CNN feature extractor for 128x64 RGB input with 2-frame stacking.
    Input shape for SB3 will be (C, H, W) = (6, 64, 128)
    """

    def __init__(self, observation_space, features_dim=256):
        super(SmallRacingCNN, self).__init__(observation_space, features_dim)

        n_input_channels = observation_space["image"].shape[0]  # should be 6

        # Convolutional encoder
        self.cnn = nn.Sequential(
            nn.Conv2d(n_input_channels, 32, kernel_size=5, stride=2, padding=2),  
            nn.ReLU(),
            nn.Conv2d(32, 64, kernel_size=3, stride=2, padding=1),  
            nn.ReLU(),
            nn.Conv2d(64, 128, kernel_size=3, stride=2, padding=1),  
            nn.ReLU(),
            nn.Conv2d(128, 128, kernel_size=3, stride=2, padding=1),
            nn.ReLU()
        )

        # Determine output size of CNN
        with th.no_grad():
            sample = th.zeros(1, n_input_channels, observation_space["image"].shape[1], observation_space["image"].shape[2])
            cnn_out = self.cnn(sample)
            cnn_out_dim = cnn_out.view(1, -1).shape[1]

        # Final linear layer
        self.linear = nn.Sequential(
            nn.Linear(cnn_out_dim, features_dim),
            nn.ReLU()
        )

    def forward(self, obs_dict: Dict[str, th.Tensor]) -> th.Tensor:
        x = obs_dict["image"]
        x = self.cnn(x)
        x = th.flatten(x, 1)
        return self.linear(x)
