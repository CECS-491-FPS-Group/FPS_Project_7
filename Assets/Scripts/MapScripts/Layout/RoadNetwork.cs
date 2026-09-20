using System.Collections.Generic;
using UnityEngine;

public struct RoadSample
{
    public float Distance;
    public float Height;
    public bool Hit;
    /// <summary>Nearest segment spans water, so the terrain beneath it is left untouched.</summary>
    public bool Bridge;
}

/// <summary>Road centrelines as graded polylines, with a spatial index for nearest-segment queries.</summary>
public sealed class RoadNetwork
{
    // x/z are world position, y is the graded road height at that point.
    readonly Vector3[] points;
    readonly bool[] bridgePoints;
    readonly int[] segmentA;
    readonly int[] segmentB;
    readonly int[] roadFirstPoint;
    readonly int[] roadPointCount;
    readonly SpatialGrid grid;

    public readonly float HalfWidth;
    public readonly float Shoulder;

    public int SegmentCount { get { return segmentA.Length; } }
    public int RoadCount { get { return roadFirstPoint.Length; } }
    public Vector3[] Points { get { return points; } }

    public float MaxInfluence
    {
        get { return HalfWidth + Shoulder; }
    }

    public RoadNetwork(Vector3[] points, bool[] bridgePoints, int[] segmentA, int[] segmentB,
        int[] roadFirstPoint, int[] roadPointCount, Rect worldBounds, float halfWidth, float shoulder)
    {
        this.points = points;
        this.bridgePoints = bridgePoints;
        this.segmentA = segmentA;
        this.segmentB = segmentB;
        this.roadFirstPoint = roadFirstPoint;
        this.roadPointCount = roadPointCount;
        HalfWidth = halfWidth;
        Shoulder = shoulder;

        float influence = MaxInfluence;
        grid = SpatialGrid.Build(segmentA.Length, index => SegmentBounds(index, influence), worldBounds, Mathf.Max(influence * 2f, 16f));
    }

    public void GetSegment(int index, out Vector3 a, out Vector3 b)
    {
        a = points[segmentA[index]];
        b = points[segmentB[index]];
    }

    /// <summary>Point range of one road.</summary>
    public void GetRoad(int road, out int firstPoint, out int pointCount)
    {
        firstPoint = roadFirstPoint[road];
        pointCount = roadPointCount[road];
    }

    public bool IsBridgePoint(int pointIndex)
    {
        return bridgePoints[pointIndex];
    }

    /// <summary>A segment is a bridge only when both ends are over water; a segment with one land end is the approach ramp.</summary>
    public bool IsBridgeSegment(int segmentIndex)
    {
        return bridgePoints[segmentA[segmentIndex]] && bridgePoints[segmentB[segmentIndex]];
    }

    Rect SegmentBounds(int index, float padding)
    {
        Vector3 a = points[segmentA[index]];
        Vector3 b = points[segmentB[index]];

        return Rect.MinMaxRect(
            Mathf.Min(a.x, b.x) - padding,
            Mathf.Min(a.z, b.z) - padding,
            Mathf.Max(a.x, b.x) + padding,
            Mathf.Max(a.z, b.z) + padding);
    }

    /// <summary>Nearest road centreline within <paramref name="radius"/>.</summary>
    public RoadSample Sample(Vector2 worldXZ, float radius, List<int> buffer)
    {
        RoadSample result = new RoadSample { Distance = float.MaxValue, Height = 0f, Hit = false, Bridge = false };

        if (segmentA.Length == 0)
        {
            return result;
        }

        grid.Query(worldXZ, radius, buffer);

        for (int i = 0; i < buffer.Count; i++)
        {
            int index = buffer[i];
            Vector3 a = points[segmentA[index]];
            Vector3 b = points[segmentB[index]];

            Vector2 start = new Vector2(a.x, a.z);
            Vector2 delta = new Vector2(b.x - a.x, b.z - a.z);
            float lengthSqr = delta.sqrMagnitude;

            float t = lengthSqr > 1e-6f ? Mathf.Clamp01(Vector2.Dot(worldXZ - start, delta) / lengthSqr) : 0f;
            Vector2 closest = start + delta * t;
            float distance = Vector2.Distance(worldXZ, closest);

            if (distance < result.Distance)
            {
                result.Distance = distance;
                result.Height = Mathf.Lerp(a.y, b.y, t);
                result.Hit = true;
                result.Bridge = IsBridgeSegment(index);
            }
        }

        return result;
    }
}
