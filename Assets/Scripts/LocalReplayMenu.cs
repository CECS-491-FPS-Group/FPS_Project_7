using System;
using FishNet;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Source theater-mode@8ee49ee presentation adapted to actual local movement files.
public sealed class LocalReplayMenu : MonoBehaviour
{
    private static LocalReplayMenu _open;
    private LocalReplayCapture.Recording _data;
    private ReplayPathGraphic _plot;
    private RectTransform _marker;
    private TMP_Text _status;
    private Slider _timeline;
    private float _time;
    private bool _playing;

    public static void Install(Scene scene)
    {
        if (scene.name != "MainMenu") return;
        var entry = IntegrationUi.Canvas("Local Theater Entry", 15);
        var button = IntegrationUi.Button(entry.transform, "Theater / Replays", Vector2.zero, Show, new Vector2(210, 38));
        var rect = (RectTransform)button.transform;
        rect.anchorMin = rect.anchorMax = Vector2.zero; rect.anchoredPosition = new Vector2(125, 35);
    }
    public static void Show()
    {
        if (_open != null) return;
        var manager = InstanceFinder.NetworkManager;
        if (manager != null && (manager.IsClientStarted || manager.IsServerStarted))
        { Debug.LogWarning("[Replay] Leave the session before opening theater."); return; }
        var root = IntegrationUi.Canvas("Local Theater", 150);
        IntegrationUi.Backdrop(root.transform);
        _open = root.AddComponent<LocalReplayMenu>(); _open.Build();
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
    }
    private void Build()
    {
        var panel = IntegrationUi.Panel(transform, "Theater", new Vector2(1100, 660));
        IntegrationUi.Text(panel, "THEATER — LOCAL MOVEMENT", new Vector2(0, 300), new Vector2(1000, 40), 26);
        _status = IntegrationUi.Text(panel, "Select a capture. Movement only; top-down view.", new Vector2(100, 250), new Vector2(850, 40));
        string[] files;
        try { files = ReplayArchive.Files(); }
        catch (Exception) { files = Array.Empty<string>(); _status.text = "Local recordings unavailable."; }
        if (files.Length == 0)
            IntegrationUi.Text(panel, "No captures. In a match use Menu -> Save movement replay.", new Vector2(-370, 120), new Vector2(320, 180));
        for (int i = 0; i < files.Length; i++)
        {
            string leaf = files[i];
            IntegrationUi.Button(panel, leaf.Length > 32 ? leaf.Substring(0, 29) + "..." : leaf,
                new Vector2(-370, 205 - i * 40), () => Load(leaf), new Vector2(330, 34));
        }
        var plot = IntegrationUi.Panel(panel, "Path", new Vector2(620, 420)); plot.anchoredPosition = new Vector2(210, 10);
        var pathObject = new GameObject("Movement path", typeof(RectTransform), typeof(ReplayPathGraphic));
        var pathRect = pathObject.GetComponent<RectTransform>(); pathRect.SetParent(plot, false);
        pathRect.anchorMin = Vector2.zero; pathRect.anchorMax = Vector2.one; pathRect.offsetMin = pathRect.offsetMax = Vector2.zero;
        _plot = pathObject.GetComponent<ReplayPathGraphic>(); _plot.color = Color.cyan;
        _marker = IntegrationUi.Panel(plot, "Position", new Vector2(10, 10));
        _marker.GetComponent<Image>().color = Color.yellow; _marker.gameObject.SetActive(false);
        var sliderObject = DefaultControls.CreateSlider(new DefaultControls.Resources());
        _timeline = sliderObject.GetComponent<Slider>(); _timeline.transform.SetParent(panel, false);
        var sliderRect = (RectTransform)_timeline.transform;
        sliderRect.anchorMin = sliderRect.anchorMax = new Vector2(.5f, .5f);
        sliderRect.anchoredPosition = new Vector2(210, -235); sliderRect.sizeDelta = new Vector2(600, 25);
        _timeline.interactable = false;
        sliderObject.transform.Find("Background").GetComponent<Image>().color = new Color(.2f, .23f, .28f);
        sliderObject.transform.Find("Fill Area/Fill").GetComponent<Image>().color = Color.cyan;
        _timeline.onValueChanged.AddListener(value => _time = value);
        IntegrationUi.Button(panel, "Play / Pause", new Vector2(80, -295), () => { if (_data != null) _playing = !_playing; }, new Vector2(230, 40));
        IntegrationUi.Button(panel, "Back", new Vector2(350, -295), () => Destroy(gameObject), new Vector2(230, 40));
    }
    private void Load(string leaf)
    {
        try
        {
            _data = ReplayArchive.Load(leaf); _plot.SetRecording(_data);
            _time = 0; _playing = false;
            _timeline.minValue = 0; _timeline.maxValue = Mathf.Max(.01f, _data.frames[_data.frames.Count - 1].time);
            _timeline.interactable = true; _marker.gameObject.SetActive(true);
            _status.text = $"{_data.scene} | owner {_data.owner} | {_data.frames.Count} samples — movement only";
        }
        catch (Exception) { _data = null; _marker.gameObject.SetActive(false); _status.text = "Recording invalid, unsupported or unavailable."; }
    }
    private void Update()
    {
        if (UnityEngine.InputSystem.Keyboard.current?.escapeKey.wasPressedThisFrame == true) { Destroy(gameObject); return; }
        if (_data == null) return;
        float end = _data.frames[_data.frames.Count - 1].time;
        if (_playing) _time = Mathf.Min(end, _time + Time.unscaledDeltaTime);
        if (_time >= end) _playing = false;
        _timeline.SetValueWithoutNotify(_time);
        _marker.anchoredPosition = _plot.Project(ReplayArchive.PositionAt(_data, _time));
    }
    private void OnDestroy() { if (_open == this) _open = null; }
}
