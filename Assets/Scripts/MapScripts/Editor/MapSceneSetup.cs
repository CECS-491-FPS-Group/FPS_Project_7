using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Wires a playable character into a scene that has a TerrainGenerator: puts the terrain and the
/// character's ground mask on the same layer, drops in a player and camera if the scene has none,
/// and configures TerrainSpawnPlacer so the character lands on generated ground instead of
/// falling while chunks are still being built.
/// </summary>
public static class MapSceneSetup
{
    const string PlayerPrefabPath = "Assets/Starter Assets/Runtime/FirstPersonController/Prefabs/PlayerCapsule.prefab";
    const string PreferredLayerName = "Ground";
    const string CameraRootName = "PlayerCameraRoot";

    [MenuItem("Tools/Map/Set Up Player In Scene")]
    public static void Run()
    {
        TerrainGenerator generator = Object.FindFirstObjectByType<TerrainGenerator>(FindObjectsInactive.Include);

        if (generator == null)
        {
            EditorUtility.DisplayDialog("Map Setup", "No TerrainGenerator in the open scene.", "OK");
            return;
        }

        StringBuilder report = new StringBuilder();
        int terrainLayer = ResolveTerrainLayer(report);

        Undo.RecordObject(generator, "Set Up Player");
        if (generator.terrainLayer != terrainLayer)
        {
            report.AppendLine("TerrainGenerator.terrainLayer: " + generator.terrainLayer + " -> " + terrainLayer);
            generator.terrainLayer = terrainLayer;
        }

        GameObject player = FindExistingPlayer();
        if (player == null)
        {
            player = InstantiatePlayer(generator, report);
            if (player == null)
            {
                Debug.LogError("[MapSceneSetup] " + PlayerPrefabPath + " is missing, so no player could be created.");
                return;
            }
        }
        else
        {
            report.AppendLine("Reusing existing player: " + player.name);
        }

        EnsureCamera(player, report);

        string controllerName;
        MonoBehaviour controller = FindGroundedController(player, terrainLayer, out controllerName);
        if (controller != null)
        {
            report.AppendLine(controllerName + ".GroundLayers now includes '" + LayerMask.LayerToName(terrainLayer) + "'");
        }

        ConfigureSpawnPlacer(player, generator, terrainLayer, controller, report);
        ConfigureBoundary(generator, terrainLayer, report);

        Undo.RecordObject(generator, "Set Up Player");
        generator.viewer = player.transform;
        report.AppendLine("TerrainGenerator.viewer -> " + player.name);

        DisablePlaceholderViewer(generator, player, report);

        EditorUtility.SetDirty(generator);
        EditorSceneManager.MarkSceneDirty(generator.gameObject.scene);

        Debug.Log("[MapSceneSetup]\n" + report);
    }

    static int ResolveTerrainLayer(StringBuilder report)
    {
        int layer = LayerMask.NameToLayer(PreferredLayerName);

        if (layer < 0)
        {
            report.AppendLine("No '" + PreferredLayerName + "' layer exists; using Default. Add one in Tags and Layers to keep terrain separable from props.");
            return 0;
        }

        report.AppendLine("Terrain layer: " + PreferredLayerName + " (" + layer + ")");
        return layer;
    }

    static GameObject FindExistingPlayer()
    {
        TerrainSpawnPlacer placer = Object.FindFirstObjectByType<TerrainSpawnPlacer>(FindObjectsInactive.Include);
        if (placer != null)
        {
            return placer.gameObject;
        }

        CharacterController controller = Object.FindFirstObjectByType<CharacterController>(FindObjectsInactive.Include);
        return controller != null ? controller.gameObject : null;
    }

    static GameObject InstantiatePlayer(TerrainGenerator generator, StringBuilder report)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        if (prefab == null)
        {
            return null;
        }

        GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(prefab, generator.gameObject.scene);
        Undo.RegisterCreatedObjectUndo(player, "Set Up Player");

        Vector2 centre = generator.worldSettings != null
            ? generator.worldSettings.WorldCentre(generator.MeshWorldSize)
            : Vector2.zero;

        player.transform.position = new Vector3(centre.x, 100f, centre.y);
        report.AppendLine("Created player from " + PlayerPrefabPath);

