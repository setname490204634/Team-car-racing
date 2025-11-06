using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class TilePoints : MonoBehaviour
{
    [Tooltip("Points for track, height is ignored")]
    public List<Vector3> localPoints = new List<Vector3>();
    public List<Vector2> GetWorldPoints2D()
    {
        List<Vector2> worldPoints2D = new List<Vector2>(localPoints.Count);
        foreach (var localPoint in localPoints)
        {
            Vector3 worldPoint = transform.TransformPoint(localPoint);
            worldPoints2D.Add(new Vector2(worldPoint.x, worldPoint.z));
        }
        return worldPoints2D;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        var points = GetWorldPoints2D();
        foreach (var p in points)
        {
            Gizmos.DrawSphere(new Vector3(p.x, 1.0f, p.y), 0.6f);
        }
    }
#endif
}
