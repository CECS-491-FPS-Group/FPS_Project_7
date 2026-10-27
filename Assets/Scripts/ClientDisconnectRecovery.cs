using System.Collections;
using FishNet.Broadcast;
using FishNet.Managing;
using FishNet.Transporting;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Separate persistent runner survives disposal of the disconnected client's old manager.
public sealed class ClientDisconnectRecovery : MonoBehaviour
{
    public struct Notice : IBroadcast { public byte Reason; }
    private static ClientDisconnectRecovery _instance;
    private readonly DisconnectNoticeState _state = new DisconnectNoticeState();
    private NetworkManager _manager;
    private bool _queueBusy, _quitting;
    private GameObject _noticeUi;
    public static bool InProgress => _instance != null && _instance._state.Recovering;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _instance = null;

    public static void Install(NetworkManager manager)
    {
        if (_instance == null)
        {
            var root = new GameObject("Client Disconnect Recovery");
            _instance = root.AddComponent<ClientDisconnectRecovery>();
            DontDestroyOnLoad(root);
        }
        _instance.Bind(manager);
    }

    private void Bind(NetworkManager manager)
    {
        if (_manager == manager) return;
        Unbind();
        _manager = manager;
        _manager.ClientManager.RegisterBroadcast<Notice>(OnNotice);
        _manager.ClientManager.OnAuthenticated += OnAuthenticated;
        _manager.ClientManager.OnClientConnectionState += OnClientState;
        _manager.SceneManager.OnQueueStart += QueueStart;
        _manager.SceneManager.OnQueueEnd += QueueEnd;
    }

    private void OnAuthenticated() => _state.Authenticate();
    private void QueueStart() => _queueBusy = true;
    private void QueueEnd() => _queueBusy = false;
    private void OnNotice(Notice notice, Channel channel)
    {
        if (_manager != null && !_manager.IsServerStarted && _manager.IsClientStarted &&
            _manager.ClientManager.Connection.IsAuthenticated) _state.ReceiveReason(notice.Reason);
    }
    private void OnClientState(ClientConnectionStateArgs args)
    {
        if (args.ConnectionState == LocalConnectionState.Starting) ClearForAttempt();
        if (args.ConnectionState != LocalConnectionState.Stopped || _quitting || _manager.IsServerStarted) return;
        if (_state.BeginRecovery(false)) StartCoroutine(ReturnLocally());
    }

    public static void ClearForAttempt()
    {
        if (_instance == null) return;
        _instance._state.BeginAttempt();
        _instance.HideNotice();
    }

    public static void ReturnToMenu()
    {
        if (_instance == null) { SceneManager.LoadScene("MainMenu"); return; }
        var self = _instance;
        if (self._state.Recovering) return;
        self._state.BeginRecovery(true); // Mark before StopConnection can invoke callbacks.
        self.HideNotice();
        if (self._manager != null)
        {
            // Stopping a host is allowed only for its explicit local Return action.
            if (self._manager.IsServerStarted) self._manager.ServerManager.StopConnection(true);
            self._manager.ClientManager.StopConnection();
        }
        self.StartCoroutine(self.ReturnLocally());
    }

    private IEnumerator ReturnLocally()
    {
        // Let FishNet finish its stop callback and any already-started scene operation.
        yield return null;
        while (_manager != null && _queueBusy) yield return null;
        if (_manager != null && _manager.IsServerStarted)
        {
            _state.CompleteRecovery();
            yield break; // Never tear down an active host in the unexpected-loss path.
        }
        GameObject oldManager = _manager == null ? null : _manager.gameObject;
        Unbind();
        // MainMenu contains its own manager. Remove the old one first, including stale
        // FishNet scene/session queues, so persistence rules cannot destroy the fresh one.
        if (oldManager != null) Destroy(oldManager);
        yield return null;
        LocalLobbySession.Clear();
        Time.timeScale = 1f;
        RestoreCursor();
        yield return SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
        _state.CompleteRecovery();
        RestoreCursor();
        ShowNotice();
        Debug.Log("[Disconnect] Local MainMenu recovery completed.");
    }

    private void LateUpdate()
    {
        if (_state.Recovering || _noticeUi != null) RestoreCursor();
    }
    private static void RestoreCursor() { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }

    private void ShowNotice()
    {
        if (string.IsNullOrEmpty(_state.Message)) return;
        HideNotice();
        // Runtime uGUI + TMP follows existing menus; MainMenu already owns an InputSystem EventSystem.
        _noticeUi = new GameObject("Disconnect Notice", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = _noticeUi.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;
        var scaler = _noticeUi.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        var panel = new GameObject("Message", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(_noticeUi.transform, false);
        var rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.sizeDelta = new Vector2(620, 210);
        panel.GetComponent<Image>().color = new Color(.08f, .1f, .13f, .98f);
        AddText(panel.transform, _state.Message, new Vector2(0, 28), new Vector2(570, 120), 23);
        var buttonObject = new GameObject("Dismiss", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(panel.transform, false);
        var buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(.5f, .5f);
        buttonRect.sizeDelta = new Vector2(150, 42);
        buttonRect.anchoredPosition = new Vector2(0, -66);
        buttonObject.GetComponent<Image>().color = new Color(.2f, .32f, .45f);
        var button = buttonObject.GetComponent<Button>();
        button.targetGraphic = buttonObject.GetComponent<Image>();
        button.onClick.AddListener(Dismiss);
        AddText(buttonObject.transform, "Dismiss", Vector2.zero, new Vector2(145, 40), 22);
        UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(buttonObject);
    }

    private static void AddText(Transform parent, string value, Vector2 position, Vector2 size, int fontSize)
    {
        var textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        var text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(.5f, .5f);
        text.rectTransform.anchoredPosition = position;
        text.rectTransform.sizeDelta = size;
    }
    private void Dismiss() { _state.Dismiss(); HideNotice(); }
    private void HideNotice() { if (_noticeUi != null) Destroy(_noticeUi); _noticeUi = null; }
    private void Unbind()
    {
        if (_manager != null)
        {
            _manager.ClientManager.UnregisterBroadcast<Notice>(OnNotice);
            _manager.ClientManager.OnAuthenticated -= OnAuthenticated;
            _manager.ClientManager.OnClientConnectionState -= OnClientState;
            _manager.SceneManager.OnQueueStart -= QueueStart;
            _manager.SceneManager.OnQueueEnd -= QueueEnd;
        }
        _manager = null;
        _queueBusy = false;
    }
    private void OnApplicationQuit() { _quitting = true; _state.Dismiss(); }
    private void OnDestroy() { Unbind(); HideNotice(); if (_instance == this) _instance = null; }
}
