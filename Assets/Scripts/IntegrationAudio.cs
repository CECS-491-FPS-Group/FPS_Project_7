using UnityEngine;
using UnityEngine.SceneManagement;

// One local listener and audio root survive scene gaps; never a NetworkObject.
[DefaultExecutionOrder(1000)] // Follow after mouse look, recoil and the shooter update the view.
public sealed class IntegrationAudio : MonoBehaviour
{
    private static IntegrationAudio _instance;
    private AudioListener _listener;
    private AudioSource _music, _clicks;
    private Camera _ownerCamera, _menuCamera;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _instance = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
#if !UNITY_SERVER
        Ensure();
#endif
    }

    private static IntegrationAudio Ensure()
    {
        if (_instance == null) new GameObject("Local Audio").AddComponent<IntegrationAudio>();
        return _instance;
    }

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        _listener = gameObject.AddComponent<AudioListener>();
        _music = gameObject.AddComponent<AudioSource>();
        _clicks = gameObject.AddComponent<AudioSource>();
        _music.playOnAwake = _clicks.playOnAwake = false;
        _music.spatialBlend = _clicks.spatialBlend = 0f;
        _music.loop = true;
        _music.clip = Resources.Load<AudioClip>("IntegrationAudio/MenuMusic");
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
        GameAudioSettings.Changed += ApplyVolume;
        ApplyVolume();
    }

    public static void RegisterOwnerCamera(Camera camera)
    {
#if !UNITY_SERVER
        if (camera == null) return;
        var self = Ensure();
        self._ownerCamera = camera;
        foreach (AudioListener listener in camera.GetComponents<AudioListener>()) listener.enabled = false;
        self.FollowCamera();
        Debug.Log($"[Audio] listener=local camera=owner scene={camera.gameObject.scene.name}.");
#endif
    }

    public static void ReleaseOwnerCamera(Camera camera)
    {
        if (_instance == null || _instance._ownerCamera != camera) return;
        _instance._ownerCamera = null;
        _instance.FollowCamera();
    }

    public static void PlayClick(AudioClip clip, float volume)
    {
#if !UNITY_SERVER
        if (clip != null) Ensure()._clicks.PlayOneShot(clip, Mathf.Clamp01(volume));
#endif
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (AudioListener candidate in root.GetComponentsInChildren<AudioListener>(true))
                if (candidate != _listener) candidate.enabled = false;
            if (scene.name == "MainMenu")
                foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
                    if (camera.isActiveAndEnabled && camera.GetComponentInParent<FishNet.Object.NetworkObject>() == null)
                        _menuCamera = camera;
        }
        bool menu = scene.name == "MainMenu" || scene.name == "LobbyScene_v1";
        if (menu)
        {
            if (_music.clip != null && !_music.isPlaying) _music.Play();
            LobbyAudioSettingsUI.Install(scene);
            LocalBindingsMenu.Install(scene);
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.GetComponentInChildren<UnityEngine.UI.Button>(true) != null && root.GetComponent<ButtonClickSound>() == null)
                    root.AddComponent<ButtonClickSound>();
        }
        else if (scene.name == "RoundImplementation") _music.Stop();
        FollowCamera();
        Debug.Log($"[Audio] scene={scene.name} listener=local ownerCamera={(_ownerCamera != null)}.");
    }

    private void OnSceneUnloaded(Scene scene)
    {
        if (!SceneManager.GetSceneByName("MainMenu").isLoaded && !SceneManager.GetSceneByName("LobbyScene_v1").isLoaded)
            _music.Stop();
        FollowCamera();
    }

    private void ApplyVolume()
    {
        _music.volume = .12f * GameAudioSettings.MusicVolume;
        _clicks.volume = GameAudioSettings.SfxVolume;
    }

    private void LateUpdate() => FollowCamera();
    private void FollowCamera()
    {
        Camera camera = _ownerCamera != null && _ownerCamera.isActiveAndEnabled ? _ownerCamera : _menuCamera;
        if (camera != null && camera.isActiveAndEnabled)
            transform.SetPositionAndRotation(camera.transform.position, camera.transform.rotation);
    }
    private void OnApplicationPause(bool paused) { if (paused) GameAudioSettings.Save(); }
    private void OnApplicationQuit() => GameAudioSettings.Save();
    private void OnDestroy()
    {
        if (_instance != this) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        GameAudioSettings.Changed -= ApplyVolume;
        GameplayAudioClips.Release();
        _instance = null;
    }
}
