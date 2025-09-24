using UnityEngine;

public interface ICarInputProvider
{
    public CarInput getInput();

    public void SetInput(CarInput input);
}

public struct CarInput
{
    public byte Steering;
    public byte Throttle;
    public bool UseSpeedSteering;
}

