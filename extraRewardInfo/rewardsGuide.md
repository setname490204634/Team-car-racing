Reward Name                   | Range      | Multiagent | Unit                 | Description
------------------------------|------------|------------|----------------------|----------------------------------------------------------
steeringSmoothnessPenalty     | -1 – 0     | No         | None                 | 0 = no change, -1 = maximum steering change
throttleSmoothnessPenalty     | -1 – 0     | No         | None                 | 0 = no change, -1 = maximum throttle change
outOfBoundsPenalty            | -1 - 0     | No         | None                 | 0 = normal, -1 = car under map or too far away
collisionPenalty              | -1 - 0     | No         | None                 | 0 = normal, -1 = car collided
grassPenalty                  | -1 - 0     | No         | None                 | 0 = on track, -1 = car on grass
teamDistancePenalty           | -∞ – 0     | Yes        | m                    | Sum of differences of teammates position from "optimal" distance (10m)
lapTimePenalty                | -∞ – 0     | No         | seconds              | 0 = lap was not completed, Lap time in seconds
teamLapTimePenalty            | -∞ – 0     | Yes        | seconds              | 0 = lap was not completed, sum of teammates lap times in seconds
finalPlacementReward          | 0 – 25     | Yes        | points               | Points according to F1 rating
currentPlacementReward        | 0 – 25     | Yes        | points               | Points according to F1 rating at current tick
teamFinalPlacementReward      | 0 – 25*n   | Yes        | points               | Sum of teammates points according to F1 rating, n = number of teammates
currentTeamPlacementReward    | 0 – 25*n   | Yes        | points               | Sum of teammates points according to F1 rating at current tick, n = number of teammates
tickPenalty                   | -1         | No         | None                 | Penalty per step/tick
speedReward                   | –∞ – ∞     | No         | m/s                  | Forward speed, what would car tochometer show
accelerationReward            | –∞ – ∞     | No         | m/s²                 | Forward acceleration in m/s²
distanceReward                | 0 – ∞      | No         | m                    | Distance traveled in meters from last step in any direction
speedRewardI                  | –∞ – ∞     | No         | m/s                  | Speed in special direction I
speedRewardII                 | –∞ – ∞     | No         | m/s                  | Speed in special direction II
speedRewardIII                | –∞ – ∞     | No         | m/s                  | Speed in special direction III
speedRewardIV                 | –∞ – ∞     | No         | m/s                  | Speed in special direction IV
speedRewardV                  | –∞ – ∞     | No         | m/s                  | Speed in special direction V
accelerationRewardI           | –∞ – ∞     | No         | m/s²                 | Acceleration in special direction I
accelerationRewardII          | –∞ – ∞     | No         | m/s²                 | Acceleration in special direction II
accelerationRewardIII         | –∞ – ∞     | No         | m/s²                 | Acceleration in special direction III
accelerationRewardIV          | –∞ – ∞     | No         | m/s²                 | Acceleration in special direction IV
accelerationRewardV           | –∞ – ∞     | No         | m/s²                 | Acceleration in special direction V
anglePenaltyI                 | -1 – 0     | No         | None                 | Deviation from angle in special direction I (-1 = 180° off)
anglePenaltyII                | -1 – 0     | No         | None                 | Deviation from angle in special direction II (-1 = 180° off)
anglePenaltyIII               | -1 – 0     | No         | None                 | Deviation from angle in special direction III (-1 = 180° off)
anglePenaltyIV                | -1 – 0     | No         | None                 | Deviation from angle in special direction IV (-1 = 180° off)
anglePenaltyV                 | -1 – 0     | No         | None                 | Deviation from angle in special direction V (-1 = 180° off)
distancePenaltyI              | 0          | No         | None                 | 0 for now
distancePenaltyII             | 0          | No         | None                 | 0 for now
distancePenaltyIII            | –∞ – 0     | No         | m                    | Distance from optimal trajectory approximation, if trajectory is straight then its always 0
progressReward                | -1 – 1     | No         | None                 | 0 = no progress, +-1 = moved forward/backward in checkpoints
