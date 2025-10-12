using System.Net.Sockets;
using System.Net;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;
using System.Runtime.Remoting;

public class CarObservationTransmitter
{
    private TcpClient client;
    private NetworkStream stream;
    private string ip;
    private int port;
    private Thread sendThread;
    private bool running = false;
    private int delay;

    private List<gameControlScript.CarEntry> cars;

    public CarObservationTransmitter(string ip, int port, List<gameControlScript.CarEntry> cars, int delay = 33)
    {
        this.ip = ip;
        this.port = port;
        this.cars = cars;
        this.delay = delay;
    }

    class ObsRef
    {
        public CarObservation Value;
    }

    public void Start()
    {
        running = true;
        sendThread = new Thread(SendLoop);
        sendThread.IsBackground = true;
        sendThread.Start();
    }

    public void Stop()
    {
        running = false;
        stream?.Close();
        client?.Close();
        if (sendThread?.IsAlive ?? false) sendThread.Abort();
    }

    private void SendLoop()
    {
        try
        {
            client = new TcpClient();
            client.Connect(IPAddress.Parse(ip), port);
            stream = client.GetStream();
            Debug.Log("Observation transmitter connected to " + ip + ":" + port);

            while (running)
            {
                // Collect observations
                List<byte[]> observationPackets = new List<byte[]>();

                for (int i = 0; i < cars.Count; i++)
                {
                    var entry = cars[i];
                    if (entry.agent != null)
                    {
                        ObsRef obsRef = new ObsRef();

                        UnityMainThreadDispatcher.Instance().Enqueue(() =>
                        {
                            obsRef.Value = entry.agent.GetCarObservation(); // main thread safe
                        });

                        // Wait until obsRef.Value is assigned
                        while (obsRef.Value.Speed == 0) // or some sentinel check
                        {
                            Thread.Sleep(0);
                        }

                        CarObservation obs = obsRef.Value;
                        int reward = obs.Speed; // simple reward = speed
                        byte[] packet = CarObservationSerializer.PackCarObservation(obs, i, reward);
                        if (packet != null)
                            observationPackets.Add(packet);
                    }
                }

                // Send all packets sequentially
                foreach (var packet in observationPackets)
                {
                    stream.Write(packet, 0, packet.Length);
                }

                // Optional: control the framerate of sending
                Thread.Sleep(delay); // ~30 Hz
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Observation transmitter error: " + ex.Message);
        }
    }
}
