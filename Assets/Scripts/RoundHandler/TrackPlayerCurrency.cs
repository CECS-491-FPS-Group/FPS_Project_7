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

    private readonly Dictionary<string, int> _balances = new();
    private readonly Dictionary<NetworkConnection, string> _tokensByConnection = new();
    private readonly Dictionary<NetworkConnection, Action<NetworkObject>> _subscriptions = new();
    private NetworkManager _networkManager;
    private RejoinIdentityManager _identityManager;
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
        _identityManager = _networkManager.GetComponent<RejoinIdentityManager>();
        if (_identityManager != null)
            _identityManager.ServerIdentityRegistered += OnServerIdentityRegistered;
        else
            Debug.LogError("[Currency] RejoinIdentityManager is missing.", this);
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
        if (!connection.IsActive || !connection.IsAuthenticated) return;
        if (!_subscriptions.ContainsKey(connection))
        {
            Action<NetworkObject> handler = player => RegisterPlayer(connection, player);
            _subscriptions.Add(connection, handler);
            connection.OnObjectAdded += handler;
        }
        foreach (NetworkObject player in connection.Objects) RegisterPlayer(connection, player);
    }

    private void OnServerIdentityRegistered(
        NetworkConnection connection,
        string playerToken,
        bool isReturningPlayer)
    {
        if (connection == null || string.IsNullOrWhiteSpace(playerToken)) return;
        _tokensByConnection[connection] = playerToken;
        if (!_balances.ContainsKey(playerToken))
            _balances.Add(playerToken, 0);
        Subscribe(connection);
    }

    private static bool IsGameplayPlayer(NetworkObject player)
    {
        return player != null && player.gameObject.scene.name == "RoundImplementation" &&
            player.CompareTag("Player");
    }

    private void RegisterPlayer(NetworkConnection connection, NetworkObject player)
    {
        if (!_networkManager.IsServerStarted || !connection.IsActive || !connection.IsAuthenticated ||
            !connection.Objects.Contains(player) || !IsGameplayPlayer(player) ||
            !TryGetPlayerToken(connection, out string playerToken)) return;

        if (!_balances.ContainsKey(playerToken))
        {
            _balances.Add(playerToken, 0);
            Debug.Log($"[Currency] Server registered connection {connection.ClientId} with balance 0.", this);
        }
        SendBalance(connection, playerToken);
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
            if (!TryGetPlayerToken(connection, out string playerToken)) return;
            if (!_balances.ContainsKey(playerToken)) RegisterPlayer(connection, player);
            else SendBalance(connection, playerToken);
            return;
        }
    }

    // Server-side API only: deliberately not exposed through an RPC or broadcast request.
    public bool AddCurrency(NetworkConnection connection, int amount)
    {
        if (_networkManager == null || !_networkManager.IsServerStarted ||
            connection == null || !connection.IsActive || !connection.IsAuthenticated ||
            !TryGetPlayerToken(connection, out string playerToken) ||
            !_balances.TryGetValue(playerToken, out int oldBalance) ||
            amount <= 0 || oldBalance > int.MaxValue - amount) return false;

        int newBalance = oldBalance + amount;
        _balances[playerToken] = newBalance;
        Debug.Log($"[Currency] Server connection {connection.ClientId}: {oldBalance} + {amount} = {newBalance}.", this);
        SendBalance(connection, playerToken);
        return true;
    }

    // Server-side only; membership is sampled when the payout occurs.
    public void AddCurrencyToRegisteredPlayers(int amount)
    {
        if (_networkManager == null || !_networkManager.IsServerStarted) return;

        // Only currently connected players receive passive income.
        var connections = new List<NetworkConnection>(_tokensByConnection.Keys);
        foreach (NetworkConnection connection in connections)
            AddCurrency(connection, amount);
    }

    private void SendBalance(NetworkConnection connection, string playerToken)
    {
        _networkManager.ServerManager.Broadcast(connection,
            new BalanceSnapshot { Balance = _balances[playerToken] }, true, Channel.Reliable);
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
        _tokensByConnection.Remove(connection);
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
        _tokensByConnection.Clear();
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
            if (_identityManager != null)
                _identityManager.ServerIdentityRegistered -= OnServerIdentityRegistered;
        }
        ClearServerState();
        if (instance == this) instance = null;
    }
}
