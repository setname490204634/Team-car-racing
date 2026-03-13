using System;
using System.Collections.Generic;
using System.Threading;

public class InstructionBuffer
{
    // Since the buffer is accessed from game loop and reciever it has to be locked to avoid race conditions
    private readonly object lockObj = new object();
    private readonly Dictionary<int, CarInput> latest = new Dictionary<int, CarInput>();

    // Number of cars we expect instructions for
    private readonly int expectedCount;

    public InstructionBuffer(int expectedCount)
    {
        this.expectedCount = expectedCount;
    }

    // Called from network thread to submit/update instruction for a car
    public void AddInstruction(int carIndex, CarInput input)
    {
        lock (lockObj)
        {
            latest[carIndex] = input;
        }
    }

    // Returns and clears the buffer
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
    public bool HasAllInstructions()
    {
        lock (lockObj)
        {
            for (int i = 0; i < expectedCount; i++)
            {
                if (!latest.ContainsKey(i))
                    return false;
            }
            return true;
        }
    }
}

public class GameCommandBuffer
{
    // Since the buffer is accessed from game loop and reciever it has to be locked to avoid race conditions
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
    // Returns and clears the buffer
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
