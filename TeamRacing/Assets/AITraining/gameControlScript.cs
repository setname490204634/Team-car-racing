using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

public class gameControlScript : MonoBehaviour
{
    public struct TransformEntry
    {
        public Vector3 position;
        public Quaternion rotation;
    }

    [Header("Assign cars in Inspector")]
    public List<GameObject> assignedCarObjects;  // inspector list only

    [Header("car body materials")]
    public List<Material> bodyMaterials; // assign in inspector

    private List<CarEntry> cars = new List<CarEntry>(); //never change order of cars! (it is used as index)
    private List<TransformEntry> startTransforms = new List<TransformEntry>(); //starting locations, this can be permutated
    private int lapCount = 1;
    public List<int> winners = new List<int>();
    private int carCount = 1;

    private TcpListener controlServer;
    private TcpListener instructionsServer;
    private Thread controlThread;
    private Thread instructionsThread;
    private bool running = false;

    private CarObservationTransmitter transmitter;

    // Buffers
    private InstructionBuffer instructionBuffer;
    private GameCommandBuffer commandBuffer;

    [Header("Server Settings")]
    public int controlPort = 5005;
    public int carInstructionsPort = 5006;
    public int observationTransmitterPort = 5007;

    [Header("Simulation Settings (can be changed with commands)")]
    public int fixedHz = 60;           // physics frequency
    public int framesPerObservation = 5;  // send obs every N frames
    private float fixedDt;
    private long tickCount = 0;

    private enum State { Idle, WaitingToStart, Running, Stopped }
    private State state = State.Idle;
    private bool observationsSent = false;

    public MapSegmentHandler currentSegmentHandler;
    public MapManager mapManager;
    //the fifth checkpoint is finish line
    private int _finishLineSegmentIndex = 5;

    void Start()
    {
        InitializeParamsFromArgs(Environment.GetCommandLineArgs());

        mapManager = GetComponent<MapManager>();
        this.currentSegmentHandler = mapManager.currentSegmentHandler;

        Application.runInBackground = true;
        //The simulation will run in real time
        SetRealtimeMode();
        Physics.simulationMode = SimulationMode.Script;
        UpdateSimulationDeltaTime();

        InitializeCars();
        ResetCars();

        InitializeNetworking();

        return;
    }
    void Update()
    {
        // Handle commands queued by the control thread
        var commands = commandBuffer.ConsumeAll();
        foreach ((byte command, byte value) in commands)
            ProcessCommand(command, value);

        if (state == State.Running)
        {
            //every framesPerObservation frame get instructions and send observations
            if (tickCount % framesPerObservation == 0)
            {
                if (observationsSent == false)
                {
                    // After simulating N frames, collect + send observations
                    transmitter.CollectObservations();
                    transmitter.SendObservations();
                    observationsSent = true;
                }
                // Wait until we have full instruction set
                while (instructionBuffer.HasAllInstructions() != true)
                {
                    return; //wait
                }
                observationsSent = false;
                var instructions = instructionBuffer.ConsumeAll();
                ApplyCarInputs(instructions);
            }
            Physics.Simulate(fixedDt);
            tickCount++;
            UpdateCarSegmentPos();
            HandleCarCollisionsAndPlacement();
        }
    }

    //also does placement for legacy reasons (lap was done based on colliders before now its based on segment index)
    private void HandleCarCollisionsAndPlacement()
    {
        int[] positions = new int[cars.Count];
        for (int i = 0; i < cars.Count; i++)
        {
            CarEntry car = cars[i];
            positions[i] = car.segmentProgress;

            (bool collided, bool onGrass, bool outOfBounds) = car.controller.ConsumeCollisionFlags();
            if (collided)
            {
                car.rewards.RegisterCollision();
            }

            if (onGrass)
            {
                car.rewards.RegisterOnGrass();
            }
            if (outOfBounds)
            {
                car.rewards.RegisterOutOfBounds();
            }

            if (car.segmentIndex == _finishLineSegmentIndex &&
                car.segmentProgress >= (car.raceState.lapCount + 1) * this.currentSegmentHandler.road.Count)
            {
                car.raceState.lapCount ++;
                if (this.lapCount == car.raceState.lapCount && !car.raceState.finished)
                {
                    this.winners.Add(i);
                    car.rewards.RegisterFinalPlacement(winners.Count);
                    car.raceState.finished = true;
                    foreach (int ID in car.rewards.teammatesID)
                    {
                        cars[ID].rewards.RegisterFinalTeammatePlacement(winners.Count);
                    }
                }
            }
        }

        var sorted = new List<(int index, int progress)>();

        for (int i = 0; i < positions.Length; i++)
        {
            sorted.Add((i, positions[i]));
        }

        // Sort descending (highest progress = best position)
        sorted.Sort((a, b) => b.progress.CompareTo(a.progress));

        // Assign placements
        for (int place = 0; place < sorted.Count; place++)
        {
            int carIndex = sorted[place].index;
            int placement = place + 1; // 1-based placement

            cars[carIndex].rewards.RegisterCurrentPlacement(placement);

            // Teammates
            foreach (int teammateID in cars[carIndex].rewards.teammatesID)
            {
                cars[teammateID].rewards.RegisterCurrentTeammatePlacement(placement);
            }
        }
    }

