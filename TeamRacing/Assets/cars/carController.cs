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
    public float brakeForce = 3000f;

    [Header("Surface Grip Settings")]
    public float roadGripMultiplier = 1f;
    public float grassGripMultiplier = 0.4f;
    public float grassSpeedMultiplier = 0.6f;

    private Rigidbody rb;
    private ICarInputProvider inputProvider;

    private float currentGripMultiplier = 1f;
    private float currentSpeedMultiplier = 1f;

    // Collision tracking
    private bool collided = false; //crash
    private bool crossedFinish = false;
    private bool crossedHalfway = false;
    private bool onGrass = false;
    private bool outOfBounds = false;

    // acceleration, speed and distance
    public float distance = 0;
    public float discountedDistance = 0;
    private float discountFactor = 0.9f;
    private Vector3 lastPos = Vector3.zero;

    public Vector3 acceleration = Vector3.zero;
    private Vector3 lastSpeed = Vector3.zero;

    public Vector3 speed { get { return this.rb.linearVelocity; } }
    public Vector3 position { get { return this.rb.position; } }
    public Quaternion rotation { get { return this.rb.rotation; } }
    public Vector2 speed2D { get { return new Vector2(this.rb.linearVelocity.x, this.rb.linearVelocity.z); } }
    public Vector2 position2D { get { return new Vector2(this.rb.position.x, this.rb.position.z); } }
    public Vector2 forward2D { get { Vector3 forward3D = this.rb.rotation * Vector3.forward; return new Vector2(forward3D.x, forward3D.z); } }
    public Vector2 acceleration2D { get { return new Vector2(this.acceleration.x, this.acceleration.z); } }

    public void ResetCar()
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        distance = 0;
        discountedDistance = 0;
        lastPos = this.rb.position;
        acceleration = Vector3.zero;
        lastSpeed = Vector3.zero;

        collided = false;
        crossedFinish = false;
        crossedHalfway = false;
        onGrass = false;
        outOfBounds = false;
    }

    void Awake()
    {
        Debug.Log("OnActionReceived called");
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass = new Vector3(0f, -0.5f, 0f);

        // Default input provider
        inputProvider = GetComponent<ICarInputProvider>();

        // Initialize friction
        SetWheelFriction(LFWheel, roadGripMultiplier);
        SetWheelFriction(RFWheel, roadGripMultiplier);
        SetWheelFriction(LRWheel, roadGripMultiplier);
        SetWheelFriction(RRWheel, roadGripMultiplier);
    }

    void FixedUpdate()
    {
        if (inputProvider == null) return;

        //out of bounds
        if (this.position.y < -5f || this.position.y > 5f) this.outOfBounds = true;

        CarInput input = inputProvider.getInput();
        ApplySteering(input);
        float throttleInput = DecodeByteToNormalized(input.Throttle);

        // Reset multipliers
        currentGripMultiplier = roadGripMultiplier;
        currentSpeedMultiplier = 1f;


        // Adjust grip/speed by surface
        AdjustWheelGripAndSpeed(LFWheel);
        AdjustWheelGripAndSpeed(RFWheel);
        AdjustWheelGripAndSpeed(LRWheel);
        AdjustWheelGripAndSpeed(RRWheel);

        // Reset brakes
        LFWheel.brakeTorque = 0f;
        RFWheel.brakeTorque = 0f;
        LRWheel.brakeTorque = 0f;
        RRWheel.brakeTorque = 0f;

        // Throttle / brake logic
        ApplyMotorTorqueAndBrakes(throttleInput);

        // Downforce
        float currentDownforce = downforce * rb.linearVelocity.magnitude;
        rb.AddForce(-transform.up * currentDownforce);

        // Apply final grip scaling
        float gripMultiplierWithDownforce = currentGripMultiplier * (1f + currentDownforce / 50000f);
        SetWheelFriction(LFWheel, gripMultiplierWithDownforce * 0.9f);
        SetWheelFriction(RFWheel, gripMultiplierWithDownforce * 0.9f);
        SetWheelFriction(LRWheel, gripMultiplierWithDownforce);
        SetWheelFriction(RRWheel, gripMultiplierWithDownforce);

        UpdateAllWheelVisuals();
        UpdateSpeedDistanceAcceleration();
    }

    private void UpdateSpeedDistanceAcceleration()
    {
        Vector3 curPos = rb.position;
        this.discountedDistance *= discountFactor;
        this.discountedDistance += (curPos - lastPos).magnitude;
        this.distance += (curPos - lastPos).magnitude;
        lastPos = curPos;

        Vector3 curSpeed = rb.linearVelocity;
        this.acceleration = curSpeed - lastSpeed;
        lastSpeed = curSpeed;
    }

    private void ApplySteering(CarInput input)
    {
        float steeringInput = DecodeByteToNormalized(input.Steering);

        // Steering
        float steering = maxSteeringAngle * steeringInput;
        if (input.UseSpeedSteering)
        {
            float speedFactor = rb.linearVelocity.magnitude / 7.0f;
            steering = maxSteeringAngle / (1f + speedFactor) * steeringInput;
        }
        LFWheel.steerAngle = steering;
        RFWheel.steerAngle = steering;
    }

    private void ApplyMotorTorqueAndBrakes(float throttleInput)
    {
        float forwardVel = Vector3.Dot(rb.linearVelocity, transform.forward);

        if (throttleInput > 0f)
        {
            // Forward drive
            LRWheel.motorTorque = throttleInput * driveSpeed * currentSpeedMultiplier;
            RRWheel.motorTorque = throttleInput * driveSpeed * currentSpeedMultiplier;
        }
        else if (throttleInput < 0f)
        {
            if (forwardVel > 0.1f)
            {
                // Brake instead of instantly reversing
                float brake = brakeForce * -throttleInput;
                LFWheel.brakeTorque = brake;
                RFWheel.brakeTorque = brake;
                LRWheel.brakeTorque = brake;
                RRWheel.brakeTorque = brake;

                LRWheel.motorTorque = 0f;
                RRWheel.motorTorque = 0f;
            }
            else
            {
                // Reverse
                LRWheel.motorTorque = throttleInput * driveSpeed * 0.4f * currentSpeedMultiplier;
                RRWheel.motorTorque = throttleInput * driveSpeed * 0.4f * currentSpeedMultiplier;
            }
        }
        else
        {
            LRWheel.motorTorque = 0f;
            RRWheel.motorTorque = 0f;
        }
    }

    private void AdjustWheelGripAndSpeed(WheelCollider wheel)
    {
        if (wheel.GetGroundHit(out WheelHit hit))
        {
            if (hit.collider != null && hit.collider.gameObject.layer == LayerMask.NameToLayer("Grass"))
            {
                currentGripMultiplier = Mathf.Min(currentGripMultiplier, grassGripMultiplier);
                currentSpeedMultiplier = Mathf.Min(currentSpeedMultiplier, grassSpeedMultiplier);
                this.onGrass = true;
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
        Quaternion rot;
        collider.GetWorldPose(out pos, out rot);

        mesh.position = pos;
        mesh.rotation = rot * Quaternion.Euler(0f, 0f, 90f);
    }

    private void UpdateAllWheelVisuals()
    {
        UpdateWheelVisuals(LFWheel, LFWheelMesh);
        UpdateWheelVisuals(RFWheel, RFWheelMesh);
        UpdateWheelVisuals(LRWheel, LRWheelMesh);
        UpdateWheelVisuals(RRWheel, RRWheelMesh);
    }

    public float GetSpeed() => rb.linearVelocity.magnitude;

    public float GetSteeringAngle() => LFWheel.steerAngle;

    private float DecodeByteToNormalized(byte value)
    {
        return (value - 128f) / 128f;  // 128 = neutral
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Detect collision with anything that isn’t the ground or triggers
        if (collision.collider != null && !collision.collider.isTrigger)
        {
            if (collision.collider.GetComponent<WheelCollider>() != null)
                return; //ignore wheel colider just in case the wheel toutches it
            collided = true;
            // Debug.Log($"{name} collided with {collision.collider.name}");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("finishLine"))
        {
            crossedFinish = true;
            // Debug.Log($"{name} crossed the finish line");
        }
        else if (other.CompareTag("halfway"))
        {
            crossedHalfway = true;
            // Debug.Log($"{name} crossed the halfway point");
        }
    }

    public (bool collided, bool finish, bool halfway, bool onGrass, bool outOfBounds) ConsumeCollisionFlags()
    {
        var result = (collided, crossedFinish, crossedHalfway, onGrass, outOfBounds);
        collided = false;
        crossedFinish = false;
        crossedHalfway = false;
        onGrass = false;
        outOfBounds = false;
        return result;
    }
}
