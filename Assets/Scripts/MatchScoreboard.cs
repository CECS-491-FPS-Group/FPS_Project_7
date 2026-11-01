using System.Collections.Generic;
using System.Text;
using FishNet;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;

// Lives on the gameplay Timer object; scene teardown resets the entire match roster.
public sealed class MatchScoreboard : MonoBehaviour
{
    public static MatchScoreboard Instance { get; private set; }
    public struct Row
    {
        public int ClientId;
        public string Name;
        public int Kills;
        public int Deaths;
    }
    public struct Snapshot : IBroadcast { public Row[] Rows; }
    public struct SnapshotRequest : IBroadcast { }

    private readonly Dictionary<string, Row> _scores = new();
    private readonly Dictionary<NetworkConnection, string> _tokensByConnection = new();
    private NetworkManager _manager;
    private RejoinIdentityManager _identityManager;
    private bool _listening;
    private bool _receivedSnapshot;
    private float _nextRequest;
    public string DisplayText { get; private set; } = "Waiting for scoreboard...";
    public string PlayerNamesText { get; private set; } = "PLAYERS\n\nWaiting...";
    public string PlayerStatsText { get; private set; } = "KILLS / DEATHS";

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private void Start()
    {
        if (Instance != this) return;
        _manager = InstanceFinder.NetworkManager;
        if (_manager == null) return;
        _manager.ClientManager.RegisterBroadcast<Snapshot>(OnSnapshot);
        _manager.ClientManager.OnClientConnectionState += OnClientConnectionState;
        _manager.ServerManager.RegisterBroadcast<SnapshotRequest>(OnRequest);
        _manager.ServerManager.OnRemoteConnectionState += OnConnectionState;
        _identityManager = _manager.GetComponent<RejoinIdentityManager>();
        if (_identityManager != null)
            _identityManager.ServerIdentityRegistered += OnServerIdentityRegistered;
        else
            Debug.LogError("[Scoreboard] RejoinIdentityManager is missing.", this);
        _listening = true;
        if (_manager.IsServerStarted)
        {
            foreach (NetworkConnection connection in _manager.ServerManager.Clients.Values) Register(connection);
            Publish();
        }
    }

    private void Update()
    {
        // Retry only initial acquisition, covering different client/server scene-load timing.
        if (!_listening || _receivedSnapshot || !_manager.IsClientStarted ||
            !_manager.ClientManager.Connection.IsAuthenticated || Time.unscaledTime < _nextRequest) return;
        _nextRequest = Time.unscaledTime + 1f;
        _manager.ClientManager.Broadcast(new SnapshotRequest(), Channel.Reliable);
    }

    private void Register(NetworkConnection connection)
    {
        if (connection == null || !connection.IsActive || !connection.IsAuthenticated ||
            _identityManager == null ||
            !_identityManager.TryGetPlayerToken(connection, out string playerToken)) return;

        Register(connection, playerToken);
    }

    private void Register(NetworkConnection connection, string playerToken)
    {
        if (connection == null || !connection.IsActive || !connection.IsAuthenticated ||
            string.IsNullOrWhiteSpace(playerToken)) return;

        _tokensByConnection[connection] = playerToken;
        if (!_scores.TryGetValue(playerToken, out Row row))
        {
            row = new Row();
        }

        row.ClientId = connection.ClientId;
        row.Name = connection.IsLocalClient
            ? $"<Host> Player {connection.ClientId}"
            : $"Player {connection.ClientId}";
        _scores[playerToken] = row;
    }

    private void OnServerIdentityRegistered(
        NetworkConnection connection,
        string playerToken,
        bool isReturningPlayer)
    {
        Register(connection, playerToken);
        Publish();
    }

    private void OnConnectionState(NetworkConnection connection, RemoteConnectionStateArgs args)
    {
        if (args.ConnectionState == RemoteConnectionState.Stopped)
            _tokensByConnection.Remove(connection);
    }

    // Called only from Health's first positive-to-zero server transition, never an RPC.
    public void RecordDeath(NetworkConnection victim, NetworkConnection killer)
    {
        if (!_listening || !_manager.IsServerStarted || victim == null ||
            !victim.IsActive || !victim.IsAuthenticated ||
            !TryGetPlayerToken(victim, out string victimToken) ||
            !_scores.TryGetValue(victimToken, out Row victimRow)) return;
        victimRow.Deaths++;
        _scores[victimToken] = victimRow;
        if (killer != null && killer != victim && killer.IsActive && killer.IsAuthenticated &&
            TryGetPlayerToken(killer, out string killerToken) &&
            _scores.TryGetValue(killerToken, out Row killerRow))
        {
            killerRow.Kills++;
            _scores[killerToken] = killerRow;
        }
        Publish();
    }

    private bool TryGetPlayerToken(NetworkConnection connection, out string playerToken)
    {
        if (connection != null &&
            _tokensByConnection.TryGetValue(connection, out playerToken))
        {
            return true;
        }

        if (_identityManager != null &&
            _identityManager.TryGetPlayerToken(connection, out playerToken))
        {
            _tokensByConnection[connection] = playerToken;
            return true;
        }

        playerToken = string.Empty;
        return false;
    }

    private Snapshot CreateSnapshot()
    {
        var rows = new List<Row>(_scores.Values);
        rows.Sort((a, b) => a.ClientId.CompareTo(b.ClientId));
        return new Snapshot { Rows = rows.ToArray() };
    }

    private void Publish()
    {
        if (_manager.IsServerStarted)
            _manager.ServerManager.Broadcast(CreateSnapshot(), true, Channel.Reliable);
    }

    private void OnRequest(NetworkConnection sender, SnapshotRequest request, Channel channel)
    {
        if (!_manager.IsServerStarted || !sender.IsActive || !sender.IsAuthenticated) return;
        _manager.ServerManager.Broadcast(sender, CreateSnapshot(), true, Channel.Reliable);
    }

    private void OnSnapshot(Snapshot snapshot, Channel channel)
    {
        var text = new StringBuilder("PLAYER                         KILLS   DEATHS\n");
        var names = new StringBuilder("PLAYERS\n\n");
        var stats = new StringBuilder("KILLS / DEATHS\n\n");
        foreach (Row row in snapshot.Rows ?? System.Array.Empty<Row>())
        {
            text.Append(row.Name).Append("     ").Append(row.Kills).Append("     ").Append(row.Deaths).Append('\n');
            names.Append(row.Name).Append('\n');
            stats.Append(row.Kills).Append(" / ").Append(row.Deaths).Append('\n');
        }
        PlayerNamesText = names.ToString();
        PlayerStatsText = stats.ToString();
        DisplayText = text.ToString();
        _receivedSnapshot = true;
    }

    private void OnClientConnectionState(ClientConnectionStateArgs args)
    {
        if (args.ConnectionState != LocalConnectionState.Stopped) return;
        _receivedSnapshot = false;
        _nextRequest = 0f;
    }

    private void OnDestroy()
    {
        if (_listening && _manager != null)
        {
            _manager.ClientManager.UnregisterBroadcast<Snapshot>(OnSnapshot);
            _manager.ClientManager.OnClientConnectionState -= OnClientConnectionState;
            _manager.ServerManager.UnregisterBroadcast<SnapshotRequest>(OnRequest);
            _manager.ServerManager.OnRemoteConnectionState -= OnConnectionState;
            if (_identityManager != null)
                _identityManager.ServerIdentityRegistered -= OnServerIdentityRegistered;
        }
        _scores.Clear();
        _tokensByConnection.Clear();
        if (Instance == this) Instance = null;
    }
}
