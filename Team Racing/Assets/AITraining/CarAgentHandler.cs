using UnityEngine;

[RequireComponent(typeof(CarController))]
[RequireComponent(typeof(ICarInputProvider))]
public class CarAgent : MonoBehaviour, ICarObserver
{
    private CarController car;

    [Header("settings")]
    public Camera leftCamera;
    public Camera rightCamera;
    public int cameraWidth = 64;
    public int cameraHeight = 64;

    [Header("Input Provider")]
    public AgentInputProvider agentInputProvider;

    private ICarInputProvider inputProvider;

    void Awake()
    {
        car = GetComponent<CarController>();
        inputProvider = agentInputProvider;

        // Assign to CarController's private field
        var inputField = car.GetType().GetField(
            "inputProvider",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
        );
        if (inputField != null)
            inputField.SetValue(car, inputProvider);
        else
            Debug.LogError("CarController does not have an inputProvider field!");
    }

    public CarObservation getCarObservation()
    {
        return new CarObservation
        {
            leftCameraTexture = leftCamera != null ? leftCamera.targetTexture : null,
            rightCameraTexture = rightCamera != null ? rightCamera.targetTexture : null,
            Speed = car.GetSpeed(),
            SteeringAngle = car.GetSteeringAngle()
        };
    }

    public void ApplyAction(CarInput input)
    {
        inputProvider.SetInput(input);
    }
}
