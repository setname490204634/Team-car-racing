using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using UnityEngine;

public class CarObservationTransmitter
{
    private TcpClient client;
    private NetworkStream stream;
    private string ip;
    private int port;
    private bool connected = false;

    private List<gameControlScript.CarEntry> cars;

    // Internal buffer for latest observations
    private List<byte[]> observationPackets = new List<byte[]>();
    private readonly object lockObj = new object();

    public CarObservationTransmitter(string ip, int port, List<gameControlScript.CarEntry> cars)
    {
        this.ip = ip;
        this.port = port;
        this.cars = cars;
    }

    public void Connect()
    {
        client = new TcpClient();
        client.Connect(IPAddress.Parse(ip), port);
        stream = client.GetStream();
        connected = true;
        Debug.Log("Connected to " + ip + ":" + port);
    }

    public void Disconnect()
    {
        connected = false;
        stream?.Close();
        client?.Close();
    }

    // Collect observations from all cars on the main Unity thread.
    public void CollectObservations()
    {
        lock (lockObj)
        {
            observationPackets.Clear();

            for (int i = 0; i < cars.Count; i++)
            {
                var entry = cars[i];
                if (entry.agent == null) continue;


                // safe on main thread
                CarObservation obs = entry.agent.GetCarObservation(); 
                float reward = entry.rewards.CalculateReward();
                // safe on main thread


                byte[] packet = CarObservationSerializer.PackCarObservation(obs, i, reward);
                if (packet != null)
                    observationPackets.Add(packet);
            }
        }
    }

    /// <summary>
    /// Send last collected observations.
    /// </summary>
    public void SendObservations()
    {
        if (!connected || stream == null)
            return;

        lock (lockObj)
        {
            foreach (var packet in observationPackets)
            {
                stream.Write(packet, 0, packet.Length);
            }
        }
    }
}
