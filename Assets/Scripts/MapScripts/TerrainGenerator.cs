using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds a bounded grid of terrain chunks from a single seed. Every client that receives the
/// same seed produces the same world, so terrain is never replicated.
/// </summary>
public class TerrainGenerator : MonoBehaviour
{
    public WorldSettings worldSettings;
    public MeshSettings meshSettings;
    public HeightMapSettings heightMapSettings;
    public TextureData textureSettings;

    [Tooltip("Which entry of detailLevels is used to build MeshColliders. 0 = full detail collider.")]
    public int colliderLODIndex;
    public LODInfo[] detailLevels;

    [Tooltip("Layer assigned to every generated chunk. Must be included in the character controller GroundLayers mask.")]
    public int terrainLayer;

    [Tooltip("Transform used to pick LOD levels. May be assigned at runtime via SetViewer().")]
    public Transform viewer;
    public Material mapMaterial;

    [Tooltip("ServerOnly skips renderers, materials and non-collider LOD meshes.")]
    public WorldBuildProfile buildProfile = WorldBuildProfile.Full;

    public bool generateOnStart = true;

    const float viewerMoveThresholdForLODUpdate = 25f;
    const float sqrViewerMoveThresholdForLODUpdate = viewerMoveThresholdForLODUpdate * viewerMoveThresholdForLODUpdate;

    readonly Dictionary<Vector2, TerrainChunk> chunks = new Dictionary<Vector2, TerrainChunk>();
    readonly List<TerrainChunk> chunkList = new List<TerrainChunk>();

    Vector2 viewerPositionOld;
    int readyChunkCount;
    bool generating;

    public event Action<float> OnGenerationProgress;
    public event Action OnWorldGenerationComplete;

    public bool IsGenerated { get; private set; }
    public int Seed { get; private set; }
    public WorldLayout Layout { get; private set; }

    public float MeshWorldSize
    {
        get { return meshSettings != null ? meshSettings.meshWorldSize : 0f; }
    }

    public float WorldSize
    {
        get { return worldSettings != null ? worldSettings.WorldSize(MeshWorldSize) : 0f; }
    }

    public float GenerationProgress
    {
        get
        {
            int total = ExpectedChunkCount;
            return total == 0 ? 0f : readyChunkCount / (float)total;
        }
    }

    int ExpectedChunkCount
    {
        get { return worldSettings != null ? worldSettings.ChunkCount : 0; }
    }

    bool RendersTerrain
    {
        get { return buildProfile == WorldBuildProfile.Full; }
    }

    void OnValidate()
    {
        if (detailLevels != null && detailLevels.Length > 0)
        {
            colliderLODIndex = Mathf.Clamp(colliderLODIndex, 0, detailLevels.Length - 1);
        }
    }

    void Start()
    {
        if (generateOnStart)
        {
            Generate();
        }
    }

    public void Generate()
    {
        Generate(worldSettings != null ? worldSettings.editorSeed : 0);
    }

    public void Generate(int seed)
    {
        if (!ValidateSettings())
        {
            return;
        }

        Clear();

        Seed = seed;
        Layout = BuildLayout(seed);
        generating = true;
        IsGenerated = false;
        readyChunkCount = 0;

        if (RendersTerrain)
        {
            textureSettings.ApplyToMaterial(mapMaterial);
            textureSettings.UpdateMeshHeights(mapMaterial, heightMapSettings.minHeight, heightMapSettings.maxHeight);
        }

        TerrainChunkConfig config = new TerrainChunkConfig
        {
            heightMapSettings = heightMapSettings,
            meshSettings = meshSettings,
            worldSettings = worldSettings,
            detailLevels = detailLevels,
            colliderLODIndex = colliderLODIndex,
            parent = transform,
            material = mapMaterial,
            layer = terrainLayer,
            buildProfile = buildProfile,
            seed = seed,
            layout = Layout
        };

        int min = worldSettings.ChunkCoordMin;
        int max = worldSettings.ChunkCoordMax;

        for (int y = min; y <= max; y++)
        {
            for (int x = min; x <= max; x++)
            {
                Vector2 coord = new Vector2(x, y);
                TerrainChunk chunk = new TerrainChunk(coord, config, viewer);
                chunk.onReady += OnChunkReady;
                chunks.Add(coord, chunk);
                chunkList.Add(chunk);
            }
        }

        for (int i = 0; i < chunkList.Count; i++)
        {
            chunkList[i].Load();
        }
    }

