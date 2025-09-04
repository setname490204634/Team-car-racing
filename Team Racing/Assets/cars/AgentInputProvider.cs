using UnityEngine;

public class AgentInputProvider : MonoBehaviour, ICarInputProvider
{
    private float steering;
    private float throttle;
    private bool useSpeedSteering;

    public void SetInputs(float s, float t, bool speedSteer)
    {
        steering = s;
        throttle = t;
        useSpeedSteering = speedSteer;
    }

    public float GetSteering() => steering;
    public float GetThrottle() => throttle;
    public bool UseSpeedSteering() => useSpeedSteering;
}
