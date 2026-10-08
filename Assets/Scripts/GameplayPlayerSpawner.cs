using System.Collections;
using System.Collections.Generic;
using FishNet.Broadcast;
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
    private const string LobbySceneName = "LobbyScene_v1";
    private static readonly Vector2[] SpawnOffsets =
    {
        new Vector2(-4f, 0f), new Vector2(4f, 0f),
        new Vector2(0f, -4f), new Vector2(0f, 4f)
    };

    public struct TerrainReadyMessage : IBroadcast
    {
        public int Seed;
    }

    [SerializeField] private NetworkObject playerPrefab;

    private readonly Dictionary<NetworkConnection, NetworkObject> _players =
        new Dictionary<NetworkConnection, NetworkObject>();
    private NetworkManager _networkManager;
    private readonly Dictionary<NetworkConnection, int> _terrainReady = new();
    private readonly HashSet<NetworkConnection> _spawnAttempted = new();
    private TerrainGenerator _terrain;
    private bool _reportedTerrainReady;
    private bool _returningToLobby;

    private void Awake()
    {
        _networkManager = GetComponent<NetworkManager>();
        _networkManager.SceneManager.OnLoadEnd += OnSceneLoadEnd;
        _networkManager.SceneManager.OnClientPresenceChangeEnd += OnClientPresenceChangeEnd;
        _networkManager.SceneManager.OnClientLoadedStartScenes += OnClientLoadedStartScenes;
        _networkManager.ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
        _networkManager.ServerManager.OnServerConnectionState += OnServerConnectionState;
        _networkManager.ServerManager.RegisterBroadcast<TerrainReadyMessage>(OnTerrainReady);
        _networkManager.ClientManager.OnClientConnectionState += OnClientConnectionState;
    }

    private void OnDestroy()
    {
        if (_networkManager == null) return;

        _networkManager.SceneManager.OnLoadEnd -= OnSceneLoadEnd;
        _networkManager.SceneManager.OnClientPresenceChangeEnd -= OnClientPresenceChangeEnd;
        _networkManager.SceneManager.OnClientLoadedStartScenes -= OnClientLoadedStartScenes;
        _networkManager.ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;
        _networkManager.ServerManager.OnServerConnectionState -= OnServerConnectionState;
        _networkManager.ServerManager.UnregisterBroadcast<TerrainReadyMessage>(OnTerrainReady);
        _networkManager.ClientManager.OnClientConnectionState -= OnClientConnectionState;
    }

    private void Update()
    {
        if (_returningToLobby) return;
        Scene scene = UnityEngine.SceneManagement.SceneManager.GetSceneByName(GameplaySceneName);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            _terrain = null;
            _reportedTerrainReady = false;
            return;
        }

        if (_terrain == null || _terrain.gameObject.scene != scene)
        {
            _terrain = null;
            _reportedTerrainReady = false;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                _terrain = root.GetComponentInChildren<TerrainGenerator>();
                if (_terrain != null) break;
            }
        }

        if (_terrain == null || !_terrain.IsGenerated) return;

        if (!_reportedTerrainReady && _networkManager.IsClientStarted &&
            _networkManager.ClientManager.Connection.IsAuthenticated)
        {
            _networkManager.ClientManager.Broadcast(new TerrainReadyMessage { Seed = _terrain.Seed });
            _reportedTerrainReady = true;
        }

        // Revisit eligibility when either terrain generation or scene presence finishes last.
        if (_networkManager.IsServerStarted)
        {
            foreach (NetworkConnection connection in _networkManager.ServerManager.Clients.Values)
                TrySpawnPlayer(connection, scene);
        }
    }

    private void OnTerrainReady(NetworkConnection connection, TerrainReadyMessage message, Channel channel)
    {
        Scene scene = UnityEngine.SceneManagement.SceneManager.GetSceneByName(GameplaySceneName);
        if (_returningToLobby || !scene.IsValid() || !scene.isLoaded ||
            !connection.IsActive || !connection.IsAuthenticated) return;
        if (_terrainReady.ContainsKey(connection)) return;
        _terrainReady.Add(connection, message.Seed);
        Debug.Log($"[GameplayPlayerSpawner] Terrain ready for client {connection.ClientId} (seed {message.Seed}).", this);
    }

    private void OnClientConnectionState(ClientConnectionStateArgs args)
    {
        if (args.ConnectionState == LocalConnectionState.Stopped)
            _reportedTerrainReady = false;
    }

    private void OnClientPresenceChangeEnd(ClientPresenceChangeEventArgs args)
    {
        if (!_networkManager.IsServerStarted || args.Scene.name != GameplaySceneName) return;

        if (args.Added)
        {
            TrySpawnPlayer(args.Connection, args.Scene);
        }
        else
        {
            _players.TryGetValue(args.Connection, out NetworkObject player);
            // Ignore removal from an older scene if this connection already has a new player.
            if (player != null && player.gameObject.scene != args.Scene) return;

            _players.Remove(args.Connection);
            _terrainReady.Remove(args.Connection);
            _spawnAttempted.Remove(args.Connection);
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
        if (_returningToLobby || !_networkManager.IsServerStarted || !connection.IsActive || !connection.IsAuthenticated ||
            !connection.LoadedStartScenes(true) || !scene.IsValid() || !scene.isLoaded ||
            scene.name != GameplaySceneName || !connection.Scenes.Contains(scene)) return;

        if (_players.TryGetValue(connection, out NetworkObject existing) && existing != null && existing.IsSpawned)
            return;

        if (_terrain == null || _terrain.gameObject.scene != scene || !_terrain.IsGenerated ||
            !_terrainReady.TryGetValue(connection, out int clientSeed) || !_spawnAttempted.Add(connection)) return;

        if (clientSeed != _terrain.Seed)
        {
            Debug.LogError($"[GameplayPlayerSpawner] Terrain seed mismatch for client {connection.ClientId}; player not spawned.", this);
            return;
        }

        if (playerPrefab == null)
        {
            Debug.LogError("[GameplayPlayerSpawner] No player prefab is assigned.", this);
            return;
        }

        if (!TryGetSpawnPosition(out Vector3 spawnPosition))
        {
            Debug.LogError($"[GameplayPlayerSpawner] No valid terrain spawn among the four centre candidates for client {connection.ClientId}; player not spawned.", this);
            return;
        }

        NetworkObject player = Instantiate(playerPrefab, spawnPosition, playerPrefab.transform.rotation);
        _players[connection] = player;
        _networkManager.ServerManager.Spawn(player, connection, scene);

        if (!player.IsSpawned)
        {
            _players.Remove(connection);
            Destroy(player.gameObject);
            Debug.LogError($"[GameplayPlayerSpawner] Failed to spawn player for client {connection.ClientId}.", this);
            return;
        }

        Debug.Log($"[GameplayPlayerSpawner] Spawned player for client {connection.ClientId} in {scene.name} at {spawnPosition}.", this);
    }

    public bool TryGetRespawnPosition(NetworkObject player, out Vector3 position)
    {
        position = default;
        if (_returningToLobby || !_networkManager.IsServerStarted || player == null || !player.IsSpawned ||
            !player.Owner.IsActive || !player.Owner.IsAuthenticated ||
            !_players.TryGetValue(player.Owner, out NetworkObject registered) || registered != player ||
            _terrain == null || !_terrain.IsGenerated || _terrain.gameObject.scene != player.gameObject.scene ||
            !_terrainReady.TryGetValue(player.Owner, out int seed) || seed != _terrain.Seed) return false;
        return TryGetSpawnPosition(out position, player);
    }

    private bool TryGetSpawnPosition(out Vector3 position, NetworkObject excludedPlayer = null)
    {
        position = default;
        CharacterController controller = playerPrefab.GetComponent<CharacterController>();
        if (controller == null) return false;

        Vector3 scale = playerPrefab.transform.lossyScale;
        float halfHeight = Mathf.Max(controller.height * Mathf.Abs(scale.y) * 0.5f,
            controller.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)));
        float feetOffset = halfHeight - controller.center.y * scale.y;
        Vector2 centre = _terrain.worldSettings.WorldCentre(_terrain.MeshWorldSize);

        foreach (Vector2 offset in SpawnOffsets)
        {
            Vector3 candidate = new Vector3(centre.x + offset.x, 0f, centre.y + offset.y);
            bool separated = true;
            foreach (NetworkObject player in _players.Values)
            {
                if (player == null || !player.IsSpawned || player == excludedPlayer) continue;
                Vector3 delta = player.transform.position - candidate;
                if (delta.x * delta.x + delta.z * delta.z < 16f)
                {
                    separated = false;
                    break;
                }
            }

            if (!separated || !_terrain.IsColliderReadyAt(candidate) ||
                !_terrain.TrySampleHeight(candidate, out float height) ||
                !_terrain.TryGetChunkAt(candidate, out TerrainChunk chunk)) continue;

            // Target this generated chunk directly, never another player's collider.
            MeshCollider collider = chunk.GameObject.GetComponent<MeshCollider>();
            if (collider == null) continue;
            Bounds bounds = collider.bounds;
            Vector3 origin = new Vector3(candidate.x, Mathf.Max(height, bounds.max.y) + 1f, candidate.z);
            if (!collider.Raycast(new Ray(origin, Vector3.down), out RaycastHit hit,
                origin.y - bounds.min.y + 1f)) continue;

            position = new Vector3(candidate.x, hit.point.y + feetOffset + 0.05f, candidate.z);
            return true;
        }

        return false;
    }

    public void ReturnToLobby()
    {
        if (!_networkManager.IsServerStarted || _returningToLobby) return;
        _returningToLobby = true;
        foreach (NetworkObject player in _players.Values)
            if (player != null && player.IsSpawned) player.GetComponent<Health>().EndMatch();
        Debug.Log("[Match] Match ended; returning to lobby.", this);
        StartCoroutine(ReturnToLobbyRoutine());
    }

    private IEnumerator ReturnToLobbyRoutine()
    {
        // Keep cameras/HUD alive briefly while the synchronized control lock arrives.
        yield return new WaitForSecondsRealtime(1f);
        if (!_networkManager.IsServerStarted) yield break;

        // Unregister network objects before unloading their scene, as in the lobby->game path.
        foreach (NetworkObject player in new List<NetworkObject>(_players.Values))
            if (player != null && player.IsSpawned) _networkManager.ServerManager.Despawn(player);
        ClearGameplayState();
        var data = new SceneLoadData(LobbySceneName) { ReplaceScenes = ReplaceOption.All };
        Debug.Log("[Match] Gameplay players despawned; loading LobbyScene_v1.", this);
        _networkManager.SceneManager.LoadGlobalScenes(data);
    }

    private void OnSceneLoadEnd(SceneLoadEndEventArgs args)
    {
        foreach (Scene scene in args.LoadedScenes)
        {
            if (scene.name != LobbySceneName) continue;
            // Runs locally on clients too, ensuring the next terrain must report readiness again.
            ClearGameplayState();
            _returningToLobby = false;
            break;
        }
    }

    private void ClearGameplayState()
    {
        _players.Clear();
        _terrainReady.Clear();
        _spawnAttempted.Clear();
        _terrain = null;
        _reportedTerrainReady = false;
    }

    private void OnRemoteConnectionState(NetworkConnection connection, RemoteConnectionStateArgs args)
    {
        // FishNet despawns this connection's owned objects when it disconnects.
        if (args.ConnectionState == RemoteConnectionState.Stopped)
        {
            _players.Remove(connection);
            _terrainReady.Remove(connection);
            _spawnAttempted.Remove(connection);
        }
    }

    private void OnServerConnectionState(ServerConnectionStateArgs args)
    {
        if (args.ConnectionState == LocalConnectionState.Stopped)
        {
            StopAllCoroutines();
            ClearGameplayState();
            _returningToLobby = false;
        }
    }
}
