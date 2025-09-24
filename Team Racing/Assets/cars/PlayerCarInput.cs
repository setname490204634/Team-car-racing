using UnityEngine;

public class PlayerCarInput : MonoBehaviour, ICarInputProvider
{
    [Tooltip("Enable speed-sensitive steering (less steering at high speeds).")]
    public bool useSpeedSensitiveSteering = true;

    private CarInput currentInput;

    void Update()
    {
        float steer = Input.GetAxis("Horizontal");  // -1..1
        float throttle = Input.GetAxis("Vertical"); // -1..1

        currentInput.Steering = (byte)Mathf.Clamp(Mathf.RoundToInt((steer * 127f) + 128f), 0, 255);
        currentInput.Throttle = (byte)Mathf.Clamp(Mathf.RoundToInt((throttle * 127f) + 128f), 0, 255);
        currentInput.UseSpeedSteering = useSpeedSensitiveSteering;
    }


    public CarInput getInput() => currentInput;

    public void SetInput(CarInput input)
    {
        // Not used here, purely manual input
        currentInput = input;
    }
}
