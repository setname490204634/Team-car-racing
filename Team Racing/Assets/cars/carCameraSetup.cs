using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CarCameraSetup : MonoBehaviour
{
    public Camera carCamera;        // assign in prefab

    public RenderTexture carTexture;

    public int textureWidth = 128;
    public int textureHeight = 128;

    void Awake()
    {
        if (carCamera == null) carCamera = GetComponent<Camera>();

        // Create a RenderTexture dynamically
        carTexture = new RenderTexture(textureWidth, textureHeight, 24, RenderTextureFormat.ARGB32);
        carTexture.name = $"{name}_RT";

        // Assign to camera
        carCamera.targetTexture = carTexture;
    }

    public RenderTexture GetCameraTexture() => carTexture;
}
