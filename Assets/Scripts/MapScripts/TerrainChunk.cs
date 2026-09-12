using UnityEngine;

public struct TerrainChunkConfig
{
    public HeightMapSettings heightMapSettings;
    public MeshSettings meshSettings;
    public WorldSettings worldSettings;
    public LODInfo[] detailLevels;
    public int colliderLODIndex;
    public Transform parent;
    public Material material;
    public int layer;
    public WorldBuildProfile buildProfile;
    public int seed;
    public WorldLayout layout;

    public bool RendersTerrain
    {
        get { return buildProfile == WorldBuildProfile.Full; }
    }
}

public class TerrainChunk
{
    public event System.Action<TerrainChunk> onReady;

    public readonly Vector2 coord;

    readonly TerrainChunkConfig config;
    readonly LODInfo[] detailLevels;
    readonly LODMesh[] lodMeshes;
    readonly int colliderLODIndex;
    readonly GameObject meshObject;
    readonly MeshFilter meshFilter;
    readonly MeshCollider meshCollider;
    readonly MeshRenderer meshRenderer;
    readonly Bounds bounds;
    readonly HeightMapContext context;

    HeightMap heightMap;
    Transform viewer;
    bool heightMapReceived;
    bool hasCollider;
    bool hasRenderMesh;
    bool ready;
    int currentLODIndex = -1;

    public bool HasCollider { get { return hasCollider; } }
    public bool IsReady { get { return ready; } }
    public Bounds Bounds { get { return bounds; } }
    public GameObject GameObject { get { return meshObject; } }
    public HeightMap Heights { get { return heightMap; } }

    public HeightMapSampler Sampler
    {
        get { return context.CreateSampler(heightMap.values); }
    }

    public TerrainChunk(Vector2 coord, TerrainChunkConfig config, Transform viewer)
    {
        this.coord = coord;
        this.config = config;
        this.viewer = viewer;

        detailLevels = config.detailLevels;
        // An index past the end of detailLevels leaves the MeshCollider without a mesh,
        // which drops anything standing on the chunk straight through it.
        colliderLODIndex = Mathf.Clamp(config.colliderLODIndex, 0, detailLevels.Length - 1);
        context = HeightMapContext.ForChunk(coord, config.meshSettings, config.worldSettings, config.seed, config.layout);

        Vector2 position = coord * config.meshSettings.meshWorldSize;
        bounds = new Bounds(position, Vector2.one * config.meshSettings.meshWorldSize);

        meshObject = new GameObject("Terrain Chunk " + coord.x + "," + coord.y);
        meshObject.layer = config.layer;
        meshObject.transform.position = new Vector3(position.x, 0f, position.y);
        meshObject.transform.parent = config.parent;

        meshCollider = meshObject.AddComponent<MeshCollider>();

        if (config.RendersTerrain)
        {
            meshFilter = meshObject.AddComponent<MeshFilter>();
            meshRenderer = meshObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = config.material;
        }

        lodMeshes = new LODMesh[detailLevels.Length];
        for (int i = 0; i < detailLevels.Length; i++)
        {
            lodMeshes[i] = new LODMesh(detailLevels[i].lod);
        }
    }

    public void Load()
    {
        MeshSettings meshSettings = config.meshSettings;
        HeightMapSettings heightMapSettings = config.heightMapSettings;
        int size = meshSettings.numVertsPerLine;
        HeightMapContext chunkContext = context;

        GenerationScheduler.Request(
            () => HeightMapGenerator.GenerateHeightMap(size, size, heightMapSettings, chunkContext),
            OnHeightMapReceived);
    }

    public void SetViewer(Transform newViewer)
    {
        viewer = newViewer;
    }

    void OnHeightMapReceived(object heightMapObject)
    {
        heightMap = (HeightMap)heightMapObject;
        heightMapReceived = true;

        RequestMesh(colliderLODIndex);

        if (config.RendersTerrain)
        {
            UpdateLevelOfDetail();
        }
    }

    void RequestMesh(int lodIndex)
    {
        LODMesh lodMesh = lodMeshes[lodIndex];
        if (lodMesh.hasMesh || lodMesh.hasRequestedMesh)
        {
            return;
        }

        lodMesh.RequestMesh(heightMap, config.meshSettings, () => OnMeshReady(lodIndex));
    }

    void OnMeshReady(int lodIndex)
    {
        if (lodIndex == colliderLODIndex && !hasCollider)
        {
            meshCollider.sharedMesh = lodMeshes[lodIndex].mesh;
            hasCollider = true;
        }

        if (config.RendersTerrain)
        {
            UpdateLevelOfDetail();
        }

        RaiseReadyIfComplete();
    }

    /// <summary>Picks the LOD mesh matching the viewer's distance. Bounded worlds never hide a chunk.</summary>
    public void UpdateLevelOfDetail()
    {
        if (!heightMapReceived || !config.RendersTerrain)
        {
            return;
        }

        int lodIndex = SelectLODIndex();
        LODMesh lodMesh = lodMeshes[lodIndex];

        if (lodMesh.hasMesh)
        {
            if (currentLODIndex != lodIndex)
            {
                currentLODIndex = lodIndex;
                meshFilter.sharedMesh = lodMesh.mesh;
                hasRenderMesh = true;
                RaiseReadyIfComplete();
            }
            return;
        }

        RequestMesh(lodIndex);

        if (!hasRenderMesh && lodMeshes[colliderLODIndex].hasMesh)
        {
            currentLODIndex = colliderLODIndex;
            meshFilter.sharedMesh = lodMeshes[colliderLODIndex].mesh;
            hasRenderMesh = true;
            RaiseReadyIfComplete();
        }
    }

    int SelectLODIndex()
    {
        if (viewer == null)
        {
            return 0;
        }

        Vector2 viewerPosition = new Vector2(viewer.position.x, viewer.position.z);
        float distance = Mathf.Sqrt(bounds.SqrDistance(viewerPosition));

        for (int i = 0; i < detailLevels.Length - 1; i++)
        {
            if (distance <= detailLevels[i].visibleDstThreshold)
            {
                return i;
            }
        }

        return detailLevels.Length - 1;
    }

    void RaiseReadyIfComplete()
    {
        if (ready || !hasCollider)
        {
            return;
        }

        if (config.RendersTerrain && !hasRenderMesh)
        {
            return;
        }

        ready = true;

        if (onReady != null)
        {
            onReady(this);
        }
    }

    public void Destroy()
    {
        if (meshObject != null)
        {
            Object.Destroy(meshObject);
        }
    }
}

class LODMesh
{
    public Mesh mesh;
    public bool hasRequestedMesh;
    public bool hasMesh;

    readonly int lod;

    public LODMesh(int lod)
    {
        this.lod = lod;
    }

    public void RequestMesh(HeightMap heightMap, MeshSettings meshSettings, System.Action onReceived)
    {
        hasRequestedMesh = true;
        float[,] values = heightMap.values;
        float[,] mask = heightMap.surfaceMask;
        int level = lod;

        GenerationScheduler.Request(
            () => MeshGenerator.GenerateTerrainMesh(values, mask, meshSettings, level),
            meshDataObject =>
            {
                mesh = ((MeshData)meshDataObject).CreateMesh();
                hasMesh = true;
                onReceived();
            });
    }
}
