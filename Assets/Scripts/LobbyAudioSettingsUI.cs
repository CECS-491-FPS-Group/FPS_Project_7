using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Adds Music and Sound Effects sliders next to the lobby's existing Gain slider.
public class LobbyAudioSettingsUI : MonoBehaviour
{
    private const string LobbySceneName = "LobbyScene_v1";
    private const float RowSpacing = 50f;

    // AfterSceneLoad only fires for the first scene, so also listen for later scene loads.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Register()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        AutoCreate();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        AutoCreate();
    }

    private static void AutoCreate()
    {
        if (SceneManager.GetActiveScene().name != LobbySceneName || FindFirstObjectByType<LobbyAudioSettingsUI>() != null)
        {
            return;
        }

        new GameObject("LobbyAudioSettingsUI").AddComponent<LobbyAudioSettingsUI>();
    }

    private void Start()
    {
        Slider gain = null;
        foreach (Slider slider in FindObjectsByType<Slider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (slider.name == "GainSlider")
            {
                gain = slider;
                break;
            }
        }

        Canvas canvas = gain != null ? gain.GetComponentInParent<Canvas>() : FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("LobbyAudioSettingsUI: no canvas found.");
            return;
        }

        RectTransform parent = (RectTransform)canvas.transform;
        Vector2 basePosition = gain != null ? ((RectTransform)gain.transform).anchoredPosition : new Vector2(600f, 0f);
        Vector2 size = gain != null ? ((RectTransform)gain.transform).sizeDelta : new Vector2(160f, 20f);

        if (gain != null)
        {
            CreateLabel(parent, "Gain", basePosition, size);
        }

        CreateRow(parent, "Music", basePosition - new Vector2(0f, RowSpacing), size,
            GameAudioSettings.MusicVolume, v => GameAudioSettings.MusicVolume = v);
        CreateRow(parent, "Sound Effects", basePosition - new Vector2(0f, RowSpacing * 2f), size,
            GameAudioSettings.SfxVolume, v => GameAudioSettings.SfxVolume = v);
    }

    private static void CreateRow(RectTransform parent, string label, Vector2 position, Vector2 size,
        float value, UnityEngine.Events.UnityAction<float> onChanged)
    {
        DefaultControls.Resources resources = new DefaultControls.Resources
        {
            standard = UnityEngine.Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd"),
            background = UnityEngine.Resources.GetBuiltinResource<Sprite>("UI/Skin/Background.psd"),
            knob = UnityEngine.Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd"),
        };

        GameObject sliderObject = DefaultControls.CreateSlider(resources);
        sliderObject.name = label + "Slider";
        sliderObject.layer = parent.gameObject.layer;
        RectTransform rect = (RectTransform)sliderObject.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Slider slider = sliderObject.GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.SetValueWithoutNotify(value);
        slider.onValueChanged.AddListener(onChanged);

        CreateLabel(parent, label, position, size);
    }

    private static void CreateLabel(RectTransform parent, string text, Vector2 sliderPosition, Vector2 sliderSize)
    {
        GameObject labelObject = new GameObject(text + "Label", typeof(RectTransform));
        labelObject.layer = parent.gameObject.layer;
        RectTransform rect = (RectTransform)labelObject.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(sliderSize.x + 80f, 24f);
        rect.anchoredPosition = sliderPosition + new Vector2(0f, sliderSize.y + 4f);

        TextMeshProUGUI tmp = labelObject.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 18f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
    }
}
