using UnityEngine;

/// <summary>
/// A flattened rectangular pad. Phase 5 places building prefabs on these; the height layer
/// only needs the pad's footprint and target height.
/// </summary>
public struct BuildingPlot
{
    public Vector2 Centre;
    public Vector2 HalfExtents;
    public float Rotation;
    public float Height;
    public float Shoulder;

    public float Radius
    {
        get { return HalfExtents.magnitude + Shoulder; }
    }

    public Rect Bounds
    {
        get
        {
            float radius = Radius;
            return Rect.MinMaxRect(Centre.x - radius, Centre.y - radius, Centre.x + radius, Centre.y + radius);
        }
    }

    /// <summary>Distance from the pad edge, clamped to zero inside the pad.</summary>
    public float DistanceOutside(Vector2 worldXZ)
    {
        Vector2 delta = worldXZ - Centre;
        float cos = Mathf.Cos(-Rotation);
        float sin = Mathf.Sin(-Rotation);
        Vector2 local = new Vector2(delta.x * cos - delta.y * sin, delta.x * sin + delta.y * cos);

        float qx = Mathf.Abs(local.x) - HalfExtents.x;
        float qy = Mathf.Abs(local.y) - HalfExtents.y;

        return new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
    }

    public bool Overlaps(BuildingPlot other, float spacing)
    {
        float combined = Radius + other.Radius + spacing;
        return (Centre - other.Centre).sqrMagnitude < combined * combined;
    }

    /// <summary>Corner positions in world XZ, clockwise from the local negative corner.</summary>
    public void GetCorners(Vector2[] corners)
    {
        float cos = Mathf.Cos(Rotation);
        float sin = Mathf.Sin(Rotation);

        corners[0] = Rotate(new Vector2(-HalfExtents.x, -HalfExtents.y), cos, sin) + Centre;
        corners[1] = Rotate(new Vector2(-HalfExtents.x, HalfExtents.y), cos, sin) + Centre;
        corners[2] = Rotate(new Vector2(HalfExtents.x, HalfExtents.y), cos, sin) + Centre;
        corners[3] = Rotate(new Vector2(HalfExtents.x, -HalfExtents.y), cos, sin) + Centre;
    }

    static Vector2 Rotate(Vector2 v, float cos, float sin)
    {
        return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
    }
}
