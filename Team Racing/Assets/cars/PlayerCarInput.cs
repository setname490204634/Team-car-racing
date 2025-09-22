using UnityEngine;

public class PlayerCarInput : MonoBehaviour, ICarInputProvider
{
    [Tooltip("Enable speed-sensitive steering (less steering at high speeds).")]
    public bool useSpeedSensitiveSteering = true;

    private CarInput currentInput;

    void Update()
    {
        // Read player input every frame
        currentInput.Steering = Input.GetAxis("Horizontal") * 100f;
        currentInput.Throttle = Input.GetAxis("Vertical") * 100f;
        currentInput.UseSpeedSteering = useSpeedSensitiveSteering;
    }

    // ICarInputProvider implementation
    public CarInput getInput() => currentInput;

    public void SetInput(CarInput input)
    {
        // Not used here, because this provider is purely manual
        currentInput = input;
    }
}