using UnityEngine;

/// <summary>Flat water plane at the world's sea level, extended past the grid so the horizon reads as water rather than a hard edge.</summary>
[DisallowMultipleComponent]
public class WaterSurface : MonoBehaviour
{
    public TerrainGenerator terrainGenerator;
    public Material waterMaterial;

    [Tooltip("How far past the world edge the plane extends.")]
    public float margin = 400f;

    [Tooltip("Metres of water per texture repeat.")]
    public float textureLength = 20f;

    public bool rebuildOnLayout = true;

    Transform container;

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
        if (terrainGenerator != null && rebuildOnLayout)
        {
            terrainGenerator.OnLayoutBuilt += Build;
        }
    }

    void OnDisable()
    {
        if (terrainGenerator != null)
        {
            terrainGenerator.OnLayoutBuilt -= Build;
        }
    }

    void Start()
    {
        Build();
    }

    public void Build()
    {
        Clear();

        if (terrainGenerator == null || terrainGenerator.worldSettings == null || terrainGenerator.MeshWorldSize <= 0f)
        {
            return;
        }

        if (terrainGenerator.buildProfile != WorldBuildProfile.Full)
        {
            return;
        }

        Rect world = terrainGenerator.worldSettings.WorldRect(terrainGenerator.MeshWorldSize);
        Rect rect = Rect.MinMaxRect(world.xMin - margin, world.yMin - margin, world.xMax + margin, world.yMax + margin);
        float y = terrainGenerator.SeaLevel;

        GameObject host = new GameObject("Water");
        host.transform.SetParent(transform, false);
        container = host.transform;

        float repeat = Mathf.Max(1f, textureLength);

        Mesh mesh = new Mesh();
        mesh.vertices = new[]
        {
            new Vector3(rect.xMin, y, rect.yMin),
            new Vector3(rect.xMin, y, rect.yMax),
            new Vector3(rect.xMax, y, rect.yMax),
            new Vector3(rect.xMax, y, rect.yMin)
        };
        mesh.uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(0f, rect.height / repeat),
            new Vector2(rect.width / repeat, rect.height / repeat),
            new Vector2(rect.width / repeat, 0f)
        };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        host.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = host.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = waterMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
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
}
