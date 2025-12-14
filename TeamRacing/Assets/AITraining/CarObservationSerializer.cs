using UnityEngine;
using System;

public static class CarObservationSerializer
{
    // Packs merged (RGB24) image + speed + steering + carID + reward
    public static byte[] PackCarObservation(CarObservation observation, int carID, Rewards reward)
    {
        RenderTexture rt = observation.cameraTexture;
        int width = rt.width;
        int height = rt.height;

        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;

        Texture2D tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply(false, false);

        RenderTexture.active = prev;

        byte[] imageBytes = tex.GetRawTextureData();

        UnityEngine.Object.Destroy(tex);

        // Prepare header
        byte speedByte = observation.Speed;
        byte steerByte = observation.SteeringAngle;
        byte[] idBytes = BitConverter.GetBytes(carID);

        float[] rewardArray = reward.ToArray();
        byte[] rewardBytes = new byte[rewardArray.Length * sizeof(float)];
        Buffer.BlockCopy(rewardArray, 0, rewardBytes, 0, rewardBytes.Length);


        byte[] header = new byte[1 + 1 + 4 + rewardBytes.Length];
        header[0] = speedByte;
        header[1] = steerByte;
        Array.Copy(idBytes, 0, header, 2, 4);
        Array.Copy(rewardBytes, 0, header, 6, rewardBytes.Length);

        // Combine header + image
        byte[] payload = new byte[header.Length + imageBytes.Length];
        Buffer.BlockCopy(header, 0, payload, 0, header.Length);
        Buffer.BlockCopy(imageBytes, 0, payload, header.Length, imageBytes.Length);

        return payload;
    }
}
