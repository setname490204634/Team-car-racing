using UnityEngine;

public class carController : MonoBehaviour
{
    public WheelCollider LFWheel;
    public WheelCollider LRWheel;
    public WheelCollider RFWheel;
    public WheelCollider RRWheel;
    float horizontalInput;
    float verticalInput;

    public float driveSpeed;
    public float maxSteeringAngle;

    void Update()
    {
        horizontalInput = Input.GetAxis("Horizontal");
        verticalInput = Input.GetAxis("Vertical");
    }
    void FixedUpdate()
    {
        float motor = driveSpeed * verticalInput;
        float steering = maxSteeringAngle * horizontalInput;

        LFWheel.steerAngle = steering;
        RFWheel.steerAngle = steering;

        LRWheel.motorTorque = motor;
        RRWheel.motorTorque = motor;
    }
}