    WorldLayout BuildLayout(int seed)
    {
        if (worldSettings.layoutSettings == null)
        {
            return null;
        }

        float meshWorldSize = meshSettings.meshWorldSize;
        WorldFalloff falloff = WorldFalloff.From(worldSettings, meshWorldSize);
        TerrainHeightField field = new TerrainHeightField(heightMapSettings, falloff, seed, meshSettings.meshScale);

        return WorldLayout.Build(seed, worldSettings.WorldRect(meshWorldSize), field, worldSettings.layoutSettings);
    }

    public void Clear()
    {
        for (int i = 0; i < chunkList.Count; i++)
        {
            chunkList[i].Destroy();
        }

        chunks.Clear();
        chunkList.Clear();
        readyChunkCount = 0;
        generating = false;
        IsGenerated = false;
        Layout = null;
    }

    bool ValidateSettings()
    {
        if (worldSettings == null || meshSettings == null || heightMapSettings == null)
        {
            Debug.LogError("[TerrainGenerator] worldSettings, meshSettings and heightMapSettings are all required.", this);
            return false;
        }

        if (detailLevels == null || detailLevels.Length == 0)
        {
            Debug.LogError("[TerrainGenerator] detailLevels is empty - no terrain can be generated.", this);
            return false;
        }

        if (RendersTerrain && (mapMaterial == null || textureSettings == null))
        {
            Debug.LogError("[TerrainGenerator] mapMaterial and textureSettings are required unless buildProfile is ServerOnly.", this);
            return false;
        }

        if (worldSettings.layoutSettings != null && heightMapSettings.noiseSettings.normalizeMode != Noise.NormalizeMode.Global)
        {
            Debug.LogError("[TerrainGenerator] Roads require normalizeMode Global. Local normalises each chunk against its own range, so the layout's graded road heights will not match the terrain the chunks actually build.", this);
            return false;
        }

        colliderLODIndex = Mathf.Clamp(colliderLODIndex, 0, detailLevels.Length - 1);
        return true;
    }

    void OnChunkReady(TerrainChunk chunk)
    {
        readyChunkCount++;

        if (OnGenerationProgress != null)
        {
            OnGenerationProgress(GenerationProgress);
        }

        if (!generating || readyChunkCount < ExpectedChunkCount)
        {
            return;
        }

        generating = false;
        IsGenerated = true;

        if (OnWorldGenerationComplete != null)
        {
            OnWorldGenerationComplete();
        }
    }

    public void SetViewer(Transform newViewer)
    {
        viewer = newViewer;

        for (int i = 0; i < chunkList.Count; i++)
        {
            chunkList[i].SetViewer(newViewer);
        }

        if (viewer != null)
        {
            viewerPositionOld = new Vector2(viewer.position.x, viewer.position.z);
            UpdateLevelsOfDetail();
        }
    }

    public bool IsColliderReadyAt(Vector3 worldPosition)
    {
        TerrainChunk chunk;
        return TryGetChunkAt(worldPosition, out chunk) && chunk.HasCollider;
    }

    public bool TryGetChunkAt(Vector3 worldPosition, out TerrainChunk chunk)
    {
        chunk = null;
        float size = MeshWorldSize;
        if (size <= 0f)
        {
            return false;
        }

        Vector2 coord = new Vector2(
            Mathf.RoundToInt(worldPosition.x / size),
            Mathf.RoundToInt(worldPosition.z / size));

        return chunks.TryGetValue(coord, out chunk);
    }

    /// <summary>Terrain height at a world position, sampled from the heightmap rather than physics.</summary>
    public bool TrySampleHeight(Vector3 worldPosition, out float height)
    {
        height = 0f;

        TerrainChunk chunk;
        if (!TryGetChunkAt(worldPosition, out chunk) || chunk.Heights.values == null)
        {
            return false;
        }

        height = chunk.Sampler.SampleHeight(new Vector2(worldPosition.x, worldPosition.z));
        return true;
    }

    void Update()
    {
        if (!RendersTerrain || viewer == null || chunkList.Count == 0)
        {
            return;
        }

        Vector2 viewerPosition = new Vector2(viewer.position.x, viewer.position.z);
        if ((viewerPositionOld - viewerPosition).sqrMagnitude <= sqrViewerMoveThresholdForLODUpdate)
        {
            return;
        }

        viewerPositionOld = viewerPosition;
        UpdateLevelsOfDetail();
    }

    void UpdateLevelsOfDetail()
    {
        for (int i = 0; i < chunkList.Count; i++)
        {
            chunkList[i].UpdateLevelOfDetail();
        }
    }
}

[Serializable]
public struct LODInfo
{
    [Range(0, MeshSettings.numSupportedLODs - 1)]
    public int lod;
    public float visibleDstThreshold;

    public float sqrVisibleDstThreshold
    {
        get { return visibleDstThreshold * visibleDstThreshold; }
    }
}
