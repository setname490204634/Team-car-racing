using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class gameControlScript : MonoBehaviour
{
    [System.Serializable]
    public class CarEntry
    {
        public GameObject carObject;
        public CarAgent agent;                // optional
        public ICarInputProvider inputProvider;
    }

    [System.Serializable]
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
        // Fill lists from inspector
        foreach (var obj in assignedCarObjects)
        {
            if (obj == null) continue;

            CarEntry entry = new CarEntry();
            entry.carObject = obj;
            entry.agent = obj.GetComponent<CarAgent>();
            entry.inputProvider = obj.GetComponent<ICarInputProvider>();
            cars.Add(entry);

            // Save start transform
            TransformEntry t = new TransformEntry
            {
                position = obj.transform.position,
                rotation = obj.transform.rotation
            };
            startTransforms.Add(t);
        }

        // Start TCP servers
        running = true;

        controlThread = new Thread(ListenForControlCommands);
        controlThread.IsBackground = true;
        controlThread.Start();

        instructionsThread = new Thread(ListenForCarInstructions);
        instructionsThread.IsBackground = true;
        instructionsThread.Start();

        // Start observation transmitter
        transmitter = new CarObservationTransmitter("127.0.0.1", observationTransmitterPort, cars);
        transmitter.Start();
    }

    void OnApplicationQuit()
    {
        running = false;

        controlServer?.Stop();
        instructionsServer?.Stop();

        if (controlThread?.IsAlive ?? false) controlThread.Abort();
        if (instructionsThread?.IsAlive ?? false) instructionsThread.Abort();
    }

    // Reset all cars to their start transforms
    public void ResetCars()
    {
        for (int i = 0; i < cars.Count; i++)
        {
            var entry = cars[i];
            var t = startTransforms[i];

            entry.carObject.transform.position = t.position;
            entry.carObject.transform.rotation = t.rotation;

            // reset velocity if Rigidbody is attached
            Rigidbody rb = entry.carObject.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
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
                    int commandByte = stream.ReadByte();
                    if (commandByte == -1) continue; // client disconnected

                    switch (commandByte)
                    {
                        case 0: // reset
                            UnityMainThreadDispatcher.Instance().Enqueue(ResetCars);
                            break;
                        case 1: // shuffle
                            UnityMainThreadDispatcher.Instance().Enqueue(ShuffleStartTransforms);
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
                    byte[] buffer = new byte[3]; // 1 byte car ID + 2 bytes inputs
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead < 3)
                    {
                        Debug.LogWarning("Incomplete input packet received");
                        continue;
                    }

                    int carIndex = buffer[0];
                    byte steeringByte = buffer[1];
                    byte throttleByte = buffer[2];

                    CarInput input = new CarInput
                    {
                        Steering = steeringByte,
                        Throttle = throttleByte,
                        UseSpeedSteering = true // or send another byte if needed
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

    // Helper class for JSON deserialization
    [System.Serializable]
    public class CarInputMessage
    {
        public int carIndex;
        public CarInput input;
    }
}