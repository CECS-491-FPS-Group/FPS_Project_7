using System;
using System.Collections.Generic;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkManager))]
// Gives each running player a stable identity that survives a reconnect.
// The server can use this identity later to restore position and match state.
public sealed class RejoinIdentityManager : MonoBehaviour
{
    public struct PlayerIdentityClaim : IBroadcast
    {
        public string PlayerToken;
    }

    public static RejoinIdentityManager Instance { get; private set; }

    // The bool value is true when the server has seen this token before.
    public event Action<NetworkConnection, string, bool> ServerIdentityRegistered;
    public event Action<NetworkConnection, string> ServerIdentityDisconnected;

    private readonly Dictionary<NetworkConnection, string> _tokensByConnection = new();
    private readonly Dictionary<string, NetworkConnection> _activeConnectionsByToken = new();
    private readonly HashSet<string> _knownTokens = new();

    private NetworkManager _networkManager;
    private bool _subscribed;
    private bool _claimSentForCurrentConnection;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            enabled = false;
            return;
        }

        Instance = this;
        _networkManager = GetComponent<NetworkManager>();
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void Start()
    {
        // Start is a second chance in case FishNet initialized after OnEnable.
        Subscribe();

        if (_networkManager != null &&
            _networkManager.IsClientStarted &&
            _networkManager.ClientManager.Connection.IsAuthenticated)
        {
            SendIdentityClaim();
        }
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnDestroy()
    {
        Unsubscribe();
        ClearServerState();

        if (Instance == this)
            Instance = null;
    }

    public bool TryGetPlayerToken(NetworkConnection connection, out string playerToken)
    {
        if (connection != null && _tokensByConnection.TryGetValue(connection, out playerToken))
            return true;

        playerToken = string.Empty;
        return false;
    }

    public bool TryGetActiveConnection(string playerToken, out NetworkConnection connection)
    {
        if (IsValidPlayerToken(playerToken) &&
            _activeConnectionsByToken.TryGetValue(playerToken, out connection) &&
            connection != null && connection.IsActive)
        {
            return true;
        }

        connection = null;
        return false;
    }

    private void Subscribe()
    {
        if (_subscribed || _networkManager == null ||
            _networkManager.ClientManager == null ||
            _networkManager.ServerManager == null)
        {
            return;
        }

        _networkManager.ClientManager.OnAuthenticated += OnClientAuthenticated;
        _networkManager.ClientManager.OnClientConnectionState += OnClientConnectionState;
        _networkManager.ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
        _networkManager.ServerManager.OnServerConnectionState += OnServerConnectionState;
        _networkManager.ServerManager.RegisterBroadcast<PlayerIdentityClaim>(OnPlayerIdentityClaim);
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed || _networkManager == null)
            return;

        _networkManager.ClientManager.OnAuthenticated -= OnClientAuthenticated;
        _networkManager.ClientManager.OnClientConnectionState -= OnClientConnectionState;
        _networkManager.ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;
        _networkManager.ServerManager.OnServerConnectionState -= OnServerConnectionState;
        _networkManager.ServerManager.UnregisterBroadcast<PlayerIdentityClaim>(OnPlayerIdentityClaim);
        _subscribed = false;
    }

    private void OnClientAuthenticated()
    {
        SendIdentityClaim();
    }

    private void SendIdentityClaim()
    {
        if (_claimSentForCurrentConnection ||
            _networkManager == null ||
            !_networkManager.IsClientStarted ||
            !_networkManager.ClientManager.Connection.IsAuthenticated)
        {
            return;
        }

        string playerToken = LocalLobbySession.PlayerToken;
        if (!IsValidPlayerToken(playerToken))
        {
            Debug.LogError(
                "[RejoinIdentity] No valid player token exists for this connection. " +
                "Start the connection through LobbyConnection.", this);
            return;
        }

        _claimSentForCurrentConnection = true;
        _networkManager.ClientManager.Broadcast(
            new PlayerIdentityClaim { PlayerToken = playerToken },
            Channel.Reliable);

        Debug.Log(
            $"[RejoinIdentity] Client sent identity {ShortToken(playerToken)} after authentication.",
            this);
    }

    private void OnPlayerIdentityClaim(
        NetworkConnection connection,
        PlayerIdentityClaim claim,
        Channel channel)
    {
        if (_networkManager == null || !_networkManager.IsServerStarted ||
            connection == null || !connection.IsActive || !connection.IsAuthenticated ||
            !IsValidPlayerToken(claim.PlayerToken))
        {
            Debug.LogWarning("[RejoinIdentity] Server rejected an invalid identity claim.", this);
            return;
        }

        string playerToken = claim.PlayerToken;

        if (_tokensByConnection.TryGetValue(connection, out string currentToken))
        {
            if (currentToken == playerToken)
                return;

            _activeConnectionsByToken.Remove(currentToken);
            _tokensByConnection.Remove(connection);
        }

        if (_activeConnectionsByToken.TryGetValue(playerToken, out NetworkConnection otherConnection) &&
            otherConnection != null && otherConnection != connection && otherConnection.IsActive)
        {
            Debug.LogWarning(
                $"[RejoinIdentity] Rejected duplicate active identity {ShortToken(playerToken)}.",
                this);
            return;
        }

        bool isReturningPlayer = _knownTokens.Contains(playerToken);
        _knownTokens.Add(playerToken);
        _tokensByConnection[connection] = playerToken;
        _activeConnectionsByToken[playerToken] = connection;

        Debug.Log(
            $"[RejoinIdentity] Server registered connection {connection.ClientId} as " +
            $"{ShortToken(playerToken)}. Returning={isReturningPlayer}.",
            this);

        ServerIdentityRegistered?.Invoke(connection, playerToken, isReturningPlayer);
    }

    private void OnClientConnectionState(ClientConnectionStateArgs args)
    {
        if (args.ConnectionState == LocalConnectionState.Stopped)
            _claimSentForCurrentConnection = false;
    }

    private void OnRemoteConnectionState(
        NetworkConnection connection,
        RemoteConnectionStateArgs args)
    {
        if (args.ConnectionState != RemoteConnectionState.Stopped || connection == null)
            return;

        if (!_tokensByConnection.TryGetValue(connection, out string playerToken))
            return;

        _tokensByConnection.Remove(connection);

        if (_activeConnectionsByToken.TryGetValue(playerToken, out NetworkConnection active) &&
            active == connection)
        {
            _activeConnectionsByToken.Remove(playerToken);
        }

        Debug.Log(
            $"[RejoinIdentity] Identity {ShortToken(playerToken)} disconnected from " +
            $"connection {connection.ClientId}.",
            this);

        ServerIdentityDisconnected?.Invoke(connection, playerToken);
    }

    private void OnServerConnectionState(ServerConnectionStateArgs args)
    {
        if (args.ConnectionState == LocalConnectionState.Stopped)
            ClearServerState();
    }

    private void ClearServerState()
    {
        _tokensByConnection.Clear();
        _activeConnectionsByToken.Clear();
        _knownTokens.Clear();
    }

    private static bool IsValidPlayerToken(string playerToken)
    {
        return !string.IsNullOrWhiteSpace(playerToken) &&
               Guid.TryParseExact(playerToken, "N", out _);
    }

    private static string ShortToken(string playerToken)
    {
        return string.IsNullOrEmpty(playerToken)
            ? "<none>"
            : playerToken.Substring(0, Math.Min(8, playerToken.Length));
    }
}
