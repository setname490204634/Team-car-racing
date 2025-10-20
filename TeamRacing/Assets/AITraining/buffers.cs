using System;
using System.Collections.Generic;
using System.Threading;

public class InstructionBuffer
{
    private readonly object lockObj = new object();
    private readonly Dictionary<int, CarInput> latest = new Dictionary<int, CarInput>();

    // Number of cars we expect instructions for
    private readonly int expectedCount;

    public InstructionBuffer(int expectedCount)
    {
        this.expectedCount = expectedCount;
    }

    // Called from network thread to submit/update instruction for a car
    public void SetInstruction(int carIndex, CarInput input)
    {
        lock (lockObj)
        {
            latest[carIndex] = input;
        }
    }

    // Read and clear the buffer atomically on the main thread
    // Returns a list of (CarInput, index) tuples to apply
    public List<(CarInput input, int carIndex)> ConsumeAll()
    {
        lock (lockObj)
        {
            var result = new List<(CarInput, int)>(latest.Count);
            foreach (var kv in latest)
                result.Add((kv.Value, kv.Key));
            latest.Clear();
            return result;
        }
    }

    // Check whether we have instructions for all cars
    public bool HasAll()
    {
        lock (lockObj)
        {
            return latest.Count >= expectedCount;
        }
    }
}

public class GameCommandBuffer
{
    private readonly object lockObj = new object();
    private readonly Queue<(byte, byte)> commands = new Queue<(byte, byte)>();

    // Called by network thread
    public void EnqueueCommand(byte command, byte value)
    {
        lock (lockObj)
        {
            commands.Enqueue((command, value));
        }
    }

    // Called by main thread
    public List<(byte, byte)> ConsumeAll()
    {
        lock (lockObj)
        {
            var list = new List<(byte, byte)>(commands.Count);
            while (commands.Count > 0) list.Add(commands.Dequeue());
            return list;
        }
    }
}
