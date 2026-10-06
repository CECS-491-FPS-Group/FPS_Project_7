using System;
using System.Collections.Generic;
using FishNet;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

public class TrackPlayerCurrency : MonoBehaviour
{
    public static TrackPlayerCurrency instance { get; private set; }
    public int LocalBalance { get; private set; }
    public bool HasLocalBalance { get; private set; }

    public struct BalanceRequest : IBroadcast { }
    public struct BalanceSnapshot : IBroadcast { public int Balance; }

    private readonly Dictionary<NetworkConnection, int> _balances = new();
    private readonly Dictionary<NetworkConnection, Action<NetworkObject>> _subscriptions = new();
    private NetworkManager _networkManager;
    private bool _listening;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }
        instance = this;
    }

    private void Start()
    {
        if (instance != this) return;
        _networkManager = InstanceFinder.NetworkManager;
        if (_networkManager == null) return;
        _networkManager.ClientManager.RegisterBroadcast<BalanceSnapshot>(OnBalanceSnapshot);
        _networkManager.ServerManager.RegisterBroadcast<BalanceRequest>(OnBalanceRequest);
        _networkManager.ServerManager.OnAuthenticationResult += OnAuthenticationResult;
        _networkManager.ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
        _networkManager.ServerManager.OnServerConnectionState += OnServerConnectionState;
        _networkManager.ClientManager.OnClientConnectionState += OnClientConnectionState;
        _listening = true;

        if (_networkManager.IsServerStarted)
            foreach (NetworkConnection connection in _networkManager.ServerManager.Clients.Values)
                Subscribe(connection);
    }

    private void OnAuthenticationResult(NetworkConnection connection, bool authenticated)
    {
        if (authenticated) Subscribe(connection);
    }

    private void Subscribe(NetworkConnection connection)
    {
        if (!connection.IsActive || !connection.IsAuthenticated || _subscriptions.ContainsKey(connection)) return;
        Action<NetworkObject> handler = player => RegisterPlayer(connection, player);
        _subscriptions.Add(connection, handler);
        connection.OnObjectAdded += handler;
        foreach (NetworkObject player in connection.Objects) RegisterPlayer(connection, player);
    }

    private static bool IsGameplayPlayer(NetworkObject player)
    {
        return player != null && player.gameObject.scene.name == "RoundImplementation" &&
            player.CompareTag("Player");
    }

    private void RegisterPlayer(NetworkConnection connection, NetworkObject player)
    {
        if (!_networkManager.IsServerStarted || !connection.IsActive || !connection.IsAuthenticated ||
            !connection.Objects.Contains(player) || !IsGameplayPlayer(player) || _balances.ContainsKey(connection)) return;

        _balances.Add(connection, 0);
        Debug.Log($"[Currency] Server registered connection {connection.ClientId} with balance 0.", this);
        SendBalance(connection);
    }

    public bool RequestLocalBalance()
    {
        if (!_listening || !_networkManager.IsClientStarted ||
            !_networkManager.ClientManager.Connection.IsAuthenticated) return false;
        // The listener is already registered; the request contains no identity or amount.
        _networkManager.ClientManager.Broadcast(new BalanceRequest(), Channel.Reliable);
        return true;
    }

    private void OnBalanceRequest(NetworkConnection connection, BalanceRequest request, Channel channel)
    {
        if (!_networkManager.IsServerStarted || !connection.IsActive || !connection.IsAuthenticated) return;
        foreach (NetworkObject player in connection.Objects)
        {
            if (!IsGameplayPlayer(player)) continue;
            if (!_balances.ContainsKey(connection)) RegisterPlayer(connection, player);
            else SendBalance(connection);
            return;
        }
    }

    // Server-side API only: deliberately not exposed through an RPC or broadcast request.
    public bool AddCurrency(NetworkConnection connection, int amount)
    {
        if (_networkManager == null || !_networkManager.IsServerStarted ||
            connection == null || !connection.IsActive || !connection.IsAuthenticated ||
            !_balances.TryGetValue(connection, out int oldBalance) ||
            amount <= 0 || oldBalance > int.MaxValue - amount) return false;

        int newBalance = oldBalance + amount;
        _balances[connection] = newBalance;
        Debug.Log($"[Currency] Server connection {connection.ClientId}: {oldBalance} + {amount} = {newBalance}.", this);
        SendBalance(connection);
        return true;
    }

    // Server-side only; membership is sampled when the payout occurs.
    public void AddCurrencyToRegisteredPlayers(int amount)
    {
        if (_networkManager == null || !_networkManager.IsServerStarted) return;

        // AddCurrency updates dictionary values, so iterate a snapshot of its keys.
        var connections = new List<NetworkConnection>(_balances.Keys);
        foreach (NetworkConnection connection in connections)
            AddCurrency(connection, amount);
    }

    private void SendBalance(NetworkConnection connection)
    {
        _networkManager.ServerManager.Broadcast(connection,
            new BalanceSnapshot { Balance = _balances[connection] }, true, Channel.Reliable);
    }

    private void OnBalanceSnapshot(BalanceSnapshot snapshot, Channel channel)
    {
        bool changed = !HasLocalBalance || LocalBalance != snapshot.Balance;
        LocalBalance = snapshot.Balance;
        HasLocalBalance = true;
        if (changed)
            Debug.Log($"[Currency] Client {_networkManager.ClientManager.Connection.ClientId} received balance {LocalBalance}.", this);
    }

    private void OnRemoteConnectionState(NetworkConnection connection, RemoteConnectionStateArgs args)
    {
        if (args.ConnectionState != RemoteConnectionState.Stopped) return;
        if (_subscriptions.TryGetValue(connection, out Action<NetworkObject> handler))
        {
            connection.OnObjectAdded -= handler;
            _subscriptions.Remove(connection);
        }
        _balances.Remove(connection);
    }

    private void OnServerConnectionState(ServerConnectionStateArgs args)
    {
        if (args.ConnectionState == LocalConnectionState.Stopped) ClearServerState();
    }

    private void ClearServerState()
    {
        foreach (var subscription in _subscriptions)
            subscription.Key.OnObjectAdded -= subscription.Value;
        _subscriptions.Clear();
        _balances.Clear();
    }

    private void OnClientConnectionState(ClientConnectionStateArgs args)
    {
        if (args.ConnectionState != LocalConnectionState.Stopped) return;
        LocalBalance = 0;
        HasLocalBalance = false;
    }

    private void OnDestroy()
    {
        if (_listening && _networkManager != null)
        {
            _networkManager.ClientManager.UnregisterBroadcast<BalanceSnapshot>(OnBalanceSnapshot);
            _networkManager.ServerManager.UnregisterBroadcast<BalanceRequest>(OnBalanceRequest);
            _networkManager.ServerManager.OnAuthenticationResult -= OnAuthenticationResult;
            _networkManager.ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;
            _networkManager.ServerManager.OnServerConnectionState -= OnServerConnectionState;
            _networkManager.ClientManager.OnClientConnectionState -= OnClientConnectionState;
        }
        ClearServerState();
        if (instance == this) instance = null;
    }
}
