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
    discounted_collision_penalty: float = 0.0
    grass_penalty: float = 0.0
    discounted_grass_penalty: float = 0.0

    team_distance: float = 0.0
    lap_time: float = 0.0
    discounted_lap_time: float = 0.0
    team_lap_time: float = 0.0
    discounted_team_lap_time: float = 0.0
    placement: float = 0.0
    discounted_placement: float = 0.0
    team_placement: float = 0.0
    discounted_team_placement: float = 0.0

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
    progressReward: float = 0.0
    discounted_progressReward: float = 0.0

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
            self.progressReward,

            # reserved for later use
            0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0,
            0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0
        ], dtype=np.float32)

        return arr
    
    @staticmethod
    def defaultWeights() -> "Rewards":
        """Return a Rewards instance representing the default weights."""
        return Rewards(
            steering_smoothness = -0.1,
            throttle_smoothness = -0.1,

            out_of_bounds_penalty = -0.0,
            collision_penalty = -0.0,
            discounted_collision_penalty= -0.1,
            grass_penalty = -0.0,
            discounted_grass_penalty= -0.01,

            team_distance = -0.0,
            lap_time = -0.0,
            discounted_lap_time= -0.0,
            team_lap_time = -0.0,
            discounted_team_lap_time= -0.0,
            placement = 0.0,
            discounted_placement= 0.0,
            team_placement = 0.0,
            discounted_team_placement= 0.0,

            speed = 0.0,
            acceleration = 0.0,
            distance = 0.0,
            discounted_distance = 0.01,

            speedI = 0.0,
            speedII = 0.0,
            speedIII = 0.0,
            speedIV = 0.0,
            speedV = 0.02,

            accelerationI = 0.0,
            accelerationII = 0.0,
            accelerationIII = 0.0,
            accelerationIV = 0.01,
            accelerationV = 0.0,

            angleI = -0.0,
            angleII = -0.0,
            angleIII = -0.0,
            angleIV = -0.0,
            angleV = -0.02,

            distanceI = -0.0,
            distanceII = -0.0,
            distanceIII = -0.1,
            progressReward = 0.0,
            discounted_progressReward= 0.1
        )



def calculate_total_reward(rewards: Rewards, weights: Rewards) -> float:
    return float(np.dot(rewards.as_vector(), weights.as_vector()))
