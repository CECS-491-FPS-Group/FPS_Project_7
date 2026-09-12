using UnityEngine;

/// <summary>
/// Evaluates the terrain noise at an arbitrary world position rather than on a chunk grid.
/// The layout layer needs terrain height before any chunk exists, so this is the single
/// implementation and <see cref="Noise.GenerateNoiseMap"/> drives it too.
/// </summary>
public struct NoiseSampler
{
    readonly Vector2[] octaveOffsets;
    readonly int octaves;
    readonly float scale;
    readonly float persistance;
    readonly float lacunarity;
    readonly float maxPossibleHeight;
    readonly float meshScale;

    NoiseSampler(Vector2[] octaveOffsets, NoiseSettings settings, float maxPossibleHeight, float meshScale)
    {
        this.octaveOffsets = octaveOffsets;
        this.maxPossibleHeight = maxPossibleHeight;
        this.meshScale = meshScale;
        octaves = settings.octaves;
        scale = settings.scale;
        persistance = settings.persistance;
        lacunarity = settings.lacunarity;
    }

    /// <summary>
    /// Offsets are drawn in the same order as the grid generator so a given seed produces
    /// the same octave offsets either way.
    /// </summary>
    public static NoiseSampler Create(NoiseSettings settings, int seed, float meshScale)
    {
        DeterministicRandom prng = new DeterministicRandom((uint)seed);
        Vector2[] offsets = new Vector2[settings.octaves];

        float maxPossibleHeight = 0f;
        float amplitude = 1f;

        for (int i = 0; i < settings.octaves; i++)
        {
            offsets[i] = new Vector2(
                prng.NextInt(-100000, 100000) + settings.offset.x,
                prng.NextInt(-100000, 100000) - settings.offset.y);

            maxPossibleHeight += amplitude;
            amplitude *= settings.persistance;
        }

        return new NoiseSampler(offsets, settings, maxPossibleHeight, meshScale);
    }

    /// <summary>Un-normalised fBm sum, matching the grid generator's inner loop.</summary>
    public float EvaluateRaw(Vector2 worldXZ)
    {
        // World position expressed in the noise's own units. The half-unit shift and the
        // negated Z come from inverting MeshGenerator's vertex placement.
        float px = worldXZ.x / meshScale - 0.5f;
        float pz = -worldXZ.y / meshScale - 0.5f;

        float amplitude = 1f;
        float frequency = 1f;
        float noiseHeight = 0f;

        for (int i = 0; i < octaves; i++)
        {
            float sampleX = (px + octaveOffsets[i].x) / scale * frequency;
            float sampleY = (pz + octaveOffsets[i].y) / scale * frequency;

            noiseHeight += (Mathf.PerlinNoise(sampleX, sampleY) * 2f - 1f) * amplitude;

            amplitude *= persistance;
            frequency *= lacunarity;
        }

        return noiseHeight;
    }

    public float NormalizeGlobal(float raw)
    {
        return Mathf.Max(0f, (raw + 1f) / (maxPossibleHeight / 0.9f));
    }

    public float Evaluate(Vector2 worldXZ)
    {
        return NormalizeGlobal(EvaluateRaw(worldXZ));
    }
}
