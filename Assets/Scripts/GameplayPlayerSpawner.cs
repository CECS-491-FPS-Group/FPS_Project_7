using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Managing.Scened;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkManager))]
public sealed class GameplayPlayerSpawner : MonoBehaviour
{
    private const string GameplaySceneName = "RoundImplementation";

    [SerializeField] private NetworkObject playerPrefab;

    private readonly Dictionary<NetworkConnection, NetworkObject> _players =
        new Dictionary<NetworkConnection, NetworkObject>();
    private NetworkManager _networkManager;

    private void Awake()
    {
        _networkManager = GetComponent<NetworkManager>();
        _networkManager.SceneManager.OnClientPresenceChangeEnd += OnClientPresenceChangeEnd;
        _networkManager.SceneManager.OnClientLoadedStartScenes += OnClientLoadedStartScenes;
        _networkManager.ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
        _networkManager.ServerManager.OnServerConnectionState += OnServerConnectionState;
    }

    private void OnDestroy()
    {
        if (_networkManager == null) return;

        _networkManager.SceneManager.OnClientPresenceChangeEnd -= OnClientPresenceChangeEnd;
        _networkManager.SceneManager.OnClientLoadedStartScenes -= OnClientLoadedStartScenes;
        _networkManager.ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;
        _networkManager.ServerManager.OnServerConnectionState -= OnServerConnectionState;
    }

    private void OnClientPresenceChangeEnd(ClientPresenceChangeEventArgs args)
    {
        if (!_networkManager.IsServerStarted || args.Scene.name != GameplaySceneName) return;

        if (args.Added)
        {
            TrySpawnPlayer(args.Connection, args.Scene);
        }
        else if (_players.TryGetValue(args.Connection, out NetworkObject player))
        {
            // Ignore removal from an older scene if this connection already has a new player.
            if (player != null && player.gameObject.scene != args.Scene) return;

            _players.Remove(args.Connection);
            if (player != null && player.IsSpawned)
                _networkManager.ServerManager.Despawn(player);
        }
    }

    private void OnClientLoadedStartScenes(NetworkConnection connection, bool asServer)
    {
        if (!asServer) return;

        // For a late join, scene presence is reported before initial loading is marked complete.
        foreach (Scene scene in connection.Scenes)
        {
            if (scene.name == GameplaySceneName)
            {
                TrySpawnPlayer(connection, scene);
                break;
            }
        }
    }

    private void TrySpawnPlayer(NetworkConnection connection, Scene scene)
    {
        if (!_networkManager.IsServerStarted || !connection.IsActive || !connection.IsAuthenticated ||
            !connection.LoadedStartScenes(true) || !scene.IsValid() || !scene.isLoaded ||
            scene.name != GameplaySceneName || !connection.Scenes.Contains(scene)) return;

        if (_players.TryGetValue(connection, out NetworkObject existing) && existing != null && existing.IsSpawned)
            return;

        if (playerPrefab == null)
        {
            Debug.LogError("[GameplayPlayerSpawner] No player prefab is assigned.", this);
            return;
        }

        // Preserve the existing prefab's spawn transform; terrain placement is handled separately.
        NetworkObject player = Instantiate(playerPrefab, playerPrefab.transform.position, playerPrefab.transform.rotation);
        _players[connection] = player;
        _networkManager.ServerManager.Spawn(player, connection, scene);

        if (!player.IsSpawned)
        {
            _players.Remove(connection);
            Destroy(player.gameObject);
            Debug.LogError($"[GameplayPlayerSpawner] Failed to spawn player for client {connection.ClientId}.", this);
            return;
        }

        Debug.Log($"[GameplayPlayerSpawner] Spawned player for client {connection.ClientId} in {scene.name}.", this);
    }

    private void OnRemoteConnectionState(NetworkConnection connection, RemoteConnectionStateArgs args)
    {
        // FishNet despawns this connection's owned objects when it disconnects.
        if (args.ConnectionState == RemoteConnectionState.Stopped)
            _players.Remove(connection);
    }

    private void OnServerConnectionState(ServerConnectionStateArgs args)
    {
        if (args.ConnectionState == LocalConnectionState.Stopped)
            _players.Clear();
    }
}
