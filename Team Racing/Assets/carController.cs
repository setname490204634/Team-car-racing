using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CarController : MonoBehaviour
{
    [Header("Wheel Colliders")]
    public WheelCollider LFWheel;
    public WheelCollider RFWheel;
    public WheelCollider LRWheel;
    public WheelCollider RRWheel;

    [Header("Wheel Meshes")]
    public Transform LFWheelMesh;
    public Transform RFWheelMesh;
    public Transform LRWheelMesh;
    public Transform RRWheelMesh;

    [Header("Car Settings")]
    public float driveSpeed = 1000f;
    public float maxSteeringAngle = 25f;
    public float downforce = 300f;
    public float forwardStiffness = 3f;
    public float sidewaysStiffness = 3f;

    private Rigidbody rb;
    private ICarInputProvider inputProvider;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass = new Vector3(0, -0.5f, 0);

        // Default to player input if none is assigned
        inputProvider = GetComponent<ICarInputProvider>();

        // Apply base friction
        SetWheelFriction(LFWheel);
        SetWheelFriction(RFWheel);
        SetWheelFriction(LRWheel);
        SetWheelFriction(RRWheel);
    }

    void FixedUpdate()
    {
        if (inputProvider == null) return;

        float steeringInput = Mathf.Clamp(inputProvider.GetSteering(), -100f, 100f) / 100f;
        float throttleInput = Mathf.Clamp(inputProvider.GetThrottle(), -100f, 100f) / 100f;

        float steering = maxSteeringAngle * steeringInput;

        // Apply speed-sensitive steering if requested
        if (inputProvider.UseSpeedSteering())
        {
            float speedFactor = rb.linearVelocity.magnitude / 10f;
            steering = maxSteeringAngle / (1f + speedFactor) * steeringInput;
        }

        // Apply steering to front wheels
        LFWheel.steerAngle = steering;
        RFWheel.steerAngle = steering;

        // Apply torque to rear wheels
        LRWheel.motorTorque = throttleInput * driveSpeed;
        RRWheel.motorTorque = throttleInput * driveSpeed;

        // Downforce
        float currentDownforce = downforce * rb.linearVelocity.magnitude;
        rb.AddForce(-transform.up * currentDownforce);

        // Dynamic grip scaling
        float gripMultiplier = 1f + currentDownforce / 50000f;
        SetWheelFriction(LFWheel, gripMultiplier);
        SetWheelFriction(RFWheel, gripMultiplier);
        SetWheelFriction(LRWheel, gripMultiplier);
        SetWheelFriction(RRWheel, gripMultiplier);

        // Update visuals
        UpdateWheelVisuals(LFWheel, LFWheelMesh);
        UpdateWheelVisuals(RFWheel, RFWheelMesh);
        UpdateWheelVisuals(LRWheel, LRWheelMesh);
        UpdateWheelVisuals(RRWheel, RRWheelMesh);
    }

    private void SetWheelFriction(WheelCollider wheel, float multiplier = 1f)
    {
        WheelFrictionCurve f = wheel.forwardFriction;
        f.stiffness = forwardStiffness * multiplier;
        wheel.forwardFriction = f;

        WheelFrictionCurve s = wheel.sidewaysFriction;
        s.stiffness = sidewaysStiffness * multiplier;
        wheel.sidewaysFriction = s;
    }

    private void UpdateWheelVisuals(WheelCollider collider, Transform mesh)
    {
        if (mesh == null) return;

        Vector3 pos;
        Quaternion quat;
        collider.GetWorldPose(out pos, out quat);

        mesh.position = pos;
        mesh.rotation = quat * Quaternion.Euler(0f, 0f, 90f);
    }
}
