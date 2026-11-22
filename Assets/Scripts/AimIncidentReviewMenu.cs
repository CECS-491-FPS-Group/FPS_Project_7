using System;
using System.Collections.Generic;
using System.Linq;
using FishNet;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static AimIncidentData;

// Offline host-local review. No RPC, detector mutation, policy call or map replay claim.
public sealed class AimIncidentReviewMenu : MonoBehaviour
{
    private static AimIncidentReviewMenu _open;
    private Incident[] _catalog = Array.Empty<Incident>();
    private string[] _matches = Array.Empty<string>(), _players = Array.Empty<string>();
    private int _matchIndex, _playerIndex, _page;
    private Incident _data;
    private RectTransform _panel;
    private AimIncidentGraphic _plot;
    private TMP_Text _header, _details, _matchLabel, _playerLabel;
    private TMP_InputField _note;
    private Slider _timeline;
    private readonly List<GameObject> _rows = new();
    private double _time;
    private bool _playing, _side;
    private string _reviewState;
    internal Incident Selected => _data;
    internal int SelectedFrame => _data == null ? -1 : FrameAt(_data, _time);
    public static void Show()
    {
        if (_open != null) return;
        var manager = InstanceFinder.NetworkManager;
        if (manager != null && (manager.IsClientStarted || manager.IsServerStarted))
        { Debug.LogWarning("[Aim REVIEW] Leave the session before opening local review."); return; }
        var root = IntegrationUi.Canvas("Server Aim Incident Review", 160);
        IntegrationUi.Backdrop(root.transform); _open = root.AddComponent<AimIncidentReviewMenu>(); _open.Build();
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
    }
    private static string Short(string id) => id.Length > 10 ? id.Substring(0, 10) : id;
    private void Build()
    {
        _panel = IntegrationUi.Panel(transform, "Incident review", new Vector2(1120, 660));
        IntegrationUi.Text(_panel, "THEATER / SERVER AIM INCIDENTS — evidence schematic, not map or camera replay", new Vector2(0, 300), new Vector2(1090, 40), 22);
        _header = IntegrationUi.Text(_panel, "Select a flagged match and player.", new Vector2(190, 242), new Vector2(690, 105), 15);
        var match = IntegrationUi.Button(_panel, "Match: none", new Vector2(-385, 247), () => { if (_matches.Length > 0) { _matchIndex = (_matchIndex + 1) % _matches.Length; SelectPlayers(); } }, new Vector2(310, 38));
        _matchLabel = match.GetComponentInChildren<TMP_Text>();
        var player = IntegrationUi.Button(_panel, "Flagged player: none", new Vector2(-385, 201), () => { if (_players.Length > 0) { _playerIndex = (_playerIndex + 1) % _players.Length; _page = 0; Rows(); } }, new Vector2(310, 38));
        _playerLabel = player.GetComponentInChildren<TMP_Text>();
        IntegrationUi.Button(_panel, "Refresh", new Vector2(-385, -174), Refresh, new Vector2(310, 32));
        IntegrationUi.Button(_panel, "Previous", new Vector2(-465, -130), () => { _page = Mathf.Max(0, _page - 1); Rows(); }, new Vector2(145, 32));
        IntegrationUi.Button(_panel, "Next", new Vector2(-305, -130), () => { _page++; Rows(); }, new Vector2(145, 32));
        var plot = IntegrationUi.Panel(_panel, "Evidence plane", new Vector2(670, 310)); plot.anchoredPosition = new Vector2(190, 32);
        var go = new GameObject("Observed samples", typeof(RectTransform), typeof(AimIncidentGraphic));
        var rect = go.GetComponent<RectTransform>(); rect.SetParent(plot, false); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        _plot = go.GetComponent<AimIncidentGraphic>();
        _details = IntegrationUi.Text(_panel, "Cyan: shooter / orange: target / yellow: reported aim. Dots are recorded samples.", new Vector2(190, -160), new Vector2(690, 65), 15);
        var slider = DefaultControls.CreateSlider(new DefaultControls.Resources()); slider.transform.SetParent(_panel, false);
        _timeline = slider.GetComponent<Slider>(); var sliderRect = (RectTransform)slider.transform;
        sliderRect.anchorMin = sliderRect.anchorMax = new Vector2(.5f, .5f); sliderRect.anchoredPosition = new Vector2(190, -209); sliderRect.sizeDelta = new Vector2(660, 22);
        _timeline.interactable = false; _timeline.onValueChanged.AddListener(value => { if (_data != null) Scrub(_data.frames[0].time + value); });
        var notePanel = IntegrationUi.Panel(_panel, "Local review note", new Vector2(1050, 44)); notePanel.anchoredPosition = new Vector2(0, -251);
        notePanel.gameObject.AddComponent<RectMask2D>();
        _note = notePanel.gameObject.AddComponent<TMP_InputField>();
        var noteText = IntegrationUi.Text(notePanel, "", Vector2.zero, new Vector2(1020, 40), 16); noteText.alignment = TextAlignmentOptions.MidlineLeft;
        _note.textComponent = (TextMeshProUGUI)noteText; _note.textViewport = notePanel;
        _note.placeholder = IntegrationUi.Text(notePanel, "Local note, up to 280 characters; click Reviewed + Save", Vector2.zero, new Vector2(1020, 40), 16);
        _note.characterLimit = 280; _note.interactable = false;
        IntegrationUi.Button(_panel, "Play / Pause", new Vector2(-420, -301), () => { if (_data != null) _playing = !_playing; }, new Vector2(190, 36));
        IntegrationUi.Button(_panel, "Top XZ / Side XY", new Vector2(-205, -301), () => { _side = !_side; _plot.SetEvidence(_data, _side); }, new Vector2(210, 36));
        IntegrationUi.Button(_panel, "Reviewed + Save note", new Vector2(70, -301), SaveReview, new Vector2(290, 36));
        IntegrationUi.Button(_panel, "Back", new Vector2(410, -301), () => Destroy(gameObject), new Vector2(180, 36));
        Refresh();
    }
    private void Refresh()
    {
        var found = new List<Incident>(); int invalid = 0;
        try { foreach (string leaf in AimIncidentArchive.Files()) { try { found.Add(AimIncidentArchive.Load(leaf)); } catch (Exception) { invalid++; } } }
        catch (Exception) { invalid++; }
        _catalog = found.ToArray(); _matches = _catalog.Select(d => d.matchId).Distinct().ToArray(); _matchIndex = 0;
        if (_matches.Length == 0) _header.text = "No saved aim flags on this machine. Host/server-local archive only.";
        if (invalid > 0) _header.text = "Some local incident files are unavailable or invalid.";
        SelectPlayers();
    }
    private void SelectPlayers()
    {
        _matchLabel.text = _matches.Length == 0 ? "Match: none" : $"Match {Short(_matches[_matchIndex])}  >";
        _players = _matches.Length == 0 ? Array.Empty<string>() : _catalog.Where(d => d.matchId == _matches[_matchIndex]).Select(d => d.shooter.participantId).Distinct().ToArray();
        _playerIndex = _page = 0; Rows();
    }
    private void Rows()
    {
        foreach (var row in _rows) Destroy(row); _rows.Clear();
        _playerLabel.text = _players.Length == 0 ? "Flagged player: none" : $"Player {Short(_players[_playerIndex])}  >";
        var entries = _players.Length == 0 ? Array.Empty<Incident>() : _catalog.Where(d => d.matchId == _matches[_matchIndex] && d.shooter.participantId == _players[_playerIndex]).ToArray();
        _page = Mathf.Clamp(_page, 0, Mathf.Max(0, (entries.Length - 1) / 6));
        for (int i = _page * 6; i < Mathf.Min(entries.Length, (_page + 1) * 6); i++)
        {
            Incident data = entries[i];
            var button = IntegrationUi.Button(_panel, $"{data.alertTime:F1}s {Short(data.incidentId)} { (data.source == "scripted-fixture" ? "[FIXTURE]" : "[LIVE]")}",
                new Vector2(-385, 146 - (i % 6) * 42), () => LoadIncident(data), new Vector2(310, 36));
            _rows.Add(button.gameObject);
        }
        if (entries.Length > 0) LoadIncident(entries[_page * 6]);
        else { _data = null; _plot.SetEvidence(null, _side); _timeline.interactable = _note.interactable = false; }
    }
    internal bool Select(string matchId, string participantId, string incidentId)
    {
        int match = Array.IndexOf(_matches, matchId); if (match < 0) return false;
        _matchIndex = match; SelectPlayers(); int player = Array.IndexOf(_players, participantId); if (player < 0) return false;
        _playerIndex = player; Rows(); var data = _catalog.FirstOrDefault(d => d.matchId == matchId && d.shooter.participantId == participantId && d.incidentId == incidentId);
        if (data == null) return false; LoadIncident(data); return true;
    }
    private void LoadIncident(Incident data)
    {
        _data = data; _playing = false; _time = data.frames[0].time; _plot.SetEvidence(data, _side);
        _timeline.minValue = 0; _timeline.maxValue = (float)Math.Max(.01, data.endTime - data.frames[0].time); _timeline.interactable = true; _note.interactable = true;
        try
        {
            var review = AimIncidentArchive.LoadReview(data); _note.text = review.note;
            _reviewState = review.reviewed && review.revision == data.revision ? "Reviewed" : review.reviewed ? "Evidence updated; previous review" : "Unreviewed";
        }
        catch (Exception) { _reviewState = "Review note unavailable"; _note.text = ""; }
        var evidence = data.alerts[0];
        _header.text = $"{data.source} | match {Short(data.matchId)} | incident {Short(data.incidentId)} | {_reviewState}\n" +
            $"shooter {Short(data.shooter.participantId)} (conn {data.shooter.connectionId}, life {data.shooter.life}) -> target {Short(data.target.participantId)} (conn {data.target.connectionId}, life {data.target.life})\n" +
            $"{data.rule} at {data.alertTime:F2}s | {evidence.duration:F2}s / {evidence.samples} samples / matched {evidence.matched:F2}s\n" +
            $"aim/target travel {evidence.aimTravel:F1}/{evidence.targetTravel:F1}deg | error {evidence.error:F2}deg | alerts {data.alerts.Length}\n" +
            $"{(data.final ? data.partial ? "PARTIAL" : "Complete" : "INCOMPLETE snapshot")} | {data.endReason}";
        Scrub(_time);
    }
    internal void Scrub(double matchTime)
    {
        if (_data == null) return; _time = Math.Max(_data.frames[0].time, Math.Min(_data.endTime, matchTime));
        int index = SelectedFrame; _plot.SetFrame(index);
        _timeline.SetValueWithoutNotify((float)(_time - _data.frames[0].time));
        if (index < 0) { _details.text = $"{_time:F2}s — GAP: no observed sample. No actor/aim interpolation.\nCyan shooter / orange target / yellow reported aim; schematic, not client POV."; return; }
        var frame = _data.frames[index];
        _details.text = $"match {_time:F2}s | snapshot {frame.time:F2}s | {frame.visibility} / {frame.decision} | cue={frame.cue} fresh={frame.fresh}\n" +
            $"aim {(frame.aimKnown ? frame.aimAge.ToString("F2") + "s old" : "unavailable")} target changed-goal age {frame.targetAge:F2}s | crouch {frame.shooterCrouched}/{frame.targetCrouched} | effects {frame.shooterEffect}/{frame.targetEffect}\n" +
            $"cover {frame.cover} | Y {(frame.shooterKnown ? frame.shooter.y.ToString("F2") : "missing")}/{(frame.targetKnown ? frame.target.y.ToString("F2") : "missing")} | cyan shooter / orange target / yellow reported aim; {(_side ? "XY" : "XZ")} samples";
    }
    private void SaveReview()
    {
        if (_data == null) return;
        try { AimIncidentArchive.SaveReview(_data, true, _note.text); LoadIncident(_data); }
        catch (Exception) { _header.text = "Review could not be saved. Refresh if evidence changed; gameplay/policy unaffected."; }
    }
    private void Update()
    {
        if (UnityEngine.InputSystem.Keyboard.current?.escapeKey.wasPressedThisFrame == true) { Destroy(gameObject); return; }
        if (_data == null || !_playing) return;
        Scrub(_time + Time.unscaledDeltaTime); if (_time >= _data.endTime) _playing = false;
    }
    private void OnDestroy() { if (_open == this) _open = null; }
}
