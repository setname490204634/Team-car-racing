using UnityEngine;

public class PlayerCarInput : MonoBehaviour, ICarInputProvider
{
    [Tooltip("Enable speed-sensitive steering (less steering at high speeds).")]
    public bool useSpeedSensitiveSteering = true;

    public float GetSteering()
    {
        // A/D or Left/Right arrows
        return Input.GetAxis("Horizontal") * 100f;
    }

    public float GetThrottle()
    {
        // W/S or Up/Down arrows
        return Input.GetAxis("Vertical") * 100f;
    }

    public bool UseSpeedSteering()
    {
        return useSpeedSensitiveSteering;
    }
}
