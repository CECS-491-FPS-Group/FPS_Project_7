using UnityEngine;

/// <summary>
/// Invisible walls around the generated grid. Past the outermost chunk there is no terrain at
/// all, so without these a character that walks off the edge falls indefinitely.
/// </summary>
[DisallowMultipleComponent]
public class WorldBoundary : MonoBehaviour
{
    public TerrainGenerator terrainGenerator;

    [Tooltip("How far above the highest possible terrain the walls extend.")]
    public float wallHeight = 200f;

    [Tooltip("How far below the lowest possible terrain the walls extend, so nothing slips underneath.")]
    public float wallDepth = 100f;

    public float wallThickness = 10f;

    [Tooltip("Pulls the walls inward from the exact grid edge. Positive values keep players off the falloff skirt.")]
    public float inset;

    [Tooltip("Layer for the wall colliders. Negative reuses the terrain layer. Note that putting walls on a layer the character controller treats as ground lets players register as grounded while hugging a wall.")]
    public int boundaryLayer = -1;

    [Tooltip("Rebuild automatically once the world finishes generating.")]
    public bool rebuildOnGenerationComplete = true;

    public bool drawGizmos = true;

    Transform container;

    public bool HasBounds
    {
        get { return terrainGenerator != null && terrainGenerator.worldSettings != null && terrainGenerator.MeshWorldSize > 0f; }
    }

    void Awake()
    {
        if (terrainGenerator == null)
        {
#if UNITY_2023_1_OR_NEWER
            terrainGenerator = Object.FindFirstObjectByType<TerrainGenerator>();
#else
            terrainGenerator = Object.FindObjectOfType<TerrainGenerator>();
#endif
        }
    }

    void OnEnable()
    {
        if (terrainGenerator != null && rebuildOnGenerationComplete)
        {
            terrainGenerator.OnWorldGenerationComplete += Build;
        }
    }

    void OnDisable()
    {
        if (terrainGenerator != null)
        {
            terrainGenerator.OnWorldGenerationComplete -= Build;
        }
    }

    void Start()
    {
        Build();
    }

    /// <summary>Rect the walls enclose, in world XZ.</summary>
    public bool TryGetBounds(out Rect bounds)
    {
        bounds = new Rect();

        if (!HasBounds)
        {
            return false;
        }

        Rect world = terrainGenerator.worldSettings.WorldRect(terrainGenerator.MeshWorldSize);
        bounds = Rect.MinMaxRect(world.xMin + inset, world.yMin + inset, world.xMax - inset, world.yMax - inset);

        return bounds.width > 0f && bounds.height > 0f;
    }

    public void Build()
    {
        Clear();

        Rect bounds;
        if (!TryGetBounds(out bounds))
        {
            Debug.LogWarning("[WorldBoundary] No TerrainGenerator with worldSettings, so no boundary was built.", this);
            return;
        }

        HeightMapSettings heightSettings = terrainGenerator.heightMapSettings;
        float bottom = (heightSettings != null ? heightSettings.minHeight : 0f) - wallDepth;
        float top = (heightSettings != null ? heightSettings.maxHeight : 100f) + wallHeight;
        float centreY = (bottom + top) * 0.5f;
        float sizeY = Mathf.Max(1f, top - bottom);

        int layer = boundaryLayer >= 0 ? boundaryLayer : terrainGenerator.terrainLayer;
        float half = wallThickness * 0.5f;

        GameObject host = new GameObject("World Boundary");
        host.transform.SetParent(transform, false);
        container = host.transform;

        // Walls overlap at the corners so there is no gap to squeeze through.
        CreateWall(layer, "Boundary +X",
            new Vector3(bounds.xMax + half, centreY, bounds.center.y),
            new Vector3(wallThickness, sizeY, bounds.height + wallThickness * 2f));

        CreateWall(layer, "Boundary -X",
            new Vector3(bounds.xMin - half, centreY, bounds.center.y),
            new Vector3(wallThickness, sizeY, bounds.height + wallThickness * 2f));

        CreateWall(layer, "Boundary +Z",
            new Vector3(bounds.center.x, centreY, bounds.yMax + half),
            new Vector3(bounds.width + wallThickness * 2f, sizeY, wallThickness));

        CreateWall(layer, "Boundary -Z",
            new Vector3(bounds.center.x, centreY, bounds.yMin - half),
            new Vector3(bounds.width + wallThickness * 2f, sizeY, wallThickness));
    }

    public void Clear()
    {
        if (container == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(container.gameObject);
        }
        else
        {
            DestroyImmediate(container.gameObject);
        }

        container = null;
    }

    void CreateWall(int layer, string wallName, Vector3 centre, Vector3 size)
    {
        GameObject wall = new GameObject(wallName);
        wall.layer = layer;
        wall.transform.SetParent(container, false);
        wall.transform.position = centre;

        BoxCollider collider = wall.AddComponent<BoxCollider>();
        collider.size = size;
    }

    void OnDrawGizmosSelected()
    {
        Rect bounds;
        if (!drawGizmos || !TryGetBounds(out bounds))
        {
            return;
        }

        HeightMapSettings heightSettings = terrainGenerator.heightMapSettings;
        float bottom = (heightSettings != null ? heightSettings.minHeight : 0f) - wallDepth;
        float top = (heightSettings != null ? heightSettings.maxHeight : 100f) + wallHeight;

        Gizmos.color = new Color(1f, 0.35f, 0.2f, 0.9f);
        DrawRect(bounds, bottom);
        DrawRect(bounds, top);

        Gizmos.DrawLine(new Vector3(bounds.xMin, bottom, bounds.yMin), new Vector3(bounds.xMin, top, bounds.yMin));
        Gizmos.DrawLine(new Vector3(bounds.xMax, bottom, bounds.yMin), new Vector3(bounds.xMax, top, bounds.yMin));
        Gizmos.DrawLine(new Vector3(bounds.xMax, bottom, bounds.yMax), new Vector3(bounds.xMax, top, bounds.yMax));
        Gizmos.DrawLine(new Vector3(bounds.xMin, bottom, bounds.yMax), new Vector3(bounds.xMin, top, bounds.yMax));
    }

    static void DrawRect(Rect bounds, float y)
    {
        Vector3 a = new Vector3(bounds.xMin, y, bounds.yMin);
        Vector3 b = new Vector3(bounds.xMax, y, bounds.yMin);
        Vector3 c = new Vector3(bounds.xMax, y, bounds.yMax);
        Vector3 d = new Vector3(bounds.xMin, y, bounds.yMax);

        Gizmos.DrawLine(a, b);
        Gizmos.DrawLine(b, c);
        Gizmos.DrawLine(c, d);
        Gizmos.DrawLine(d, a);
    }
}
