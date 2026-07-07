# DOCUMENTATION

This project uses Unity as the simulation backend and Python/RLlib as the training and control side. Unity sends camera and state observations over TCP, Python computes actions, and those actions are sent back to Unity for the next simulation step.

The code itself is the main source of truth; comments and this document are meant to complement it.

## Repository layout

### TeamRacing/Assets

#### Scenes
- Cars.unity: contains the car prefabs used in the simulation.
- Freeplay.unity: lightweight scene for manual driving and map testing.
- RoadTiles.unity: used to edit and preview road tiles and track pieces.
- Maps.unity: used to assemble maps from road prefabs.
- MainScene.unity: the main scene used to run training/inference.

#### Roads
- TilePoints.cs: assigns checkpoint/segment metadata to a single road tile.
- Road prefabs and materials: road, grass, wall, and finish-line assets.
- Halfway hitbox/checkpoint: used by the track logic to support lap progression.

#### Maps
- MapManager.cs: stores available map prefabs and handles switching maps.
- MapSegmentHandler.cs: converts road tiles into a ordered list of track segments and provides spatial directions between checkpoints.

#### Cars
- CarController.cs: main physics and driving controller for each car.
- ICarInputProvider.cs: interface for car input providers.
- AgentInputProvider.cs: input provider used by AI agents.
- PlayerCarInput.cs: keyboard input provider for human driving.
- CarAppearance.cs: applies visuals/materials to the car body.
- CarFollowCamera.cs: camera follow script.

#### AITraining
- CarAgentHandler.cs: connects Unity-side car state and cameras to the AI agent wrapper.
- CarObservationSerializer.cs: packs the observation payload into bytes.
- CarObservationTransmitter.cs: sends observation packets to Python over TCP.
- CarEntry.cs: per-car runtime state including controller, rewards, and race progress.
- CarRaceState.cs: per-car lap and timing state.
- CommandConstants.cs: command protocol enum shared with Python.
- FreeCameraController.cs: free camera for manual inspection.
- gameControlScript.cs: main environment loop, networking, reset/start/stop logic, and placement/lap handling.
- Rewards.cs: reward-state container and reward calculation logic.
- UnityMainThreadDispatcher.cs: helper for dispatching work back to the Unity main thread.
- buffers.cs: packet buffering helpers.

### pythonSide

Current training entrypoints are:
- RllibMARLstarter.py: single-policy PPO training for the shared-policy setup.
- coopcompeMARLstarter.py: team-based PPO setup with two policies, `team_0_policy` and `team_1_policy`.

Other important files:
- unityEnv/UnityMultiCarEnv.py: Gymnasium-compatible multi-agent environment that launches Unity and manages reset/step loops.
- unityEnv/reciever.py: TCP receiver for observation packets from Unity.
- unityEnv/sender.py: TCP sender for driving instructions and control commands.
- unityEnv/CommandConstants.py: Python-side copy of the command enum.
- unityEnv/rewards.py: Python-side reward container used by the environment wrapper.
- unityEnv/agent.py: per-agent observation/action wrapper used by the environment.
- verify_obs.py: small sanity check for observation shape and environment wiring.

## Main runtime loop

The system is driven by a loop in Unity and a loop in Python.

### Unity side
The main loop runs in `gameControlScript.cs` and roughly does the following:
1. Consume queued control commands from the command buffer.
2. If the simulation is running, collect and send observations every `framesPerObservation` frames.
3. Wait until car instructions for the current step have arrived.
4. Apply the received inputs to the cars.
5. Simulate the next physics step.
6. Update segment progress and placement/lap state.

### Python side
The environment wrapper in `pythonSide/unityEnv/UnityMultiCarEnv.py` does the following:
1. Wait until observations are available.
2. Convert them into agent observations.
3. Compute actions from the policy.
4. Send actions back to Unity.
5. Repeat for the next step.

## Physics and car control

The main car controller is `TeamRacing/Assets/Cars/CarController.cs`.

It uses wheel colliders and applies:
- steering and speed-sensitive steering
- throttle and brake torque
- surface-based grip multipliers
- downforce
- wheel friction adjustments

This is the code path that makes the vehicles behave physically in the simulation.

## Lap and placement logic

Lap counting and placement are handled in `gameControlScript.cs`.

The current behavior is:
- `segmentProgress` is a cumulative value that increases when a car moves to the next segment.
- A lap is counted when the car reaches the finish-line segment and its cumulative progress reaches the next full lap threshold.
- Placement is computed by sorting cars by their current `segmentProgress` value, highest first.

This is the current mechanism used for ranking cars during a race.

## Networking and protocol

### Unity -> Python observations
The observation payload is created by `CarObservationSerializer.cs` and sent by `CarObservationTransmitter.cs`.

Current packet structure:
- 1 byte: speed
- 1 byte: steering
- 4 bytes: car ID (`int32`, little-endian)
- 50 floats encoded as reward values in the header
- RGB image bytes for the camera frame

The Python receiver parses this in `pythonSide/unityEnv/reciever.py`.

### Python -> Unity instructions
`pythonSide/unityEnv/sender.py` sends instructions as:
- 4 bytes: car index (`int32`, little-endian)
- 1 byte: steering
- 1 byte: throttle

### Python -> Unity control commands
Control commands are 2-byte packets:
- first byte: command ID
- second byte: optional value

The command values are defined in both:
- `TeamRacing/Assets/AITraining/CommandConstants.cs`
- `pythonSide/unityEnv/CommandConstants.py`

### Current command values

| First byte | Second byte | Description |
|---|---:|---|
| 0 | x | Reset all cars |
| 1 | x | Shuffle car starting positions |
| 2 | x | Set lap count |
| 3 | x | Switch to map index `x` |
| 4 | x | Switch to a random map |
| 5 | x | Change car colours randomly |
| 6 | x | Reset cars to random locations |
| 10 | x | Start simulation |
| 11 | x | Stop simulation |
| 12 | x | Continue simulation |
| 20 | x | Set simulation refresh rate to `x`|
| 21 | x | Set observations-per-frame interval to `x`|
| 22 | x | Set max steering change per tick to `x`|
| 30 | x | Set realtime mode |
| 31 | x | Set unlimited simulation speed |

## Training entrypoints

### Single-policy training
Run:
- `python pythonSide/RllibMARLstarter.py --checkpoint pythonSide/checkpoints/checkpoint_550`

This uses one shared policy for all agents.

### Team-based training
Run:
- `python pythonSide/coopcompeMARLstarter.py --checkpoint pythonSide/checkpoints/checkpoint_550`

This uses two team policies, `team_0_policy` and `team_1_policy`, and seeds them from the checkpoint module path when a checkpoint is supplied.

## Notes

- The Python side currently expects the Unity executable at `TeamRacing/builds/TeamRacing.exe` unless overridden in the environment wrapper.
- The environment wraps the Unity process automatically and starts the TCP servers for control, car instructions, and observations.
- The current observation shape is configured in `UnityMultiCarEnv.py` and may be grayscale-history dependent.
