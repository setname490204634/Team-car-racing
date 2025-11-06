using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways] // Runs both in Play Mode and Editor
public class MapSegmentHandler : MonoBehaviour
{
    [Tooltip("Starting tile of the road network.")]
    public TilePoints startTile;

    [Tooltip("Maximum distance allowed to connect two endpoints.")]
    public float connectionThreshold = 0.1f;

    public List<Vector2> road = new List<Vector2>();

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

    // projects given velocity onto the road segment direction
    public float GetDirectionSpeed(int index, Vector2 velocity)
    {
        if (road == null || road.Count < 2) return 0f;

        int count = road.Count;
        int nextIndex = (index + 1) % count;

        Vector2 dir = (road[nextIndex] - road[index]).normalized;

        float speedAlongRoad = Vector2.Dot(velocity, dir);

        return Mathf.Abs(speedAlongRoad); ;
    }


    // absolute angle between the road segment direction and the velocity vector
    public float GetAngleToNext(int index, Vector2 velocity)
    {
        if (road == null || road.Count < 2) return 0f;

        int count = road.Count;
        int nextIndex = (index + 1) % count;

        Vector2 dir = (road[nextIndex] - road[index]);

        float angle = Vector2.SignedAngle(dir, velocity);

        return Mathf.Abs(angle);
    }

    // distance to next point, normalized to 0-1 based on the segment length
    public float GetDistanceToNext(int index, Vector2 pos)
    {
        if (road == null || road.Count < 2) return 0f;

        int count = road.Count;
        int nextIndex = (index + 1) % count;

        return Vector2.Distance(pos, road[nextIndex]) / Vector2.Distance(road[index], road[nextIndex]);
    }

    // shortest distance from pos to segment
    public float GetDistanceFromSegment(int index, Vector2 pos)
    {
        if (road == null || road.Count < 2) return 0f;

        int count = road.Count;
        int nextIndex = (index + 1) % count;

        Vector2 a = road[index];
        Vector2 b = road[nextIndex];
        Vector2 ap = pos - a;
        Vector2 ab = b - a;

        float abLenSqr = ab.sqrMagnitude;
        if (abLenSqr < 0.0001f) return ap.magnitude;

        float t = Mathf.Clamp01(Vector2.Dot(ap, ab) / abLenSqr);
        Vector2 closest = a + ab * t;

        return Vector2.Distance(pos, closest);
    }
    //speed in a direction of next point
    public Vector2 GetNextDirectionSpeed(int index, float speed)
    {
        if (road == null || road.Count < 2)
            return Vector2.zero;

        int count = road.Count;
        int nextIndex = (index + 1) % count;
        int nextNextIndex = (index + 2) % count;

        Vector2 dir = (road[nextNextIndex] - road[nextIndex]).normalized;
        return dir * speed;
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
