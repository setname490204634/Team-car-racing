using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

[ExecuteAlways] // Runs both in Play Mode and Editor
public class MapSegmentHandler : MonoBehaviour
{
    [Tooltip("Starting tile of the road network.")]
    public TilePoints startTile;

    [Tooltip("Maximum distance allowed to connect two endpoints.")]
    public const float connectionThreshold = 0.1f;

    public List<Vector2> road = null;

    private class TilePath
    {
        public List<Vector2> points;
        public Vector2 lastPoint;

        public TilePath(List<Vector2> pts)
        {
            points = new List<Vector2>(pts);
            lastPoint = points[points.Count - 1];
        }

        public List<Vector2> TryConnect(Vector2 toPoint, float threshold)
        {
            if (points.Count < 2) return null;

            float distStart = Vector2.Distance(points[0], toPoint);
            float distEnd = Vector2.Distance(lastPoint, toPoint);

            if (distEnd <= threshold)
            {
                points.Reverse();
                return points;
            }
            if (distStart <= threshold) return points;
            return null;
        }
    }

    private void OnValidate()
    {
        // Only rebuild if we’re in the editor (not playing)
        if (!Application.isPlaying)
        {
            BuildRoad();
        }
    }

    private void Start()
    {
        // Also build once when the scene starts (runtime)
        BuildRoad();
    }

    public void BuildRoad()
    {
        TilePoints[] tiles = GetComponentsInChildren<TilePoints>();
        if (startTile == null)
        {
            return;
        }

        List<TilePath> unplaced = new List<TilePath>();
        TilePath startPath = null;

        foreach (var tile in tiles)
        {
            var pts = tile.GetWorldPoints2D();
            var path = new TilePath(pts);

            if (tile == startTile)
                startPath = path;
            else
                unplaced.Add(path);
        }

        if (startPath == null)
        {
            return;
        }

        road = new List<Vector2>(startPath.points);

        bool foundConnection;
        do
        {
            foundConnection = false;
            for (int i = 0; i < unplaced.Count; i++)
            {
                var candidate = unplaced[i];
                var newSegment = candidate.TryConnect(road[^1], connectionThreshold);
                if (newSegment != null)
                {
                    road.RemoveAt(road.Count - 1);
                    road.AddRange(newSegment);
                    unplaced.RemoveAt(i);
                    foundConnection = true;
                    break;
                }
            }
        } while (foundConnection && unplaced.Count > 0);

        if (road.Count > 0)
            road.RemoveAt(road.Count - 1);
    }

