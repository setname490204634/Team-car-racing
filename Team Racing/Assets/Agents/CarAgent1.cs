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
    private ICarInputProvider inputProvider;

    public override void Initialize()
    {
        // Get references
        car = GetComponent<CarController>();
        observer = car; // CarController implements ICarObserver
        inputProvider = GetComponent<ICarInputProvider>();

        // Assign the input provider to the CarController
        var inputField = car.GetType().GetField(
            "inputProvider",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
        );
        if (inputField != null)
            inputField.SetValue(car, inputProvider);
        else
            Debug.LogError("CarController does not have an inputProvider field!");
    }

    public override void OnEpisodeBegin()
    {
        // Reset car position and rotation
        transform.localPosition = Vector3.zero + Vector3.up * 0.5f;
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

        // Visual observations handled via Camera Sensor Component in Inspector
        // No manual RenderTexture processing needed
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        // Convert continuous actions [-1,1] to steering/throttle
        float steering = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f) * 100f;
        float throttle = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f) * 100f;

        // Send inputs to the car via the interface
        inputProvider.SetInputs(steering, throttle, false);

        // Reward forward speed
        AddReward(observer.GetSpeed() * 0.001f);

        // Penalize excessive steering
        AddReward(-Mathf.Abs(steering) * 0.0005f);

        // Optional: penalize going off-road or collisions
        // if (car.OffRoad) EndEpisode();
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        // Allow manual control with keyboard
        var continuous = actionsOut.ContinuousActions;
        continuous[0] = Input.GetAxis("Horizontal"); // steering
        continuous[1] = Input.GetAxis("Vertical");   // throttle
    }
}
