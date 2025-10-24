using UnityEngine;
using System;


public interface ICarObserver
{
    CarObservation GetCarObservation();
}

public struct CarObservation
{
    public RenderTexture leftCameraTexture;
    public RenderTexture rightCameraTexture;
    public byte Speed;          // m/s
    public byte SteeringAngle;  // front wheel angle
}