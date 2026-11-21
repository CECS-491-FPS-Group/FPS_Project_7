using System;
using System.Reflection;
using FishNet.Connection;
using FishNet.Managing;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Isolated server-only Play Mode harness: real registered prefab, guards, HP/ammo,
// simulated owners (no remote transport/authentication coverage claimed).
[InitializeOnLoad]
public static class CombatServerChecks
{
    private const string Key = "FPS.CombatServerChecks.";
    private static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private static NetworkManager manager;
    private static int phase, checks;
    private static double deadline;
    static CombatServerChecks() => EditorApplication.playModeStateChanged += Changed;
    [MenuItem("Tools/Anti-Cheat/Validate Server Obstruction")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        SessionState.SetBool(Key + "running", true); SessionState.SetBool(Key + "finished", false);
        SessionState.SetBool(Key + "batch", Application.isBatchMode);
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity"); EditorApplication.EnterPlaymode();
    }
    private static void Changed(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key + "running", false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        { phase = checks = 0; deadline = EditorApplication.timeSinceStartup + 90; EditorApplication.update += Tick; }
        if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Key + "finished", false))
        {
            SessionState.SetBool(Key + "running", false);
            if (SessionState.GetBool(Key + "batch", false)) EditorApplication.Exit(SessionState.GetBool(Key + "passed", false) ? 0 : 1);
        }
    }
    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Server obstruction fixture timeout");
            if (phase == 0)
            {
                manager = UnityEngine.Object.FindFirstObjectByType<NetworkManager>(); if (manager == null) return;
                Check(manager.ServerManager.StartConnection(29991), "start isolated server"); phase++; return;
            }
            if (!manager.IsServerStarted) return;
            RunCases(); Finish(true);
        }
        catch (Exception error) { Debug.LogException(error); Finish(false); }
    }
    private static NetworkConnection Owner(int id)
    {
        var owner = new NetworkConnection(manager, id, 0, true);
        typeof(NetworkConnection).GetMethod("ConnectionAuthenticated", Hidden).Invoke(owner, null);
        typeof(NetworkConnection).GetMethod("SetLoadedStartScenes", Hidden).Invoke(owner, new object[] { true });
        return owner;
    }
    private static void RunCases()
    {
        GameObject shooterObject = null, targetObject = null, wallObject = null;
        try
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
            shooterObject = UnityEngine.Object.Instantiate(prefab); targetObject = UnityEngine.Object.Instantiate(prefab);
            shooterObject.transform.position = new Vector3(14000, 300, 14000);
            var sender = Owner(1001); var victimOwner = Owner(1002);
            manager.ServerManager.Spawn(shooterObject, sender); manager.ServerManager.Spawn(targetObject, victimOwner);
            var shooter = shooterObject.GetComponentInChildren<HitscanShooter>(true); var victim = targetObject.GetComponent<Health>();
            var setup = shooterObject.GetComponent<PlayerCameraSetup>();
            Vector3 eye = setup.ServerShotOrigin;
            targetObject.transform.position = eye + Vector3.forward * 12 - Vector3.up * targetObject.GetComponent<CharacterController>().center.y;
            wallObject = new GameObject("Server solid-cover fixture", typeof(BoxCollider));
            var wall = wallObject.GetComponent<BoxCollider>(); wall.size = new Vector3(10, 10, 1); wall.transform.position = eye + Vector3.forward * 6;
            MethodInfo shoot = typeof(HitscanShooter).GetMethod("ProcessServerShot", Hidden);
            void Fire(Vector3 direction)
            {
                typeof(HitscanShooter).GetField("_nextServerShotTime", Hidden).SetValue(shooter, 0d);
                Physics.SyncTransforms(); shoot.Invoke(shooter, new object[] { direction, sender });
            }
            Check(victim.CurrentHealth == 100 && shooter.CurrentAmmo == 30, "real server prefab initialized HP/ammo");
            Fire(Vector3.forward); Check(victim.CurrentHealth == 100 && shooter.CurrentAmmo == 29, "approved shot hits wall, no server HP loss");
            wall.transform.position += Vector3.right * 20;
            Fire(Vector3.forward); Check(victim.CurrentHealth == 60 && shooter.CurrentAmmo == 28, "opening allows authoritative 40 damage");
            wall.transform.position = eye + Vector3.forward * 6 + Vector3.left * 5.2f;
            Fire(Vector3.forward); Check(victim.CurrentHealth == 20, "partially exposed target can take unobstructed server hit");
            wall.transform.position = eye; int ammo = shooter.CurrentAmmo;
            Fire(Vector3.forward); Check(victim.CurrentHealth == 20 && shooter.CurrentAmmo == ammo, "origin inside cover rejects before ammo or damage");
            // Restore full wall. Current approved crouch/stand origin is resolved synchronously.
            wall.transform.position = eye + Vector3.forward * 6;
            var crouch = typeof(PlayerCameraSetup).GetField("_crouching", Hidden).GetValue(setup);
            var value = crouch.GetType().GetProperty("Value");
            value.SetValue(crouch, true); Fire(Vector3.forward);
            Check(victim.CurrentHealth == 20 && setup.ServerShotOrigin.y < eye.y, "immediate approved crouch uses lowered origin, wall still blocks");
            value.SetValue(crouch, false); Fire(Vector3.forward);
            Check(victim.CurrentHealth == 20 && Mathf.Abs(setup.ServerShotOrigin.y - eye.y) < .001f, "immediate stand restores approved origin, wall still blocks");

            Debug.Log($"[Combat SERVER CHECK] PASS checks={checks}; real server HP/ammo/guards, simulated owner identities; transport/interactive acceptance pending.");
        }
        finally
        {
            if (shooterObject != null) manager.ServerManager.Despawn(shooterObject);
            if (targetObject != null) manager.ServerManager.Despawn(targetObject);
            if (wallObject != null) UnityEngine.Object.Destroy(wallObject);
        }
    }
    private static void Check(bool condition, string label)
    { if (!condition) throw new Exception("[Combat SERVER CHECK] " + label); checks++; }
    private static void Finish(bool passed)
    {
        EditorApplication.update -= Tick;
        if (manager != null && manager.IsServerStarted) manager.ServerManager.StopConnection(true);
        SessionState.SetBool(Key + "passed", passed); SessionState.SetBool(Key + "finished", true); EditorApplication.ExitPlaymode();
    }
}
