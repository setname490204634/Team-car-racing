import torch
import torch.nn as nn
import torch.nn.functional as F
from stable_baselines3.common.torch_layers import BaseFeaturesExtractor
import torchvision.models as models
import gymnasium as gym


class SmallCNN(BaseFeaturesExtractor):
    def __init__(self, observation_space, features_dim=256):
        super().__init__(observation_space, features_dim)

        n_channels = observation_space.shape[0]
        h = observation_space.shape[1]
        w = observation_space.shape[2]
        
        resized_h, resized_w = 64, 64

        self.cnn = nn.Sequential(
            #upsample here actually reduces from 128x64 to 64x64
            nn.Upsample(size=( resized_h, resized_w), mode="bilinear", align_corners=False),

            nn.Conv2d(n_channels, 12, kernel_size=3, stride=2),
            nn.ReLU(),

            nn.Conv2d(12, 24, kernel_size=3, stride=2),
            nn.ReLU(),

            nn.Conv2d(24, 48, kernel_size=3, stride=2),
            nn.ReLU(),

            nn.Flatten(),
        )

        with torch.no_grad():
            sample = torch.zeros(1, n_channels, resized_h, resized_w)
            n_flatten = self.cnn(sample).shape[1]

        self.linear = nn.Sequential(
            nn.Linear(n_flatten, features_dim),
            nn.ReLU()
        )

    def forward(self, x):
        return self.linear(self.cnn(x))
    
    
class SmallCNN2(BaseFeaturesExtractor):
    def __init__(self, observation_space, features_dim=256):
        super().__init__(observation_space, features_dim)
        n_channels = observation_space.shape[0]
        h = observation_space.shape[1]
        w = observation_space.shape[2]
        
        self.cnn = nn.Sequential(
            nn.Conv2d(n_channels, 32, kernel_size=7, stride=4),
            nn.ReLU(), nn.Conv2d(32, 64, kernel_size=3, stride=2),
            nn.ReLU(), nn.Conv2d(64, 64, kernel_size=3, stride=1),
            #liche 
            nn.ReLU(), nn.Flatten(), )
        # compute output size
        with torch.no_grad():
            sample = torch.zeros(1, n_channels, h, w)
            n_flatten = self.cnn(sample).shape[1]
            
        self.linear = nn.Sequential(
            nn.Linear(n_flatten, features_dim),
            nn.ReLU() )
    def forward(self, x):
        return self.linear(self.cnn(x))

class VGG16StackedFramesExtractor(BaseFeaturesExtractor):
    def __init__(self, observation_space: gym.spaces.Box, features_dim: int = 256):
        super().__init__(observation_space, features_dim)

        n_channels = observation_space.shape[0]

        vgg = models.vgg16(weights=models.VGG16_Weights.IMAGENET1K_FEATURES)
        
        #for 12 layers
        original_conv = vgg.features[0]
        self.vgg_features = vgg.features
        self.vgg_features[0] = nn.Conv2d(
            in_channels=n_channels,
            out_channels=original_conv.out_channels,
            kernel_size=original_conv.kernel_size,
            stride=original_conv.stride,
            padding=original_conv.padding
        )

        self.avgpool = nn.AdaptiveAvgPool2d((7,7))

    def forward(self, observations: torch.Tensor) -> torch.Tensor:
        x = observations.float() / 255.0
        x = self.vgg_features(x)
        x = self.avgpool(x)
        return torch.flatten(x, 1)
