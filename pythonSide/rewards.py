import numpy as np
from dataclasses import dataclass

import numpy as np
from dataclasses import dataclass

@dataclass
class Rewards:
    steering_smoothness: float = 0.0
    throttle_smoothness: float = 0.0

    out_of_bounds_penalty: float = 0.0
    collision_penalty: float = 0.0
    grass_penalty: float = 0.0

    team_distance: float = 0.0
    lap_time: float = 0.0
    team_lap_time: float = 0.0
    placement: float = 0.0
    team_placement: float = 0.0

    speed: float = 0.0
    acceleration: float = 0.0
    distance: float = 0.0
    discounted_distance: float = 0.0

    speedI: float = 0.0
    speedII: float = 0.0
    speedIII: float = 0.0
    speedIV: float = 0.0
    speedV: float = 0.0

    accelerationI: float = 0.0
    accelerationII: float = 0.0
    accelerationIII: float = 0.0
    accelerationIV: float = 0.0
    accelerationV: float = 0.0

    angleI: float = 0.0
    angleII: float = 0.0
    angleIII: float = 0.0
    angleIV: float = 0.0
    angleV: float = 0.0

    distanceI: float = 0.0
    distanceII: float = 0.0
    distanceIII: float = 0.0

    def as_vector(self) -> np.ndarray:
        """Return rewards as a NumPy vector of length 50."""
        arr = np.array([
            self.steering_smoothness,
            self.throttle_smoothness,

            self.out_of_bounds_penalty,
            self.collision_penalty,
            self.grass_penalty,

            self.team_distance,
            self.lap_time,
            self.team_lap_time,
            self.placement,
            self.team_placement,

            self.speed,
            self.acceleration,
            self.distance,
            self.discounted_distance,

            self.speedI,
            self.speedII,
            self.speedIII,
            self.speedIV,
            self.speedV,

            self.accelerationI,
            self.accelerationII,
            self.accelerationIII,
            self.accelerationIV,
            self.accelerationV,

            self.angleI,
            self.angleII,
            self.angleIII,
            self.angleIV,
            self.angleV,

            self.distanceI,
            self.distanceII,
            self.distanceIII,

            # reserved for later use (8 + 10 zeros)
            0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0,
            0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0
        ], dtype=np.float32)

        return arr
    
    @staticmethod
    def defaultWeights() -> "Rewards":
        """Return a Rewards instance representing the default weights."""
        return Rewards(
            speed=0.15,
            steering_smoothness=0.005,
            throttle_smoothness=0.005,
            collision_penalty=10.0,
            grass_penalty=1.0,
            out_of_bounds_penalty=10.0
        )



def calculate_total_reward(rewards: Rewards, weights: Rewards) -> float:
    return float(np.dot(rewards.as_vector(), weights.as_vector()))
