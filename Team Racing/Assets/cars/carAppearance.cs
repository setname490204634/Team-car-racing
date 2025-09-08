using UnityEngine;

public class CarAppearance : MonoBehaviour
{
    [Header("Optional body material")]
    public Material bodyMaterial; // assign in inspector

    [Header("Parts to ignore")]
    public Renderer[] ignoreRenderers; // parts that keep default color

    private Renderer[] allRenderers;

    void Awake()
    {
        // Get all renderers in the car
        allRenderers = GetComponentsInChildren<Renderer>();

        ApplyMaterial();
    }

    public void ApplyMaterial()
    {
        if (bodyMaterial == null) return;

        foreach (Renderer r in allRenderers)
        {
            bool ignore = false;
            foreach (Renderer ig in ignoreRenderers)
            {
                if (r == ig)
                {
                    ignore = true;
                    break;
                }
            }
            if (!ignore)
            {
                r.material = bodyMaterial;
            }
        }
    }
}

