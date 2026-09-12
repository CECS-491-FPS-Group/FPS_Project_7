using UnityEngine;

/// <summary>
/// Edge falloff for a bounded world, evaluated in world space so it is continuous across chunks.
/// </summary>
public readonly struct WorldFalloff
{
    public readonly bool Enabled;
    public readonly Vector2 Centre;
    public readonly float Extent;
    public readonly float Start;
    public readonly float End;

    public WorldFalloff(Vector2 centre, float extent, float start, float end)
    {
        Enabled = extent > 0f;
        Centre = centre;
        Extent = extent;
        Start = start;
        End = end;
    }

    public static WorldFalloff Disabled
    {
        get { return new WorldFalloff(Vector2.zero, 0f, 0f, 1f); }
    }

    public static WorldFalloff From(WorldSettings worldSettings, float meshWorldSize)
    {
        if (worldSettings == null || !worldSettings.useFalloff)
        {
            return Disabled;
        }

        return new WorldFalloff(
            worldSettings.WorldCentre(meshWorldSize),
            worldSettings.WorldExtent(meshWorldSize),
            worldSettings.falloffStart,
            worldSettings.falloffEnd);
    }

    /// <summary>0 at the world centre, rising to 1 at the grid edge along the dominant axis.</summary>
    public float Evaluate(Vector2 worldXZ)
    {
        if (!Enabled)
        {
            return 0f;
        }

        Vector2 offset = worldXZ - Centre;
        float normalised = Mathf.Max(Mathf.Abs(offset.x), Mathf.Abs(offset.y)) / Extent;
        return FalloffGenerator.Evaluate(Mathf.InverseLerp(Start, End, normalised));
    }
}
