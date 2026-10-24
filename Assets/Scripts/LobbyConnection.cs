using FishNet;
using FishNet.Managing.Scened;
using FishNet.Transporting;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Globalization;
using System.Net;
using System.Net.Sockets;

[DisallowMultipleComponent]
public sealed class LobbyConnection : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("This was previously the IP field. It is now the 6-character lobby-code field.")]
    public TMP_InputField ipInputField;
    [SerializeField] private TMP_InputField lanHostInputField;
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

        if (statusText == null && lanHostInputField != null)
        {
            statusText = Instantiate(lanHostInputField.textComponent, lanHostInputField.transform.parent);
            statusText.name = "ConnectionStatus";
            statusText.text = string.Empty;
            statusText.fontSize = 14f;
            statusText.color = Color.white;
            statusText.raycastTarget = false;
            statusText.alignment = TextAlignmentOptions.Center;
            RectTransform rect = statusText.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, -275f);
            rect.sizeDelta = new Vector2(440f, 52f);
        }

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
        string endpoint = lanHostInputField == null ? string.Empty : lanHostInputField.text.Trim();
        if (!string.IsNullOrEmpty(endpoint))
        {
            SubscribeToNetworkEvents();
            if (!_subscribed || !CanStartConnection()) return;
            if (!TryParseLanEndpoint(endpoint, out string address, out ushort port))
            {
                SetStatus("Enter an IPv4 address, optionally followed by :port (1-65535).");
                return;
            }
            BeginJoin(new LocalLobbyRecord { address = address, port = port, code = string.Empty });
            return;
        }
        JoinByCode(ipInputField == null ? string.Empty : ipInputField.text);
    }

    private static bool TryParseLanEndpoint(string input, out string address, out ushort port)
    {
        address = string.Empty;
        port = 7770;
        string[] parts = input.Trim().Split(':');
        if (parts.Length > 2 || (parts.Length == 2 &&
            (!ushort.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out port) || port == 0))) return false;
        string[] octets = parts[0].Split('.');
        if (octets.Length != 4) return false;
        foreach (string octet in octets)
            if (octet.Length == 0 || octet.Length > 3 ||
                (octet.Length > 1 && octet[0] == '0') ||
                !byte.TryParse(octet, NumberStyles.None, CultureInfo.InvariantCulture, out _)) return false;
        if (!IPAddress.TryParse(parts[0], out IPAddress ip) || ip.AddressFamily != AddressFamily.InterNetwork) return false;
        byte[] bytes = ip.GetAddressBytes();
        if (bytes[0] == 0 || bytes[0] >= 224) return false;
        address = ip.ToString();
        return true;
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

        BeginJoin(room);
    }

    private void BeginJoin(LocalLobbyRecord room)
    {
        LocalLobbySession.BeginJoining(room);

        _joiningClient = true;
        SetStatus(string.IsNullOrEmpty(room.code) ? $"Connecting to {room.address}:{room.port}..." : $"Joining room {room.code}...");

        if (!InstanceFinder.ClientManager.StartConnection(room.address, (ushort)room.port))
        {
            _joiningClient = false;
            LocalLobbySession.Clear();
            SetStatus("FishNet could not begin the connection attempt.");
        }
    }

    public void JoinDiscoveredRoom(LocalLobbyRecord room)
    {
        SubscribeToNetworkEvents();
        if (!_subscribed || !CanStartConnection() || room == null) return;
        if (!TryParseLanEndpoint(room.address + ":" + room.port, out string address, out ushort port))
        {
            SetStatus("Invalid discovered lobby endpoint.");
            return;
        }
        LocalLobbyRecord target = room.Copy();
        target.address = address;
        target.port = port;
        BeginJoin(target);
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

            var transport = InstanceFinder.NetworkManager.TransportManager.Transport;
            string bind = transport.GetServerBindAddress(IPAddressType.IPv4);
            Debug.Log($"[LAN] Host listening on {(string.IsNullOrEmpty(bind) ? "0.0.0.0 (all IPv4 interfaces)" : bind)}:{transport.GetPort()}. Use this PC's LAN IPv4 address on remote clients.");

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

            SetStatus($"Room {LocalLobbySession.LobbyCode} created. Port: {LocalLobbySession.Port}.");

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
                SetStatus(string.IsNullOrEmpty(LocalLobbySession.LobbyCode)
                    ? $"Connected to {LocalLobbySession.Address}:{LocalLobbySession.Port}."
                    : $"Connected to room {LocalLobbySession.LobbyCode}.");
        }
        else if (args.ConnectionState == LocalConnectionState.Stopped && _joiningClient)
        {
            _joiningClient = false;
            string failedCode = LocalLobbySession.LobbyCode;
            string failedEndpoint = $"{LocalLobbySession.Address}:{LocalLobbySession.Port}";
            LocalLobbySession.Clear();
            SetStatus(string.IsNullOrEmpty(failedCode) ? $"Could not connect to {failedEndpoint}. Check address, port and firewall."
                : $"Could not connect to room {failedCode}. Refresh and try again.");
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
