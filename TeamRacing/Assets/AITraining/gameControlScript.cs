using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

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
    private CarObservationTransmitter transmitter;

    [Header("Server Settings")]
    public int controlPort = 5005;
    public int carInstructionsPort = 5006;
    public int observationTransmitterPort = 5007;

    void Start()
    {
        int index = 0;
        // Fill lists from inspector
        foreach (var obj in assignedCarObjects)
        {
            if (obj == null) continue;

            CarEntry entry = new CarEntry();
            entry.carObject = obj;
            entry.agent = obj.GetComponent<CarAgent>();
            entry.inputProvider = obj.GetComponent<ICarInputProvider>();

            int teammateID = index + 1;
            if (teammateID % 2 == 1) teammateID -= 2;
            entry.rewards = new Rewards(entry.agent, this, obj, Rewards.Default, teammateID);
            cars.Add(entry);

            // Save start transform
            TransformEntry t = new TransformEntry
            {
                position = obj.transform.position,
                rotation = obj.transform.rotation
            };
            startTransforms.Add(t);
            index++;
        }

        // Start TCP servers
        running = true;

        controlThread = new Thread(ListenForControlCommands);
        controlThread.IsBackground = true;
        controlThread.Start();

        instructionsThread = new Thread(ListenForCarInstructions);
        instructionsThread.IsBackground = true;
        instructionsThread.Start();

        // observation transmitter
        //it has to be started with the python side listening
        transmitter = new CarObservationTransmitter("127.0.0.1", observationTransmitterPort, cars);

        var _ = UnityMainThreadDispatcher.Instance();
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
                rb.Sleep(); // ensures physics doesn’t move it on the next tick
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

                    byte commandByte = buffer[0];
                    byte valueByte = buffer[1];

                    switch (commandByte)
                    {
                        case 0: // reset
                            UnityMainThreadDispatcher.Instance().Enqueue(() => ResetCars());
                            break;
                        case 1: // shuffle
                            UnityMainThreadDispatcher.Instance().Enqueue(() => ShuffleStartTransforms());
                            break;
                        case 50: // start transmitter
                            transmitter.Start();
                            break;
                        default:
                            Debug.LogWarning("Unknown command byte: " + commandByte);
                            break;
                    }
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

                    UnityMainThreadDispatcher.Instance().Enqueue(() =>
                    {
                        if (carIndex >= 0 && carIndex < cars.Count)
                            cars[carIndex].inputProvider.SetInput(input);
                    });
                }
            }
        }
        catch (SocketException e)
        {
            Debug.Log("Socket exception (instructions): " + e);
        }
    }
}