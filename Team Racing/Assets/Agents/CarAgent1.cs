using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

[RequireComponent(typeof(CarController))]
[RequireComponent(typeof(ICarInputProvider))]
public class CarAgent : Agent
{
    private CarController car;
    private ICarObserver observer;

    [Header("settings")]
    public Camera leftCamera;
    public Camera rightCamera;
    public int cameraWidth = 64;
    public int cameraHeight = 64;

    [Header("Input Providers")]
    public PlayerCarInput manualInputProvider;
    public AgentInputProvider agentInputProvider;


    private ICarInputProvider inputProvider;

    public override void Initialize()
    {
        // Get references
        car = GetComponent<CarController>();
        observer = car; // CarController implements ICarObserver

        // Choose which one to use dynamically
        if (manualInputProvider != null)
        {
            inputProvider = manualInputProvider;
        }
        else
        {
            inputProvider = agentInputProvider;
        }

        // Assign to CarController
        var inputField = car.GetType().GetField(
            "inputProvider",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
        );
        if (inputField != null)
            inputField.SetValue(car, inputProvider);
        else
            Debug.LogError("CarController does not have an inputProvider field!");

        // Add camera sensors
        if (leftCamera != null)
            AddCameraSensor(leftCamera, "CameraSensorLeft");

        if (rightCamera != null)
            AddCameraSensor(rightCamera, "CameraSensorRight");
    }

    private void AddCameraSensor(Camera cam, string name)
    {
        var sensor = gameObject.AddComponent<Unity.MLAgents.Sensors.CameraSensorComponent>();
        sensor.Camera = cam;
        sensor.Width = cameraWidth;
        sensor.Height = cameraHeight;
        sensor.Grayscale = false;
    }

    public override void OnEpisodeBegin()
    {
        // Reset car position and rotation
        transform.localPosition = Vector3.up * 0.5f;
        transform.localRotation = Quaternion.identity;

        // Reset physics
        if (TryGetComponent<Rigidbody>(out var rb))
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // Add speed (normalized)
        sensor.AddObservation(observer.GetSpeed() / 500f);

        // Add steering angle (normalized)
        sensor.AddObservation(observer.GetSteeringAngle() / car.maxSteeringAngle);

        // Cameras are handled automatically; no need to manually add RenderTextures
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        // Continuous actions: steering and throttle
        float steering = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f) * 100f;
        float throttle = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f) * 100f;

        // Send inputs to car via input provider
        inputProvider.SetInputs(steering, throttle, false);

        // Quadratic reward for forward speed
        float speed = observer.GetSpeed() / 500f;  // normalize speed to 0-1
        AddReward(speed * speed);  // reward ~ speed^2

        // Optional: scale down if too large
        // AddReward(speed * speed * 0.01f);

        // Penalize excessive steering (keep linear)
        //AddReward(-Mathf.Abs(steering) * 0.005f);

        // Optional: penalize going off-road or collisions
        // if (car.OffRoad) EndEpisode();
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        // Swap the agent input provider to your manual one
        var continuous = actionsOut.ContinuousActions;

        // Example: read from your manual input provider
        continuous[0] = inputProvider.GetSteering();  // -1 to 1
        continuous[1] = inputProvider.GetThrottle();  // -1 to 1
    }
}
