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
        new Vector2(0f, -4f), new Vector2(0f, 4f),
        new Vector2(-8f, -8f), new Vector2(8f, -8f),
        new Vector2(-8f, 8f), new Vector2(8f, 8f),
        new Vector2(-12f, 0f), new Vector2(12f, 0f),
        new Vector2(0f, -12f), new Vector2(0f, 12f)
    };

    public struct TerrainReadyMessage : IBroadcast
    {
        public int Seed;
    }

    [SerializeField] private NetworkObject playerPrefab;

    [Header("Rejoin State")]
    [Tooltip("How long the host remembers a disconnected player's position.")]
    [SerializeField, Min(5f)] private float rejoinStateGraceSeconds = 60f;

    private struct CachedPlayerState
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public bool HasHealthState;
        public Health.RejoinState HealthState;
        public bool HasWeaponState;
        public HitscanShooter.RejoinState WeaponState;
        public double DisconnectedAt;
        public bool WaitingForRejoin;
    }

    private readonly Dictionary<NetworkConnection, NetworkObject> _players =
        new Dictionary<NetworkConnection, NetworkObject>();
    private readonly Dictionary<NetworkConnection, string> _playerTokens = new();
    private readonly Dictionary<string, CachedPlayerState> _cachedPlayerStates = new();
    private readonly List<string> _expiredPlayerTokens = new();
    private NetworkManager _networkManager;
    private RejoinIdentityManager _identityManager;
    private readonly Dictionary<NetworkConnection, int> _terrainReady = new();
    private readonly HashSet<NetworkConnection> _spawnAttempted = new();
    private readonly Dictionary<NetworkConnection, float> _spawnRetryAt = new();
    private TerrainGenerator _terrain;
    private bool _reportedTerrainReady;
    private bool _returningToLobby;
    private double _nextStateSnapshotTime;

    private void Awake()
    {
        _networkManager = GetComponent<NetworkManager>();
        _identityManager = GetComponent<RejoinIdentityManager>();
        _networkManager.SceneManager.OnLoadEnd += OnSceneLoadEnd;
        _networkManager.SceneManager.OnClientPresenceChangeEnd += OnClientPresenceChangeEnd;
        _networkManager.SceneManager.OnClientLoadedStartScenes += OnClientLoadedStartScenes;
        _networkManager.ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
        _networkManager.ServerManager.OnServerConnectionState += OnServerConnectionState;
        _networkManager.ServerManager.RegisterBroadcast<TerrainReadyMessage>(OnTerrainReady);
        _networkManager.ClientManager.OnClientConnectionState += OnClientConnectionState;

        if (_identityManager != null)
        {
            _identityManager.ServerIdentityRegistered += OnServerIdentityRegistered;
            _identityManager.ServerIdentityDisconnected += OnServerIdentityDisconnected;
        }
        else
        {
            Debug.LogError(
                "[GameplayPlayerSpawner] RejoinIdentityManager is missing from the NetworkManager.",
                this);
        }
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

        if (_identityManager != null)
        {
            _identityManager.ServerIdentityRegistered -= OnServerIdentityRegistered;
            _identityManager.ServerIdentityDisconnected -= OnServerIdentityDisconnected;
        }
    }

    private void Update()
    {
        if (_returningToLobby) return;

        if (_networkManager.IsServerStarted)
        {
            SnapshotActivePlayerStates();
            RemoveExpiredPlayerStates();
        }

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
            _spawnRetryAt.Remove(args.Connection);
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

        // Do not spawn until the server knows the stable identity for this connection.
        if (_identityManager == null ||
            !_identityManager.TryGetPlayerToken(connection, out string playerToken))
        {
            return;
        }

        _playerTokens[connection] = playerToken;

        if (_terrain == null || _terrain.gameObject.scene != scene || !_terrain.IsGenerated ||
            !_terrainReady.TryGetValue(connection, out int clientSeed) || _spawnAttempted.Contains(connection)) return;
        if (_spawnRetryAt.TryGetValue(connection, out float retryAt) && Time.unscaledTime < retryAt) return;

        if (clientSeed != _terrain.Seed)
        {
            _spawnAttempted.Add(connection);
            Debug.LogError($"[GameplayPlayerSpawner] Terrain seed mismatch for client {connection.ClientId}; player not spawned.", this);
            return;
        }

        if (playerPrefab == null)
        {
            _spawnAttempted.Add(connection);
            Debug.LogError("[GameplayPlayerSpawner] No player prefab is assigned.", this);
            return;
        }

        bool hasRejoinState = TryGetCachedRejoinState(
            playerToken,
            out CachedPlayerState rejoinState);
        Vector3 spawnPosition = default;
        Quaternion spawnRotation = playerPrefab.transform.rotation;
        bool restoredPosition = hasRejoinState && TryGetRejoinPosition(
            rejoinState,
            out spawnPosition,
            out spawnRotation);

        if (!restoredPosition && !TryGetSpawnPosition(out spawnPosition))
        {
            if (!_spawnRetryAt.ContainsKey(connection))
                Debug.LogWarning($"[GameplayPlayerSpawner] Waiting for a free terrain-safe spawn for client {connection.ClientId}.", this);
            _spawnRetryAt[connection] = Time.unscaledTime + 1f;
            return;
        }

        if (!restoredPosition)
            spawnRotation = playerPrefab.transform.rotation;

        _spawnRetryAt.Remove(connection);
        _spawnAttempted.Add(connection);
        NetworkObject player = Instantiate(playerPrefab, spawnPosition, spawnRotation);
        _players[connection] = player;
        _networkManager.ServerManager.Spawn(player, connection, scene);

        if (!player.IsSpawned)
        {
            _players.Remove(connection);
            Destroy(player.gameObject);
            Debug.LogError($"[GameplayPlayerSpawner] Failed to spawn player for client {connection.ClientId}.", this);
            return;
        }

        if (hasRejoinState)
            RestorePlayerGameplayState(player, rejoinState);

        CachedPlayerState activeState = new CachedPlayerState
        {
            Position = spawnPosition,
            Rotation = spawnRotation,
            WaitingForRejoin = false,
            DisconnectedAt = 0d
        };
        CapturePlayerGameplayState(player, ref activeState);
        _cachedPlayerStates[playerToken] = activeState;

        string action = hasRejoinState ? "Restored" : "Spawned";
        Debug.Log(
            $"[GameplayPlayerSpawner] {action} player for client {connection.ClientId} " +
            $"in {scene.name} at {spawnPosition}.",
            this);
    }

    private void OnServerIdentityRegistered(
        NetworkConnection connection,
        string playerToken,
        bool isReturningPlayer)
    {
        _playerTokens[connection] = playerToken;

        Scene scene = UnityEngine.SceneManagement.SceneManager.GetSceneByName(GameplaySceneName);
        if (_networkManager.IsServerStarted && scene.IsValid() && scene.isLoaded)
            TrySpawnPlayer(connection, scene);
    }

    private void OnServerIdentityDisconnected(NetworkConnection connection, string playerToken)
    {
        CacheDisconnectedPlayerState(connection, playerToken);
        _playerTokens.Remove(connection);
    }

    private void SnapshotActivePlayerStates()
    {
        double now = Time.unscaledTimeAsDouble;
        if (now < _nextStateSnapshotTime)
            return;

        _nextStateSnapshotTime = now + 0.1d;

        foreach (var pair in _players)
        {
            NetworkConnection connection = pair.Key;
            NetworkObject player = pair.Value;
            if (player == null || !player.IsSpawned)
                continue;

            if (!_playerTokens.TryGetValue(connection, out string playerToken))
            {
                if (_identityManager == null ||
                    !_identityManager.TryGetPlayerToken(connection, out playerToken))
                {
                    continue;
                }

                _playerTokens[connection] = playerToken;
            }

            CachedPlayerState state = new CachedPlayerState
            {
                Position = player.transform.position,
                Rotation = player.transform.rotation,
                WaitingForRejoin = false,
                DisconnectedAt = 0d
            };
            CapturePlayerGameplayState(player, ref state);
            _cachedPlayerStates[playerToken] = state;
        }
    }

    private void CacheDisconnectedPlayerState(
        NetworkConnection connection,
        string playerToken)
    {
        if (string.IsNullOrWhiteSpace(playerToken))
            return;

        bool hasState = _cachedPlayerStates.TryGetValue(
            playerToken,
            out CachedPlayerState state);

        if (_players.TryGetValue(connection, out NetworkObject player) && player != null)
        {
            state.Position = player.transform.position;
            state.Rotation = player.transform.rotation;
            CapturePlayerGameplayState(player, ref state);
            hasState = true;
        }

        if (!hasState)
            return;

        state.WaitingForRejoin = true;
        state.DisconnectedAt = Time.unscaledTimeAsDouble;
        _cachedPlayerStates[playerToken] = state;

        Debug.Log(
            $"[GameplayPlayerSpawner] Saved disconnected player position {state.Position} " +
            $"for {rejoinStateGraceSeconds:0} seconds.",
            this);
    }

    private void CapturePlayerGameplayState(
        NetworkObject player,
        ref CachedPlayerState state)
    {
        if (player == null || !player.IsSpawned)
            return;

        Health health = player.GetComponent<Health>();
        if (health != null && health.TryCaptureRejoinState(out Health.RejoinState healthState))
        {
            state.HealthState = healthState;
            state.HasHealthState = true;
        }

        HitscanShooter shooter = player.GetComponentInChildren<HitscanShooter>(true);
        if (shooter != null &&
            shooter.TryCaptureRejoinState(out HitscanShooter.RejoinState weaponState))
        {
            state.WeaponState = weaponState;
            state.HasWeaponState = true;
        }
    }

    private void RestorePlayerGameplayState(
        NetworkObject player,
        CachedPlayerState state)
    {
        if (player == null || !player.IsSpawned)
            return;

        Health health = player.GetComponent<Health>();
        if (state.HasHealthState && health != null)
            health.RestoreRejoinState(state.HealthState);

        // Restore weapons after health so dead players remain in the normal
        // death/respawn weapon flow.
        HitscanShooter shooter = player.GetComponentInChildren<HitscanShooter>(true);
        if (state.HasWeaponState && shooter != null)
            shooter.RestoreRejoinState(state.WeaponState);
    }

    private bool TryGetCachedRejoinState(
        string playerToken,
        out CachedPlayerState state)
    {
        state = default;
        if (!_cachedPlayerStates.TryGetValue(playerToken, out state) ||
            !state.WaitingForRejoin)
        {
            return false;
        }

        if (Time.unscaledTimeAsDouble - state.DisconnectedAt <= rejoinStateGraceSeconds)
            return true;

        _cachedPlayerStates.Remove(playerToken);
        state = default;
        return false;
    }

    private bool TryGetRejoinPosition(
        CachedPlayerState state,
        out Vector3 position,
        out Quaternion rotation)
    {
        position = default;
        rotation = playerPrefab != null
            ? playerPrefab.transform.rotation
            : Quaternion.identity;

        if (!IsFinite(state.Position) ||
            _terrain == null || !_terrain.IsGenerated ||
            !_terrain.IsColliderReadyAt(state.Position) ||
            !_terrain.TrySampleHeight(state.Position, out float height) ||
            !_terrain.TryGetChunkAt(state.Position, out TerrainChunk chunk) ||
            !TryGetFeetOffset(out float feetOffset))
        {
            Debug.LogWarning(
                "[GameplayPlayerSpawner] Saved rejoin position was not terrain-safe; " +
                "using a normal spawn while preserving health and weapons.",
                this);
            return false;
        }

        foreach (NetworkObject otherPlayer in _players.Values)
        {
            if (otherPlayer == null || !otherPlayer.IsSpawned)
                continue;

            Vector3 delta = otherPlayer.transform.position - state.Position;
            if (delta.x * delta.x + delta.z * delta.z < 4f)
            {
                Debug.LogWarning(
                    "[GameplayPlayerSpawner] Saved rejoin position is occupied; " +
                    "using a normal spawn while preserving health and weapons.",
                    this);
                return false;
            }
        }

        MeshCollider collider = chunk.GameObject.GetComponent<MeshCollider>();
        if (collider == null)
            return false;

        Bounds bounds = collider.bounds;
        float originY = Mathf.Max(state.Position.y + 2f, Mathf.Max(height, bounds.max.y) + 1f);
        Vector3 origin = new Vector3(state.Position.x, originY, state.Position.z);
        if (!collider.Raycast(
                new Ray(origin, Vector3.down),
                out RaycastHit hit,
                origin.y - bounds.min.y + 1f))
        {
            return false;
        }

        float minimumY = hit.point.y + feetOffset + 0.05f;
        position = new Vector3(
            state.Position.x,
            Mathf.Max(state.Position.y, minimumY),
            state.Position.z);
        rotation = Quaternion.Euler(0f, state.Rotation.eulerAngles.y, 0f);
        return true;
    }

    private void RemoveExpiredPlayerStates()
    {
        double now = Time.unscaledTimeAsDouble;
        _expiredPlayerTokens.Clear();

        foreach (var pair in _cachedPlayerStates)
        {
            if (pair.Value.WaitingForRejoin &&
                now - pair.Value.DisconnectedAt > rejoinStateGraceSeconds)
            {
                _expiredPlayerTokens.Add(pair.Key);
            }
        }

        foreach (string playerToken in _expiredPlayerTokens)
            _cachedPlayerStates.Remove(playerToken);
    }

    private bool TryGetFeetOffset(out float feetOffset)
    {
        feetOffset = 0f;
        if (playerPrefab == null)
            return false;

        CharacterController controller = playerPrefab.GetComponent<CharacterController>();
        if (controller == null)
            return false;

        Vector3 scale = playerPrefab.transform.lossyScale;
        float halfHeight = Mathf.Max(
            controller.height * Mathf.Abs(scale.y) * 0.5f,
            controller.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)));
        feetOffset = halfHeight - controller.center.y * scale.y;
        return true;
    }

    private static bool IsFinite(Vector3 value)
    {
        return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
               !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
               !float.IsNaN(value.z) && !float.IsInfinity(value.z);
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
        if (!TryGetFeetOffset(out float feetOffset)) return false;
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
        _playerTokens.Clear();
        _cachedPlayerStates.Clear();
        _expiredPlayerTokens.Clear();
        _terrainReady.Clear();
        _spawnAttempted.Clear();
        _spawnRetryAt.Clear();
        _terrain = null;
        _reportedTerrainReady = false;
        _nextStateSnapshotTime = 0d;
    }

    private void OnRemoteConnectionState(NetworkConnection connection, RemoteConnectionStateArgs args)
    {
        // FishNet despawns this connection's owned objects when it disconnects.
        if (args.ConnectionState == RemoteConnectionState.Stopped)
        {
            if (_playerTokens.TryGetValue(connection, out string playerToken))
                CacheDisconnectedPlayerState(connection, playerToken);

            _players.Remove(connection);
            _playerTokens.Remove(connection);
            _terrainReady.Remove(connection);
            _spawnAttempted.Remove(connection);
            _spawnRetryAt.Remove(connection);
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
