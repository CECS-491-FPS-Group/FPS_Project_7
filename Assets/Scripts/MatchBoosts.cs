using System;
using System.Collections.Generic;
using FishNet;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

// Scene-scoped prototype: server chooses, expires and consumes every pickup.
// Clients receive cosmetic pickup data and approved effect deadlines only.
public sealed class MatchBoosts : MonoBehaviour
{
    public static MatchBoosts Instance { get; private set; }
    public enum Kind { None, Shield, Speed, Jump, Damage }
    public struct Pickup { public int Id; public Vector3 Position; public Kind Type; public double Expires; }
    public struct Effect { public int ClientId; public Kind Type; public double Expires; }
    public struct State : IBroadcast { public Pickup[] Pickups; public Effect[] Effects; }
    public float duration = 20f;
    private readonly List<Pickup> _pickups = new();
    private readonly Dictionary<string, Effect> _effects = new();
    private readonly Dictionary<NetworkConnection, string> _tokensByConnection = new();
    private readonly Dictionary<int, GameObject> _visuals = new();
    private readonly Dictionary<int, Effect> _clientEffects = new();
    private NetworkManager _manager;
    private RejoinIdentityManager _identityManager;
    private RoundTimer _timer;
    private TerrainGenerator _terrain;
    private double _nextSpawn, _nextSnapshot;
    private int _nextId;
    private GameObject _visualPrefab;
    private bool _loggedSnapshot;
    private double Now => _manager.TimeManager.TicksToTime(_manager.TimeManager.Tick);

    private void Awake() { Instance = this; _timer = GetComponent<RoundTimer>(); }
    private void Start()
    {
        _manager = InstanceFinder.NetworkManager;
        _visualPrefab = Resources.Load<GameObject>("BoostVisual");
        if (_visualPrefab == null) Debug.LogError("[Boost] Missing Resources/BoostVisual cosmetic prefab.", this);
        _manager.ClientManager.RegisterBroadcast<State>(OnState);
        _manager.ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
        _identityManager = _manager.GetComponent<RejoinIdentityManager>();
        if (_identityManager != null)
        {
            _identityManager.ServerIdentityRegistered += OnServerIdentityRegistered;
            if (_manager.IsServerStarted)
                foreach (NetworkConnection connection in _manager.ServerManager.Clients.Values)
                    RegisterIdentity(connection);
        }
        else
        {
            Debug.LogError("[Boost] RejoinIdentityManager is missing.", this);
        }
    }
    public void Clear(NetworkConnection connection)
    {
        if (_manager != null && _manager.IsServerStarted &&
            TryGetPlayerToken(connection, out string playerToken) &&
            _effects.Remove(playerToken))
        {
            _nextSnapshot = 0d;
        }
    }
    private bool Has(NetworkConnection connection, Kind kind) => _manager != null && _manager.IsServerStarted &&
        TryGetPlayerToken(connection, out string playerToken) &&
        _effects.TryGetValue(playerToken, out Effect effect) && effect.Type == kind && effect.Expires > Now;
    public bool Invulnerable(NetworkConnection connection) => Has(connection, Kind.Shield);
    public float DamageMultiplier(NetworkConnection connection) => Has(connection, Kind.Damage) ? 2f : 1f;
    public Kind LocalKind
    {
        get
        {
            if (_manager == null || !_manager.IsClientStarted) return Kind.None;
            return _clientEffects.TryGetValue(_manager.ClientManager.Connection.ClientId, out Effect effect) && effect.Expires > Now
                ? effect.Type : Kind.None;
        }
    }
    public string LocalText => LocalKind == Kind.None ? "" : $"Boost: {LocalKind}";

    private void Update()
    {
        if (_manager == null || !_manager.IsServerStarted) return;
        bool inGame = _timer.timerIsRunning && _timer.currentState == RoundTimer.gameStates.GAME;
        if (!inGame)
        {
            if (_effects.Count > 0 || _pickups.Count > 0) _nextSnapshot = 0;
            _effects.Clear(); _pickups.Clear(); _nextSpawn = Now;
        }
        else
        {
            if (_pickups.RemoveAll(p => p.Expires <= Now) > 0) _nextSnapshot = 0;
            foreach (string playerToken in new List<string>(_effects.Keys))
                if (_effects[playerToken].Expires <= Now) _effects.Remove(playerToken);
            if (_terrain == null) _terrain = FindFirstObjectByType<TerrainGenerator>();
            if (_terrain != null && _terrain.IsGenerated && Now >= _nextSpawn && _pickups.Count < 4)
            {
                _nextSpawn = Now + 10d;
                SpawnPickup();
            }
            foreach (NetworkConnection c in _manager.ServerManager.Clients.Values)
            {
                if (!c.IsActive || !c.IsAuthenticated ||
                    !TryGetPlayerToken(c, out string playerToken)) continue;
                foreach (NetworkObject obj in c.Objects)
                {
                    Health health = obj.GetComponent<Health>();
                    if (health == null || !health.CanAct || obj.gameObject.scene.name != "RoundImplementation") continue;
                    for (int i = _pickups.Count - 1; i >= 0; i--)
                    {
                        if ((obj.transform.position - _pickups[i].Position).sqrMagnitude > 6.25f) continue;
                        Pickup p = _pickups[i];
                        _pickups.RemoveAt(i);
                        _effects[playerToken] = new Effect { ClientId = c.ClientId, Type = p.Type, Expires = Now + duration };
                        Debug.Log($"[Boost] Server granted {p.Type} to connection {c.ClientId}.");
                        _nextSnapshot = 0;
                        break;
                    }
                }
            }
        }
        if (Now >= _nextSnapshot)
        {
            _nextSnapshot = Now + 0.5d;
            _manager.ServerManager.Broadcast(new State { Pickups = _pickups.ToArray(), Effects = new List<Effect>(_effects.Values).ToArray() }, true, Channel.Reliable);
        }
    }

