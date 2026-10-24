using UnityEngine;
using UnityEngine.SceneManagement;

// Scene-local audio: no persistent NetworkObjects, microphone capture, or gameplay input.
public sealed class IntegrationAudio : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "MainMenu" && scene.name != "LobbyScene_v1") return;
        var host = new GameObject("Menu Audio");
        SceneManager.MoveGameObjectToScene(host, scene);
        // MainMenu already has a listener; LobbyScene_v1 does not. This fallback
        // belongs to the menu scene and is destroyed with it before gameplay.
        AudioListener listener = null;
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (AudioListener candidate in root.GetComponentsInChildren<AudioListener>(true))
                if (candidate.isActiveAndEnabled)
                {
                    if (listener == null) listener = candidate;
                    else candidate.enabled = false;
                }
        if (listener == null) host.AddComponent<AudioListener>();
        AudioSource source = host.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.clip = Resources.Load<AudioClip>("IntegrationAudio/MenuMusic");
        source.loop = true; source.volume = 0.12f;
        if (source.clip != null) source.Play();
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.GetComponentInChildren<UnityEngine.UI.Button>(true) != null && root.GetComponent<ButtonClickSound>() == null)
                root.AddComponent<ButtonClickSound>();
    }
}
