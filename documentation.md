# DOCUMENTATION
- Unity side is used as simulation for cars, it can take driving input from network connection as well as send output including visual.

## Team Racing/Assets
### /Scenes
- cars.unity, includes car prefabs
- freeplay.unity, used to test car driving and maps, it has 1 car connected to user input in the unity.
- roadTiles.unity, contains road tiles prefabs, it is used to edit and add tiles
- Maps.unity, here are build maps from tiles also in prefab form
- mainScene.unity, used to run enviroment

### /roads
- contains road tiles prefabs
- grass, road, wall, finish line materials
- wall prefabs

### /Maps
- contains maps prefabs

### /cars
- contains car prefabs
- contains car tyre texture
- contains car colours as materials
- ICarInputProvider.cs, interface that is used by a car to request input
- ICarObserver.cs, used to request output from car sensors, cameras speed, steering
- ICarRewardProvider.cs, not used now

- CarController.cs, 

- AgentInputProvider.cs, simple implementation of ICarInputProvider for agents
- PlayerCarInput.cs, WSAD input implemented as ICarInputProvider

- Mirror.cs, script that is used for mirrors, it uses camera and plane to render texture on
- CarAppearance.cs, changes material of all childrens of the object, with list of exceptions.

- CarFollowCamera.cs, used to move camera with car

### /AITraining
- CarAgentHandler.cs, represents unity instance of agent, connects cameras to output and agent input to car controller
- CarObservationSerializer.cs, packs observation into byte array
- CarObservationTransmitter.cs, sends packed observation via network
- FreeCameraController.cs, used to move camera with wsad and mouse to observe how the cars behave from 3rd person
- GameControlScript.cs, manages envirometn input/output, handles restarting and the game
- UnityMainThreadDispatcher.cs, used to execute tasks on main thread.