    private void OnServerIdentityRegistered(
        NetworkConnection connection,
        string playerToken,
        bool isReturningPlayer)
    {
        if (connection == null || string.IsNullOrWhiteSpace(playerToken)) return;
        _tokensByConnection[connection] = playerToken;
        if (_effects.TryGetValue(playerToken, out Effect effect))
        {
            effect.ClientId = connection.ClientId;
            _effects[playerToken] = effect;
            _nextSnapshot = 0d;
        }
    }

    private void RegisterIdentity(NetworkConnection connection)
    {
        if (connection == null || !connection.IsActive || !connection.IsAuthenticated ||
            _identityManager == null ||
            !_identityManager.TryGetPlayerToken(connection, out string playerToken)) return;
        _tokensByConnection[connection] = playerToken;
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

    private void OnRemoteConnectionState(
        NetworkConnection connection,
        RemoteConnectionStateArgs args)
    {
        if (args.ConnectionState == RemoteConnectionState.Stopped)
        {
            if (_tokensByConnection.TryGetValue(connection, out string playerToken) &&
                _effects.TryGetValue(playerToken, out Effect effect))
            {
                // Do not leave the effect associated with an old reusable client ID.
                effect.ClientId = -1;
                _effects[playerToken] = effect;
                _nextSnapshot = 0d;
            }
            _tokensByConnection.Remove(connection);
        }
    }

    private void SpawnPickup()
    {
        Vector2 centre = _terrain.worldSettings.WorldCentre(_terrain.MeshWorldSize);
        for (int attempt = 0; attempt < 8; attempt++)
        {
            float angle = ((_nextId + attempt) % 8) * Mathf.PI / 4;
            Vector3 point = new Vector3(centre.x + Mathf.Cos(angle) * 10f, 0f, centre.y + Mathf.Sin(angle) * 10f);
            if (_pickups.Exists(p => new Vector2(p.Position.x - point.x, p.Position.z - point.z).sqrMagnitude < 16f) || !_terrain.IsColliderReadyAt(point) ||
                !_terrain.TrySampleHeight(point, out float height) || !_terrain.TryGetChunkAt(point, out TerrainChunk chunk)) continue;
            MeshCollider collider = chunk.GameObject.GetComponent<MeshCollider>();
            if (collider == null) continue;
            Vector3 origin = new Vector3(point.x, Mathf.Max(height, collider.bounds.max.y) + 1f, point.z);
            if (!collider.Raycast(new Ray(origin, Vector3.down), out RaycastHit hit, origin.y - collider.bounds.min.y + 1f)) continue;
            _pickups.Add(new Pickup { Id = ++_nextId, Position = hit.point + Vector3.up * 0.8f,
                Type = (Kind)(1 + (_nextId - 1) % 4), Expires = Now + 30d });
            _nextSnapshot = 0;
            break;
        }
    }

    private void OnState(State state, Channel channel)
    {
        if (!_loggedSnapshot)
        {
            Debug.Log($"[Boost] Client {_manager.ClientManager.Connection.ClientId} receiving authoritative pickup snapshots.", this);
            _loggedSnapshot = true;
        }
        _clientEffects.Clear();
        foreach (Effect effect in state.Effects ?? Array.Empty<Effect>()) _clientEffects[effect.ClientId] = effect;
        var present = new HashSet<int>();
        foreach (Pickup pickup in state.Pickups ?? Array.Empty<Pickup>())
        {
            present.Add(pickup.Id);
            if (_visuals.ContainsKey(pickup.Id)) continue;
            if (_visualPrefab == null) continue;
            // An explicit resource retains mesh/material in standalone builds.
            // This prefab has no collider, scripts or NetworkObject.
            GameObject visual = Instantiate(_visualPrefab, transform);
            visual.name = $"Boost {pickup.Type}";
            visual.transform.position = pickup.Position;
            visual.transform.localScale = Vector3.one * 0.65f;
            visual.GetComponent<Renderer>().material.color = pickup.Type switch
            { Kind.Shield => Color.cyan, Kind.Speed => Color.green, Kind.Jump => Color.yellow, _ => Color.red };
            _visuals.Add(pickup.Id, visual);
            Debug.Log($"[Boost] Client {_manager.ClientManager.Connection.ClientId} showing pickup {pickup.Id} ({pickup.Type}) at {pickup.Position}.", this);
        }
        foreach (int id in new List<int>(_visuals.Keys))
            if (!present.Contains(id)) { Destroy(_visuals[id].GetComponent<Renderer>().material); Destroy(_visuals[id]); _visuals.Remove(id); }
    }
    private void OnDestroy()
    {
        if (_manager != null)
        {
            _manager.ClientManager.UnregisterBroadcast<State>(OnState);
            _manager.ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;
        }
        if (_identityManager != null)
            _identityManager.ServerIdentityRegistered -= OnServerIdentityRegistered;
        _effects.Clear();
        _tokensByConnection.Clear();
        foreach (GameObject visual in _visuals.Values) if (visual != null) Destroy(visual.GetComponent<Renderer>().material);
        if (Instance == this) Instance = null;
    }
}
