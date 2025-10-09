using UnityEngine;
using System;

/// <summary>
/// Interface for anything that can provide car observations
/// </summary>
public interface ICarObserver
{
    CarObservation GetCarObservation();
}

/// <summary>
/// Runtime observation (Unity-side), contains RenderTextures
/// </summary>
public struct CarObservation
{
    public RenderTexture leftCameraTexture;
    public RenderTexture rightCameraTexture;
    public byte Speed;          // m/s
    public byte SteeringAngle;  // front wheel angle
}

/// <summary>
/// Serializable observation (network/JSON side),
/// contains image data instead of Unity RenderTextures
/// </summary>
[Serializable]
public struct SerializableCarObservation
{
    public string leftCameraImage;   // Base64 encoded PNG/JPG/raw bytes
    public string rightCameraImage;
    public byte Speed;
    public byte SteeringAngle;
}
