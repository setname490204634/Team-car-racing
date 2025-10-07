### DOCUMENTATION
- Unity side is used as simulation for cars, it can take driving input from network connection as well as send output including visual.

## Team Racing/Assets
# Scenes
- cars.unity, includes car prefabs
- freeplay.unity, used to test car driving and maps, it has 1 car connected to user input in the unity.
- roadTiles.unity, contains road tiles prefabs, it is used to edit and add tiles
- Maps.unity, here are build maps from tiles also in prefab form
- mainScene.unity, used to train the AI cars

# roads
- contains road tiles prefabs

# Maps
- contains maps prefabs

# ML-Agents
- outdated, used for ML agents training

# cars
- contains car prefabs
- ICarInputProvider.cs, interface that is used by car to request input
- ICarObserver.cs, used to request output from car sensors, cameras speed, steering
- ICarRewardProvider.cs, not used now

- AgentInputProvider.cs, simple implementation of ICarInputProvider for agents
- PlayerCarInput.cs, WSAD input implemented as ICarInputProvider

- mirror.cs, script that is used for mirrors, it uses camera and plane to render texture on
- carAppearance, changes material of all childrens of the object, with list of exceptions.

- CarFollowCamera, used to move camera with car

