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
    public enum Kind { None, Shield, Speed, Jump, Damage, Armor, Invisibility }
    public struct Pickup { public int Id; public Vector3 Position; public Kind Type; public double Expires; }
    public struct Effect { public int ClientId; public Kind Type; public double Expires; }
    public struct State : IBroadcast { public Pickup[] Pickups; public Effect[] Effects; }
    public float duration = 20f;
    private readonly List<Pickup> _pickups = new();
    private readonly Dictionary<NetworkConnection, Effect> _effects = new(ConnectionReferenceComparer.Instance);
    private readonly Dictionary<int, GameObject> _visuals = new();
    private readonly Dictionary<int, Effect> _clientEffects = new();
    private NetworkManager _manager;
    private RoundTimer _timer;
    private TerrainGenerator _terrain;
    private double _nextSpawn, _nextSnapshot;
    private int _nextId;
    private GameObject _visualPrefab;
    private bool _loggedSnapshot;
    private readonly CombatRayGeometry _collectionGeometry = new();
    private double Now => _manager.TimeManager.TicksToTime(_manager.TimeManager.Tick);

    private void Awake() { Instance = this; _timer = GetComponent<RoundTimer>(); }
    private void Start()
    {
        _manager = InstanceFinder.NetworkManager;
        _visualPrefab = Resources.Load<GameObject>("BoostVisual");
        if (_visualPrefab == null) Debug.LogError("[Boost] Missing Resources/BoostVisual cosmetic prefab.", this);
        _manager.ClientManager.RegisterBroadcast<State>(OnState);
        _manager.ServerManager.OnRemoteConnectionState += OnRemoteState;
        _manager.ServerManager.OnServerConnectionState += OnServerState;
    }
    private void OnRemoteState(NetworkConnection connection, RemoteConnectionStateArgs args)
    {
        if (args.ConnectionState != RemoteConnectionState.Stopped) return;
        if (_effects.Remove(connection)) _nextSnapshot = 0;
    }
    private void OnServerState(ServerConnectionStateArgs args)
    {
        if (args.ConnectionState != LocalConnectionState.Stopped) return;
        _effects.Clear(); _pickups.Clear(); _nextSnapshot = 0;
    }
    public void Clear(NetworkConnection connection) { if (_manager != null && _manager.IsServerStarted) _effects.Remove(connection); }
    private bool Has(NetworkConnection connection, Kind kind) => _manager != null && _manager.IsServerStarted &&
        connection != null && _effects.TryGetValue(connection, out Effect effect) && effect.Type == kind && effect.Expires > Now;
    public bool Invulnerable(NetworkConnection connection) => Has(connection, Kind.Shield);
    public float DamageMultiplier(NetworkConnection connection) => Has(connection, Kind.Damage) ? 2f : 1f;
    // Uses this server's effect dictionary and deadline; never the client snapshot/LocalKind.
    public bool ServerHasSpeedBoost(NetworkConnection connection) => Has(connection, Kind.Speed);
    public Kind ServerKind(NetworkConnection connection) => _manager != null && _manager.IsServerStarted &&
        connection != null && _effects.TryGetValue(connection, out Effect effect) && effect.Expires > Now ? effect.Type : Kind.None;
    public bool ServerMatchActive => _manager != null && _manager.IsServerStarted && _timer.timerIsRunning &&
        _timer.currentState == RoundTimer.gameStates.GAME;
    public bool ClientInvisible(int clientId) => _clientEffects.TryGetValue(clientId, out Effect effect) &&
        effect.Type == Kind.Invisibility && _manager != null && effect.Expires > Now;

    // One authority for pickups, approved development fixtures and killstreak rewards.
    private bool ApplyServerBoost(NetworkConnection connection, Kind kind, double seconds, Health expectedPlayer = null)
    {
        if (!ServerMatchActive || connection == null || !connection.IsActive || !connection.IsAuthenticated ||
            kind < Kind.Shield || kind > Kind.Invisibility || !ServerGameplayValidation.Finite(seconds) || seconds <= 0) return false;
        Health player = null;
        foreach (NetworkObject obj in connection.Objects)
            if (obj != null && obj.IsSpawned && obj.gameObject.scene == gameObject.scene &&
                obj.TryGetComponent(out Health candidate) && candidate.CanAct) { player = candidate; break; }
        if (player == null || (expectedPlayer != null && player != expectedPlayer)) return false;
        if (kind == Kind.Armor)
        {
            if (!player.AddServerArmor(100)) return false;
        }
        else _effects[connection] = new Effect { ClientId = connection.ClientId, Type = kind, Expires = Now + seconds };
        _nextSnapshot = 0;
        Debug.Log($"[Boost] Server granted {kind} to connection {connection.ClientId}.");
        return true;
    }
    public void RewardServerStreak(NetworkConnection connection, int streak)
    {
        if (!CombatEffectRules.EarnsReward(streak) || !ServerMatchActive) return;
        Kind kind = (Kind)CombatEffectRules.OriginalReward(UnityEngine.Random.Range(1, 5));
        if (ApplyServerBoost(connection, kind, duration)) Debug.Log($"[Streak] owner={connection.ClientId} streak={streak} reward={kind}.");
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // Repeatable power-up compatibility tests from the host Inspector. No grant RPC is exposed.
    public void DevelopmentGrantBoost(NetworkConnection connection, Kind kind)
    {
        if (_manager == null || !_manager.IsServerStarted || connection == null ||
            !connection.IsActive || !connection.IsAuthenticated || !_timer.timerIsRunning ||
            _timer.currentState != RoundTimer.gameStates.GAME || kind == Kind.None) return;
        ApplyServerBoost(connection, kind, 8d);
    }
#endif
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
            foreach (NetworkConnection c in new List<NetworkConnection>(_effects.Keys))
                if (!c.IsActive || !c.IsAuthenticated || _effects[c].Expires <= Now) _effects.Remove(c);
            if (_terrain == null) _terrain = FindFirstObjectByType<TerrainGenerator>();
            if (_terrain != null && _terrain.IsGenerated && Now >= _nextSpawn && _pickups.Count < 4)
            {
                _nextSpawn = Now + 10d;
                SpawnPickup();
            }
            foreach (NetworkConnection c in _manager.ServerManager.Clients.Values)
            {
                if (!c.IsActive || !c.IsAuthenticated) continue;
                foreach (NetworkObject obj in c.Objects)
                {
                    Health health = obj.GetComponent<Health>();
                    if (health == null) continue;
                    for (int i = _pickups.Count - 1; i >= 0; i--)
                    {
                        if (TryCollectServer(c, health, _pickups[i].Id)) break;
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

    // Same atomic decision for the server polling loop and server-only regression fixtures.
    // Not an RPC: clients never name pickups, expected effects, actors or round IDs.
    internal bool TryCollectServer(NetworkConnection connection, Health player, int pickupId)
    {
        if (!ServerMatchActive || connection == null || !connection.IsActive || !connection.IsAuthenticated ||
            player == null || !player.IsServerInitialized || !player.IsSpawned || !player.CanAct ||
            !ReferenceEquals(player.Owner, connection) || player.gameObject.scene != gameObject.scene) return false;
        int index = _pickups.FindIndex(p => p.Id == pickupId);
        if (index < 0) return false;
        Pickup pickup = _pickups[index];
        Vector3 delta = player.transform.position - pickup.Position;
        if (!ServerGameplayValidation.Finite(pickup.Expires) || pickup.Expires <= Now || !ServerGameplayValidation.WithinPickupRange(delta.x, delta.y, delta.z, 2.5d)) return false;
        var capsule = player.GetComponent<CharacterController>();
        if (capsule == null) return false;
        Vector3 body = player.transform.TransformPoint(capsule.center);
        if (!_collectionGeometry.OriginClear(body, player.transform) ||
            !_collectionGeometry.WorldSegmentClear(body, pickup.Position, player.transform)) return false;
        if (!ApplyServerBoost(connection, pickup.Type, duration, player)) return false;
        _pickups.RemoveAt(index); _nextSnapshot = 0;
        return true;
    }

    private void SpawnPickup()
    {
        Vector2 centre = _terrain.worldSettings.WorldCentre(_terrain.MeshWorldSize);
        for (int attempt = 0; attempt < 8; attempt++)
        {
            float angle = ((_nextId + attempt) % 8) * Mathf.PI / 4;
            Vector3 point = new Vector3(centre.x + Mathf.Cos(angle) * 10f, 0f, centre.y + Mathf.Sin(angle) * 10f);
            if (_pickups.Exists(p => new Vector2(p.Position.x - point.x, p.Position.z - point.z).sqrMagnitude < 16f) ||
                !_terrain.TryGetDrySurface(point, out RaycastHit hit)) continue;
            _pickups.Add(new Pickup { Id = ++_nextId, Position = hit.point + Vector3.up * 0.8f,
                Type = (Kind)(1 + (_nextId - 1) % 6), Expires = Now + 30d });
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
            { Kind.Shield => Color.cyan, Kind.Speed => Color.green, Kind.Jump => Color.yellow,
                Kind.Armor => Color.gray, Kind.Invisibility => new Color(.65f, .3f, .95f), _ => Color.red };
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
            _manager.ServerManager.OnRemoteConnectionState -= OnRemoteState;
            _manager.ServerManager.OnServerConnectionState -= OnServerState;
        }
        foreach (GameObject visual in _visuals.Values) if (visual != null) Destroy(visual.GetComponent<Renderer>().material);
        if (Instance == this) Instance = null;
    }
}
