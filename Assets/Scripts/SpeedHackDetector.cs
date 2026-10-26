using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

// Observes the server's replicated position. Never corrects a transform or disconnects anyone.
[DisallowMultipleComponent]
public sealed class SpeedHackDetector : NetworkBehaviour
{
    private const double SpawnGrace = 1.5d;
    private const double RespawnGrace = 1.5d;
    private const double LogInterval = 5d;
    private readonly HorizontalMovementObservation _observation = new HorizontalMovementObservation();
    private Health _health;
    private bool _serverRunning;
    private double _graceUntil, _nextLog;
    private uint _respawnToken;
    private int _suppressedLogs;

    private static double Now => Time.realtimeSinceStartupAsDouble;
    private void Awake() => _health = GetComponent<Health>();

    public override void OnStartServer()
    {
        base.OnStartServer();
        _serverRunning = true;
        _respawnToken = 0;
        _nextLog = 0d;
        _suppressedLogs = 0;
        _graceUntil = Now + SpawnGrace;
        ResetBaseline(Now);
        Debug.Log($"[Movement AC] report-only started owner={OwnerId} object={ObjectId} " +
            $"baseLimit={ApprovedMovementLimits.SprintSpeed:F1} boostedLimit=" +
            $"{ApprovedMovementLimits.SprintSpeed * ApprovedMovementLimits.SpeedMultiplier:F1}m/s.", this);
    }

    public override void OnStopServer()
    {
        _serverRunning = false;
        ResetBaseline(Now);
        base.OnStopServer();
    }

    public override void OnOwnershipServer(NetworkConnection previousOwner)
    {
        base.OnOwnershipServer(previousOwner);
        if (!_serverRunning) return;
        _graceUntil = Now + SpawnGrace;
        _respawnToken = 0;
        ResetBaseline(Now);
    }

    // Called only by Health after the server has selected a destination and issued a new token.
    // Acknowledgements do not call this, so delaying/replaying an acknowledgement cannot renew grace.
    public void AuthorizeServerRespawn(uint token)
    {
        if (!_serverRunning || !IsServerInitialized || _health == null || !_health.IsDead ||
            _health.MatchEnded || token == 0 || token == _respawnToken) return;
        _respawnToken = token;
        _graceUntil = Now + RespawnGrace;
        ResetBaseline(Now);
    }

    public void CompleteServerRespawn(uint token)
    {
        if (_serverRunning && IsServerInitialized && token != 0 && token == _respawnToken)
            ResetBaseline(Now); // Rebase after Health reapplies its destination; do not extend grace.
    }

    // Keep all authoritative horizontal allowance selection here. Never read owner controller values,
    // LocalKind, client ping, or any client-supplied speed. Other boost types retain the base limit.
    private double GetServerHorizontalSpeed()
    {
        bool speed = MatchBoosts.Instance != null && MatchBoosts.Instance.ServerHasSpeedBoost(Owner);
        return ApprovedMovementLimits.SprintSpeed * (speed ? ApprovedMovementLimits.SpeedMultiplier : 1f);
    }

    private void LateUpdate()
    {
        if (!_serverRunning || !IsServerInitialized || !IsSpawned) return;
        double now = Now;
        // Health's server flags also cover pending respawn placement and final match teardown.
        // Pregame/postgame movement remains observable while Health still permits normal gameplay.
        if (_health == null || _health.IsDead || _health.MatchEnded ||
            !Owner.IsActive || !Owner.IsAuthenticated || now < _graceUntil)
        {
            ResetBaseline(now);
            return;
        }

        Vector3 position = transform.position;
        HorizontalMovementObservation.Evidence? result =
            _observation.Observe(now, position.x, position.z, GetServerHorizontalSpeed());
        if (!result.HasValue) return;
        if (now < _nextLog)
        {
            if (_suppressedLogs < int.MaxValue) _suppressedLogs++;
            return;
        }
        _nextLog = now + LogInterval;
        var evidence = result.Value;
        Debug.LogWarning($"[Movement AC] report-only owner={OwnerId} object={ObjectId} " +
            $"serverUptime={now:F2} rule={evidence.Rule} distance={evidence.Distance:F2}m " +
            $"allowance={evidence.Allowance:F2}m elapsed={evidence.Elapsed:F2}s " +
            $"approvedLimit={GetServerHorizontalSpeed():F2}m/s budgetPeakLimit={evidence.Speed:F2}m/s " +
            $"suspicion={_observation.Suspicion:F2} " +
            $"xz=({position.x:F2},{position.z:F2}) suppressed={_suppressedLogs}.", this);
        _suppressedLogs = 0;
    }

    private void ResetBaseline(double now)
    {
        Vector3 position = transform.position;
        _observation.Reset(now, position.x, position.z);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // Server-only test fixtures: no client RPC exists for granting boosts.
    [ContextMenu("Development/Grant Speed (8 seconds)")]
    private void TestSpeed() => GrantTestBoost(MatchBoosts.Kind.Speed);
    [ContextMenu("Development/Grant Shield (8 seconds)")]
    private void TestShield() => GrantTestBoost(MatchBoosts.Kind.Shield);
    [ContextMenu("Development/Grant Jump (8 seconds)")]
    private void TestJump() => GrantTestBoost(MatchBoosts.Kind.Jump);
    [ContextMenu("Development/Grant Damage (8 seconds)")]
    private void TestDamage() => GrantTestBoost(MatchBoosts.Kind.Damage);
    private void GrantTestBoost(MatchBoosts.Kind kind)
    {
        if (IsServerInitialized && _health != null && _health.CanAct && MatchBoosts.Instance != null)
            MatchBoosts.Instance.DevelopmentGrantBoost(Owner, kind);
    }
#endif
}
