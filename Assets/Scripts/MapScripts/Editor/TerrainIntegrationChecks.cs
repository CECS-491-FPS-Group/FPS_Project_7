using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Real Unity mesh/PhysX checks, separate from multiplayer runtime acceptance.
public static class TerrainIntegrationChecks
{
    const string ScenePath = "Assets/Scenes/RoundImplementation.unity";
    static int checks;
    static readonly BindingFlags HiddenStatic = BindingFlags.Static | BindingFlags.NonPublic;

    [MenuItem("Tools/Map/Validate Integrated Terrain")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        // Batch opens the saved scene; interactive checks never discard unsaved scene work.
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        checks = 0;
        bool passed = false;
        TerrainGenerator test = null;
        try
        {
            EditorSceneManager.OpenScene(ScenePath);
            var configured = UnityEngine.Object.FindFirstObjectByType<TerrainGenerator>();
            Require(configured != null, "active scene terrain exists");
            Require(configured.GetComponent<RoadMeshBuilder>() != null && configured.GetComponent<StructureSpawner>() != null &&
                configured.GetComponent<WaterSurface>() != null && configured.GetComponent<WorldBoundary>() != null, "active scene companions wired");
            var world = configured.worldSettings;
            Require(world != null && world.structureSettings != null && world.structureSettings.HasBuildingPrefabs, "world/structure/prefab references");
            Require(world.structureSettings.buildingPrefabs.All(p => p != null && p.GetComponentInChildren<Collider>() != null), "building collision assets");
            Require(!world.structureSettings.HasBridgePrefabs, "active bridges use grade-following procedural decks");
            Require(configured.GetComponent<RoadMeshBuilder>().roadMaterial != null && configured.GetComponent<WaterSurface>().waterMaterial != null, "road/water material links");

            // Work on a transient clone; never save test objects or modify shared settings.
            test = UnityEngine.Object.Instantiate(configured);
            test.name = "Terrain Integration Check";
            test.generateOnStart = false;
            var roads = test.GetComponent<RoadMeshBuilder>();
            var structures = test.GetComponent<StructureSpawner>();
            var water = test.GetComponent<WaterSurface>();
            var boundary = test.GetComponent<WorldBoundary>();
            roads.terrainGenerator = structures.terrainGenerator = water.terrainGenerator = boundary.terrainGenerator = test;
            // Edit-mode clones do not run MonoBehaviour's Play Mode lifecycle automatically.
            Invoke(roads, "OnEnable"); Invoke(structures, "OnEnable"); Invoke(water, "OnEnable"); Invoke(boundary, "OnEnable");
            test.buildProfile = WorldBuildProfile.Full;
            Build(test, world.editorSeed);
            string signature = test.LayoutSignature;
            int buildings = structures.BuildingCount, bridgeVertices = roads.BridgeVertexCount;
            Require(buildings > 0 && roads.LandVertexCount > 0, "buildings and road geometry generated");
            Require(bridgeVertices > 0, "configured seed produces traversable bridge decks");
            Require(test.transform.Find("Water") != null &&
                test.transform.Find("Water").GetComponentsInChildren<Collider>().Length == 0, "water adds no collision");
            Vector3[] samples = test.Layout.PointsOfInterest.Select(p => new Vector3(p.x, 0, p.y)).ToArray();
            float[] heights = new float[samples.Length];
            for (int i = 0; i < samples.Length; i++)
            {
                Require(test.TryGetDrySurface(samples[i], out RaycastHit hit), "dry POI collision " + i);
                heights[i] = hit.point.y;
            }
            foreach (BridgeSpan span in test.Layout.Bridges)
            {
                var points = test.Layout.Roads.Points;
                for (int i = span.FirstPoint; i < span.LastPoint; i++)
                {
                    Vector3 midpoint = (points[i] + points[i + 1]) * .5f;
                    Require(test.TryGetDrySurface(midpoint, out RaycastHit hit) && Math.Abs(hit.point.y - midpoint.y - world.layoutSettings.roadSurfaceLift) < .15f,
                        "bridge collision follows graded centreline");
                }
            }
            CheckSpawnPlacement(test);
            test.buildProfile = WorldBuildProfile.ServerOnly;
            Build(test, world.editorSeed);
            Require(signature == test.LayoutSignature && structures.BuildingCount == buildings && roads.BridgeVertexCount == bridgeVertices, "Full/ServerOnly layout and structure consistency");
            for (int i = 0; i < samples.Length; i++)
                Require(test.TryGetDrySurface(samples[i], out RaycastHit hit) && Mathf.Abs(hit.point.y - heights[i]) < .001f, "Full/ServerOnly surface parity " + i);
            Build(test, world.editorSeed + 1);
            Require(signature != test.LayoutSignature, "different seed changes layout");
            test.Clear();
            Require(test.GetComponentsInChildren<Collider>().Length == 0 && test.transform.childCount == 0, "clear removes terrain/structures/roads/water/bounds");
            // Exercise late worker callbacks after teardown instead of only clearing a completed build.
            test.Generate(world.editorSeed);
            test.Clear();
            Drain();
            Require(!test.IsGenerated && test.transform.childCount == 0, "in-flight generation cannot resurrect a cleared world");
            Debug.Log($"[Terrain CHECK] PASS checks={checks} seed={world.editorSeed} signature={signature} buildings={buildings} bridgeVertices={bridgeVertices}.");
            passed = true;
        }
        catch (Exception error) { Debug.LogException(error); }
        finally
        {
            if (test != null) UnityEngine.Object.DestroyImmediate(test.gameObject);
            typeof(GenerationScheduler).GetMethod("Shutdown", HiddenStatic | BindingFlags.Public)?.Invoke(null, null);
            if (Application.isBatchMode) EditorApplication.Exit(passed ? 0 : 1);
        }
    }

