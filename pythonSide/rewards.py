import numpy as np
from dataclasses import dataclass

@dataclass
class Rewards:
    speed: float = 0.0
    steering_smoothness: float = 0.0
    throttle_smoothness: float = 0.0
    collision_penalty: float = 0.0
    grass_penalty: float = 0.0
    team_distance: float = 0.0
    lap_time: float = 0.0
    team_lap_time: float = 0.0
    placement: float = 0.0
    team_placement: float = 0.0

    def as_vector(self) -> np.ndarray:
        """Return rewards as a NumPy vector."""
        return np.array([
            self.speed,
            self.steering_smoothness,
            self.throttle_smoothness,
            self.collision_penalty,
            self.grass_penalty,
            self.team_distance,
            self.lap_time,
            self.team_lap_time,
            self.placement,
            self.team_placement,
        ], dtype=np.float32)


@dataclass
class RewardWeights:
    speed: float = 1.0
    steering_smoothness: float = 0.7
    throttle_smoothness: float = 0.5
    collision_penalty: float = 1000
    grass_penalty: float = 30.0
    team_distance: float = 0.0
    lap_time: float = 0.0
    team_lap_time: float = 0.0
    placement: float = 0.0
    team_placement: float = 0.0

    def as_vector(self) -> np.ndarray:
        """Return weights as a NumPy vector."""
        return np.array([
            self.speed,
            self.steering_smoothness,
            self.throttle_smoothness,
            self.collision_penalty,
            self.grass_penalty,
            self.team_distance,
            self.lap_time,
            self.team_lap_time,
            self.placement,
            self.team_placement,
        ], dtype=np.float32)


def calculate_total_reward(rewards: Rewards, weights: RewardWeights) -> float:
    """Compute the scalar reward via dot product."""
    return float(np.dot(rewards.as_vector(), weights.as_vector()))
