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

    [Header("Surface Grip Settings")]
    public float roadGripMultiplier = 1f;       // Normal grip
    public float grassGripMultiplier = 0.4f;    // Reduced grip on grass
    public float grassSpeedMultiplier = 0.6f;   // Reduce torque on grass

    private Rigidbody rb;
    private ICarInputProvider inputProvider;

    private float currentGripMultiplier = 1f;   // Updated each wheel per frame
    private float currentSpeedMultiplier = 1f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass = new Vector3(0, -0.5f, 0);

        inputProvider = GetComponent<ICarInputProvider>();

        // Apply base friction to all wheels
        SetWheelFriction(LFWheel, roadGripMultiplier);
        SetWheelFriction(RFWheel, roadGripMultiplier);
        SetWheelFriction(LRWheel, roadGripMultiplier);
        SetWheelFriction(RRWheel, roadGripMultiplier);
    }

    void FixedUpdate()
    {
        if (inputProvider == null) return;

        float steeringInput = Mathf.Clamp(inputProvider.GetSteering(), -100f, 100f) / 100f;
        float throttleInput = Mathf.Clamp(inputProvider.GetThrottle(), -100f, 100f) / 100f;

        float steering = maxSteeringAngle * steeringInput;

        // Speed-sensitive steering
        if (inputProvider.UseSpeedSteering())
        {
            float speedFactor = rb.linearVelocity.magnitude / 5.5f;
            steering = maxSteeringAngle / (1f + speedFactor) * steeringInput;
        }

        // Apply steering
        LFWheel.steerAngle = steering;
        RFWheel.steerAngle = steering;

        // Reset multipliers
        currentGripMultiplier = roadGripMultiplier;
        currentSpeedMultiplier = 1f;

        // Adjust grip and speed based on surface for each wheel
        AdjustWheelGripAndSpeed(LFWheel);
        AdjustWheelGripAndSpeed(RFWheel);
        AdjustWheelGripAndSpeed(LRWheel);
        AdjustWheelGripAndSpeed(RRWheel);

        // Apply motor torque with speed multiplier
        LRWheel.motorTorque = throttleInput * driveSpeed * currentSpeedMultiplier;
        RRWheel.motorTorque = throttleInput * driveSpeed * currentSpeedMultiplier;

        // Apply downforce
        float currentDownforce = downforce * rb.linearVelocity.magnitude;
        rb.AddForce(-transform.up * currentDownforce);

        // Apply final grip scaling
        float gripMultiplierWithDownforce = currentGripMultiplier * (1f + currentDownforce / 50000f);
        SetWheelFriction(LFWheel, gripMultiplierWithDownforce);
        SetWheelFriction(RFWheel, gripMultiplierWithDownforce);
        SetWheelFriction(LRWheel, gripMultiplierWithDownforce);
        SetWheelFriction(RRWheel, gripMultiplierWithDownforce);

        // Update wheel visuals
        UpdateWheelVisuals(LFWheel, LFWheelMesh);
        UpdateWheelVisuals(RFWheel, RFWheelMesh);
        UpdateWheelVisuals(LRWheel, LRWheelMesh);
        UpdateWheelVisuals(RRWheel, RRWheelMesh);
    }

    private void AdjustWheelGripAndSpeed(WheelCollider wheel)
    {
        if (wheel.GetGroundHit(out WheelHit hit))
        {
            if (hit.collider != null)
            {
                int grassLayer = LayerMask.NameToLayer("Grass");
                if (hit.collider.gameObject.layer == grassLayer)
                {
                    currentGripMultiplier = Mathf.Min(currentGripMultiplier, grassGripMultiplier);
                    currentSpeedMultiplier = Mathf.Min(currentSpeedMultiplier, grassSpeedMultiplier);
                }
            }
        }
    }

    private void SetWheelFriction(WheelCollider wheel, float multiplier)
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