    void OnApplicationQuit()
    {
        running = false;

        controlServer?.Stop();
        instructionsServer?.Stop();

        if (controlThread?.IsAlive ?? false) controlThread.Abort();
        if (instructionsThread?.IsAlive ?? false) instructionsThread.Abort();
    }

    public GameObject GetCarByID(int id)
    {
        return cars[id].carObject;
    }

    private void InitializeParamsFromArgs(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--controlPort":
                    if (i + 1 < args.Length && int.TryParse(args[i + 1], out int cPort))
                        controlPort = cPort;
                    break;

                case "--carInstructionsPort":
                    if (i + 1 < args.Length && int.TryParse(args[i + 1], out int instrPort))
                        carInstructionsPort = instrPort;
                    break;

                case "--observationPort":
                    if (i + 1 < args.Length && int.TryParse(args[i + 1], out int obsPort))
                        observationTransmitterPort = obsPort;
                    break;
                case "--carCount":
                    if (i + 1 < args.Length && int.TryParse(args[i + 1], out int carCount))
                        this.carCount = carCount;
                    break;
            }
        }
    }
    //start both recievers
    private void InitializeNetworking()
    {
        running = true;

        instructionsThread = new Thread(ListenForCarInstructions) { IsBackground = true };
        instructionsThread.Start();

        controlThread = new Thread(ListenForControlCommands) { IsBackground = true };
        controlThread.Start();


        // Prepare dispatcher if used elsewhere
        var _ = UnityMainThreadDispatcher.Instance();
    }

    private void UpdateSimulationDeltaTime()
    {
        fixedDt = 1f / fixedHz;
        Time.fixedDeltaTime = fixedDt;
    }

    //will fill the carEntry list "cars"
    private void InitializeCars()
    {
        RemoveExtraCars();
        int index = 0;
        foreach (var obj in assignedCarObjects)
        {
            if (obj == null) continue;

            CarEntry entry = new CarEntry
            {
                carObject = obj,
                agent = obj.GetComponent<CarAgent>(),
                inputProvider = obj.GetComponent<ICarInputProvider>(),
                controller = obj.GetComponent<CarController>(),
                raceState = new CarRaceState(),
                segmentIndex = 1, // always start from the 1, that coresponds to the middle of start tile
                carAppearance = obj.GetComponent<CarAppearance>()
            };

            //0 and 1, 2 and 3...
            //pairs teammates using modulo magic
            List<int> teammatesID = new List<int>();
            if (assignedCarObjects.Count > index + 1)
            {
                int teammateID = index + 1;
                if (teammateID % 2 == 0) teammateID -= 2;
                teammatesID.Add(teammateID);
            }
            entry.rewards = new RewardsCalculator(entry, this, this.currentSegmentHandler, teammatesID);

            cars.Add(entry);

            startTransforms.Add(new TransformEntry
            {
                position = obj.transform.position,
                rotation = obj.transform.rotation
            });

            index++;
        }

        // since i wanted to avoid creating buffer for car instructions before the list is filled buffers are created here
        // even when its not a logically good place is better than having inicialization order
        instructionBuffer = new InstructionBuffer(cars.Count);
        commandBuffer = new GameCommandBuffer();
        transmitter = new CarObservationTransmitter("127.0.0.1", observationTransmitterPort, cars);
    }

    public void UpdateCarSegmentPos()
    {
        //the refresh rate should be fast enough so the car does not skip a segment, but even if it does so it would only delay the update
        for (int i = 0; i < cars.Count; i++)
        {
            var entry = cars[i];
            int oldIndex = entry.segmentIndex;
            int newIndex = this.currentSegmentHandler.GetClosestIndex(oldIndex, entry.controller.position2D);
            if ((oldIndex + 1) % this.currentSegmentHandler.road.Count == newIndex)//moved forward
            {
                entry.segmentProgress ++;
                entry.rewards.RegisterProgressReward(1.0f);
            }
            else if ((oldIndex - 1 + this.currentSegmentHandler.road.Count) % this.currentSegmentHandler.road.Count == newIndex)//moved back
            {
                entry.segmentProgress --;
                entry.rewards.RegisterProgressReward(-1.0f);
            }
            //when passing start line for the first time reset the segment progress to match other cars (now matter how many checkpoins the car passed it will be newIndex after passing start)
            //it does not affect progress reward
            if (newIndex == _finishLineSegmentIndex && entry.segmentProgress < (this.currentSegmentHandler.road.Count >> 1))
            {
                entry.segmentProgress = newIndex;
            }
            entry.segmentIndex = newIndex;
        }
        return;
    }

    // Reset all cars to their start transforms
    public void ResetCars()
    {
        this.winners.Clear();
        for (int i = 0; i < cars.Count; i++)
        {
            var entry = cars[i];
            var t = startTransforms[i];
            Rigidbody rb = entry.carObject.GetComponent<Rigidbody>();
            if (rb != null)
            {
                // reset physics-based position & rotation
                rb.position = t.position;
                rb.rotation = t.rotation;
                rb.Sleep(); // ensures physics doesnt move it on the next tick
            }
            
            //it is important to call entry.reset here since the cars position has to be stored in this line (after its moved to starting position)
            entry.Reset();
        }
        Debug.Log("Cars have been reset.");
    }

    // will permutate starting locations randomly
    public void ShuffleStartTransforms()
    {
        System.Random rng = new System.Random();

        for (int i = startTransforms.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            var temp = startTransforms[i];
            startTransforms[i] = startTransforms[j];
            startTransforms[j] = temp;
        }

        Debug.Log("Start positions have been randomized.");
    }

    // Apply inputs directly to car inputProviders by car index
    private void RemoveExtraCars()
    {
        for (int i = assignedCarObjects.Count - 1; i >= carCount; i--)
        {
            Destroy(assignedCarObjects[i]);
            assignedCarObjects.RemoveAt(i);
        }
    }
    public void ApplyCarInputs(List<(CarInput input, int carIndex)> inputs)
    {
        foreach (var (input, carIndex) in inputs)
        {
            cars[carIndex].inputProvider.SetInput(input);
        }
    }
    private void StartGame()
    {
        state = State.Running;
        Debug.Log("Game started.");
        tickCount = 0;
        //send observations to so the AI knows it started and will not waste first move
        transmitter.Connect();
        observationsSent = false;
    }
    private void StopGame()
    {
        state = State.Stopped;
        Debug.Log("Game stopped.");
    }
    private void ContinueGame()
    {
        state = State.Running;
        Debug.Log("Game unpaused.");
    }
    void SetRealtimeMode()
    {
        QualitySettings.vSyncCount = 1;
        Application.targetFrameRate = 60;
    }
    void SetUnlimitedSimulationSpeed()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;
    }
    void SetMaxSteeringChange(byte value)
    {
        for (int i = 0; i < cars.Count; i++)
        {
            var entry = cars[i];
            entry.controller.maxSteeringChange = value;
        }
    }
    void ChangeCarColoursRandomly()
    {
        for (int i = 0; i < cars.Count; i++)
        {
            var entry = cars[i];
            entry.carAppearance.SetMaterial(bodyMaterials[UnityEngine.Random.Range(0, bodyMaterials.Count)]);
            entry.carAppearance.ApplyMaterial();
        }
    }
    void ResetCarToRandomStartLocation()
    {
        // works similar to reset only it makes the car start at random location
        if (cars.Count > currentSegmentHandler.road.Count)
        {
            Debug.LogError("Not enough road segments for unique car placement");
            return;
        }

        List<int> segmentIndices = new List<int>();

        for (int i = 0; i < currentSegmentHandler.road.Count; i++)
        {
            segmentIndices.Add(i);
        }

        for (int i = segmentIndices.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (segmentIndices[i], segmentIndices[j]) = (segmentIndices[j], segmentIndices[i]);
        }

        for (int i = 0; i < cars.Count; i++)
        {
            var entry = cars[i];
            int segmentIndex = segmentIndices[i];

            Vector2 segmentPos = currentSegmentHandler.road[segmentIndex];

            Rigidbody rb = entry.carObject.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.position = new Vector3(segmentPos.x, 0.2f, segmentPos.y);

                Vector2 dir2D = currentSegmentHandler.GetVectorI(segmentIndex, segmentPos);
                Vector3 forward = new Vector3(dir2D.x, 0f, dir2D.y);

                rb.rotation = Quaternion.LookRotation(forward, Vector3.up);
                rb.Sleep();
            }

            // Must be after movement
            entry.Reset();
            entry.segmentIndex = segmentIndex;
        }
    }
    private void ProcessCommand(byte command, byte value)
    {
        switch ((CommandCode)command)
        {
            case CommandCode.Reset:
                UnityMainThreadDispatcher.Instance().Enqueue(() => ResetCars());
                break;

            case CommandCode.ShuffleCars:
                UnityMainThreadDispatcher.Instance().Enqueue(() => ShuffleStartTransforms());
                break;

            case CommandCode.SetLapCount:
                this.lapCount = value;
                break;

            case CommandCode.ChangeMap:
                mapManager.LoadMap(value);
                this.currentSegmentHandler = mapManager.currentSegmentHandler;
                break;

            case CommandCode.ChangeMapRandom:
                mapManager.LoadRandomMap();
                this.currentSegmentHandler = mapManager.currentSegmentHandler;
                break;

            case CommandCode.ChangeCarColoursRandomly:
                ChangeCarColoursRandomly();
                break;

            case CommandCode.ResetCarToRandomStartLocation:
                UnityMainThreadDispatcher.Instance().Enqueue(() => ResetCarToRandomStartLocation());
                break;

            case CommandCode.StartSimulation:
                StartGame();
                break;

            case CommandCode.StopSimulation:
                StopGame();
                break;

            case CommandCode.ContinueSimulation:
                ContinueGame();
                break;

            case CommandCode.UpdateDeltaTime:
                this.fixedHz = value;
                UpdateSimulationDeltaTime();
                break;

            case CommandCode.SetFramesPerObservation:
                this.framesPerObservation = value;
                break;

            case CommandCode.SetMaxSteeringChange:
                SetMaxSteeringChange(value);
                break;

            case CommandCode.RealtimeSpeed:
                SetRealtimeMode();
                break;

            case CommandCode.UnlimitedSpeed:
                SetUnlimitedSimulationSpeed();
                break;

            default:
                Debug.LogWarning("Unknown command byte: " + command);
                break;
        }
    }
    private void ListenForControlCommands()
    {
        try
        {
            controlServer = new TcpListener(IPAddress.Any, controlPort);
            controlServer.Start();
            Debug.Log("Listening for commands on controlPort " + controlPort);

            while (running)
            {
                TcpClient client = controlServer.AcceptTcpClient();
                Debug.Log("Control client connected");

                ThreadPool.QueueUserWorkItem(_ => HandleControlClient(client));
            }
        }
        catch (SocketException e)
        {
            Debug.Log("Socket exception (control): " + e);
        }
    }
    private void HandleControlClient(TcpClient client)
    {
        try
        {
            using (client)
            using (NetworkStream stream = client.GetStream())
            {
                byte[] buffer = new byte[2];

                while (running && client.Connected)
                {
                    int totalRead = 0;

                    // Ensure full packet (2 bytes)
                    while (totalRead < 2)
                    {
                        int bytesRead = stream.Read(buffer, totalRead, 2 - totalRead);
                        if (bytesRead == 0)
                        {
                            Debug.Log("Control client disconnected");
                            return;
                        }
                        totalRead += bytesRead;
                    }

                    commandBuffer.EnqueueCommand(buffer[0], buffer[1]);
                }
            }
        }
        catch (Exception e)
        {
            Debug.Log("Control client error: " + e);
        }
    }
    private void ListenForCarInstructions()
    {
        try
        {
            instructionsServer = new TcpListener(IPAddress.Any, carInstructionsPort);
            instructionsServer.Start();
            Debug.Log("Listening for car instructions on port " + carInstructionsPort);

            while (running)
            {
                TcpClient client = instructionsServer.AcceptTcpClient();
                Debug.Log("Car client connected");

                ThreadPool.QueueUserWorkItem(_ => HandleCarClient(client));
            }
        }
        catch (SocketException e)
        {
            Debug.Log("Socket exception (instructions): " + e);
        }
    }
    private void HandleCarClient(TcpClient client)
    {
        try
        {
            using (client)
            using (NetworkStream stream = client.GetStream())
            {
                byte[] buffer = new byte[6];

                while (running && client.Connected)
                {
                    int totalRead = 0;

                    // Ensure full packet (6 bytes)
                    while (totalRead < 6)
                    {
                        int bytesRead = stream.Read(buffer, totalRead, 6 - totalRead);
                        if (bytesRead == 0)
                        {
                            Debug.Log("Car client disconnected");
                            return;
                        }
                        totalRead += bytesRead;
                    }

                    int carIndex = BitConverter.ToInt32(buffer, 0);
                    byte steeringByte = buffer[4];
                    byte throttleByte = buffer[5];

                    CarInput input = new CarInput
                    {
                        Steering = steeringByte,
                        Throttle = throttleByte,
                        UseSpeedSteering = true
                    };

                    instructionBuffer.AddInstruction(carIndex, input);
                }
            }
        }
        catch (Exception e)
        {
            Debug.Log("Car client error: " + e);
        }
    }
}