        return player;
    }

    static void EnsureCamera(GameObject player, StringBuilder report)
    {
        if (Object.FindFirstObjectByType<Camera>(FindObjectsInactive.Include) != null)
        {
            return;
        }

        Transform cameraRoot = FindDescendant(player.transform, CameraRootName);
        if (cameraRoot == null)
        {
            cameraRoot = player.transform;
        }

        GameObject camera = new GameObject("Main Camera");
        Undo.RegisterCreatedObjectUndo(camera, "Set Up Player");
        camera.tag = "MainCamera";
        camera.AddComponent<Camera>();
        camera.AddComponent<AudioListener>();
        camera.transform.SetParent(cameraRoot, false);

        // The controller pitches PlayerCameraRoot and yaws the player, so a plain child camera
        // tracks the look direction without pulling in Cinemachine.
        report.AppendLine("Scene had no camera; added one under " + cameraRoot.name);
    }

    /// <summary>
    /// Adds the terrain layer to whichever component exposes a GroundLayers mask. Found by
    /// serialised property name so this does not need a reference to the Starter Assets assembly.
    /// </summary>
    static MonoBehaviour FindGroundedController(GameObject player, int terrainLayer, out string componentName)
    {
        componentName = null;
        MonoBehaviour[] behaviours = player.GetComponentsInChildren<MonoBehaviour>(true);

        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null)
            {
                continue;
            }

            SerializedObject serialized = new SerializedObject(behaviour);
            SerializedProperty property = serialized.FindProperty("GroundLayers");

            if (property == null || property.propertyType != SerializedPropertyType.LayerMask)
            {
                continue;
            }

            property.intValue |= 1 << terrainLayer;
            serialized.ApplyModifiedProperties();

            componentName = behaviour.GetType().Name;
            return behaviour;
        }

        return null;
    }

    static void ConfigureSpawnPlacer(GameObject player, TerrainGenerator generator, int terrainLayer,
        MonoBehaviour controller, StringBuilder report)
    {
        TerrainSpawnPlacer placer = player.GetComponent<TerrainSpawnPlacer>();

        if (placer == null)
        {
            placer = Undo.AddComponent<TerrainSpawnPlacer>(player);
            report.AppendLine("Added TerrainSpawnPlacer to " + player.name);
        }

        Undo.RecordObject(placer, "Set Up Player");

        placer.terrainGenerator = generator;
        placer.registerAsViewer = true;
        placer.terrainMask = 1 << terrainLayer;
        placer.probeHeight = Mathf.Max(100f, generator.heightMapSettings != null ? generator.heightMapSettings.maxHeight * 3f : 100f);

        if (controller != null)
        {
            placer.scriptsToDisableWhileWaiting = new[] { controller };
        }

        report.AppendLine("TerrainSpawnPlacer: terrainMask='" + LayerMask.LayerToName(terrainLayer) +
            "', probeHeight=" + placer.probeHeight.ToString("F0") +
            (controller != null ? ", freezes " + controller.GetType().Name : ""));

        EditorUtility.SetDirty(placer);
    }

    static void ConfigureBoundary(TerrainGenerator generator, int terrainLayer, StringBuilder report)
    {
        WorldBoundary boundary = Object.FindFirstObjectByType<WorldBoundary>(FindObjectsInactive.Include);

        if (boundary == null)
        {
            boundary = Undo.AddComponent<WorldBoundary>(generator.gameObject);
            report.AppendLine("Added WorldBoundary to " + generator.name);
        }

        Undo.RecordObject(boundary, "Set Up Player");
        boundary.terrainGenerator = generator;
        boundary.boundaryLayer = terrainLayer;
        EditorUtility.SetDirty(boundary);

        Rect bounds;
        if (boundary.TryGetBounds(out bounds))
        {
            report.AppendLine("WorldBoundary encloses " + bounds.width.ToString("F0") + " x " + bounds.height.ToString("F0") + " m");
        }
    }

    /// <summary>
    /// The placeholder Viewer cube sits at the origin with a BoxCollider, which the terrain now
    /// generates around. Left active it blocks the player and can catch the spawn probe.
    /// </summary>
    static void DisablePlaceholderViewer(TerrainGenerator generator, GameObject player, StringBuilder report)
    {
        GameObject[] roots = generator.gameObject.scene.GetRootGameObjects();

        for (int i = 0; i < roots.Length; i++)
        {
            GameObject candidate = roots[i];

            if (candidate == player || candidate.name != "Viewer" || !candidate.activeSelf)
            {
                continue;
            }

            if (candidate.GetComponent<CharacterController>() != null)
            {
                continue;
            }

            Undo.RecordObject(candidate, "Set Up Player");
            candidate.SetActive(false);
            report.AppendLine("Disabled placeholder 'Viewer' - its collider sat inside the generated terrain");
        }
    }

    static Transform FindDescendant(Transform root, string name)
    {
        if (root.name == name)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDescendant(root.GetChild(i), name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
