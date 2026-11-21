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
    private static GameObject fixtureActor;
    private static OccludedAimObserver fixtureObserver;
    private static Vector3 fixturePosition;
    private static int fixtureReports, initialScenes;
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
        { phase = checks = fixtureReports = 0; deadline = EditorApplication.timeSinceStartup + 90; EditorApplication.update += Tick; }
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
            if (phase == 1)
            {
                RunCases();
                fixtureActor = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab"));
                fixturePosition = new Vector3(15000, 300, 15000); fixtureActor.transform.position = fixturePosition;
                manager.ServerManager.Spawn(fixtureActor, Owner(1003));
                fixtureObserver = fixtureActor.GetComponent<OccludedAimObserver>();
                var policy = manager.GetComponent<FileAntiCheat>();
                typeof(FileAntiCheat).GetField("dynamicMode", Hidden).SetValue(policy, FileAntiCheat.EnforcementMode.Disconnect);
                initialScenes = UnityEngine.SceneManagement.SceneManager.sceneCount;
                Application.logMessageReceived += FixtureLog;
                typeof(OccludedAimObserver).GetMethod("DevelopmentAimFixture", Hidden).Invoke(fixtureObserver, null);
                phase = 2; return;
            }
            if (typeof(OccludedAimObserver).GetField("_fixture", Hidden).GetValue(fixtureObserver) != null ||
                UnityEngine.SceneManagement.SceneManager.sceneCount != initialScenes) return;
            Check(fixtureReports > 0, "actual isolated scripted coroutine emits report-only positive");
            Check(fixtureActor.GetComponent<Health>().CurrentHealth == 100 && fixtureActor.GetComponent<Health>().Owner.IsActive,
                "fixture in DynamicDisconnect changes neither health nor owner activity");
            Check(Vector3.Distance(fixtureActor.transform.position, fixturePosition) < .001f, "fixture never moves its live actor");
            var fixtureScene = (UnityEngine.SceneManagement.Scene)typeof(OccludedAimObserver).GetField("_fixtureScene", Hidden).GetValue(fixtureObserver);
            Check(!fixtureScene.IsValid(), "fixture scene handle and scene count cleaned");
            Debug.Log($"[Combat SERVER CHECK] COMPLETE checks={checks}; fixtureReports={fixtureReports}; isolated physics scene cleaned, DynamicDisconnect; simulated owners, remote runtime pending.");
            Finish(true);
        }
        catch (Exception error) { Debug.LogException(error); Finish(false); }
    }
    private static void FixtureLog(string message, string trace, LogType kind)
    {
        if (message.Contains("[Aim AC FIXTURE]") && message.Contains("source=scripted-fixture") && message.Contains("action=ReportOnly")) fixtureReports++;
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
            CheckAimAdmission(shooter, sender, victimOwner);
            Debug.Log($"[Combat SERVER CHECK] PASS checks={checks}; real server HP/ammo/guards, simulated owner identities; transport/interactive acceptance pending.");
        }
        finally
        {
            if (shooterObject != null) manager.ServerManager.Despawn(shooterObject);
            if (targetObject != null) manager.ServerManager.Despawn(targetObject);
            if (wallObject != null) UnityEngine.Object.Destroy(wallObject);
        }
    }
    private static void CheckAimAdmission(HitscanShooter shooter, NetworkConnection sender, NetworkConnection other)
    {
        var observer = shooter.GetComponentInParent<OccludedAimObserver>();
        var type = typeof(OccludedAimObserver);
        object Read(string name) => type.GetField(name, Hidden).GetValue(observer);
        void Call(string name, params object[] args) => type.GetMethod(name, Hidden).Invoke(observer, args);
        var report = typeof(HitscanShooter).GetMethod("ProcessServerAim", Hidden);
        void Aim(Vector3 v, uint seq, NetworkConnection owner) => report.Invoke(shooter, new object[] { v, seq, owner });
        Aim(Vector3.forward, 1, other); Check((uint)Read("_aimVersion") == 0, "other owner cannot submit aim");
        Aim(Vector3.forward, 1, Owner(sender.ClientId)); Check((uint)Read("_aimVersion") == 0, "reused numeric ID is not same connection/session");
        Aim(Vector3.forward, 1, sender); Check((uint)Read("_aimVersion") == 1, "active authenticated owner aim admitted");
        Aim(Vector3.forward, 1, sender); Check((uint)Read("_aimVersion") == 1, "duplicate aim sequence rejected");
        Aim(new Vector3(float.NaN, 0, 1), 2, sender); Check((uint)Read("_aimVersion") == 1, "nonfinite aim rejected");
        Call("ResetSession");
        for (uint i = 1; i <= 100; i++) Aim(Vector3.forward, i, sender);
        Check((uint)Read("_aimVersion") == 4, "processing burst bounded to four before physics");
        var network = observer.GetComponent<FishNet.Component.Transforming.NetworkTransform>();
        var field = network.GetType().GetField("OnDataReceived", Hidden);
        int Count() => (field.GetValue(network) as Delegate)?.GetInvocationList().Length ?? 0;
        int initial = Count();
        Call("End"); Call("End"); Check(Count() == initial - 1, "repeated stop removes exactly one receive listener");
        Check(Read("_owner") == null && Read("_targetOwner") == null && (uint)Read("_aimVersion") == 0,
            "stop releases connection/target references and queued telemetry");
        Call("Begin", shooter); Call("Begin", shooter); Check(Count() == initial, "repeated start cannot accumulate receive listeners");
        Check((uint)Read("_aimVersion") == 0 && Read("_target") == null, "new session clears aim sequence, target and evidence");
    }
    private static void Check(bool condition, string label)
    { if (!condition) throw new Exception("[Combat SERVER CHECK] " + label); checks++; }
    private static void Finish(bool passed)
    {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= FixtureLog;
        if (manager != null && fixtureActor != null && fixtureActor.GetComponent<Health>().IsSpawned) manager.ServerManager.Despawn(fixtureActor);
        if (manager != null && manager.IsServerStarted) manager.ServerManager.StopConnection(true);
        SessionState.SetBool(Key + "passed", passed); SessionState.SetBool(Key + "finished", true); EditorApplication.ExitPlaymode();
    }
}
