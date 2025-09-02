using UnityEngine;

public interface ICarInputProvider
{
    // -100 to 100, where -100 = full left, 100 = full right
    float GetSteering();

    // -100 to 100, where -100 = full brake/reverse, 100 = full throttle
    float GetThrottle();

    // Should steering be speed-sensitive?
    bool UseSpeedSteering();
}

