using System.Collections;
using FishNet;
using FishNet.Transporting;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

[DisallowMultipleComponent]
// Shows a persistent rejoin prompt after an unexpected non-host disconnect.
public sealed class RejoinManager : MonoBehaviour
{
    private const string DefaultMessage =
        "It seems like you may have been disconnected from your last match. " +
        "Would you like to rejoin?";

    private static RejoinManager _activeInstance;

    [Header("UI References")]
    [SerializeField] private GameObject rejoinOverlay;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button rejoinButton;

    [Header("Return Destination")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Reconnect")]
    [SerializeField, Min(1f)] private float reconnectTimeoutSeconds = 8f;

    private bool _subscribed;
    private bool _reconnectAttemptInProgress;
    private bool _applicationQuitting;
    private Coroutine _timeoutRoutine;
    private GameObject _temporaryEventSystem;

    public static bool CanHandleDisconnect =>
        _activeInstance != null &&
        _activeInstance.isActiveAndEnabled &&
        _activeInstance.rejoinOverlay != null;

    private void Awake()
    {
        if (_activeInstance != null && _activeInstance != this)
        {
            enabled = false;
            return;
        }

        _activeInstance = this;

        if (cancelButton != null)
            cancelButton.onClick.AddListener(CancelRejoin);

        if (rejoinButton != null)
            rejoinButton.onClick.AddListener(Rejoin);

        HideOverlay();
    }

    private void OnEnable()
    {
        SubscribeToClientState();
    }

    private void Start()
    {
        // Start is a second chance if FishNet initializes after OnEnable.
        SubscribeToClientState();
    }

    private void OnDisable()
    {
        UnsubscribeFromClientState();
    }

    private void OnApplicationQuit()
    {
        _applicationQuitting = true;
    }

    private void OnDestroy()
    {
        if (cancelButton != null)
            cancelButton.onClick.RemoveListener(CancelRejoin);

        if (rejoinButton != null)
            rejoinButton.onClick.RemoveListener(Rejoin);

        if (_activeInstance == this)
            _activeInstance = null;
    }

    public void Rejoin()
    {
        if (_reconnectAttemptInProgress)
            return;

        if (!LocalLobbySession.CanReconnect)
        {
            ShowFailure("There is no previous match available to rejoin.");
            return;
        }

        if (InstanceFinder.ClientManager == null)
        {
            ShowFailure("The network client is not ready.");
            return;
        }

        if (InstanceFinder.ClientManager.Started)
        {
            HideOverlay();
            return;
        }

        _reconnectAttemptInProgress = true;
        SetButtonsInteractable(false);
        SetStatus("Rejoining match...");

        bool requestAccepted = InstanceFinder.ClientManager.StartConnection(
            LocalLobbySession.Address,
            LocalLobbySession.Port);

        if (!requestAccepted)
        {
            ShowFailure("Could not begin the rejoin attempt. Please try again.");
            return;
        }

        if (_timeoutRoutine != null)
            StopCoroutine(_timeoutRoutine);

        _timeoutRoutine = StartCoroutine(ReconnectTimeout());
    }

    public void CancelRejoin()
    {
        bool connectionMayBeRunning =
            _reconnectAttemptInProgress ||
            (InstanceFinder.ClientManager != null &&
             InstanceFinder.ClientManager.Started);

        LocalLobbySession.MarkIntentionalDisconnect();
        _reconnectAttemptInProgress = false;

        if (_timeoutRoutine != null)
        {
            StopCoroutine(_timeoutRoutine);
            _timeoutRoutine = null;
        }

        if (connectionMayBeRunning && InstanceFinder.ClientManager != null)
            InstanceFinder.ClientManager.StopConnection();

        LocalLobbySession.Clear();
        HideOverlay();
        LobbyReturnController.RequestHostJoinAfterReturn();

        if (string.IsNullOrWhiteSpace(mainMenuSceneName))
        {
            Debug.LogError("RejoinManager needs a Main Menu Scene Name.");
            return;
        }

        SceneManager.LoadScene(mainMenuSceneName);
    }

#if UNITY_EDITOR
    [ContextMenu("TEST - Simulate Unexpected Client Disconnect")]
    private void SimulateUnexpectedClientDisconnect()
    {
        if (LocalLobbySession.IsHosting)
        {
            Debug.LogWarning("Run this test from a non-host client.");
            return;
        }

        if (InstanceFinder.ClientManager == null ||
            !InstanceFinder.ClientManager.Started)
        {
            Debug.LogWarning("The local client is not connected.");
            return;
        }

        // Do not mark this as intentional; the normal rejoin flow should open.
        InstanceFinder.ClientManager.StopConnection();
    }
#endif

    private void SubscribeToClientState()
    {
        if (_subscribed || InstanceFinder.ClientManager == null)
            return;

        InstanceFinder.ClientManager.OnClientConnectionState +=
            OnClientConnectionStateChanged;

        _subscribed = true;
    }

    private void UnsubscribeFromClientState()
    {
        if (!_subscribed)
            return;

        if (InstanceFinder.ClientManager != null)
        {
            InstanceFinder.ClientManager.OnClientConnectionState -=
                OnClientConnectionStateChanged;
        }

        _subscribed = false;
    }

    private void OnClientConnectionStateChanged(ClientConnectionStateArgs args)
    {
        if (args.ConnectionState == LocalConnectionState.Started)
        {
            LocalLobbySession.MarkConnected();
            _reconnectAttemptInProgress = false;

            if (_timeoutRoutine != null)
            {
                StopCoroutine(_timeoutRoutine);
                _timeoutRoutine = null;
            }

            HideOverlay();
            return;
        }

        if (args.ConnectionState != LocalConnectionState.Stopped ||
            _applicationQuitting ||
            LocalLobbySession.IsIntentionalDisconnect ||
            LocalLobbySession.IsHosting ||
            !LocalLobbySession.CanReconnect)
        {
            return;
        }

        bool overlayWasAlreadyVisible =
            rejoinOverlay != null && rejoinOverlay.activeSelf;
        bool attemptFailed = _reconnectAttemptInProgress;
        _reconnectAttemptInProgress = false;

        if (_timeoutRoutine != null)
        {
            StopCoroutine(_timeoutRoutine);
            _timeoutRoutine = null;
        }

        if (attemptFailed)
        {
            ShowFailure(
                "Could not rejoin. The host may be unavailable. " +
                "You can try again or cancel.");
        }
        else if (!overlayWasAlreadyVisible)
        {
            ShowOverlay();
        }
    }

    private IEnumerator ReconnectTimeout()
    {
        float deadline = Time.realtimeSinceStartup + reconnectTimeoutSeconds;

        while (_reconnectAttemptInProgress &&
               InstanceFinder.ClientManager != null &&
               !InstanceFinder.ClientManager.Started &&
               Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        _timeoutRoutine = null;

        if (!_reconnectAttemptInProgress ||
            InstanceFinder.ClientManager == null ||
            InstanceFinder.ClientManager.Started)
        {
            yield break;
        }

        _reconnectAttemptInProgress = false;
        InstanceFinder.ClientManager.StopConnection();
        ShowFailure(
            "The rejoin attempt timed out. The host may be unavailable.");
    }

    private void ShowOverlay()
    {
        // The FPS controller locks and hides the cursor during gameplay.
        // Unlock it so the player can click the rejoin popup after disconnecting.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        EnsureEventSystem();

        if (messageText != null)
            messageText.text = DefaultMessage;

        SetStatus(string.Empty);
        SetButtonsInteractable(true);

        if (rejoinOverlay != null)
            rejoinOverlay.SetActive(true);
    }

    private void HideOverlay()
    {
        if (rejoinOverlay != null)
            rejoinOverlay.SetActive(false);

        RemoveTemporaryEventSystem();
    }

    private void ShowFailure(string message)
    {
        // Keep the cursor available while the popup is displaying an error.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        EnsureEventSystem();
        _reconnectAttemptInProgress = false;
        SetStatus(message);
        SetButtonsInteractable(true);

        if (rejoinOverlay != null)
            rejoinOverlay.SetActive(true);
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (cancelButton != null)
            cancelButton.interactable = interactable;

        if (rejoinButton != null)
            rejoinButton.interactable = interactable;
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }

    private void EnsureEventSystem()
    {
        // Some gameplay scenes do not contain menu UI, so their EventSystem may
        // be missing when the persistent rejoin canvas appears. Create a small
        // temporary one only when it is needed for the popup buttons.
        if (EventSystem.current != null || _temporaryEventSystem != null)
            return;

        _temporaryEventSystem = new GameObject("Temporary Rejoin EventSystem");
        _temporaryEventSystem.transform.SetParent(transform, false);
        _temporaryEventSystem.AddComponent<EventSystem>();

#if ENABLE_INPUT_SYSTEM
        _temporaryEventSystem.AddComponent<InputSystemUIInputModule>();
#else
        _temporaryEventSystem.AddComponent<StandaloneInputModule>();
#endif
    }

    private void RemoveTemporaryEventSystem()
    {
        if (_temporaryEventSystem == null)
            return;

        Destroy(_temporaryEventSystem);
        _temporaryEventSystem = null;
    }
}
