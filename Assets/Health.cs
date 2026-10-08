using System;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

public class Health : NetworkBehaviour
{
    [Min(1)] public int maxHp = 100;
    [Min(0)] public int killReward = 300;
    [Min(0f)] public float respawnDelay = 5f;
    private readonly SyncVar<int> _currentHp = new SyncVar<int>();
    private readonly SyncVar<bool> _dead = new SyncVar<bool>();
    private readonly SyncVar<bool> _matchEnded = new SyncVar<bool>();
    private readonly SyncVar<double> _respawnAt = new SyncVar<double>();
    private double _nextRespawnCheck;
    private uint _sequence;
    private uint _pendingSequence;
    private uint _localSequence;
    private bool _localPlacementPending;
    private Vector3 _respawnPosition;

    public int CurrentHealth => _currentHp.Value;
    public bool IsDead => _dead.Value || CurrentHealth <= 0;
    public bool MatchEnded => _matchEnded.Value;
    public bool CanAct => !MatchEnded && !IsDead && !_localPlacementPending;
    public double RespawnSecondsRemaining => IsDead
        ? Math.Max(0d, _respawnAt.Value - TimeManager.TicksToTime(TimeManager.Tick)) : 0d;

    private void Awake()
    {
        _currentHp.OnChange += OnHealthChanged;
    }

    private void OnDestroy()
    {
        _currentHp.OnChange -= OnHealthChanged;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        _currentHp.Value = Mathf.Max(1, maxHp);
        _dead.Value = false;
        _matchEnded.Value = false;
        _pendingSequence = 0;
    }

    public override void OnStopServer()
    {
        _pendingSequence = 0;
        base.OnStopServer();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        Debug.Log($"[Health] Client {LocalConnection.ClientId}: player owner={OwnerId} initial HP={CurrentHealth}.", this);
    }

    public override void OnStopClient()
    {
        _localPlacementPending = false;
        _localSequence = 0;
        base.OnStopClient();
    }

    // Not an RPC. The shooter connection comes from the validated server shot.
    public void TakeDamage(int damage, NetworkConnection shooter)
    {
        if (!IsServerInitialized || !IsSpawned || damage <= 0 || IsDead || MatchEnded) return;
        int previous = CurrentHealth;
        _currentHp.Value = Mathf.Max(0, previous - damage);
        Debug.Log($"[Combat] Damage shooter={shooter?.ClientId} victim={OwnerId} damage={damage} HP={previous}->{CurrentHealth}.", this);
        if (previous > 0 && CurrentHealth == 0)
        {
            _dead.Value = true;
            _respawnAt.Value = TimeManager.TicksToTime(TimeManager.Tick) + Mathf.Max(0f, respawnDelay);
            _nextRespawnCheck = _respawnAt.Value;
            _pendingSequence = 0;
            if (shooter != null && shooter != Owner && shooter.IsActive && shooter.IsAuthenticated &&
                TrackPlayerCurrency.instance != null)
                TrackPlayerCurrency.instance.AddCurrency(shooter, killReward);
            Debug.Log($"[Respawn] Player {OwnerId} died; killer={shooter?.ClientId}, delay={respawnDelay}s.", this);
        }
    }

    public void EndMatch()
    {
        if (!IsServerInitialized || !IsSpawned || MatchEnded) return;
        _matchEnded.Value = true;
        _pendingSequence = 0;
        _nextRespawnCheck = double.PositiveInfinity;
        _respawnAt.Value = 0d;
    }

    private void Update()
    {
        if (!IsServerInitialized || !IsSpawned || MatchEnded || !_dead.Value || _pendingSequence != 0 ||
            !Owner.IsActive || !Owner.IsAuthenticated) return;
        double now = TimeManager.TicksToTime(TimeManager.Tick);
        if (now < _nextRespawnCheck) return;
        _nextRespawnCheck = now + 1d;
        GameplayPlayerSpawner spawner = NetworkManager.GetComponent<GameplayPlayerSpawner>();
        if (spawner == null || !spawner.TryGetRespawnPosition(NetworkObject, out _respawnPosition)) return;

        // Keep this object/session; the server selects the destination and the owner applies it.
        _sequence++;
        if (_sequence == 0) _sequence++;
        _pendingSequence = _sequence;
        SetServerPosition(_respawnPosition);
        Debug.Log($"[Respawn] Server placing player {OwnerId} at {_respawnPosition}, token={_pendingSequence}.", this);
        PlaceForRespawn(Owner, _pendingSequence, _respawnPosition);
    }

    private void SetServerPosition(Vector3 position)
    {
        CharacterController controller = GetComponent<CharacterController>();
        bool wasEnabled = controller != null && controller.enabled;
        if (controller != null) controller.enabled = false;
        transform.position = position;
        if (controller != null) controller.enabled = wasEnabled;
    }

    [TargetRpc]
    private void PlaceForRespawn(NetworkConnection target, uint token, Vector3 position)
    {
        if (!IsOwner || MatchEnded || token == 0 || token <= _localSequence) return;
        _localSequence = token;
        _localPlacementPending = true;
        PlayerCameraSetup controls = GetComponent<PlayerCameraSetup>();
        if (controls == null || !controls.ApplyRespawnPosition(position)) return;
        AcknowledgeRespawn(token);
    }

    // The client sends only the server-issued token, never a position, HP, or reward.
    [ServerRpc(RequireOwnership = true)]
    private void AcknowledgeRespawn(uint token, NetworkConnection sender = null)
    {
        if (!IsServerInitialized || !IsSpawned || MatchEnded || sender == null || sender != Owner ||
            !sender.IsActive || !sender.IsAuthenticated || !_dead.Value ||
            token == 0 || token != _pendingSequence) return;

        SetServerPosition(_respawnPosition);
        _pendingSequence = 0;
        _currentHp.Value = Mathf.Max(1, maxHp);
        _dead.Value = false;
        _respawnAt.Value = 0d;
        Debug.Log($"[Respawn] Server accepted player {OwnerId} token={token}; HP={CurrentHealth}.", this);
        CompleteRespawn(Owner, token);
    }

    [TargetRpc]
    private void CompleteRespawn(NetworkConnection target, uint token)
    {
        if (IsOwner && !MatchEnded && token == _localSequence) _localPlacementPending = false;
    }

    private void OnHealthChanged(int previous, int next, bool asServer)
    {
        if (!asServer)
            Debug.Log($"[Health] Client {LocalConnection.ClientId}: player owner={OwnerId} synchronized HP={previous}->{next}.", this);
    }
}
