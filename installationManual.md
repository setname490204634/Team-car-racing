## Free Drive Mode (Manual WSAD Control)

This mode lets you open the game and freely drive a single car around the map using **WSAD** controls.

### Steps

1. **Install Requirements**
   - Install **Unity**.
   - Install **Visual Studio** with the **“Game development with Unity”** workload.

2. **Clone the Repository**
   ```
   git clone https://github.com/setname490204634/Team-car-racing
   ```

3. **Open the Project**
   - Launch **Unity**.
   - Open the cloned project folder.

4. **Load the Freeplay Scene**
   - In Unity, navigate to `Assets/Scenes/`.
   - Open the **`freeplay`** scene.

5. **Run the Game**
   - Click **Play** ▶️ (top center of the Unity editor).

6. **Stop the Game**
   - Click **Stop** ■ (top center of the Unity editor).

---

## Agent Environment Mode (For Python Integration)

This mode runs the simulation and communicates with a Python script — sending observations and receiving control instructions.

### Steps

1. **Install Requirements**
   - Install **Unity**.
   - Install **Visual Studio** with the **“Game development with Unity”** workload.
   - Install **Python**.

2. **Clone the Repository**
   ```
   git clone https://github.com/setname490204634/Team-car-racing
   ```

3. **Open the Project**
   - Launch **Unity**.
   - Open the cloned project folder.

4. **Load the Main Scene**
   - In Unity, navigate to `Assets/Scenes/`.
   - Open the **`mainScene`** scene.

5. **Run the Simulation**
   - Click **Play** ▶️ in Unity.
   - In a terminal, run:
     ```
     python pythonSide/main.py
     ```

6. **Stop the Simulation**
   - Click **Stop** ■ in Unity.
