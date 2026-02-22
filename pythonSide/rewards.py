import numpy as np
from dataclasses import dataclass

import numpy as np
from dataclasses import dataclass

@dataclass
class Rewards:
    # input change
    steeringSmoothnessPenalty: float = 0.0
    throttleSmoothnessPenalty: float = 0.0

    # collisions
    outOfBoundsPenalty: float = 0.0
    collisionPenalty: float = 0.0
    grassPenalty: float = 0.0

    # game state
    teamDistancePenalty: float = 0.0
    lapTimePenalty: float = 0.0
    teamLapTimePenalty: float = 0.0
    finalPlacementReward: float = 0.0
    currentPlacementReward: float = 0.0
    teamFinalPlacementReward: float = 0.0
    currentTeamPlacementReward: float = 0.0
    tickPenalty: float = 0.0

    # car controller
    speedReward: float = 0.0
    accelerationReward: float = 0.0
    distanceReward: float = 0.0

    # segments
    speedRewardI: float = 0.0
    speedRewardII: float = 0.0
    speedRewardIII: float = 0.0
    speedRewardIV: float = 0.0
    speedRewardV: float = 0.0

    accelerationRewardI: float = 0.0
    accelerationRewardII: float = 0.0
    accelerationRewardIII: float = 0.0
    accelerationRewardIV: float = 0.0
    accelerationRewardV: float = 0.0

    anglePenaltyI: float = 0.0
    anglePenaltyII: float = 0.0
    anglePenaltyIII: float = 0.0
    anglePenaltyIV: float = 0.0
    anglePenaltyV: float = 0.0

    distancePenaltyI: float = 0.0
    distancePenaltyII: float = 0.0
    distancePenaltyIII: float = 0.0

    progressReward: float = 0.0

    def as_vector(self) -> np.ndarray:
        """Return rewards as a NumPy vector of length 50."""
        arr = np.array([
            # input change
            self.steeringSmoothnessPenalty,
            self.throttleSmoothnessPenalty,

            # collisions
            self.outOfBoundsPenalty,
            self.collisionPenalty,
            self.grassPenalty,

            # game state
            self.teamDistancePenalty,
            self.lapTimePenalty,
            self.teamLapTimePenalty,
            self.finalPlacementReward,
            self.currentPlacementReward,
            self.teamFinalPlacementReward,
            self.currentTeamPlacementReward,
            self.tickPenalty,

            # car controller
            self.speedReward,
            self.accelerationReward,
            self.distanceReward,

            # segments
            self.speedRewardI,
            self.speedRewardII,
            self.speedRewardIII,
            self.speedRewardIV,
            self.speedRewardV,

            self.accelerationRewardI,
            self.accelerationRewardII,
            self.accelerationRewardIII,
            self.accelerationRewardIV,
            self.accelerationRewardV,

            self.anglePenaltyI,
            self.anglePenaltyII,
            self.anglePenaltyIII,
            self.anglePenaltyIV,
            self.anglePenaltyV,
            
            #I and II are not used right now
            self.distancePenaltyI,
            self.distancePenaltyII,
            self.distancePenaltyIII,

            self.progressReward,

            # reserved for later use to make 50 items
            0.0, 0.0, 0.0, 0.0, 0.0,
            0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0
        ], dtype=np.float32)

        return arr
    
    @staticmethod
    def defaultWeights() -> "Rewards":
        """Return a Rewards instance representing the default weights."""
        return Rewards(
            steeringSmoothnessPenalty = 0.0,
            throttleSmoothnessPenalty = 0.0,

            outOfBoundsPenalty = 0.0,
            collisionPenalty = 5.0,
            grassPenalty = 0.03,

            teamDistancePenalty = 0.0,
            lapTimePenalty = 0.0,
            teamLapTimePenalty = 0.0,
            finalPlacementReward = 0.0,
            currentPlacementReward = 0.0,
            teamFinalPlacementReward = 0.0,
            currentTeamPlacementReward = 0.0,
            tickPenalty = 0.0,

            speedReward = 0.0,
            accelerationReward = 0.0,
            distanceReward = 0.0,

            speedRewardI = 0.0,
            speedRewardII = 0.0,
            speedRewardIII = 0.0,
            speedRewardIV = 0.0,
            speedRewardV = 0.1,

            accelerationRewardI = 0.0,
            accelerationRewardII = 0.0,
            accelerationRewardIII = 0.0,
            accelerationRewardIV = 0.0,
            accelerationRewardV = 0.0,

            anglePenaltyI = 0.0,
            anglePenaltyII = 0.0,
            anglePenaltyIII = 0.0,
            anglePenaltyIV = 0.0,
            anglePenaltyV = 0.0,

            distancePenaltyI = 0.0,
            distancePenaltyII = 0.0,
            distancePenaltyIII = 0.0,
            
            progressReward = 1.0
        )

def calculate_total_reward(rewards: Rewards, weights: Rewards) -> float:
    return float(np.dot(rewards.as_vector(), weights.as_vector()))
