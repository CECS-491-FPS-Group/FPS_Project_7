using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Automated unconnected Play Mode lifecycle checks. No claim of audible/network acceptance.
[InitializeOnLoad]
public static class AudioIntegrationChecks
{
    private const string Key = "FPS.AudioChecks.";
    private static int phase, checks, rootId;
    private static double deadline;
    private static AsyncOperation loading;
    private static string failure;
    private static readonly string[] Scenes = { "LobbyScene_v1", "RoundImplementation", "LobbyScene_v1", "MainMenu" };
    private static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

    static AudioIntegrationChecks() => EditorApplication.playModeStateChanged += OnPlayState;

    [MenuItem("Tools/Audio/Validate Audio Lifecycle")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        foreach (string name in new[] { "music", "sfx" })
        {
            SessionState.SetBool(Key + name + ".had", PlayerPrefs.HasKey("audio." + name));
            SessionState.SetFloat(Key + name, PlayerPrefs.GetFloat("audio." + name, 1));
        }
        SessionState.SetBool(Key + "running", true);
        SessionState.SetBool(Key + "finished", false);
        SessionState.SetBool(Key + "batch", Application.isBatchMode);
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        EditorApplication.EnterPlaymode();
    }

    private static void OnPlayState(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key + "running", false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            phase = checks = 0; failure = null; loading = null;
            deadline = EditorApplication.timeSinceStartup + 120;
            Application.logMessageReceived += OnLog;
            EditorApplication.update += Tick;
        }
        if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Key + "finished", false))
        {
            RestorePreferences();
            SessionState.SetBool(Key + "running", false);
            if (SessionState.GetBool(Key + "batch", false)) EditorApplication.Exit(SessionState.GetBool(Key + "passed", false) ? 0 : 1);
        }
    }

    private static void OnLog(string message, string trace, LogType type)
    {
        if (type == LogType.Exception || type == LogType.Error || message.Contains("no audio listeners") || message.Contains("2 audio listeners"))
            failure = message;
    }

    private static void CheckBindings()
    {
        var controls = UnityEngine.Object.Instantiate(Resources.Load<UnityEngine.InputSystem.InputActionAsset>("GameplayInput"));
        var keyboard = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
        var mouse = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
        try
        {
            controls.devices = new UnityEngine.InputSystem.InputDevice[] { keyboard, mouse }; controls.Enable();
            Require(controls.FindAction("Sprint").controls.Any(c => ReferenceEquals(c, keyboard.leftShiftKey)) &&
                !controls.FindAction("Aim").controls.Any(c => ReferenceEquals(c, keyboard.leftShiftKey)), "default Shift resolves to sprint only, not Aim");
            Require(controls.FindAction("Aim").controls.Any(c => ReferenceEquals(c, mouse.rightButton)), "Aim resolves to right mouse");
            controls.Disable(); controls.FindAction("Aim").ApplyBindingOverride(0, "<Keyboard>/leftShift"); controls.Enable();
            Require(controls.FindAction("Aim").controls.Any(c => ReferenceEquals(c, keyboard.leftShiftKey)), "intentional Aim override survives");
        }
        finally
        {
            controls.Disable(); UnityEngine.Object.Destroy(controls);
            UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard); UnityEngine.InputSystem.InputSystem.RemoveDevice(mouse);
        }
    }

    private static void CheckIntegrityWorker()
    {
        var component = UnityEngine.Object.FindFirstObjectByType<FileAntiCheat>();
        Require(component != null, "integrity lifecycle installed on persistent manager");
        var type = typeof(FileAntiCheat);
        object Read(string name) => type.GetField(name, Hidden).GetValue(component);
        void Write(string name, object value) => type.GetField(name, Hidden).SetValue(component, value);
        var update = type.GetMethod("Update", Hidden);
        int generation = (int)Read("_generation");
        var pending = new System.Threading.Tasks.TaskCompletionSource<FileAntiCheat.Report>();
        var cancel = new System.Threading.CancellationTokenSource();
        int timeouts = 0, retiring = 0;
        Application.LogCallback capture = (message, trace, kind) =>
        { if (message.Contains("local hash worker timed out")) timeouts++; if (message.Contains("previous hash worker still retiring")) retiring++; };
        Application.logMessageReceived += capture;
        try
        {
            Write("_work", pending.Task); Write("_cancel", cancel); Write("_workGeneration", generation);
            Write("_workDeadline", -1d); Write("_workTimedOut", false);
            update.Invoke(component, null);
            Require((bool)Read("_workTimedOut") && cancel.IsCancellationRequested, "hash deadline cancels best effort");
            update.Invoke(component, null);
            Require(timeouts == 1 && ReferenceEquals(Read("_work"), pending.Task), "one timeout and one retained worker, no unbounded retry");
            Write("_generation", generation + 1); Write("_initialPending", true); Write("_clientStartupDeadline", -1d);
            update.Invoke(component, null); update.Invoke(component, null);
            Require(retiring == 1 && !(bool)Read("_initialPending"), "new session unable to start worker receives one explicit terminal unavailable result");
            pending.SetResult(new FileAntiCheat.Report { schema = 1, mode = 1 });
            update.Invoke(component, null);
            Require(Read("_work") == null && Read("_cancel") == null, "retired worker completion cleaned up");
        }
        finally
        {
            Application.logMessageReceived -= capture; cancel.Dispose();
            Write("_work", null); Write("_cancel", null); Write("_initialPending", false); Write("_generation", generation);
        }
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying || SessionState.GetBool(Key + "finished", false)) return;
        try
        {
            if (failure != null) throw new InvalidOperationException(failure);
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Audio lifecycle check timed out.");
            if (loading != null && !loading.isDone) return;
            if (phase == 0)
            {
                var root = UnityEngine.Object.FindFirstObjectByType<IntegrationAudio>();
                Require(root != null, "audio root installed before first scene"); rootId = root.GetInstanceID();
                CheckRoot();
                foreach (string clip in new[] { "MenuMusic", "Shot", "Shotgun", "Step" })
                    Require(Resources.Load<AudioClip>("IntegrationAudio/" + clip) != null, "shipped clip " + clip);
                CheckPreferences(root);
                CheckOwnerHandoff(root);
                CheckHealthAndFalloff();
                CheckBindings();
                CheckIntegrityWorker();
                LocalReplayMenu.Show();
                Require(UnityEngine.Object.FindFirstObjectByType<LocalReplayMenu>() != null, "local theater opens without network actors");
                CheckRoot();
                loading = SceneManager.LoadSceneAsync(Scenes[0]); phase = 1;
            }
            else
            {
                CheckRoot();
                if (SceneManager.GetActiveScene().name == "RoundImplementation")
                    Require(!UnityEngine.Object.FindFirstObjectByType<IntegrationAudio>().GetComponents<AudioSource>()[0].isPlaying,
                        "menu music stopped in gameplay");
                else
                    Require(UnityEngine.Object.FindObjectsByType<LobbyAudioSettingsUI>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1,
                        "one scene-local settings UI");
                if (phase < Scenes.Length) { loading = SceneManager.LoadSceneAsync(Scenes[phase]); phase++; }
                else
                {
                    var changed = (Delegate)typeof(GameAudioSettings).GetField("Changed", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                    Require(changed != null && changed.GetInvocationList().Length == 1, "scene/UI/player subscriptions cleaned up");
                    Debug.Log($"[Audio CHECK] PASS checks={checks}; unconnected Play Mode lifecycle, audible/network tests pending.");
                    Finish(true);
                }
            }
        }
        catch (Exception error) { Debug.LogException(error); Finish(false); }
    }

    private static void CheckRoot()
    {
        var roots = UnityEngine.Object.FindObjectsByType<IntegrationAudio>(FindObjectsSortMode.None);
        Require(roots.Length == 1 && roots[0].GetInstanceID() == rootId, "single persistent audio root survives transition");
        Require(roots[0].GetComponent<FishNet.Object.NetworkObject>() == null, "audio is not a persistent NetworkObject");
        Require(UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(l => l.isActiveAndEnabled) == 1,
            "exactly one active listener");
    }

    private static void CheckPreferences(IntegrationAudio root)
    {
        Require(GameAudioSettings.Normalize(-1) == 0 && GameAudioSettings.Normalize(2) == 1 &&
            GameAudioSettings.Normalize(float.NaN) == 1 && GameAudioSettings.Normalize(float.PositiveInfinity) == 1, "bounded finite volume values");
        var ui = UnityEngine.Object.FindFirstObjectByType<LobbyAudioSettingsUI>(FindObjectsInactive.Include);
        Require(ui != null && ui.GetComponentInParent<Canvas>(true).name == "SettingsMenuCanvas", "audio controls use existing Settings menu");
        ui.GetComponentInParent<Canvas>(true).gameObject.SetActive(true);
        Slider[] sliders = ui.GetComponentsInChildren<Slider>();
        Require(sliders.Length == 2, "music and SFX controls created");
        sliders.Single(s => s.name == "Music Volume").value = .37f;
        sliders.Single(s => s.name == "Sound Effects Volume").value = .41f;
        Require(Mathf.Abs(GameAudioSettings.MusicVolume - .37f) < .001f && Mathf.Abs(GameAudioSettings.SfxVolume - .41f) < .001f,
            "slider callbacks select local settings");
        AudioSource[] sources = root.GetComponents<AudioSource>();
        Require(Mathf.Abs(sources[0].volume - .12f * .37f) < .001f && Mathf.Abs(sources[1].volume - .41f) < .001f, "independent music/click gain");
        GameAudioSettings.MusicVolume = GameAudioSettings.SfxVolume = 0;
        Require(sources.All(s => s.volume == 0), "zero actually mutes local categories");
        ui.GetComponentInParent<Canvas>(true).gameObject.SetActive(false);
        Require(PlayerPrefs.GetFloat("audio.music") == 0 && PlayerPrefs.GetFloat("audio.sfx") == 0, "preferences written on menu dismissal");
    }

    private static void CheckOwnerHandoff(IntegrationAudio root)
    {
        var cameraObject = new GameObject("Audio owner fixture", typeof(Camera));
        var camera = cameraObject.GetComponent<Camera>();
        camera.transform.SetPositionAndRotation(new Vector3(13, 4, -5), Quaternion.Euler(0, 90, 0));
        IntegrationAudio.RegisterOwnerCamera(camera);
        Require(Vector3.Distance(root.transform.position, camera.transform.position) < .001f, "listener follows owning camera position");
        Require(Quaternion.Angle(root.transform.rotation, camera.transform.rotation) < .001f, "listener follows owning camera orientation");
        var player = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab"));
        var setup = player.GetComponent<PlayerCameraSetup>();
        Require(setup != null && setup.audioListener != null && !setup.audioListener.enabled && !setup.playerCamera.enabled,
            "unowned player cannot activate camera/listener");
        IntegrationAudio.ReleaseOwnerCamera(setup.playerCamera);
        Require((Camera)typeof(IntegrationAudio).GetField("_ownerCamera", Hidden).GetValue(root) == camera,
            "unowned player's cleanup cannot release owner camera");
        AudioSource[] sources = player.GetComponentsInChildren<AudioSource>(true);
        Require(sources.Length == 3 && sources.All(s => s.dopplerLevel == 0 && s.volume == 0), "player SFX share mute without Doppler");
        GameAudioSettings.SfxVolume = .5f;
        Require(sources.Any(s => Mathf.Abs(s.volume - .175f) < .001f), "live SFX setting reaches player sources");
        IntegrationAudio.ReleaseOwnerCamera(camera);
        UnityEngine.Object.Destroy(player); UnityEngine.Object.Destroy(cameraObject);
        CheckRoot();
    }

    private static void CheckHealthAndFalloff()
    {
        Require(PlayerAudio.SelectHealthCue(true, true, true, 100, 60) == PlayerAudio.HealthCue.Hurt, "host accepted damage plays once");
        Require(PlayerAudio.SelectHealthCue(true, true, false, 100, 60) == PlayerAudio.HealthCue.None, "host client-side callback cannot duplicate hurt");
        Require(PlayerAudio.SelectHealthCue(true, false, false, 60, 0) == PlayerAudio.HealthCue.Death, "remote death replaces hurt");
        Require(PlayerAudio.SelectHealthCue(false, true, true, 60, 0) == PlayerAudio.HealthCue.None, "dedicated server emits no cue");
        Require(PlayerAudio.SelectHealthCue(true, false, false, 0, 100) == PlayerAudio.HealthCue.None &&
            PlayerAudio.SelectHealthCue(true, false, false, 100, 100) == PlayerAudio.HealthCue.None, "initial/revived/unchanged HP is silent");
        Require(GameplayAudioClips.Hurt.length > .24f && GameplayAudioClips.Death.length > .79f, "branch hurt/death synthesis available");
        AnimationCurve curve = PlayerAudio.ShotRolloff(6, 90);
        Require(Math.Abs(curve.Evaluate(0) - 1) < .001 && Math.Abs(curve.Evaluate(1)) < .001 &&
            curve.Evaluate(.5f) > 0 && curve.Evaluate(.5f) < 1, "positional shots fall to silence by max distance");
    }

    private static void Require(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("[Audio CHECK] FAIL: " + label);
        checks++;
    }
    private static void RestorePreferences()
    {
        foreach (string name in new[] { "music", "sfx" })
            if (SessionState.GetBool(Key + name + ".had", false)) PlayerPrefs.SetFloat("audio." + name, SessionState.GetFloat(Key + name, 1));
            else PlayerPrefs.DeleteKey("audio." + name);
        PlayerPrefs.Save();
    }
    private static void Finish(bool passed)
    {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        RestorePreferences();
        SessionState.SetBool(Key + "passed", passed);
        SessionState.SetBool(Key + "finished", true);
        EditorApplication.ExitPlaymode();
    }
}
