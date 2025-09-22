using UnityEngine;

public interface ICarInputProvider
{
    public CarInput getInput();

    public void SetInput(CarInput input);
}

public struct CarInput
{
    public float Steering;
    public float Throttle;
    public bool UseSpeedSteering;
}

