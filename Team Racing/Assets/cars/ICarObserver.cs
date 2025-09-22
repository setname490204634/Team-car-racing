using UnityEngine;

public interface ICarObserver
{
    public CarObservation getCarObservation();
}
    
public struct CarObservation
{
    public RenderTexture leftCameraTexture;
    public RenderTexture rightCameraTexture;
    public float Speed;                  // m/s
    public float SteeringAngle;          // front wheel angle
}