using UnityEngine;

/// <summary>
/// Converts between world XZ and heightmap indices for a single chunk, and samples height
/// and slope without touching physics. Safe to use from a worker thread.
/// </summary>
public readonly struct HeightMapSampler
{
    // Indices 0 and NumVertsPerLine-1 are the out-of-mesh border ring that MeshGenerator uses
    // only for normals; the visible surface spans indices 1 .. NumVertsPerLine-2.
    readonly float[,] values;

    public readonly int NumVertsPerLine;
    public readonly float MeshWorldSize;
    public readonly Vector2 ChunkOrigin;

    public HeightMapSampler(float[,] values, int numVertsPerLine, float meshWorldSize, Vector2 chunkOrigin)
    {
        this.values = values;
        NumVertsPerLine = numVertsPerLine;
        MeshWorldSize = meshWorldSize;
        ChunkOrigin = chunkOrigin;
    }

    public HeightMapSampler(HeightMap heightMap, int numVertsPerLine, float meshWorldSize, Vector2 chunkCoord)
        : this(heightMap.values, numVertsPerLine, meshWorldSize, chunkCoord * meshWorldSize)
    {
    }

    public float SampleSpacing
    {
        get { return MeshWorldSize / (NumVertsPerLine - 3); }
    }

    public static Vector2 ChunkOriginFor(Vector2 chunkCoord, float meshWorldSize)
    {
        return chunkCoord * meshWorldSize;
    }

    public static Vector2 IndexToWorld(int x, int y, int numVertsPerLine, float meshWorldSize, Vector2 chunkOrigin)
    {
        float spacing = meshWorldSize / (numVertsPerLine - 3);
        float half = meshWorldSize * 0.5f;
        return new Vector2(
            chunkOrigin.x - half + (x - 1) * spacing,
            chunkOrigin.y + half - (y - 1) * spacing);
    }

    public Vector2 IndexToWorld(int x, int y)
    {
        return IndexToWorld(x, y, NumVertsPerLine, MeshWorldSize, ChunkOrigin);
    }

    public Vector2 WorldToIndex(Vector2 worldXZ)
    {
        float perUnit = (NumVertsPerLine - 3) / MeshWorldSize;
        float half = MeshWorldSize * 0.5f;
        return new Vector2(
            1f + (worldXZ.x - ChunkOrigin.x + half) * perUnit,
            1f + (ChunkOrigin.y + half - worldXZ.y) * perUnit);
    }

    public bool Contains(Vector2 worldXZ)
    {
        float half = MeshWorldSize * 0.5f;
        return Mathf.Abs(worldXZ.x - ChunkOrigin.x) <= half
            && Mathf.Abs(worldXZ.y - ChunkOrigin.y) <= half;
    }

    public float SampleHeight(Vector2 worldXZ)
    {
        Vector2 index = WorldToIndex(worldXZ);
        return SampleHeightAtIndex(index.x, index.y);
    }

    public float SampleHeightAtIndex(float fx, float fy)
    {
        int last = NumVertsPerLine - 1;
        fx = Mathf.Clamp(fx, 0f, last);
        fy = Mathf.Clamp(fy, 0f, last);

        int x0 = Mathf.Min((int)fx, last - 1);
        int y0 = Mathf.Min((int)fy, last - 1);
        float tx = fx - x0;
        float ty = fy - y0;

        float near = Mathf.Lerp(values[x0, y0], values[x0 + 1, y0], tx);
        float far = Mathf.Lerp(values[x0, y0 + 1], values[x0 + 1, y0 + 1], tx);
        return Mathf.Lerp(near, far, ty);
    }

    public Vector3 SampleSurface(Vector2 worldXZ)
    {
        return new Vector3(worldXZ.x, SampleHeight(worldXZ), worldXZ.y);
    }

    public Vector3 SampleNormal(Vector2 worldXZ)
    {
        float spacing = SampleSpacing;
        float dx = SampleHeight(worldXZ + new Vector2(spacing, 0f)) - SampleHeight(worldXZ - new Vector2(spacing, 0f));
        float dz = SampleHeight(worldXZ + new Vector2(0f, spacing)) - SampleHeight(worldXZ - new Vector2(0f, spacing));
        return new Vector3(-dx, 2f * spacing, -dz).normalized;
    }

    public float SampleSlopeDegrees(Vector2 worldXZ)
    {
        return Mathf.Acos(Mathf.Clamp01(SampleNormal(worldXZ).y)) * Mathf.Rad2Deg;
    }
}
