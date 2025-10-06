using UnityEngine;

public class StaticMirror : MonoBehaviour
{
    [Header("Assign manually")]
    public Transform mirrorPlane;   // The plane showing the mirror
    public Camera mirrorCamera;     // The camera rendering the mirror
    public int resolutionWidth;
    public int resolutionHeight;

    private RenderTexture rt;

    void Awake()
    {
        if (mirrorPlane == null || mirrorCamera == null)
        {
            Debug.LogError("MirrorPlane and MirrorCamera must be assigned!");
            enabled = false;
            return;
        }

        // Create a tiny RenderTexture for this mirror
        rt = new RenderTexture(resolutionWidth, resolutionHeight, 16);
        rt.name = "MirrorRT_" + gameObject.name;
        mirrorCamera.targetTexture = rt;

        // Assign RenderTexture to the plane material
        Renderer rend = mirrorPlane.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.material = new Material(rend.sharedMaterial); // instance per mirror
            rend.material.mainTexture = rt;
        }
        else
        {
            Debug.LogWarning("Mirror plane has no Renderer component!");
        }
    }

    void OnDisable()
    {
        if (rt != null)
        {
            if (mirrorCamera != null && mirrorCamera.targetTexture == rt)
            {
                mirrorCamera.targetTexture = null;
            }
            Destroy(rt);
            rt = null;
        }
    }
}
