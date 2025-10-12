# DOCUMENTATION
- Unity side is used as simulation for cars, it can take driving input from network connection as well as send output including visual.

## files and folders

## TeamRacing/Assets
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
- contains mirror prefabs
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

## pythonSide

- main.py, runs comumnication
- sender.py, sends car driving instructions to unity
- reciever.py, recievs and unpacks observation from unity

## Editing and Adding Roads

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

## Creating Maps

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

### Car Creation and Editing Guide

#### Creating a New Car

1. Add a new car GameObject
   - In Unity’s Hierarchy, create a new empty GameObject.
   - Name it appropriately

2. Add Wheel Colliders
   - Each car requires four Wheel Colliders.
   - Create 4 empty child objects named:
     - `WheelCollider_FL` (Front Left)
     - `WheelCollider_FR` (Front Right)
     - `WheelCollider_RL` (Rear Left)
     - `WheelCollider_RR` (Rear Right)
   - Add the Wheel Collider component to each of them.
   - Position them exactly where the wheels should be located.

3. **Attach Car Controller Script**
   - Add the `CarController` script to the car’s main object.
   - Set parameters such as:
     - `Drive Speed`
     - `Brake Force`
     - `Downforce`
   - Assign the four Wheel Colliders to their respective fields in the script.

4. Add Colliders
   - To handle physical collisions, add a Box Collider to the main car object.
   - Adjust its size to cover the body of the car properly.

5. Car Appearance
   - Add the `CarAppearance` script to the main object.
   - In the script fields:
     - Assign the Body Material (used for coloring).
     - Assign Ignored Renderers, such as wheels or mirrors, that should not change color.

#### Optional Add-ons

1. **Mirrors**
   - You can either:
     - Use a Mirror Prefab from the project, or  
     - Add the `Mirror.cs` script manually.
   - If adding manually:
     - Create a plane for the mirror surface.
     - Assign a Camera in the `Mirror.cs` script.
     - Adjust reflection angle and field of view as needed.

2. Manual (WSAD) Control
   - Add the `PlayerCarInput.cs` script.
   - This allows you to drive the car using keyboard.
   - Optionally, enable speed sensitive steering in the inspector.

3. AI Agent Integration
   - To make the car controllable by the AI system:
     - Add `AgentInputProvider.cs`.
     - Add `CarAgentHandler.cs`.
   - Assign cameras in the CarAgentHandler.
   - Configure any additional parameters such as camera resolution.


## Networking and python integration

Unity side is used as simulation for cars, it can take driving input from network connection as well as send output including visual.

### Python Side

* **sender.py**: Sends driving instructions and control commands to Unity:

  * `send_car_instruction(car_id: int, steering: int, throttle: int)` — sends 32-bit car ID + 1-byte steering + 1-byte throttle.
  * `send_command(command_byte: int)` — sends single-byte commands for control like reset or shuffle.
* **reciever.py**: Receives merged RGB24 camera observations from Unity and extracts header information (speed, steering, car ID, reward). Returns a NumPy array representing the merged image.

### Communication Protocol

- Observation Packet (Unity -> Python)

  * 10 bytes Header: 
    * 1 byte speed
    * 1 byte steering
    * 4 bytes car ID (int32, little-endian)
    * 4 bytes reward (int32, little-endian)
  * RGB24 camera image

- Driving Instruction Packet (Python -> Unity)

  * 4 bytes car ID (int32, little-endian)
  * 1 byte steering
  * 1 byte throttle

- Game Control Packet (Python -> Unity)

   * 1 byte command number
   * 1 byte extra value (if command does not have options number is ignored)



### Example Usage

#### Python Side

```python
import sender
import reciever

# Send command to Unity
sender.send_command(0)  # Reset Cars

# Send driving instructions
sender.send_car_instruction(car_id=3, steering=120, throttle=200)

# Receive observation
header, image = reciever.receive_observations()
```

### Notes

* Merged image simplifies Python side by combining left/right cameras.
* RGB24 format ensures 8 bits per channel with no transparency.
* Unity and Python ports must match for successful communication.
* Use threading or async to receive observations while sending commands for real-time control.
