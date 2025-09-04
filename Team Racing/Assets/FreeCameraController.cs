using UnityEngine;

public class FreeCameraController : MonoBehaviour
{
    public float moveSpeed = 10f;       // WASD movement speed
    public float lookSpeed = 2f;        // Mouse sensitivity
    public float fastMultiplier = 2f;   // Shift for fast move
    public float slowMultiplier = 0.5f; // Ctrl for slow move

    private float yaw = 0f;
    private float pitch = 0f;

    void Start()
    {
        // Lock cursor for FPS-like control
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        yaw = transform.eulerAngles.y;
        pitch = transform.eulerAngles.x;
    }

    void Update()
    {
        // --- Mouse Look ---
        yaw += Input.GetAxis("Mouse X") * lookSpeed;
        pitch -= Input.GetAxis("Mouse Y") * lookSpeed;
        pitch = Mathf.Clamp(pitch, -89f, 89f);

        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);

        // --- Movement ---
        float speed = moveSpeed;
        if (Input.GetKey(KeyCode.LeftShift)) speed *= fastMultiplier;
        if (Input.GetKey(KeyCode.LeftControl)) speed *= slowMultiplier;

        Vector3 move = new Vector3(
            Input.GetAxis("Horizontal"),
            0f,
            Input.GetAxis("Vertical")
        );

        // Move relative to camera orientation
        transform.Translate(move * speed * Time.deltaTime);

        // Move up/down
        if (Input.GetKey(KeyCode.E)) transform.Translate(Vector3.up * speed * Time.deltaTime);
        if (Input.GetKey(KeyCode.Q)) transform.Translate(Vector3.down * speed * Time.deltaTime);
    }
}
