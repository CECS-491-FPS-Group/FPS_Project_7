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

    private readonly Dictionary<NetworkConnection, Row> _scores = new();
    private NetworkManager _manager;
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
        _manager.ServerManager.RegisterBroadcast<SnapshotRequest>(OnRequest);
        _manager.ServerManager.OnAuthenticationResult += OnAuthentication;
        _manager.ServerManager.OnRemoteConnectionState += OnConnectionState;
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
        if (!connection.IsActive || !connection.IsAuthenticated || _scores.ContainsKey(connection)) return;
        _scores.Add(connection, new Row
        {
            ClientId = connection.ClientId,
            Name = connection.IsLocalClient ? $"<Host> Player {connection.ClientId}" : $"Player {connection.ClientId}"
        });
    }

    private void OnAuthentication(NetworkConnection connection, bool authenticated)
    {
        if (!authenticated) return;
        Register(connection);
        Publish();
    }

    private void OnConnectionState(NetworkConnection connection, RemoteConnectionStateArgs args)
    {
        if (args.ConnectionState == RemoteConnectionState.Stopped && _scores.Remove(connection)) Publish();
    }

    // Called only from Health's first positive-to-zero server transition, never an RPC.
    public void RecordDeath(NetworkConnection victim, NetworkConnection killer)
    {
        if (!_listening || !_manager.IsServerStarted || victim == null ||
            !victim.IsActive || !victim.IsAuthenticated || !_scores.TryGetValue(victim, out Row victimRow)) return;
        victimRow.Deaths++;
        _scores[victim] = victimRow;
        if (killer != null && killer != victim && killer.IsActive && killer.IsAuthenticated &&
            _scores.TryGetValue(killer, out Row killerRow))
        {
            killerRow.Kills++;
            _scores[killer] = killerRow;
        }
        Publish();
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

    private void OnDestroy()
    {
        if (_listening && _manager != null)
        {
            _manager.ClientManager.UnregisterBroadcast<Snapshot>(OnSnapshot);
            _manager.ServerManager.UnregisterBroadcast<SnapshotRequest>(OnRequest);
            _manager.ServerManager.OnAuthenticationResult -= OnAuthentication;
            _manager.ServerManager.OnRemoteConnectionState -= OnConnectionState;
        }
        _scores.Clear();
        if (Instance == this) Instance = null;
    }
}
