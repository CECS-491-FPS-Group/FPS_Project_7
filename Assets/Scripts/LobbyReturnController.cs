using System.Collections;
using FishNet;
using FishNet.Transporting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
// Safely leaves a FishNet lobby before returning to the main menu.
public sealed class LobbyReturnController : MonoBehaviour
{
    private static bool _openHostJoinAfterReturn;

    [Header("Return UI")]
    [Tooltip("The lobby Return button. If this script is on the button, it is found automatically.")]
    [SerializeField] private Button returnButton;

    [Header("Scene")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Shutdown")]
    [SerializeField, Min(0.5f)] private float shutdownTimeoutSeconds = 5f;

    private bool _subscribed;
    private bool _returning;
    private bool _menuLoadRequested;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetReturnDestination()
    {
        // Prevent a previous Editor play session from reopening Host/Join.
        _openHostJoinAfterReturn = false;
    }

    public static bool ConsumeHostJoinReturnRequest()
    {
        if (!_openHostJoinAfterReturn)
            return false;

        _openHostJoinAfterReturn = false;
        return true;
    }

    private void Awake()
    {
        if (returnButton == null)
            returnButton = GetComponent<Button>();
    }

    private void OnEnable()
    {
        if (returnButton != null)
            returnButton.onClick.AddListener(ReturnToMainMenu);

        SubscribeToClientState();
    }

    private void Start()
    {
        // OnEnable normally finds FishNet immediately. Start is a second chance
        // for scenes in which the NetworkManager finishes Awake slightly later.
        SubscribeToClientState();
    }

    private void OnDisable()
    {
        if (returnButton != null)
            returnButton.onClick.RemoveListener(ReturnToMainMenu);

        UnsubscribeFromClientState();
    }

    public void ReturnToMainMenu()
    {
        if (_returning)
            return;

        _returning = true;

        if (returnButton != null)
            returnButton.interactable = false;

        StartCoroutine(StopNetworkingAndLoadMenu());
    }

    private IEnumerator StopNetworkingAndLoadMenu()
    {
        bool wasHosting = LocalLobbySession.IsHosting ||
            (InstanceFinder.ServerManager != null &&
             InstanceFinder.ServerManager.Started);

        // Remove a hosted room immediately instead of waiting for its heartbeat
        // entry to become stale in the local registry.
        if (wasHosting)
            RemovePublishedRoom();

        // A host runs both a server and a local client. Stopping the server with
        // sendDisconnectMessage=true also tells remote clients to leave cleanly.
        if (InstanceFinder.ServerManager != null &&
            InstanceFinder.ServerManager.Started)
        {
            InstanceFinder.ServerManager.StopConnection(true);
        }

        if (InstanceFinder.ClientManager != null &&
            InstanceFinder.ClientManager.Started)
        {
            InstanceFinder.ClientManager.StopConnection();
        }

        float deadline = Time.realtimeSinceStartup + shutdownTimeoutSeconds;

        while (NetworkIsRunning() && Time.realtimeSinceStartup < deadline)
            yield return null;

        if (NetworkIsRunning())
        {
            Debug.LogWarning(
                "FishNet did not fully stop before the lobby return timeout. " +
                "Loading MainMenu after a final stop request.");

            if (InstanceFinder.ServerManager != null &&
                InstanceFinder.ServerManager.Started)
            {
                InstanceFinder.ServerManager.StopConnection(true);
            }

            if (InstanceFinder.ClientManager != null &&
                InstanceFinder.ClientManager.Started)
            {
                InstanceFinder.ClientManager.StopConnection();
            }

            yield return null;
        }

        LocalLobbySession.Clear();
        LoadMainMenu();
    }

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
        if (args.ConnectionState != LocalConnectionState.Stopped || _returning)
            return;

        // If the host closes the room, connected clients should not remain on a
        // dead lobby screen. Send them back to MainMenu automatically.
        _returning = true;

        if (returnButton != null)
            returnButton.interactable = false;

        LocalLobbySession.Clear();
        LoadMainMenu();
    }

    private void RemovePublishedRoom()
    {
        LocalLobbyPublisher publisher = null;

        if (InstanceFinder.NetworkManager != null)
        {
            publisher = InstanceFinder.NetworkManager
                .GetComponent<LocalLobbyPublisher>();
        }

        if (publisher != null)
        {
            publisher.CancelRoom();
            return;
        }

        // Fallback for a host whose publisher component was unexpectedly lost.
        if (string.IsNullOrEmpty(LocalLobbySession.LobbyCode) ||
            string.IsNullOrEmpty(LocalLobbySession.OwnerToken))
        {
            return;
        }

        LocalLobbyRegistry.TryRemoveRoom(
            LocalLobbySession.LobbyCode,
            LocalLobbySession.OwnerToken,
            out string error);

        if (!string.IsNullOrEmpty(error))
            Debug.LogWarning(error);
    }

    private static bool NetworkIsRunning()
    {
        bool serverRunning = InstanceFinder.ServerManager != null &&
            InstanceFinder.ServerManager.Started;

        bool clientRunning = InstanceFinder.ClientManager != null &&
            InstanceFinder.ClientManager.Started;

        return serverRunning || clientRunning;
    }

    private void LoadMainMenu()
    {
        if (_menuLoadRequested)
            return;

        _menuLoadRequested = true;

        if (string.IsNullOrWhiteSpace(mainMenuSceneName))
        {
            Debug.LogError(
                "LobbyReturnController needs a Main Menu Scene Name.");
            return;
        }

        // HostOrJoin is a panel inside MainMenu, not a separate Unity scene.
        // MainMenuReturnController consumes this one-time request after load.
        _openHostJoinAfterReturn = true;
        SceneManager.LoadScene(mainMenuSceneName);
    }
}
