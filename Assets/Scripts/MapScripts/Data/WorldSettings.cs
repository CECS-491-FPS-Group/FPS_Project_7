using UnityEngine;

public enum WorldBuildProfile
{
    Full,
    ServerOnly
}

[CreateAssetMenu(menuName = "Map/World Settings")]
public class WorldSettings : UpdatableData
{
    [Min(1)]
    public int gridSize = 5;

    [Tooltip("Seed used when nothing supplies one at runtime. The server overrides this per match.")]
    public int editorSeed = 1;

    [Tooltip("Roads, points of interest and building pads. Leave empty to generate bare terrain.")]
    public LayoutSettings layoutSettings;

    public bool useFalloff = true;

    [Tooltip("Normalised distance from world centre at which the edge falloff begins.")]
    [Range(0f, 1f)]
    public float falloffStart = 0.55f;

    [Tooltip("Normalised distance from world centre at which terrain has fallen away completely.")]
    [Range(0f, 1f)]
    public float falloffEnd = 1f;

    public int ChunkCoordMin
    {
        get { return -(gridSize / 2); }
    }

    public int ChunkCoordMax
    {
        get { return ChunkCoordMin + gridSize - 1; }
    }

    public int ChunkCount
    {
        get { return gridSize * gridSize; }
    }

    public float WorldSize(float meshWorldSize)
    {
        return gridSize * meshWorldSize;
    }

    public float WorldExtent(float meshWorldSize)
    {
        return gridSize * meshWorldSize * 0.5f;
    }

    public Rect WorldRect(float meshWorldSize)
    {
        Vector2 centre = WorldCentre(meshWorldSize);
        float extent = WorldExtent(meshWorldSize);
        return Rect.MinMaxRect(centre.x - extent, centre.y - extent, centre.x + extent, centre.y + extent);
    }

    /// <summary>Centre of the grid in world XZ. Odd grid sizes centre on the origin.</summary>
    public Vector2 WorldCentre(float meshWorldSize)
    {
        float middle = (ChunkCoordMin + ChunkCoordMax) * 0.5f;
        return new Vector2(middle, middle) * meshWorldSize;
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        falloffEnd = Mathf.Max(falloffEnd, falloffStart + 0.01f);
        base.OnValidate();
    }
#endif
}
