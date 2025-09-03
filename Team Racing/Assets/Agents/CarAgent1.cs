using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

[RequireComponent(typeof(CarController))]
public class CarAgent : Agent
{
    private CarController car;
    private ICarObserver observer;
    private ICarInputProvider inputProvider;

    public override void Initialize()
    {
        car = GetComponent<CarController>();
        observer = car; // CarController implements ICarObserver

        // Create input provider for this agent
        inputProvider = new AgentInputProvider();
        car.GetType().GetField("inputProvider",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .SetValue(car, inputProvider);
    }

    public override void OnEpisodeBegin()
    {
        // Reset car
        transform.localPosition = Vector3.zero + Vector3.up * 0.5f;
        transform.localRotation = Quaternion.identity;

        Rigidbody rb = GetComponent<Rigidbody>();
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // Speed (normalized)
        sensor.AddObservation(observer.GetSpeed() / 200f);

        // Steering angle (normalized)
        sensor.AddObservation(observer.GetSteeringAngle() / car.maxSteeringAngle);

        // Visual observation using RenderTexture
        if (observer.GetCameraTexture() != null)
        {
            AddVisualObservation(observer.GetCameraTexture());
        }
    }

    private void AddVisualObservation(RenderTexture rt)
    {
        // Using Unity�s built-in camera observation system:
        var cam = car.carCamera;
        if (cam != null)
        {
            var visualObs = new CameraSensorComponent();
            visualObs.Camera = cam;
            visualObs.Width = rt.width;
            visualObs.Height = rt.height;
            visualObs.Grayscale = false; // keep color
            visualObs.ObservationStacks = 1;
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        float steering = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f) * 100f;
        float throttle = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f) * 100f;

        ((AgentInputProvider)inputProvider).SetInputs(steering, throttle, true);
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuous = actionsOut.ContinuousActions;
        continuous[0] = Input.GetAxis("Horizontal");
        continuous[1] = Input.GetAxis("Vertical");
    }
}

// Input provider for the agent
public class AgentInputProvider : ICarInputProvider
{
    private float steering;
    private float throttle;
    private bool useSpeedSteering;

    public void SetInputs(float s, float t, bool speedSteer)
    {
        steering = s;
        throttle = t;
        useSpeedSteering = speedSteer;
    }

    public float GetSteering() => steering;
    public float GetThrottle() => throttle;
    public bool UseSpeedSteering() => useSpeedSteering;
}
