using UnityEngine;

public interface ICarObserver
{
    RenderTexture GetCameraTexture();  
    float GetSpeed();                  // km/h
    float GetSteeringAngle();          // front wheel angle
}