using UnityEngine;

public class CarRaceState
{
    public int lapCount = 0;
    public float currentLapTime = 0f;
    public float lastLapTotalTime = 0f;
    public float bestLapTime = float.MaxValue;

    public bool passedHalfway = false;
    public bool crossedFinish = true;

    public bool finished = false;

    public void Reset()
    {
        lapCount = 0;
        currentLapTime = 0f;
        lastLapTotalTime = 0f;
        bestLapTime = float.MaxValue;
        passedHalfway = false;
        crossedFinish = true;
        finished = false;
    }
}
