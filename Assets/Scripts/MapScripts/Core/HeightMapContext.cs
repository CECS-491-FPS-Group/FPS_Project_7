using UnityEngine;

/// <summary>
/// Everything a chunk's heightmap needs to know about where it sits in the world.
/// Built on the main thread from the settings assets, then passed by value to a worker.
/// </summary>
public readonly struct HeightMapContext
{
    public readonly int Seed;
    public readonly Vector2 ChunkOrigin;
    public readonly float MeshWorldSize;
    public readonly float MeshScale;
    public readonly int NumVertsPerLine;
    public readonly WorldFalloff Falloff;
    public readonly WorldLayout Layout;

    HeightMapContext(int seed, Vector2 chunkOrigin, float meshWorldSize, float meshScale, int numVertsPerLine,
        WorldFalloff falloff, WorldLayout layout)
    {
        Seed = seed;
        ChunkOrigin = chunkOrigin;
        MeshWorldSize = meshWorldSize;
        MeshScale = meshScale;
        NumVertsPerLine = numVertsPerLine;
        Falloff = falloff;
        Layout = layout;
    }

    public static HeightMapContext ForChunk(Vector2 chunkCoord, MeshSettings meshSettings, WorldSettings worldSettings, int seed, WorldLayout layout)
    {
        float meshWorldSize = meshSettings.meshWorldSize;

        return new HeightMapContext(
            seed,
            HeightMapSampler.ChunkOriginFor(chunkCoord, meshWorldSize),
            meshWorldSize,
            meshSettings.meshScale,
            meshSettings.numVertsPerLine,
            WorldFalloff.From(worldSettings, meshWorldSize),
            layout);
    }

    /// <summary>Single unbounded chunk at the origin, for the editor preview.</summary>
    public static HeightMapContext Preview(MeshSettings meshSettings, int seed)
    {
        return new HeightMapContext(
            seed,
            Vector2.zero,
            meshSettings.meshWorldSize,
            meshSettings.meshScale,
            meshSettings.numVertsPerLine,
            WorldFalloff.Disabled,
            null);
    }

    public NoiseSampler CreateNoiseSampler(NoiseSettings settings)
    {
        return NoiseSampler.Create(settings, Seed, MeshScale);
    }

    public HeightMapSampler CreateSampler(float[,] values)
    {
        return new HeightMapSampler(values, NumVertsPerLine, MeshWorldSize, ChunkOrigin);
    }

    public Vector2 IndexToWorld(int x, int y)
    {
        return HeightMapSampler.IndexToWorld(x, y, NumVertsPerLine, MeshWorldSize, ChunkOrigin);
    }
}
