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

    public static CarInput Default = new CarInput
    {
        Steering = 128,
        Throttle = 128,
        UseSpeedSteering = false
    };
}

