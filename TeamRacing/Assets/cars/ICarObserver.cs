using UnityEngine;
using System;


public interface ICarObserver
{
    CarObservation GetCarObservation();
}

public struct CarObservation
{
    public RenderTexture cameraTexture;
    public byte Speed;          // m/s
    public byte SteeringAngle;  // front wheel angle
}