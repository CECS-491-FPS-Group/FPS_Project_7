using System;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using FishNet;
using FishNet.Managing;
using FishNet.Managing.Scened;
using FishNet.Transporting;
using FishNet.Transporting.Multipass;
using FishNet.Transporting.Tugboat;
using FishNet.Transporting.UTP;
using TMPro;
using Unity.Services.Core;
using Unity.Services.Relay;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class LobbyConnection : MonoBehaviour
{
    public TMP_InputField ipInputField;
    [SerializeField] private TMP_InputField lanHostInputField;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private GameObject browserButton, browserOverlay, hostOptions, visibilityGroup;
    [SerializeField] private Toggle publicLobbyToggle;
    [SerializeField] private TMP_Text lobbyVisibilityText;
    [SerializeField] private bool listCreatedRoom = true;
    [SerializeField, Min(1)] private int maximumPlayers = 4;
    [SerializeField] private string roomName = "";

    private enum Phase { Idle, Preparing, SigningIn, RelayAllocation, RelayLookup, Server, RelayBind, Client, Authenticating }
    private Phase _phase;
    private int _generation;
    private float _deadline;
    private bool _internet, _host;
    private GameObject _attemptView;
    private Button _lanButton, _internetButton;
    private NetworkManager _manager;
    private Multipass _multipass;
    private LocalLobbyPublisher _publisher;
    private RelayHostBinding _binding;
    private Transform Controls => ipInputField.transform.parent;
    private int Index => _internet ? 1 : 0;

    private void Start()
    {
        _manager = InstanceFinder.NetworkManager;
        if (_manager != null)
        {
            _multipass = _manager.TransportManager.Transport as Multipass;
            _manager.ServerManager.OnServerConnectionState += OnServerState;
            _manager.ClientManager.OnClientConnectionState += OnClientState;
            _manager.ClientManager.OnAuthenticated += OnAuthenticated;
        }

        _lanButton = IntegrationUi.Button(Controls, "LAN", new Vector2(-74, 145), () => SelectInternet(false), new Vector2(136, 32));
        _internetButton = IntegrationUi.Button(Controls, "INTERNET", new Vector2(74, 145), () => SelectInternet(true), new Vector2(136, 32));
        if (statusText == null)
            statusText = IntegrationUi.Text(Controls.parent, "", new Vector2(0, -275), new Vector2(540, 52), 16);
        if (publicLobbyToggle != null)
            publicLobbyToggle.onValueChanged.AddListener(OnPublicLobbyToggleChanged);
        SelectInternet(false);
    }

    private void OnDestroy()
    {
        CancelAttempt("Menu closed.");
        if (_manager != null)
        {
            _manager.ServerManager.OnServerConnectionState -= OnServerState;
            _manager.ClientManager.OnClientConnectionState -= OnClientState;
            _manager.ClientManager.OnAuthenticated -= OnAuthenticated;
        }
        if (publicLobbyToggle != null)
            publicLobbyToggle.onValueChanged.RemoveListener(OnPublicLobbyToggleChanged);
    }

    private void SelectInternet(bool internet)
    {
        CancelAttempt("Connection cancelled.");
        if (_internet != internet) ipInputField.SetTextWithoutNotify("");
        _internet = internet;
        ipInputField.characterLimit = internet ? RelayLobby.MaxCodeLength : LobbyCodeUtility.CodeLength;
        lanHostInputField.gameObject.SetActive(!internet);
        browserButton.SetActive(!internet);
        visibilityGroup.SetActive(!internet);
        _lanButton.interactable = internet;
        _internetButton.interactable = !internet;
        RefreshLobbyVisibilityUI();
        statusText.text = internet ? "Internet rooms are invite-only." : "";
    }

    private bool Begin(bool host)
    {
        if (_phase != Phase.Idle || ClientDisconnectRecovery.InProgress) return false;
        if (_multipass == null || _multipass.Transports.Count != 2 ||
            !(_multipass.Transports[0] is Tugboat) || !(_multipass.Transports[1] is FishyUnityTransport))
        {
            SetStatus("MainMenu transport configuration is missing.");
            return false;
        }
        foreach (var child in _multipass.Transports)
            if (child.GetConnectionState(true) != LocalConnectionState.Stopped ||
                child.GetConnectionState(false) != LocalConnectionState.Stopped)
            {
                SetStatus("Transport is still busy. Try again in a moment.");
                return false;
            }

        _generation++;
        _host = host;
        _attemptView = host ? hostOptions : browserOverlay.activeInHierarchy ? browserOverlay : Controls.gameObject;
        ClientDisconnectRecovery.ClearForAttempt();
        _multipass.SetClientTransport(Index);
        SetPhase(Phase.Preparing);
        return true;
    }

    private void SetPhase(Phase phase)
    {
        _phase = phase;
        _deadline = Time.realtimeSinceStartup + 35f;
    }

    private void Update()
    {
        if (_phase == Phase.Idle) return;
        if (!_attemptView.activeInHierarchy) CancelAttempt("Connection cancelled.");
        else if (Time.realtimeSinceStartup >= _deadline) CancelAttempt($"{_phase} timed out. Try again.");
        else if (_phase == Phase.RelayBind && _binding.Established)
        {
            ReleaseBinding();
            SetStatus("Relay confirmed the host DTLS bind.");
            StartClient();
        }
    }

    public void StartHost()
    {
        if (!Begin(true)) return;
        if (_internet) { PrepareInternet(null); return; }
        if (!LocalLobbyRegistry.TryReserveRoom(roomName, IsPublicLobbySelected(), maximumPlayers, out var room, out string error))
        {
            CancelAttempt(error);
            return;
        }
        _publisher = _manager.GetComponent<LocalLobbyPublisher>() ?? _manager.gameObject.AddComponent<LocalLobbyPublisher>();
        _publisher.PrepareRoom(room);
        LocalLobbySession.BeginHosting(room);
        ConfigureLan(room);
        StartServer();
    }

    public void JoinGame()
    {
        string endpoint = lanHostInputField.text.Trim();
        if (_internet || endpoint.Length == 0) { JoinByCode(ipInputField.text); return; }
        if (!Begin(false)) return;
        if (!TryParseLanEndpoint(endpoint, out string address, out ushort port))
        {
            CancelAttempt("Enter an IPv4 address, optionally followed by :port (1-65535).");
            return;
        }
        JoinLan(new LocalLobbyRecord { address = address, port = port, code = string.Empty });
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
        if (!Begin(false)) return;
        if (_internet)
        {
            string code = (enteredCode ?? "").Trim().ToUpperInvariant();
            if (!RelayLobby.ValidCode(code)) { CancelAttempt("Enter the 6–12 character Internet invite code."); return; }
            PrepareInternet(code);
            return;
        }
        string localCode = LobbyCodeUtility.Normalize(enteredCode);
        if (!LocalLobbyRegistry.TryFindOpenRoom(localCode, out var room, out string error))
        {
            CancelAttempt(error);
            return;
        }
        ipInputField.SetTextWithoutNotify(localCode);
        JoinLan(room);
    }

    public void JoinDiscoveredRoom(LocalLobbyRecord room)
    {
        if (_internet || room == null || !Begin(false)) return;
        if (!TryParseLanEndpoint(room.address + ":" + room.port, out string address, out ushort port))
        {
            CancelAttempt("Invalid discovered lobby endpoint.");
            return;
        }
        var target = room.Copy();
        target.address = address;
        target.port = port;
        JoinLan(target);
    }

    private void ConfigureLan(LocalLobbyRecord room)
    {
        var tugboat = (Tugboat)_multipass.Transports[0];
        tugboat.SetPort((ushort)room.port);
        tugboat.SetClientAddress(room.address);
    }

    private void JoinLan(LocalLobbyRecord room)
    {
        LocalLobbySession.BeginJoining(room);
        ConfigureLan(room);
        StartClient();
    }

    private async void PrepareInternet(string code)
    {
        int generation = _generation;
        void CheckAttempt()
        {
            if (this == null || generation != _generation ||
                !_attemptView.activeInHierarchy) throw new OperationCanceledException();
        }
        try
        {
            SetPhase(Phase.SigningIn);
            SetStatus("UGS initialization and anonymous sign-in started.");
            await RelayLobby.Initialize();
            CheckAttempt();
            SetPhase(_host ? Phase.RelayAllocation : Phase.RelayLookup);
            SetStatus(_host ? "UGS signed in. Requesting Relay allocation." : "UGS signed in. Looking up Relay invite.");
            var transport = (FishyUnityTransport)_multipass.Transports[1];
            if (_host)
                code = await RelayLobby.ConfigureHost(transport, maximumPlayers - 1, CheckAttempt);
            else
                await RelayLobby.ConfigureClient(transport, code, CheckAttempt);
            CheckAttempt();
            LocalLobbySession.BeginInternet(code, _host);
            SetStatus(_host ? "Relay allocation configured (DTLS)." : "Relay lookup succeeded (DTLS).");
            if (_host) StartRelayServer(transport); else StartClient();
        }
        catch (Exception error)
        {
            if (this == null || generation != _generation) return;
            string reason = error is RelayServiceException { Reason: RelayExceptionReason.JoinCodeNotFound }
                ? "Invite not found or expired (Relay JoinCodeNotFound). Ask the host for the current code."
                : error is RelayServiceException relay ? $"Relay {relay.Reason} ({relay.ErrorCode}). Try again."
                : error is RequestFailedException service ? $"Unity Services request failed ({service.ErrorCode}). Try again."
                : error is OperationCanceledException ? "Connection cancelled."
                : $"Internet setup failed ({error.GetType().Name}). Try again.";
            CancelAttempt(reason);
        }
    }

    private void StartRelayServer(FishyUnityTransport transport)
    {
        _binding = new RelayHostBinding(transport);
        StartServer();
    }

    private void ReleaseBinding()
    {
        _binding?.Dispose();
        _binding = null;
    }

    private void StartServer()
    {
        SetPhase(Phase.Server);
        SetStatus("Starting FishNet server transport.");
        if (!_multipass.StartConnection(true, Index)) CancelAttempt("Server transport failed to start.");
    }

    private void OnServerState(ServerConnectionStateArgs args)
    {
        if (_phase == Phase.Idle || !_host || args.TransportIndex != Index) return;
        if (args.ConnectionState == LocalConnectionState.Stopped) { CancelAttempt("Server transport stopped."); return; }
        if (_phase != Phase.Server || args.ConnectionState != LocalConnectionState.Started) return;
        if (_binding != null)
        {
            SetPhase(Phase.RelayBind);
            SetStatus("Server transport started. Waiting for Relay to confirm the DTLS bind.");
            return;
        }
        if (!_internet) SetStatus($"Host listening on UDP port {LocalLobbySession.Port}.");
        StartClient();
    }

    private void StartClient()
    {
        SetPhase(Phase.Client);
        string target = _internet ? (_host ? "in-process" : "Relay DTLS") : $"{LocalLobbySession.Address}:{LocalLobbySession.Port}";
        SetStatus($"Starting FishNet client ({target}).");
        if (!_manager.ClientManager.StartConnection()) CancelAttempt("Client transport failed to start.");
    }

    private void OnClientState(ClientConnectionStateArgs args)
    {
        if (_phase == Phase.Idle || args.TransportIndex != Index) return;
        if (args.ConnectionState == LocalConnectionState.Stopped) CancelAttempt("Client transport stopped before authentication.");
        else if (args.ConnectionState == LocalConnectionState.Started)
        {
            SetPhase(Phase.Authenticating);
            SetStatus("Client transport connected. Waiting for FishNet authentication.");
        }
    }

    private void OnAuthenticated()
    {
        if (_phase != Phase.Authenticating) return;
        if (!_attemptView.activeInHierarchy) { CancelAttempt("Connection cancelled."); return; }
        _phase = Phase.Idle;
        _generation++;
        SetStatus("FishNet authenticated. Entering lobby.");
        if (_host)
            _manager.SceneManager.LoadGlobalScenes(new SceneLoadData("LobbyScene_v1") { ReplaceScenes = ReplaceOption.All });
    }

    private void CancelAttempt(string message)
    {
        if (_phase == Phase.Idle) return;
        _phase = Phase.Idle;
        _generation++;
        ReleaseBinding();
        var child = _multipass.Transports[Index];
        if (child.GetConnectionState(false) != LocalConnectionState.Stopped) _manager.ClientManager.StopConnection();
        if (_host && child.GetConnectionState(true) != LocalConnectionState.Stopped) _multipass.StopServerConnection(false, Index);
        _publisher?.CancelRoom();
        LocalLobbySession.Clear();
        SetStatus(message);
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

    private void RefreshLobbyVisibilityUI()
    {
        if (lobbyVisibilityText == null) return;
        lobbyVisibilityText.text = _internet ? "INTERNET LOBBY - invite by code"
            : IsPublicLobbySelected() ? "PUBLIC LOBBY - visible in room list" : "PRIVATE LOBBY - join by code only";
    }

    private void SetStatus(string message)
    {
        if (statusText != null) statusText.text = message;
        Debug.Log($"[Lobby {(_internet ? "Internet" : "LAN")}] {DateTime.UtcNow:HH:mm:ss.fff}Z {message}");
    }
}
