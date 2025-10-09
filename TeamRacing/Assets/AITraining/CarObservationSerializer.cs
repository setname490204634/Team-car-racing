using UnityEngine;
using System;

public static class CarObservationSerializer
{
    // Packs 2 cameras + speed + steering + carID + reward into a single byte array
    public static byte[] PackCarObservation(CarObservation observation, int carID, int reward)
    {
        // Get RenderTextures
        RenderTexture leftRT = observation.leftCameraTexture;
        RenderTexture rightRT = observation.rightCameraTexture;

        int width = leftRT.width;
        int height = leftRT.height;
        int numPixels = width * height;

        // Use bytes for speed and steering (0–255)
        byte speedByte = observation.Speed;
        byte steerByte = observation.SteeringAngle;

        // Convert carID and reward to int (4 bytes each)
        byte[] idBytes = BitConverter.GetBytes(carID);
        byte[] rewardBytes = BitConverter.GetBytes(reward);

        // Header: 1 byte speed, 1 byte steering, 4 bytes carID, 4 bytes reward
        byte[] header = new byte[10];
        header[0] = speedByte;
        header[1] = steerByte;
        Array.Copy(idBytes, 0, header, 2, 4);
        Array.Copy(rewardBytes, 0, header, 6, 4);

        // Allocate full payload: header + 2 cameras * 2 bytes per pixel
        byte[] payload = new byte[header.Length + numPixels * 2 * 2];

        // Copy header
        Array.Copy(header, 0, payload, 0, header.Length);

        // Copy camera data
        CopyRenderTextureRGB565(leftRT, payload, header.Length);                  // left camera
        CopyRenderTextureRGB565(rightRT, payload, header.Length + numPixels * 2); // right camera

        return payload;
    }

    // Reads RenderTexture as RGB565 and writes into byte array starting at offset
    private static void CopyRenderTextureRGB565(RenderTexture rt, byte[] target, int offset)
    {
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;

        Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB565, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();

        byte[] rawBytes = tex.GetRawTextureData(); // 2 bytes per pixel
        Array.Copy(rawBytes, 0, target, offset, rawBytes.Length);

        UnityEngine.Object.Destroy(tex);
        RenderTexture.active = prev;
    }
}