    // expected use: index = GetClosestIndex(index, currentPos)
    // works as position update
    public int GetClosestIndex(int index, Vector2 pos)
    {
        if (road == null || road.Count == 0) return index;

        int count = road.Count;
        float bestDist = float.MaxValue;
        int bestIndex = index % count;

        for (int i = -1; i <= 1; i++)
        {
            int checkIndex = (index + i + count) % count;
            float dist = Vector2.Distance(road[checkIndex], pos);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestIndex = checkIndex;
            }
        }
        return bestIndex;
    }

    public Vector2 GetVectorI(int index, Vector2 pos)
    {
        if (road == null || road.Count < 3) return Vector2.zero;
        int count = road.Count;
        int index1 = (index + 1) % count;

        return (road[index1] - pos).normalized;
    }

    public Vector2 GetVectorII(int index, Vector2 pos)
    {
        if (road == null || road.Count < 3) return Vector2.zero;
        int count = road.Count;
        int index2 = (index + 2) % count;

        return (road[index2] - pos).normalized;
    }

    public Vector2 GetVectorIII(int index, Vector2 pos)
    {
        if (road == null || road.Count < 3) return Vector2.zero;
        int count = road.Count;
        int index1 = (index + 1) % count;

        return (road[index1] - road[index]).normalized;
    }

    public Vector2 GetVectorIV(int index, Vector2 pos)
    {
        if (road == null || road.Count < 3) return Vector2.zero;
        int count = road.Count;
        int index1 = (index + 1) % count;
        int index2 = (index + 2) % count;

        return (road[index2] - road[index1]).normalized;
    }

    public Vector2 GetVectorV(int index, Vector2 pos)
    {
        if (road == null || road.Count < 3) return Vector2.zero;

        int count = road.Count;
        int index1 = (index + 1) % count;
        int index2 = (index + 2) % count;

        (Vector2 center, float radius) = MakeCircle(road[index], road[index1], road[index2]);
        if (float.Equals(radius, 0f)) return GetVectorIII(index, pos); // if circle cant be made or is too large the direction is straight

        Vector2 carLine = (pos - center).normalized;

        // Tangent direction (90 degrees to radius)
        Vector2 tangent = new Vector2(-carLine.y, carLine.x);

        // Ensure tangent direction goes from i toward i+1
        if (Vector2.Dot(tangent, road[index1] - road[index]) < 0)
            tangent = -tangent;

        return tangent.normalized;
    }

    private (Vector2 center, float radius) MakeCircle(Vector2 a, Vector2 b, Vector2 c)
    {
        Vector2 mid1 = (a + b) / 2;
        Vector2 mid2 = (b + c) / 2;

        // Perpendicular bisectors of each segment
        Vector2 line1vec = (b - a).normalized;
        Vector2 line2vec = (c - b).normalized;

        Vector2 perp1 = new Vector2(-line1vec.y, line1vec.x);
        Vector2 perp2 = new Vector2(-line2vec.y, line2vec.x);

        // Solve for circle center (intersection of bisectors)
        bool found = LineLineIntersection(mid1, mid1 + perp1, mid2, mid2 + perp2, out Vector2 center);
        if (!found) return (Vector2.zero, 0f);

        float radius = (a - center).magnitude;
        return (center, radius);
    }

    private bool LineLineIntersection(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, out Vector2 intersection)
    {
        intersection = Vector2.zero;
        float A1 = p2.y - p1.y;
        float B1 = p1.x - p2.x;
        float C1 = A1 * p1.x + B1 * p1.y;

        float A2 = p4.y - p3.y;
        float B2 = p3.x - p4.x;
        float C2 = A2 * p3.x + B2 * p3.y;

        float denominator = A1 * B2 - A2 * B1;

        if (Mathf.Abs(denominator) < 0.01f) return false; // almost parallel

        intersection = new Vector2(
            (B2 * C1 - B1 * C2) / denominator,
            (A1 * C2 - A2 * C1) / denominator
        );
        return true;
    }

    public float GetDistanceI(int index, Vector2 pos)
    {
        if (road == null || road.Count < 3) return 0f;
        int count = road.Count;
        int index1 = (index + 1) % count;

        return Vector2.Distance(pos, road[index1]) / Vector2.Distance(road[index], road[index1]);
    }

    public float GetDistanceII(int index, Vector2 pos)
    {
        if (road == null || road.Count < 3) return 0f;

        int count = road.Count;
        int index1 = (index + 1) % count;
        int index2 = (index + 2) % count;

        (Vector2 center, float radius) = MakeCircle(road[index], road[index1], road[index2]);
        if (float.Equals(radius, 0f)) return 0f;

        float output = (radius - Vector2.Distance(pos, center));
        if (output < 0) output = -output;
        return output;
    }

    public float GetDistanceIII(int index, Vector2 pos)
    {
        if (road == null || road.Count < 3) return 0f;

        int count = road.Count;
        int index1 = (index + 1) % count;
        int index2 = (index + 2) % count;

        (Vector2 center, float radius) = MakeCircle(road[index], road[index1], road[index2]);
        if (float.Equals(radius, 0f)) return 0f;

        float output = (Vector2.Distance(pos, center) - radius);
        if (output < 0) output = -output;
        return output;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (road == null || road.Count < 2) return;
        Gizmos.color = Color.yellow;
        for (int i = 0; i < road.Count; i++)
        {
            Vector3 a = new Vector3(road[i].x, 1.0f, road[i].y);
            Vector3 b = new Vector3(road[(i + 1) % road.Count].x, 1.0f, road[(i + 1)% road.Count].y);
            Gizmos.DrawLine(a, b);
        }
    }
#endif
}
