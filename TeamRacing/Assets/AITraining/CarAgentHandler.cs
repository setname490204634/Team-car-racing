using UnityEngine;
using System;

[RequireComponent(typeof(CarController))]
[RequireComponent(typeof(ICarInputProvider))]
public class CarAgent : MonoBehaviour, ICarObserver
{
    // Build on carController working as agent
    private CarController car;

    [Header("Settings")]
    public Camera leftCamera;
    public Camera rightCamera;
    public int cameraWidth = 64;
    public int cameraHeight = 64;

    [Header("Input Provider")]
    public AgentInputProvider agentInputProvider;

    [Header("Mirrors")]
    public StaticMirror[] mirrors;

    private ICarInputProvider inputProvider;

    void Awake()
    {
        car = GetComponent<CarController>();
        inputProvider = agentInputProvider;

        // Assign input provider to CarController's private field via reflection
        var inputField = car.GetType().GetField(
            "inputProvider",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
        );
        if (inputField != null)
        {
            inputField.SetValue(car, inputProvider);
        }
        else
        {
            Debug.LogError("CarController does not have an inputProvider field!");
        }

        // Ensure cameras have RenderTextures
        SetupCameraRenderTexture(leftCamera, cameraWidth, cameraHeight);
        SetupCameraRenderTexture(rightCamera, cameraWidth, cameraHeight);
    }

    private void SetupCameraRenderTexture(Camera cam, int width, int height)
    {
        if (cam == null) return;

        if (cam.targetTexture == null ||
            cam.targetTexture.width != width ||
            cam.targetTexture.height != height)
        {
            RenderTexture rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 1;
            rt.Create();
            cam.targetTexture = rt;
            cam.enabled = true;
        }
    }

    public CarObservation GetCarObservation()
    {
        if (mirrors != null)
        {
            foreach (var mirror in mirrors)
            {
                mirror.RenderMirror();
            }
        }

        if (leftCamera != null)
            leftCamera.Render();

        if (rightCamera != null)
            rightCamera.Render();

        const float maxSpeed = 200f;
        const float maxSteering = 180f;

        byte speedByte = (byte)Mathf.Clamp(
            Mathf.RoundToInt(car.GetSpeed() / maxSpeed * 255f), 0, 255);

        byte steeringByte = (byte)Mathf.Clamp(
            Mathf.RoundToInt((car.GetSteeringAngle() + maxSteering) / (2f * maxSteering) * 255f), 0, 255);

        return new CarObservation
        {
            leftCameraTexture = leftCamera.targetTexture,
            rightCameraTexture = rightCamera.targetTexture,
            Speed = speedByte,
            SteeringAngle = steeringByte
        };
    }


    public void ApplyAction(CarInput input)
    {
        inputProvider.SetInput(input);
    }
}
