using UnityEngine;

public class CarCamera : MonoBehaviour
{
    public Transform target; // car
    public Vector3 offset = new Vector3(0, 5, -10);
    public float smoothSpeed = 0.125f;

    void LateUpdate()
    {
        if (!target) return;

        // Rotate the offset relative to car’s rotation
        Vector3 desiredPosition = target.position + target.rotation * offset;

        // Smooth follow
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);

        // Smoothly rotate camera to match car’s facing direction
        Quaternion desiredRotation = Quaternion.LookRotation(target.position - transform.position, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, smoothSpeed);
    }
}
