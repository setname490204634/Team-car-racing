using System.Collections.Generic;
using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

public class gameControlScript : MonoBehaviour
{
    [Header("Assign cars in Inspector")]
    public List<GameObject> cars;

    private List<Vector3> startPositions = new List<Vector3>();
    private List<Quaternion> startRotations = new List<Quaternion>();

    private TcpListener server;
    private Thread listenerThread;
    private bool running = false;

    [Header("Server Settings")]
    public int port = 5005; // set port in Inspector

    void Start()
    {
        // Save original transforms
        foreach (var car in cars)
        {
            startPositions.Add(car.transform.position);
            startRotations.Add(car.transform.rotation);
        }

        // Start TCP server
        running = true;
        listenerThread = new Thread(new ThreadStart(ListenForCommands));
        listenerThread.IsBackground = true;
        listenerThread.Start();
    }

    void OnApplicationQuit()
    {
        running = false;
        if (server != null)
        {
            server.Stop();
        }
        if (listenerThread != null && listenerThread.IsAlive)
        {
            listenerThread.Abort();
        }
    }

    // Reset all cars to start positions
    public void ResetCars()
    {
        for (int i = 0; i < cars.Count; i++)
        {
            cars[i].transform.position = startPositions[i];
            cars[i].transform.rotation = startRotations[i];

            // reset velocity if Rigidbody is attached
            Rigidbody rb = cars[i].GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
        Debug.Log("Cars have been reset.");
    }

    private void ListenForCommands()
    {
        try
        {
            server = new TcpListener(IPAddress.Any, port);
            server.Start();
            Debug.Log("Listening for reset commands on port " + port);

            while (running)
            {
                using (TcpClient client = server.AcceptTcpClient())
                using (NetworkStream stream = client.GetStream())
                {
                    byte[] buffer = new byte[1024];
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    string command = Encoding.ASCII.GetString(buffer, 0, bytesRead).Trim().ToLower();

                    Debug.Log("Received command: " + command);

                    if (command == "reset")
                    {
                        // Run on main Unity thread
                        UnityMainThreadDispatcher.Instance().Enqueue(ResetCars);
                    }
                }
            }
        }
        catch (SocketException e)
        {
            Debug.Log("Socket exception: " + e);
        }
    }
}
