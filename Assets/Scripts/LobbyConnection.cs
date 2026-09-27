using FishNet;
using FishNet.Managing.Scened;
using FishNet.Transporting;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class LobbyConnection : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("This was previously the IP field. It is now the 6-character lobby-code field.")]
    public TMP_InputField ipInputField;
    [SerializeField] private TMP_Text statusText;

    [Header("Room visibility")]
    [Tooltip("Checked creates a public room. Unchecked creates a private room.")]
    [SerializeField] private Toggle publicLobbyToggle;
    [Tooltip("Optional text that explains the current Public or Private selection.")]
    [SerializeField] private TMP_Text lobbyVisibilityText;
    [Tooltip("Fallback used only when Public Lobby Toggle is not assigned.")]
    [SerializeField] private bool listCreatedRoom = true;

    [Header("Local room test")]
    [SerializeField, Min(1)] private int maximumPlayers = 4;
    [SerializeField] private string roomName = "";

    private bool _subscribed;
    private bool _waitingForHostServer;
    private bool _joiningClient;
    private bool _lobbySceneLoadRequested;
    private LocalLobbyPublisher _publisher;

    private void Start()
    {
        // Start runs after every object's Awake, so FishNet's managers are ready.
        SubscribeToNetworkEvents();

        if (publicLobbyToggle != null)
            publicLobbyToggle.onValueChanged.AddListener(OnPublicLobbyToggleChanged);

        RefreshLobbyVisibilityUI();
    }

    private void OnDestroy()
    {
        if (publicLobbyToggle != null)
            publicLobbyToggle.onValueChanged.RemoveListener(OnPublicLobbyToggleChanged);

        UnsubscribeFromNetworkEvents();
    }

    public void StartHost()
    {
        SubscribeToNetworkEvents();

        if (!_subscribed)
            return;

        if (!CanStartConnection())
            return;

        bool createPublicRoom = IsPublicLobbySelected();

        if (!LocalLobbyRegistry.TryReserveRoom(
                roomName,
                createPublicRoom,
                maximumPlayers,
                out LocalLobbyRecord room,
                out string error))
        {
            SetStatus(error);
            return;
        }

        LocalLobbySession.BeginHosting(room);

        _publisher = GetOrCreatePublisher();
        _publisher.PrepareRoom(room);

        SetLobbyVisibilityInteractable(false);
        _waitingForHostServer = true;
        _lobbySceneLoadRequested = false;
        string visibilityName = createPublicRoom ? "public" : "private";
        SetStatus($"Creating {visibilityName} room {room.code}...");

        // Start only the server here. The local host client starts after the
        // server confirms that its unique port was opened successfully.
        if (!InstanceFinder.ServerManager.StartConnection((ushort)room.port))
        {
            _waitingForHostServer = false;
            _publisher.CancelRoom();
            SetLobbyVisibilityInteractable(true);
            SetStatus("FishNet could not start the local server.");
        }
    }

    public void JoinGame()
    {
        JoinByCode(ipInputField == null ? string.Empty : ipInputField.text);
    }

    public void JoinByCode(string enteredCode)
    {
        SubscribeToNetworkEvents();

        if (!_subscribed)
            return;

        if (!CanStartConnection())
            return;

        string normalizedCode = LobbyCodeUtility.Normalize(enteredCode);

        if (!LocalLobbyRegistry.TryFindOpenRoom(
                normalizedCode,
                out LocalLobbyRecord room,
                out string error))
        {
            SetStatus(error);
            return;
        }

        if (ipInputField != null)
            ipInputField.SetTextWithoutNotify(normalizedCode);

        LocalLobbySession.BeginJoining(room);

        _joiningClient = true;
        SetStatus($"Joining room {room.code}...");

        if (!InstanceFinder.ClientManager.StartConnection(room.address, (ushort)room.port))
        {
            _joiningClient = false;
            LocalLobbySession.Clear();
            SetStatus("FishNet could not begin the connection attempt.");
        }
    }

    private void SubscribeToNetworkEvents()
    {
        if (_subscribed)
            return;

        if (InstanceFinder.ServerManager == null || InstanceFinder.ClientManager == null)
        {
            SetStatus("FishNet NetworkManager is not ready.");
            return;
        }

        InstanceFinder.ServerManager.OnServerConnectionState += OnServerStateChanged;
        InstanceFinder.ClientManager.OnClientConnectionState += OnClientStateChanged;
        _subscribed = true;
    }

    private void UnsubscribeFromNetworkEvents()
    {
        if (!_subscribed)
            return;

        if (InstanceFinder.ServerManager != null)
            InstanceFinder.ServerManager.OnServerConnectionState -= OnServerStateChanged;

        if (InstanceFinder.ClientManager != null)
            InstanceFinder.ClientManager.OnClientConnectionState -= OnClientStateChanged;

        _subscribed = false;
    }

    private void OnServerStateChanged(ServerConnectionStateArgs args)
    {
        if (!_waitingForHostServer)
            return;

        if (args.ConnectionState == LocalConnectionState.Started)
        {
            _waitingForHostServer = false;

            // The server owns this port now, so it is safe to connect its host client.
            bool hostClientRequestAccepted = InstanceFinder.ClientManager.StartConnection(
                LocalLobbySession.Address,
                LocalLobbySession.Port);

            if (!hostClientRequestAccepted)
            {
                InstanceFinder.ServerManager.StopConnection(true);

                if (_publisher != null)
                    _publisher.CancelRoom();

                SetLobbyVisibilityInteractable(true);
                SetStatus("The server started, but its host player could not connect.");
                return;
            }

            SetStatus($"Room {LocalLobbySession.LobbyCode} created.");

            if (!_lobbySceneLoadRequested)
            {
                _lobbySceneLoadRequested = true;
                SceneLoadData sceneLoadData = new SceneLoadData("LobbyScene_v1");
                sceneLoadData.ReplaceScenes = ReplaceOption.All;
                InstanceFinder.SceneManager.LoadGlobalScenes(sceneLoadData);
            }
        }
        else if (args.ConnectionState == LocalConnectionState.Stopped)
        {
            _waitingForHostServer = false;

            if (_publisher != null)
                _publisher.CancelRoom();

            SetLobbyVisibilityInteractable(true);
            SetStatus("The local server stopped before the room was created.");
        }
    }

    private void OnClientStateChanged(ClientConnectionStateArgs args)
    {
        if (args.ConnectionState == LocalConnectionState.Started)
        {
            _joiningClient = false;

            if (!LocalLobbySession.IsHosting)
                SetStatus($"Connected to room {LocalLobbySession.LobbyCode}.");
        }
        else if (args.ConnectionState == LocalConnectionState.Stopped && _joiningClient)
        {
            _joiningClient = false;
            string failedCode = LocalLobbySession.LobbyCode;
            LocalLobbySession.Clear();
            SetStatus($"Could not connect to room {failedCode}. Refresh and try again.");
        }
    }

    private LocalLobbyPublisher GetOrCreatePublisher()
    {
        GameObject networkManagerObject = InstanceFinder.NetworkManager.gameObject;
        LocalLobbyPublisher publisher =
            networkManagerObject.GetComponent<LocalLobbyPublisher>();

        if (publisher == null)
            publisher = networkManagerObject.AddComponent<LocalLobbyPublisher>();

        return publisher;
    }

    private bool CanStartConnection()
    {
        if (_waitingForHostServer || _joiningClient ||
            InstanceFinder.ServerManager.Started ||
            InstanceFinder.ClientManager.Started)
        {
            SetStatus("A network connection is already running or starting.");
            return false;
        }

        return true;
    }

    public void SetLobbyPublic()
    {
        SetLobbyVisibility(true);
    }

    public void SetLobbyPrivate()
    {
        SetLobbyVisibility(false);
    }

    private bool IsPublicLobbySelected()
    {
        return publicLobbyToggle != null
            ? publicLobbyToggle.isOn
            : listCreatedRoom;
    }

    private void OnPublicLobbyToggleChanged(bool isPublic)
    {
        listCreatedRoom = isPublic;
        RefreshLobbyVisibilityUI();
    }

    private void SetLobbyVisibility(bool isPublic)
    {
        listCreatedRoom = isPublic;

        if (publicLobbyToggle != null)
            publicLobbyToggle.SetIsOnWithoutNotify(isPublic);

        RefreshLobbyVisibilityUI();
    }

    private void SetLobbyVisibilityInteractable(bool interactable)
    {
        if (publicLobbyToggle != null)
            publicLobbyToggle.interactable = interactable;
    }

    private void RefreshLobbyVisibilityUI()
    {
        if (lobbyVisibilityText == null)
            return;

        lobbyVisibilityText.text = IsPublicLobbySelected()
            ? "PUBLIC LOBBY - visible in room list"
            : "PRIVATE LOBBY - join by code only";
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;

        if (!string.IsNullOrEmpty(message))
            Debug.Log(message);
    }
}
