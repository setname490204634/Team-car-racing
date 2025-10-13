using UnityEngine;
using System;

public static class CarObservationSerializer
{
    // Packs merged (RGB24) image + speed + steering + carID + reward
    public static byte[] PackCarObservation(CarObservation observation, int carID, float reward)
    {
        RenderTexture leftRT = observation.leftCameraTexture;
        RenderTexture rightRT = observation.rightCameraTexture;

        int width = leftRT.width;
        int height = leftRT.height;
        int mergedWidth = width * 2;
        int mergedHeight = height;

        // Merge both camera views
        RenderTexture mergedRT = MergeRenderTextures(leftRT, rightRT);

        // Read merged RenderTexture to Texture2D (RGB24)
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = mergedRT;

        Texture2D tex = new Texture2D(mergedWidth, mergedHeight, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, mergedWidth, mergedHeight), 0, 0);
        tex.Apply();

        RenderTexture.active = prev;

        // Extract raw bytes (3 bytes per pixel)
        byte[] imageBytes = tex.GetRawTextureData();

        UnityEngine.Object.Destroy(tex);
        UnityEngine.Object.Destroy(mergedRT);

        // Prepare header (10 bytes)
        byte speedByte = observation.Speed;
        byte steerByte = observation.SteeringAngle;
        byte[] idBytes = BitConverter.GetBytes(carID);
        byte[] rewardBytes = BitConverter.GetBytes(reward);

        byte[] header = new byte[10];
        header[0] = speedByte;
        header[1] = steerByte;
        Array.Copy(idBytes, 0, header, 2, 4);
        Array.Copy(rewardBytes, 0, header, 6, 4);

        // Combine header + image
        byte[] payload = new byte[header.Length + imageBytes.Length];
        Buffer.BlockCopy(header, 0, payload, 0, header.Length);
        Buffer.BlockCopy(imageBytes, 0, payload, header.Length, imageBytes.Length);

        return payload;
    }

    // Merge two RenderTextures side by side (left | right)
    private static RenderTexture MergeRenderTextures(RenderTexture left, RenderTexture right)
    {
        int width = left.width;
        int height = left.height;

        RenderTexture merged = new RenderTexture(width * 2, height, 0, RenderTextureFormat.ARGB32);
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = merged;

        GL.PushMatrix();
        GL.LoadPixelMatrix(0, merged.width, merged.height, 0);

        Graphics.DrawTexture(new Rect(0, 0, width, height), left);
        Graphics.DrawTexture(new Rect(width, 0, width, height), right);

        GL.PopMatrix();
        RenderTexture.active = prev;

        return merged;
    }
}
