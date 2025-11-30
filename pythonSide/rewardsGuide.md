steering_smoothness -0 no change at all 1 maximum change
throttle_smoothness -0 no change at all 1 maximum change
out_of_bounds_penalty -0 nothing, 1 the car is under the map or too far away
collision_penalty -0 nothing, 1 the car collided
discounted_collision_penalty -discounted sum of all collisions penalties
grass_penalty -0 nothing, 1 the car is on grass
discounted_grass_penalty -discounted sum of all grass penalties
team_distance  -sum of diferences of temeates positions
lap_time -lap time in seconds
discounted_lap_time -discounted sum of all lap time in seconds
team_lap_time -teammate lap time in seconds
discounted_team_lap_time -discounted sum of all teammate lap times in seconds
placement -points acording to F1 rating
discounted_placement -discounted sum of all points acording to F1 rating
team_placement -points of teammates acording to F1 rating
discounted_team_placement -discounted sum of all points of teammates acording to F1 rating
speed -speed in forward direction in m/s
acceleration -acceleration in forward direction in m/(s*s)
distance -distance traveled in m
discounted_distance -discounted sum of all distance traveled in m
speedI -speed in special direction in m/s
speedII -speed in special direction in m/s
speedIII -speed in special direction in m/s
speedIV -speed in special direction in m/s
speedV -speed in special direction in m/s
accelerationI -acceleration in special direction in m/(s*s)
accelerationII -acceleration in special direction in m/(s*s)
accelerationIII -acceleration in special direction in m/(s*s)
accelerationIV -acceleration in special direction in m/(s*s)
accelerationV -acceleration in special direction in m/(s*s)
angleI -deviation from angle in special direction in 0.0-1.0 1.0 being 180 degrees off
angleII -deviation from angle in special direction in 0.0-1.0 1.0 being 180 degrees off
angleIII -deviation from angle in special direction in 0.0-1.0 1.0 being 180 degrees off
angleIV -deviation from angle in special direction in 0.0-1.0 1.0 being 180 degrees off
angleV -deviation from angle in special direction in 0.0-1.0 1.0 being 180 degrees off
distanceI -distance to next road segment 0.0-1.0 DONT USE 
distanceII -distance to next next road segment 0.0-1.0 DONT USE 
distanceIII -distance from optimal trajectory aproximation 0-1.0
progressReward -0.0 no progress +-1.0 if moved 
discounted_progressReward -discounted sum of progressReward