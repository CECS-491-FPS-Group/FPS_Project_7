using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Adapted from Audio_Management@11c62b6. Scene-local rows; no microphone/Gain UI.
public sealed class LobbyAudioSettingsUI : MonoBehaviour
{
    private Slider _music, _sfx;

    public static void Install(Scene scene)
    {
        Canvas canvas = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.GetComponentInChildren<LobbyAudioSettingsUI>(true) != null) return;
            foreach (Canvas candidate in root.GetComponentsInChildren<Canvas>(true))
                if (scene.name == "MainMenu" ? candidate.name == "SettingsMenuCanvas" : candidate.name == "Canvas")
                    canvas = candidate;
        }
        if (canvas == null) { Debug.LogWarning($"[Audio] No settings canvas in {scene.name}."); return; }
        var panel = new GameObject("Local Audio Settings", typeof(RectTransform), typeof(Image));
        panel.layer = canvas.gameObject.layer;
        var rect = panel.GetComponent<RectTransform>();
        rect.SetParent(canvas.transform, false);
        rect.anchorMin = rect.anchorMax = Vector2.one;
        rect.anchoredPosition = new Vector2(-180, -120);
        rect.sizeDelta = new Vector2(330, 145);
        panel.GetComponent<Image>().color = new Color(.06f, .075f, .1f, .96f);
        var ui = panel.AddComponent<LobbyAudioSettingsUI>();
        Label(rect, "Audio", new Vector2(0, 49), 20);
        ui._music = Row(rect, "Music", 13, GameAudioSettings.MusicVolume);
        ui._sfx = Row(rect, "Sound Effects", -30, GameAudioSettings.SfxVolume);
        ui._music.onValueChanged.AddListener(ui.SetMusic);
        ui._sfx.onValueChanged.AddListener(ui.SetSfx);

        if (scene.name == "LobbyScene_v1")
        {
            var toggle = new GameObject("Audio Settings Button", typeof(RectTransform), typeof(Image), typeof(Button));
            toggle.layer = canvas.gameObject.layer;
            var toggleRect = toggle.GetComponent<RectTransform>();
            toggleRect.SetParent(canvas.transform, false);
            toggleRect.anchorMin = toggleRect.anchorMax = Vector2.one;
            toggleRect.anchoredPosition = new Vector2(-78, -26);
            toggleRect.sizeDelta = new Vector2(125, 34);
            toggle.GetComponent<Image>().color = new Color(.12f, .16f, .22f, 1);
            Label(toggleRect, "Audio", Vector2.zero, 18);
            toggle.GetComponent<Button>().onClick.AddListener(() => panel.SetActive(!panel.activeSelf));
            panel.SetActive(false);
        }
    }

    private static Slider Row(RectTransform parent, string label, float y, float value)
    {
        Label(parent, label, new Vector2(-95, y), 16);
        // Unity 6 no longer supplies the old UI/Skin/*.psd builtin paths used by
        // the source branch. Plain Image controls avoid missing-resource errors.
        var resources = new DefaultControls.Resources();
        GameObject obj = DefaultControls.CreateSlider(resources);
        obj.name = label + " Volume";
        obj.layer = parent.gameObject.layer;
        var rect = (RectTransform)obj.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.anchoredPosition = new Vector2(65, y);
        rect.sizeDelta = new Vector2(170, 22);
        Slider slider = obj.GetComponent<Slider>();
        slider.minValue = 0; slider.maxValue = 1;
        slider.SetValueWithoutNotify(value);
        obj.transform.Find("Background").GetComponent<Image>().color = new Color(.18f, .2f, .24f);
        obj.transform.Find("Fill Area/Fill").GetComponent<Image>().color = new Color(.25f, .65f, .9f);
        return slider;
    }

    private static void Label(RectTransform parent, string text, Vector2 position, float size)
    {
        var obj = new GameObject(text, typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.layer = parent.gameObject.layer;
        var rect = obj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(135, 28);
        var label = obj.GetComponent<TMP_Text>();
        label.font = TMP_Settings.defaultFontAsset;
        label.text = text; label.fontSize = size; label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white; label.raycastTarget = false;
    }

    private void SetMusic(float value) => GameAudioSettings.MusicVolume = value;
    private void SetSfx(float value) => GameAudioSettings.SfxVolume = value;
    private void OnEnable() { GameAudioSettings.Changed += Refresh; Refresh(); }
    private void Refresh()
    {
        if (_music != null) _music.SetValueWithoutNotify(GameAudioSettings.MusicVolume);
        if (_sfx != null) _sfx.SetValueWithoutNotify(GameAudioSettings.SfxVolume);
    }
    private void OnDisable() { GameAudioSettings.Changed -= Refresh; GameAudioSettings.Save(); }
    private void OnDestroy()
    {
        GameAudioSettings.Changed -= Refresh;
        if (_music != null) _music.onValueChanged.RemoveListener(SetMusic);
        if (_sfx != null) _sfx.onValueChanged.RemoveListener(SetSfx);
    }
}
