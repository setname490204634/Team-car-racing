# DOCUMENTATION
- Unity side is used as simulation for cars, it can take driving input from network connection as well as send output including visual.

## files and folders

## Team Racing/Assets
### /Scenes
- Cars.unity, includes car prefabs
- Freeplay.unity, used to test car driving and maps, it has 1 car connected to user input in the unity.
- RoadTiles.unity, contains road tiles prefabs, it is used to edit and add tiles
- Maps.unity, here are build maps from tiles also in prefab form
- MainScene.unity, used to run enviroment

### /Roads
- contains road tiles prefabs
- grass, road, wall, finish line materials
- wall prefabs

### /Maps
- contains maps prefabs

### /Cars
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


# Editing and Adding Roads

### Edit and Add Roads

1. Open the Road Tiles Scene
   - Navigate to `Assets/Scenes/RoadTiles.unity` and open it in Unity.

2. Editing Road Tiles
   - All road segments in this scene are prefabs.  
   - When you modify a road prefab, make sure to click "Apply to All Prefabs" (top right of the Inspector window) to propagate your changes across all instances on every map.

3. Tile Grid and Sizes  
   - The road system is based on a 10x10 tile grid.  
   - There are two main road widths:
     - 10-tile wide roads with walls.
     - 20-tile wide roads with grass and walls.
   - To check the exact measurements, inspect the road prefabs in the scene.

4. Grass and Surface Notes  
   - Road tiles do not include grass — it’s added later during map creation to reduce the number of rendered planes and improve performance.  
   - All road meshes are placed 0.01 units above ground level to prevent grass-road clipping.

5. Saving and Prefab Creation  
   - If you create a new road or modify an existing one, make sure to save it as a prefab.  
   - Add new or updated road prefabs to the `/Assets/Roads` directory.

# Creating Maps

### Add or Edit Maps

1. Open the Maps Scene  
   - Navigate to `Assets/Scenes/Maps.unity` and open it in Unity.

2. Building the Map  
   - Build the layout by tiling road prefabs from the `/Assets/Roads` folder.  
   - Adjust and align them according to your map design.

3. Scaling the Map  
   - Once the map is complete, select the parent map object.  
   - Scale the entire map by 1.2x to fit the intended world proportions.

4. Saving the Map as a Prefab  
   - After completing and scaling your map, make it into a prefab.  
   - Save it in the `/Assets/Maps` directory.