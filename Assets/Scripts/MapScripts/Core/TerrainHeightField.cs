using UnityEngine;

/// <summary>
/// Terrain height before any layout carving, queryable at any world position.
/// The layout layer uses this to grade roads and flatten building pads without
/// needing a chunk to exist yet.
/// </summary>
public sealed class TerrainHeightField
{
    readonly NoiseSampler noise;
    readonly WorldFalloff falloff;
    readonly AnimationCurve heightCurve;
    readonly float heightMultiplier;
    readonly bool applyFalloff;

    public TerrainHeightField(HeightMapSettings settings, WorldFalloff falloff, int seed, float meshScale)
    {
        noise = NoiseSampler.Create(settings.noiseSettings, seed, meshScale);
        heightCurve = new AnimationCurve(settings.heightCurve.keys);
        heightMultiplier = settings.heightMultiplier;
        this.falloff = falloff;
        applyFalloff = settings.useFalloff && falloff.Enabled;
    }

    /// <summary>
    /// Shared by the grid generator and the layout so both agree exactly. The noise is
    /// multiplied back in rather than replaced, matching the original generator.
    /// </summary>
    public static float Combine(float normalisedNoise, float falloffAmount, bool applyFalloff, AnimationCurve curve, float multiplier)
    {
        float value = applyFalloff ? Mathf.Clamp01(normalisedNoise - falloffAmount) : normalisedNoise;
        return value * curve.Evaluate(value) * multiplier;
    }

    public float Height(Vector2 worldXZ)
    {
        float normalised = noise.Evaluate(worldXZ);
        float falloffAmount = applyFalloff ? falloff.Evaluate(worldXZ) : 0f;
        return Combine(normalised, falloffAmount, applyFalloff, heightCurve, heightMultiplier);
    }

    public float SlopeDegrees(Vector2 worldXZ, float step)
    {
        float dx = Height(worldXZ + new Vector2(step, 0f)) - Height(worldXZ - new Vector2(step, 0f));
        float dz = Height(worldXZ + new Vector2(0f, step)) - Height(worldXZ - new Vector2(0f, step));
        Vector3 normal = new Vector3(-dx, 2f * step, -dz).normalized;
        return Mathf.Acos(Mathf.Clamp01(normal.y)) * Mathf.Rad2Deg;
    }
}
