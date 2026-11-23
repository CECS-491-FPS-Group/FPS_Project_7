using System;
using System.IO;
using System.Linq;
using FishNet.Component.Transforming;
using FishNet.Managing;
using FishNet.Object;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

// Editor-only active-asset regression and repeatable real export; never ships in a player.
public static class AntiCheatCloseoutChecks
{
    private static int checks;
    private static void Check(bool value, string label)
    { if (!value) throw new InvalidOperationException("[AC WIRING] " + label); checks++; }

    [MenuItem("Tools/Anti-Cheat/Validate Active Wiring")]
    public static void Run()
    {
        try { Validate(); if (Application.isBatchMode) EditorApplication.Exit(0); }
        catch (Exception error) { Debug.LogException(error); if (Application.isBatchMode) EditorApplication.Exit(1); else throw; }
    }

    private static void Validate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before validation.");
        var before = EditorSceneManager.GetSceneManagerSetup(); checks = 0;
        if (Enumerable.Range(0, UnityEngine.SceneManagement.SceneManager.sceneCount)
            .Any(i => UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty))
            throw new InvalidOperationException("Save scene work before validation; no scene work will be discarded.");
        try
        {
            var enabled = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            Check(enabled.FirstOrDefault() == "Assets/Scenes/MainMenu.unity", "MainMenu is the actual player entry scene");
            Check(enabled.Contains("Assets/Scenes/LobbyScene_v1.unity") && enabled.Contains("Assets/Scenes/RoundImplementation.unity"), "lobby/match included in export");
            EditorSceneManager.OpenScene(enabled[0]);
            var manager = UnityEngine.Object.FindFirstObjectByType<NetworkManager>();
            Check(manager != null && manager.GetComponent<GameplayPlayerSpawner>() != null, "active manager owns the anti-cheat installation path");
            var spawner = new SerializedObject(manager.GetComponent<GameplayPlayerSpawner>());
            var player = spawner.FindProperty("playerPrefab").objectReferenceValue as NetworkObject;
            Check(player != null && AssetDatabase.GetAssetPath(player) == "Assets/Prefabs/Player.prefab", "active registered player variant, not a historical controller");
            var collection = new SerializedObject(new SerializedObject(manager).FindProperty("_spawnablePrefabs").objectReferenceValue);
            var prefabs = collection.FindProperty("_prefabs");
            Check(Enumerable.Range(0, prefabs.arraySize).Any(i => prefabs.GetArrayElementAtIndex(i).objectReferenceValue == player), "selected variant is in this manager's spawnable collection");
            Check(player.GetComponent<Health>() != null && player.GetComponent<SpeedHackDetector>()?.enabled == true, "live health and movement observer enabled on selected variant");
            var transform = player.GetComponent<NetworkTransform>();
            Check(transform != null, "selected variant has replicated movement");
            var network = new SerializedObject(transform);
            Check(network.FindProperty("_clientAuthoritative").boolValue, "movement authority remains client-owned");
            Check(!network.FindProperty("_synchronizeScale").boolValue && !network.FindProperty("_synchronizeParent").boolValue, "supported unscaled/unparented replication geometry");
            var setup = player.GetComponent<PlayerCameraSetup>(); var shots = player.GetComponentsInChildren<HitscanShooter>(true);
            Check(setup != null && setup.enabled && setup.playerCamera != null && shots.Length == 1 && shots[0].enabled &&
                shots[0].GetComponent<Camera>() == setup.playerCamera, "approved stance/eye and single authoritative shooter share the active camera");
            Check(player.GetComponent<CharacterController>() != null && player.GetComponent<StarterAssets.FirstPersonController>() != null, "live capsule/controller source for approved geometry");
            EditorSceneManager.OpenScene("Assets/Scenes/LobbyScene_v1.unity");
            var lobby = UnityEngine.Object.FindFirstObjectByType<NetworkLobbyUI>();
            Check(lobby != null && lobby.enabled && lobby.GetComponent<NetworkObject>() != null, "network lobby roster/request path active");
            int maximum = new SerializedObject(lobby).FindProperty("maximumPlayers").intValue;
            Check(maximum >= 1 && maximum <= 16, "supported bounded lobby size");
            EditorSceneManager.OpenScene("Assets/Scenes/RoundImplementation.unity");
            var round = UnityEngine.Object.FindFirstObjectByType<RoundTimer>();
            var terrain = UnityEngine.Object.FindFirstObjectByType<TerrainGenerator>();
            Check(round != null && round.enabled, "round installer/lifecycle active");
            Check(terrain != null && terrain.enabled && terrain.worldSettings?.structureSettings != null &&
                terrain.worldSettings.structureSettings.HasBuildingPrefabs, "actual terrain/solid-cover configuration present");
            Debug.Log($"[AC WIRING] PASS checks={checks}; active serialized scenes/variant/registration. Runtime installer assertions run in Validate Server Obstruction.");
        }
        finally
        {
            // A batch Editor may start in an untitled empty scene, which is not a restorable asset path.
            if (before.Length > 0 && before.All(s => !string.IsNullOrEmpty(s.path))) EditorSceneManager.RestoreSceneManagerSetup(before);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }

    // Command-line only: explicit fresh paths prevent overwriting any accepted build/baseline.
    public static void BuildRelease()
    {
        try
        {
            Validate();
            string exe = Environment.GetEnvironmentVariable("FPS_RELEASE_BUILD_EXE");
            string archive = Environment.GetEnvironmentVariable("FPS_RELEASE_APPROVED_MANIFEST");
            if (string.IsNullOrEmpty(exe) || string.IsNullOrEmpty(archive) || !Path.IsPathRooted(exe) || !Path.IsPathRooted(archive))
                throw new InvalidOperationException("Supply absolute FPS_RELEASE_BUILD_EXE and FPS_RELEASE_APPROVED_MANIFEST paths.");
            string root = Path.GetFullPath(Path.GetDirectoryName(exe)); archive = Path.GetFullPath(archive);
            if (Directory.Exists(root) || File.Exists(archive) || archive.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Build output must be fresh; trusted archive must be new and outside it.");
            if (PlayerSettings.GetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone) != ScriptingImplementation.Mono2x)
                throw new InvalidOperationException("Select Mono first; this check never rewrites project backend settings.");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                target = BuildTarget.StandaloneWindows64, locationPathName = exe, options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded || (report.summary.options & BuildOptions.Development) != 0)
                throw new InvalidOperationException("Non-Development Windows export did not succeed.");
            var manifest = BuildIntegrityManifestTool.CreateForCompletedWindowsBuild(exe);
            string json = JsonUtility.ToJson(manifest, true);
            Directory.CreateDirectory(Path.GetDirectoryName(archive));
            using (var writer = new StreamWriter(new FileStream(archive, FileMode.CreateNew))) writer.Write(json);
            File.WriteAllText(BuildIntegrity.ConfinedPath(BuildIntegrity.DataRootForBuild(exe, "Windows"), BuildIntegrity.ClientManifest), json);
            Debug.Log($"[AC RELEASE] export=Succeeded development=False build={manifest.buildId} bytes={report.summary.totalSize} " +
                $"durationSeconds={report.summary.totalTime.TotalSeconds:F2} exe={exe} archive={archive}; default server installation UNCHANGED.");
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
}
