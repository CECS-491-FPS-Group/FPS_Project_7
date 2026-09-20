using System.Collections.Generic;
using UnityEngine;

/// <summary>Instantiates buildings on the layout's pads and bridge prefabs across its spans.</summary>
[DisallowMultipleComponent]
public class StructureSpawner : MonoBehaviour
{
    public TerrainGenerator terrainGenerator;
    public bool rebuildOnLayout = true;

    Transform container;
    readonly List<int> roadBuffer = new List<int>(32);

    public int BuildingCount { get; private set; }
    public int BridgeCount { get; private set; }

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
        if (terrainGenerator == null)
        {
            return;
        }

        if (rebuildOnLayout)
        {
            terrainGenerator.OnLayoutBuilt += Build;
        }

        if (terrainGenerator.Layout != null)
        {
            Build();
        }
    }

    void OnDisable()
    {
        if (terrainGenerator != null)
        {
            terrainGenerator.OnLayoutBuilt -= Build;
        }
    }

    public void Build()
    {
        Clear();

        if (terrainGenerator == null || terrainGenerator.Layout == null || terrainGenerator.worldSettings == null)
        {
            return;
        }

        StructureSettings settings = terrainGenerator.worldSettings.structureSettings;
        if (settings == null)
        {
            return;
        }

        WorldLayout layout = terrainGenerator.Layout;
        bool renders = terrainGenerator.buildProfile == WorldBuildProfile.Full;
        int layer = terrainGenerator.terrainLayer;

        GameObject host = new GameObject("Structures");
        host.transform.SetParent(transform, false);
        container = host.transform;

        DeterministicRandom rng = DeterministicRandom.ForChunk(terrainGenerator.Seed, 0, 0, 0x5B);

        if (settings.HasBuildingPrefabs)
        {
            SpawnBuildings(layout, settings, ref rng, renders, layer);
        }

        if (settings.HasBridgePrefabs)
        {
            SpawnBridges(layout, settings, renders, layer);
        }
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
        BuildingCount = 0;
        BridgeCount = 0;
    }

    void SpawnBuildings(WorldLayout layout, StructureSettings settings, ref DeterministicRandom rng, bool renders, int layer)
    {
        BuildingPlot[] plots = layout.Plots;

        for (int i = 0; i < plots.Length; i++)
        {
            BuildingPlot plot = plots[i];
            GameObject prefab = settings.buildingPrefabs[rng.NextInt(0, settings.buildingPrefabs.Length)];
            if (prefab == null)
            {
                continue;
            }

            GameObject instance = Instantiate(prefab, container);
            Bounds bounds = LocalBounds(instance);

            float footprintX = plot.HalfExtents.x * 2f * settings.buildingFootprintFill;
            float footprintZ = plot.HalfExtents.y * 2f * settings.buildingFootprintFill;
            float scale = Mathf.Min(footprintX / Mathf.Max(bounds.size.x, 0.01f), footprintZ / Mathf.Max(bounds.size.z, 0.01f));
            scale = Mathf.Clamp(scale, settings.buildingScaleRange.x, settings.buildingScaleRange.y);

            float yaw = plot.Rotation * Mathf.Rad2Deg;
            if (settings.faceNearestRoad && layout.Roads != null && layout.Roads.SegmentCount > 0)
            {
                yaw = YawTowardRoad(layout, plot.Centre, yaw);
            }

            Vector3 position = new Vector3(plot.Centre.x, plot.Height - bounds.min.y * scale, plot.Centre.y);
            position -= Quaternion.Euler(0f, yaw, 0f) * new Vector3(bounds.center.x, 0f, bounds.center.z) * scale;

            instance.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            instance.transform.localScale = Vector3.one * scale;
            Finish(instance, renders, layer);
            BuildingCount++;
        }
    }

    void SpawnBridges(WorldLayout layout, StructureSettings settings, bool renders, int layer)
    {
        float middleLength = settings.bridgeMiddleLength;
        if (middleLength <= 0f)
        {
            GameObject probe = Instantiate(settings.bridgeMiddle, container);
            middleLength = Mathf.Max(0.5f, LocalBounds(probe).size.z);
            DestroyImmediate(probe);
        }

        for (int i = 0; i < layout.Bridges.Length; i++)
        {
            BridgeSpan span = layout.Bridges[i];
            Vector3 start = new Vector3(span.Start.x, span.DeckHeight + settings.bridgePivotOffset, span.Start.z);
            Vector3 end = new Vector3(span.End.x, span.DeckHeight + settings.bridgePivotOffset, span.End.z);
            Vector3 direction = end - start;
            float length = direction.magnitude;
            if (length < 0.5f)
            {
                continue;
            }

            direction /= length;
            Quaternion forward = Quaternion.LookRotation(direction, Vector3.up);
            Quaternion backward = Quaternion.LookRotation(-direction, Vector3.up);

            float cursor = 0f;
            if (settings.bridgeStart != null)
            {
                GameObject piece = Instantiate(settings.bridgeStart, start, forward, container);
                cursor += Mathf.Max(0.1f, LocalBounds(piece).size.z);
                Finish(piece, renders, layer);
            }

            float endLength = 0f;
            if (settings.bridgeEnd != null)
            {
                GameObject piece = Instantiate(settings.bridgeEnd, end, backward, container);
                endLength = Mathf.Max(0.1f, LocalBounds(piece).size.z);
                Finish(piece, renders, layer);
            }

            int tiles = Mathf.Max(1, Mathf.CeilToInt((length - cursor - endLength) / middleLength));
            float stretch = (length - cursor - endLength) / (tiles * middleLength);

            for (int t = 0; t < tiles; t++)
            {
                GameObject piece = Instantiate(settings.bridgeMiddle, start + direction * cursor, forward, container);
                piece.transform.localScale = new Vector3(1f, 1f, stretch);
                cursor += middleLength * stretch;
                Finish(piece, renders, layer);
            }

            BridgeCount++;
        }
    }

    float YawTowardRoad(WorldLayout layout, Vector2 centre, float fallback)
    {
        RoadSample road = layout.Roads.Sample(centre, layout.Roads.MaxInfluence * 4f, roadBuffer);
        if (!road.Hit)
        {
            return fallback;
        }

        Vector3 nearest = NearestRoadPoint(layout.Roads, centre);
        Vector2 toRoad = new Vector2(nearest.x, nearest.z) - centre;
        if (toRoad.sqrMagnitude < 1e-4f)
        {
            return fallback;
        }

        return Mathf.Atan2(toRoad.x, toRoad.y) * Mathf.Rad2Deg;
    }

    static Vector3 NearestRoadPoint(RoadNetwork roads, Vector2 centre)
    {
        Vector3[] points = roads.Points;
        Vector3 best = points[0];
        float bestSqr = float.MaxValue;

        for (int i = 0; i < points.Length; i++)
        {
            float sqr = (new Vector2(points[i].x, points[i].z) - centre).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = points[i];
            }
        }

        return best;
    }

    static Bounds LocalBounds(GameObject instance)
    {
        Transform t = instance.transform;
        Vector3 position = t.position;
        Quaternion rotation = t.rotation;
        Vector3 scale = t.localScale;
        t.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        t.localScale = Vector3.one;

        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds(Vector3.zero, Vector3.one);
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        t.SetPositionAndRotation(position, rotation);
        t.localScale = scale;
        return bounds;
    }

    static void Finish(GameObject instance, bool renders, int layer)
    {
        instance.isStatic = true;
        Transform[] transforms = instance.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            transforms[i].gameObject.layer = layer;
        }

        if (!renders)
        {
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].enabled = false;
            }
        }
    }
}
