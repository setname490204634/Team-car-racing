using UnityEngine;

public class camera : MonoBehaviour
{
    public Transform target; // your car
    public Vector3 offset = new Vector3(0, 5, -10); // adjust as you like
    public float smoothSpeed = 0.125f;

    void LateUpdate()
    {
        Vector3 desiredPosition = target.position + offset;
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
        transform.position = smoothedPosition;

        //transform.rotation = Quaternion.LookRotation(target.forward);
        transform.LookAt(target);
    }
}