# Team Car Racing – System Specification

## 1. Overview

Team Car Racing is a car racing simulation built in Unity, where cars can be controlled either by human players or by a python program that can use AI. The Unity simulation serves as the environment for training and testing AI driving behavior.The simulation also includes a network listener that allows external AI programs to control vehicles, making it suitable for neural network integration. Using camera sensors, along with speed and steering inputs, the simulation will send environment observations and listen to controls. Simulation will have realistic vehicle physics like downforce effects, surface dependent wheel friction, and breaking. The game includes functional mirrors to see cars behind. Several pre-made racing tracks are provided for AI training, built from modular road tiles that can also be combined to create new custom tracks.

## 2. Functional Requirements

### 2.1 Unity Side

#### 2.1.1 Simulation Environment

* 3D environment.
* Supports multiple cars and up to one human player.
* Uses a tile-based road system. Modular tracks can be created or edited by connecting pre-made tiles.
* Each car has:

  * Parametrized driving physics
  * Wheel colliders and grip simulation
  * Throttle, braking, and steering control
* Maps for AI training.
* Free-roaming camera to observe the training.

#### 2.1.2 Car System

* Car controller handles car physics and applies input from input providers via interfaces.
* Cameras attached to cars provide visual observations.
* Provides information about speed, steering, position, and collisions.

#### 2.1.3 Observation and Reward System

* System for reward calculation, connecting information from different parts of the simulation to compute rewards.
* Expandable for future reward functions.

#### 2.1.4 Environment Control

* Control script manages:

  * Simulation time and physics stepping
  * Car collisions, checkpoints, and laps
* Serves as the main connection point for the simulation.

### 2.2 Python Side

* Shell for AI integration.
* Sends driving instructions and game commands.
* Receives observations and rewards.

### 2.3 Networking

* Bi-directional communication with Python.
* Unity listens for:

  * Car driving instructions
  * Environment commands
* Unity sends:

  * Observations and rewards.

## 3. Extensibility

* Tile-based maps that can be extended or edited.
* Reward system designed to support new reward types per agent.
* Network protocol easily extendable for new commands.

## 4. Non-functional Requirements

* Performance: Supports multiple agents in simulation. Simulation speed can be controlled and can run without graphics.
* Portability: Windows and Linux.