    static void Build(TerrainGenerator generator, int seed)
    {
        generator.Generate(seed);
        Drain();
        Physics.SyncTransforms();
        Require(generator.IsGenerated, "all terrain colliders ready");
    }

    static void CheckSpawnPlacement(TerrainGenerator terrain)
    {
        // Call the real server placement method in Edit Mode, with no connections or
        // Awake/transport lifecycle. A collider deliberately blocks the first result.
        GameObject host = new GameObject("Inactive spawn placement fixture");
        GameObject obstacle = new GameObject("Spawn obstruction fixture");
        host.SetActive(false);
        try
        {
            var spawner = host.AddComponent<GameplayPlayerSpawner>();
            const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
            var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
            Require(player != null, "current network player prefab exists");
            int boundaryLayer = terrain.GetComponent<WorldBoundary>().boundaryLayer;
            var movement = player.GetComponent<StarterAssets.FirstPersonController>();
            Require(boundaryLayer >= 0 && movement != null && (movement.GroundLayers.value & (1 << boundaryLayer)) == 0,
                "boundary walls cannot satisfy the player's ground probe");
            Require(!Physics.GetIgnoreLayerCollision(boundaryLayer, player.layer), "boundary still collides with player layer");
            typeof(GameplayPlayerSpawner).GetField("playerPrefab", fields).SetValue(spawner, player.GetComponent<FishNet.Object.NetworkObject>());
            typeof(GameplayPlayerSpawner).GetField("_terrain", fields).SetValue(spawner, terrain);
            MethodInfo place = typeof(GameplayPlayerSpawner).GetMethod("TryGetSpawnPosition", fields);
            object[] args = { Vector3.zero, null };
            Require((bool)place.Invoke(spawner, args), "configured map has a safe initial/respawn candidate");
            Vector3 first = (Vector3)args[0];
            BoxCollider block = obstacle.AddComponent<BoxCollider>();
            block.size = new Vector3(3, 4, 3);
            obstacle.transform.position = first + Vector3.up;
            Physics.SyncTransforms();
            Require((bool)place.Invoke(spawner, args) && Vector3.Distance((Vector3)args[0], first) > 3f, "placement avoids occupied capsule and finds another candidate");
            obstacle.transform.position = new Vector3(first.x, 0, first.z);
            block.size = new Vector3(100, 500, 100);
            Physics.SyncTransforms();
            Require(!(bool)place.Invoke(spawner, args), "fully obstructed spawn area waits instead of spawning inside collision");
            obstacle.SetActive(false);
            Physics.SyncTransforms();
            Require((bool)place.Invoke(spawner, args), "placement recovers after obstruction removed");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(obstacle);
            UnityEngine.Object.DestroyImmediate(host);
        }
    }
    static void Drain()
    {
        var pump = typeof(GenerationScheduler).GetMethod("Pump", HiddenStatic);
        DateTime deadline = DateTime.UtcNow.AddSeconds(120);
        while (GenerationScheduler.Outstanding > 0)
        {
            pump.Invoke(null, null);
            if (DateTime.UtcNow > deadline) throw new TimeoutException("Terrain callbacks did not finish.");
            Thread.Sleep(1);
        }
    }
    static void Invoke(object instance, string method) => instance.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(instance, null);
    static void Require(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("[Terrain CHECK] FAIL: " + name);
        checks++;
    }
}
