using FishNet;
using FishNet.Connection;
using FishNet.Managing.Timing;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using TMPro;
using UnityEngine;

public class NetworkLobbyUI : NetworkBehaviour
{
    [Header("Drag your 4 PlayerNameText objects here")]
    public TextMeshProUGUI[] playerSlots;

    [Header("Drag your 4 ConnectionIcon objects here in the same order")]
    public ConnectionBarsUI[] connectionBars = new ConnectionBarsUI[4];

    [Header("Ping display")]
    [SerializeField, Range(1000, 60000)] private int maximumReportedPing = 60000;

    [Header("Lobby capacity")]
    [SerializeField, Range(1, 16)] private int maximumPlayers = 4;

    // These two lists use matching indices so every row has a stable ClientId.
    private readonly SyncList<string> _playerNames = new SyncList<string>();
    private readonly SyncList<int> _playerIds = new SyncList<int>();

    // Only the server writes this dictionary. FishNet synchronizes it to all clients.
    private readonly SyncDictionary<int, int> _playerPings = new SyncDictionary<int, int>();

    private TimeManager _timeManager;

    private void Awake()
    {
        _playerNames.OnChange += PlayerNames_OnChange;
        _playerIds.OnChange += PlayerIds_OnChange;
        _playerPings.OnChange += PlayerPings_OnChange;
    }

    private void OnDestroy()
    {
        _playerNames.OnChange -= PlayerNames_OnChange;
        _playerIds.OnChange -= PlayerIds_OnChange;
        _playerPings.OnChange -= PlayerPings_OnChange;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        _playerNames.Clear();
        _playerIds.Clear();
        _playerPings.Clear();

        ServerManager.OnAuthenticationResult += OnAuthenticationResult;
        ServerManager.OnRemoteConnectionState += OnClientConnectionState;

        // The lobby scene may load after clients have already connected.
        // Add the local host first, followed by authenticated remote clients.
        foreach (NetworkConnection connection in ServerManager.Clients.Values)
        {
            if (connection.IsAuthenticated && connection.IsLocalClient)
                TryAddPlayer(connection);
        }

        foreach (NetworkConnection connection in ServerManager.Clients.Values)
        {
            if (connection.IsAuthenticated && !connection.IsLocalClient)
                TryAddPlayer(connection);
        }
    }

    public override void OnStopServer()
    {
        ServerManager.OnAuthenticationResult -= OnAuthenticationResult;
        ServerManager.OnRemoteConnectionState -= OnClientConnectionState;
        base.OnStopServer();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        RefreshLocalHostEntry();
        UpdateUI();

        _timeManager = InstanceFinder.TimeManager;
        if (_timeManager != null)
            _timeManager.OnRoundTripTimeUpdated += OnLocalRoundTripTimeUpdated;
    }

    public override void OnStopClient()
    {
        if (_timeManager != null)
            _timeManager.OnRoundTripTimeUpdated -= OnLocalRoundTripTimeUpdated;

        _timeManager = null;
        base.OnStopClient();
    }

    private void RefreshLocalHostEntry()
    {
        // During the server authentication callback FishNet may not yet identify
        // the local connection as the host. OnStartClient runs late enough to fix it.
        if (!InstanceFinder.IsServerStarted || InstanceFinder.ClientManager == null)
            return;

        NetworkConnection localConnection = InstanceFinder.ClientManager.Connection;
        if (localConnection == null)
            return;

        int playerIndex = _playerIds.IndexOf(localConnection.ClientId);
        if (playerIndex < 0)
            return;

        string hostName = $"<Host> Player {localConnection.ClientId}";
        if (_playerNames[playerIndex] != hostName)
            _playerNames[playerIndex] = hostName;

        _playerPings[localConnection.ClientId] = 0;
    }

    private void OnLocalRoundTripTimeUpdated(long roundTripTimeMs)
    {
        long clampedPing = System.Math.Min(
            System.Math.Max(roundTripTimeMs, 0L),
            maximumReportedPing);

        ReportPingServerRpc((int)clampedPing);
    }

    [ServerRpc(RequireOwnership = false)]
    private void ReportPingServerRpc(int pingMs, NetworkConnection sender = null)
    {
        if (sender == null)
            return;

        int playerIndex = _playerIds.IndexOf(sender.ClientId);
        if (playerIndex < 0)
            return;

        int safePing = Mathf.Clamp(pingMs, 0, maximumReportedPing);

        if (_playerPings.TryGetValue(sender.ClientId, out int oldPing) && oldPing == safePing)
            return;

        _playerPings[sender.ClientId] = safePing;
    }

    private void OnAuthenticationResult(NetworkConnection connection, bool authenticated)
    {
        if (authenticated)
            TryAddPlayer(connection);
    }

    private void TryAddPlayer(NetworkConnection connection)
    {
        if (connection == null || !connection.IsAuthenticated)
            return;

        if (_playerIds.IndexOf(connection.ClientId) >= 0)
            return;

        if (_playerIds.Count >= maximumPlayers)
            return;

        string playerName = connection.IsLocalClient
            ? $"<Host> Player {connection.ClientId}"
            : $"Player {connection.ClientId}";

        _playerNames.Add(playerName);
        _playerIds.Add(connection.ClientId);

        if (connection.IsLocalClient)
            _playerPings[connection.ClientId] = 0;
    }

    private void OnClientConnectionState(NetworkConnection connection, RemoteConnectionStateArgs args)
    {
        // Started is intentionally ignored. The authentication callback adds players.
        if (args.ConnectionState != RemoteConnectionState.Stopped)
            return;

        int playerIndex = _playerIds.IndexOf(connection.ClientId);

        if (playerIndex >= 0)
        {
            _playerNames.RemoveAt(playerIndex);
            _playerIds.RemoveAt(playerIndex);
        }

        _playerPings.Remove(connection.ClientId);
    }

    private void PlayerNames_OnChange(
        SyncListOperation operation,
        int index,
        string oldItem,
        string newItem,
        bool asServer)
    {
        UpdateUI();
    }

    private void PlayerIds_OnChange(
        SyncListOperation operation,
        int index,
        int oldItem,
        int newItem,
        bool asServer)
    {
        UpdateUI();
    }

    private void PlayerPings_OnChange(
        SyncDictionaryOperation operation,
        int clientId,
        int pingMs,
        bool asServer)
    {
        UpdateUI();
    }

    private void UpdateUI()
    {
        int playerSlotCount = playerSlots == null ? 0 : playerSlots.Length;
        int connectionBarCount = connectionBars == null ? 0 : connectionBars.Length;

        for (int i = 0; i < playerSlotCount; i++)
        {
            if (playerSlots[i] != null)
                playerSlots[i].text = "Waiting . . .";
        }

        for (int i = 0; i < connectionBarCount; i++)
        {
            if (connectionBars[i] != null)
                connectionBars[i].gameObject.SetActive(false);
        }

        int visiblePlayerCount = Mathf.Min(_playerNames.Count, playerSlotCount);

        for (int i = 0; i < visiblePlayerCount; i++)
        {
            if (playerSlots[i] != null)
                playerSlots[i].text = _playerNames[i];

            if (i >= connectionBarCount || connectionBars[i] == null)
                continue;

            ConnectionBarsUI bars = connectionBars[i];
            bars.gameObject.SetActive(true);

            if (i < _playerIds.Count &&
                _playerPings.TryGetValue(_playerIds[i], out int pingMs))
            {
                bars.SetLatencyMs(pingMs);
            }
            else
            {
                bars.SetUnavailable();
            }
        }
    }
}
