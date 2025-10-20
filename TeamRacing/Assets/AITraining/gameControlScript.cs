using System;
using System.Collections.Generic;
using System.Data;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;

public class gameControlScript : MonoBehaviour
{
    public class CarEntry
    {
        public GameObject carObject;
        public CarAgent agent; //can be null
        public ICarInputProvider inputProvider;
        public Rewards rewards;
    }
    public struct TransformEntry
    {
        public Vector3 position;
        public Quaternion rotation;
    }

    [Header("Assign cars in Inspector")]
    public List<GameObject> assignedCarObjects;  // inspector list only

    private List<CarEntry> cars = new List<CarEntry>();
    private List<TransformEntry> startTransforms = new List<TransformEntry>();

    private TcpListener controlServer;
    private TcpListener instructionsServer;
    private Thread controlThread;
    private Thread instructionsThread;
    private bool running = false;

    // Buffers
    private InstructionBuffer instructionBuffer;
    private GameCommandBuffer commandBuffer;

    private CarObservationTransmitter transmitter;

    [Header("Server Settings")]
    public int controlPort = 5005;
    public int carInstructionsPort = 5006;
    public int observationTransmitterPort = 5007;

    [Header("Simulation Settings (can be changed with commands)")]
    public int fixedHz = 48;           // physics frequency
    public int framesPerObservation = 8;  // send obs every N frames
    private float fixedDt;

    private enum State { Idle, WaitingToStart, Running, Stopped }
    private State state = State.Idle;

    void Start()
    {
        InitializeCars();

        InitializeNetworking();

        Physics.simulationMode = SimulationMode.Script;
        updateSimulationFramerate();
    }
    void Update()
    {
        // Handle commands queued by the control thread
        var commands = commandBuffer.ConsumeAll();
        foreach ((byte command, byte value) in commands)
            ProcessCommand(command, value);

        if (state == State.Running)
        {
            // Wait until we have full instruction set
            var instructions = instructionBuffer.ConsumeAll();
            ApplyCarInputs(instructions);

            // Simulate N frames
            for (int i = 0; i < framesPerObservation; i++)
            {
                Physics.Simulate(fixedDt);
            }

            // After simulating N frames, collect + send observations
            transmitter.CollectObservations();
            transmitter.SendObservations();
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

    private void InitializeNetworking()
    {
        running = true;

        controlThread = new Thread(ListenForControlCommands) { IsBackground = true };
        controlThread.Start();

        instructionsThread = new Thread(ListenForCarInstructions) { IsBackground = true };
        instructionsThread.Start();

        // Prepare dispatcher if used elsewhere
        var _ = UnityMainThreadDispatcher.Instance();
    }

    private void updateSimulationFramerate()
    {
        fixedDt = 1f / fixedHz;
        Time.fixedDeltaTime = fixedDt;
    }

    private void InitializeCars()
    {
        int index = 0;
        foreach (var obj in assignedCarObjects)
        {
            if (obj == null) continue;

            CarEntry entry = new CarEntry
            {
                carObject = obj,
                agent = obj.GetComponent<CarAgent>(),
                inputProvider = obj.GetComponent<ICarInputProvider>()
            };

            int teammateID = index + 1;
            if (teammateID % 2 == 1) teammateID -= 2;
            entry.rewards = new Rewards(entry.agent, this, obj, Rewards.Default, teammateID);

            cars.Add(entry);

            startTransforms.Add(new TransformEntry
            {
                position = obj.transform.position,
                rotation = obj.transform.rotation
            });

            index++;
        }

        instructionBuffer = new InstructionBuffer(cars.Count);
        commandBuffer = new GameCommandBuffer();
        transmitter = new CarObservationTransmitter("127.0.0.1", observationTransmitterPort, cars);
    }

    // Reset all cars to their start transforms
    public void ResetCars()
    {
        for (int i = 0; i < cars.Count; i++)
        {
            var entry = cars[i];
            var t = startTransforms[i];

            Rigidbody rb = entry.carObject.GetComponent<Rigidbody>();
            if (rb != null)
            {
                // reset physics-based position & rotation
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.position = t.position;
                rb.rotation = t.rotation;
                rb.Sleep(); // ensures physics doesn�t move it on the next tick
            }
            else
            {
                // fallback if no Rigidbody
                entry.carObject.transform.SetPositionAndRotation(t.position, t.rotation);
            }

            // clear inputs so car doesn't immediately move again
            if (entry.inputProvider != null)
            {
                entry.inputProvider.SetInput(new CarInput());
            }
        }
        Debug.Log("Cars have been reset.");
    }

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

    // Apply inputs directly to cars by index
    public void ApplyCarInputs(List<(CarInput input, int carIndex)> inputs)
    {
        foreach (var (input, carIndex) in inputs)
        {
            cars[carIndex].inputProvider.SetInput(input);
        }
    }

    private void StartGame()
    {
        ResetCars();
        state = State.Running;
        Debug.Log("Game started.");
    }

    private void StopGame()
    {
        state = State.Stopped;
        Debug.Log("Game stopped.");
    }

    private void ProcessCommand(byte command, byte value)
    {
        switch (command)
        {
            case 0: // reset
                UnityMainThreadDispatcher.Instance().Enqueue(() => ResetCars());
                break;
            case 1: // shuffle
                UnityMainThreadDispatcher.Instance().Enqueue(() => ShuffleStartTransforms());
                break;
            case 10:
                StartGame();
                break;
            case 11:
                StopGame();
                break;
            case 20:
                this.fixedHz = value;
                updateSimulationFramerate();
                break;
            case 21:
                this.framesPerObservation = value;
                break;
            case 50:
                
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
                using (TcpClient client = controlServer.AcceptTcpClient())
                using (NetworkStream stream = client.GetStream())
                {
                    byte[] buffer = new byte[2]; // command byte + value byte
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead < 2)
                    {
                        Debug.LogWarning("Incomplete command packet received");
                        continue;
                    }
                    commandBuffer.EnqueueCommand(buffer[0], buffer[1]);
                }
            }
        }
        catch (SocketException e)
        {
            Debug.Log("Socket exception: " + e);
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
                using (TcpClient client = instructionsServer.AcceptTcpClient())
                using (NetworkStream stream = client.GetStream())
                {
                    byte[] buffer = new byte[6]; // 4 bytes car ID + 1 byte steering + 1 byte throttle
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead < 6)
                    {
                        Debug.LogWarning("Incomplete input packet received");
                        continue;
                    }

                    // --- Extract car ID (32-bit integer, little-endian) ---
                    int carIndex = BitConverter.ToInt32(buffer, 0);

                    // --- Steering and throttle ---
                    byte steeringByte = buffer[4];
                    byte throttleByte = buffer[5];

                    CarInput input = new CarInput
                    {
                        Steering = steeringByte,
                        Throttle = throttleByte,
                        UseSpeedSteering = true
                    };

                    instructionBuffer.SetInstruction(carIndex, input);
                }
            }
        }
        catch (SocketException e)
        {
            Debug.Log("Socket exception (instructions): " + e);
        }
    }
}