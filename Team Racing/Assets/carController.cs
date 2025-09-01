using UnityEngine;

public class CarController : MonoBehaviour
{
    [Header("Wheel Colliders")]
    public WheelCollider LFWheel; // Left Front
    public WheelCollider RFWheel; // Right Front
    public WheelCollider LRWheel; // Left Rear
    public WheelCollider RRWheel; // Right Rear

    [Header("Wheel Meshes")]
    public Transform LFWheelMesh;
    public Transform RFWheelMesh;
    public Transform LRWheelMesh;
    public Transform RRWheelMesh;

    [Header("Car Settings")]
    public float driveSpeed = 3000f;        // torque for acceleration
    public float maxSteeringAngle = 40f;    // max steering angle
    public float downforce = 10000f;          // downforce coefficient
    public float forwardStiffness = 2f;     // wheel grip forward
    public float sidewaysStiffness = 2f;    // wheel grip sideways

    private Rigidbody rb;
    private float horizontalInput;
    private float verticalInput;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass = new Vector3(0, -0.5f, 0); // lower center for stability

        // Apply friction stiffness to all wheels
        SetWheelFriction(LFWheel);
        SetWheelFriction(RFWheel);
        SetWheelFriction(LRWheel);
        SetWheelFriction(RRWheel);
    }

    void Update()
    {
        horizontalInput = Input.GetAxis("Horizontal");
        verticalInput = Input.GetAxis("Vertical");

        // Update wheel meshes visually
        UpdateWheelVisuals(LFWheel, LFWheelMesh);
        UpdateWheelVisuals(RFWheel, RFWheelMesh);
        UpdateWheelVisuals(LRWheel, LRWheelMesh);
        UpdateWheelVisuals(RRWheel, RRWheelMesh);
    }

    void FixedUpdate()
    {
        // Speed-sensitive steering
        float speedFactor = rb.linearVelocity.magnitude / 100f;
        float steering = maxSteeringAngle / (1 + speedFactor) * horizontalInput;

        // Apply steering to front wheels
        LFWheel.steerAngle = steering;
        RFWheel.steerAngle = steering;

        // Apply motor torque to rear wheels
        LRWheel.motorTorque = verticalInput * driveSpeed;
        RRWheel.motorTorque = verticalInput * driveSpeed;

        // Apply downforce to Rigidbody
        float currentDownforce = downforce * rb.linearVelocity.magnitude;
        rb.AddForce(-transform.up * currentDownforce);

        // Increase wheel friction based on downforce
        float gripMultiplier = 1f + currentDownforce / 100000f; // tweak denominator for effect
        SetWheelFriction(LFWheel, gripMultiplier);
        SetWheelFriction(RFWheel, gripMultiplier);
        SetWheelFriction(LRWheel, gripMultiplier);
        SetWheelFriction(RRWheel, gripMultiplier);
    }

    void SetWheelFriction(WheelCollider wheel, float multiplier)
    {
        WheelFrictionCurve f = wheel.forwardFriction;
        f.stiffness = forwardStiffness * multiplier;
        wheel.forwardFriction = f;

        WheelFrictionCurve s = wheel.sidewaysFriction;
        s.stiffness = sidewaysStiffness * multiplier;
        wheel.sidewaysFriction = s;
    }

    void UpdateWheelVisuals(WheelCollider collider, Transform mesh)
    {
        Vector3 pos;
        Quaternion quat;
        collider.GetWorldPose(out pos, out quat);

        mesh.position = pos;
        mesh.rotation = quat * Quaternion.Euler(0f, 0f, 90f);
    }

    void SetWheelFriction(WheelCollider wheel)
    {
        WheelFrictionCurve forwardFriction = wheel.forwardFriction;
        forwardFriction.stiffness = forwardStiffness;
        wheel.forwardFriction = forwardFriction;

        WheelFrictionCurve sidewaysFriction = wheel.sidewaysFriction;
        sidewaysFriction.stiffness = sidewaysStiffness;
        wheel.sidewaysFriction = sidewaysFriction;
    }
}
