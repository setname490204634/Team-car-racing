using UnityEngine;

public class PlayerCarInput : MonoBehaviour, ICarInputProvider
{
    [Tooltip("Enable speed-sensitive steering (less steering at high speeds).")]
    public bool useSpeedSensitiveSteering = true;

    private float steeringInput;
    private float throttleInput;
    private bool useSpeedSteeringFlag;

    void Update()
    {
        // Read player input every frame
        steeringInput = Input.GetAxis("Horizontal") * 100f;
        throttleInput = Input.GetAxis("Vertical") * 100f;
        useSpeedSteeringFlag = useSpeedSensitiveSteering;
    }

    // ICarInputProvider implementation
    public float GetSteering() => steeringInput;
    public float GetThrottle() => throttleInput;
    public bool UseSpeedSteering() => useSpeedSteeringFlag;

    // Allows external scripts (ML agents etc.) to override input
    public void SetInputs(float steering, float throttle, bool useSpeedSteering)
    {
        steeringInput = Mathf.Clamp(steering, -100f, 100f);
        throttleInput = Mathf.Clamp(throttle, -100f, 100f);
        useSpeedSteeringFlag = useSpeedSteering;
    }
}
