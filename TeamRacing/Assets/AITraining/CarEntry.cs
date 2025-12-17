using UnityEngine;

public class CarEntry
{
    public GameObject carObject;
    public CarAgent agent; //can be null
    public ICarInputProvider inputProvider;
    public RewardsCalculator rewards;
    public CarRaceState raceState;
    public CarController controller;
    public int segmentIndex;
    public CarAppearance carAppearance;

    public void Reset()
    {
        inputProvider.SetInput(CarInput.Default);
        rewards.Reset();
        raceState.Reset();
        controller.ResetCar();
        segmentIndex = 0;
    }
}
