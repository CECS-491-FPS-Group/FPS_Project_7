using FishNet.Connection;
using FishNet.Object;
using UnityEngine;
using FishNet.Component.Transforming;
using StarterAssets;
using Unity.Profiling;

// Observes the server's replicated position; optional server policy handles sustained abuse.
[DisallowMultipleComponent]
public sealed class SpeedHackDetector : NetworkBehaviour
{
    private static readonly ProfilerMarker ObserveMarker = new("AC.Movement.Observe");
    private const double SpawnGrace = 1.5d;
    private const double RespawnGrace = 1.5d;
    private const double LogInterval = 5d;
    private readonly HorizontalMovementObservation _observation = new HorizontalMovementObservation();
    private Health _health;
    private bool _serverRunning;
    private double _graceUntil, _nextLog;
    private uint _respawnToken;
    private int _suppressedLogs;
    private readonly ReceivedMovementObservation _received = new();
    private readonly MovementSupportQuery _support = new();
    private NetworkTransform _networkTransform;
    private CharacterController _capsule;
    private TerrainGenerator _terrain;
    private RoundTimer _round;
    private int _phase = -1;
    private bool _pendingGoal, _haveTick, _parentedGoal;
    private uint _receivedTick;
    private Vector3 _goal;
    private double _arrival, _nextReceivedSample, _lastSecondaryFrame, _secondarySpeedPeak, _secondaryUpPeak;
    private float _gravity, _slope, _step, _radius, _feetOffset, _levelTolerance;
    private bool _geometryValid;
    private readonly double[] _nextSecondaryLog = new double[3];
    private readonly int[] _secondarySuppressed = new int[3];
    private int _coalesced;

    private static double Now => Time.realtimeSinceStartupAsDouble;
    private void Awake() => _health = GetComponent<Health>();

    public override void OnStartServer()
    {
        base.OnStartServer();
        _serverRunning = true;
        _networkTransform = GetComponent<NetworkTransform>(); _capsule = GetComponent<CharacterController>();
        var controller = GetComponent<FirstPersonController>();
        _terrain = FindFirstObjectByType<TerrainGenerator>(); _round = FindFirstObjectByType<RoundTimer>();
        _phase = _round != null ? (int)_round.currentState : -1;
        _gravity = controller != null ? controller.Gravity : 0;
        _slope = _capsule != null ? _capsule.slopeLimit : 90;
        _step = _capsule != null ? _capsule.stepOffset : 0;
        _radius = _capsule != null ? _capsule.radius : 0;
        _feetOffset = _capsule != null ? _capsule.center.y - _capsule.height * .5f : 0;
        _levelTolerance = _capsule != null ? Mathf.Max(.05f, 2 * _capsule.skinWidth) : .05f;
        _geometryValid = controller != null && _capsule != null && ServerGameplayValidation.Finite(_gravity) && _gravity < 0 &&
            ServerGameplayValidation.Finite(_slope) && _slope >= 0 && _slope < 89 && _radius > 0;
        _geometryValid &= ServerGameplayValidation.Finite(_radius) && ServerGameplayValidation.Finite(_step) &&
            _step >= 0 && ServerGameplayValidation.Finite(_feetOffset) && ServerGameplayValidation.Finite(_levelTolerance);
        if (_networkTransform != null) { _networkTransform.OnDataReceived -= OnReceivedGoal; _networkTransform.OnDataReceived += OnReceivedGoal; }
        if (_networkTransform == null || !_geometryValid)
            Debug.LogWarning($"[Movement AC] received-goal observations unavailable owner={OwnerId}: server controller geometry/configuration incomplete; not a cheating verdict.", this);
        _respawnToken = 0;
        _nextLog = 0d;
        _suppressedLogs = 0;
        _graceUntil = Now + SpawnGrace;
        ResetBaseline(Now);
        Debug.Log($"[Movement AC] observer started owner={OwnerId} object={ObjectId} " +
            $"baseLimit={ApprovedMovementLimits.SprintSpeed:F1} boostedLimit=" +
            $"{ApprovedMovementLimits.SprintSpeed * ApprovedMovementLimits.SpeedMultiplier:F1}m/s.", this);
    }

    public override void OnStopServer()
    {
        _serverRunning = false;
        if (_networkTransform != null) _networkTransform.OnDataReceived -= OnReceivedGoal;
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
        using var observationScope = ObserveMarker.Auto();
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
        ObserveReceivedGoals(now); // Always separate from punitive horizontal suspicion/policy.
        HorizontalMovementObservation.Evidence? result =
            _observation.Observe(now, position.x, position.z, GetServerHorizontalSpeed());
        if (!result.HasValue) return;
        var evidence = result.Value;
        // Evaluate each fresh, non-overlapping result even while warning output is throttled.
        // Displacements and invalid coordinates cannot qualify for dynamic enforcement.
        var policy = NetworkManager.GetComponent<FileAntiCheat>();
        if (policy != null)
            policy.EvaluateDynamic(Owner, evidence.Rule, _observation.Suspicion,
                $"object={ObjectId} rule={evidence.Rule} distance={evidence.Distance:F2}m allowance={evidence.Allowance:F2}m " +
                $"elapsed={evidence.Elapsed:F2}s suspicion={_observation.Suspicion:F2} uptime={now:F2}");
        if (now < _nextLog)
        {
            if (_suppressedLogs < int.MaxValue) _suppressedLogs++;
            return;
        }
        _nextLog = now + LogInterval;
        Debug.LogWarning($"[Movement AC] observed owner={OwnerId} object={ObjectId} " +
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
        ResetReceived(now);
    }

    private void ResetReceived(double now)
    {
        _received.Reset(); _pendingGoal = _haveTick = false; _coalesced = 0;
        _lastSecondaryFrame = now; _nextReceivedSample = now;
        _secondarySpeedPeak = _secondaryUpPeak = 0;
        System.Array.Clear(_nextSecondaryLog, 0, _nextSecondaryLog.Length);
        System.Array.Clear(_secondarySuppressed, 0, _secondarySuppressed.Length);
    }
    private void OnReceivedGoal(NetworkTransform.TransformData previous, NetworkTransform.TransformData next)
    {
        if (!_serverRunning || !IsServerInitialized || !IsSpawned || Owner.IsLocalClient ||
            !Owner.IsActive || !Owner.IsAuthenticated || _health == null || !_health.CanAct || Now < _graceUntil) return;
        // Tick is a novelty marker only; never use client time to grant distance/time.
        if (!ReceivedMovementObservation.IsNewTick(_haveTick, _receivedTick, next.Tick)) return;
        _haveTick = true; _receivedTick = next.Tick;
        if (_pendingGoal && _coalesced < int.MaxValue) _coalesced++;
        _pendingGoal = true; _goal = next.Position; _arrival = Now; _parentedGoal = next.ParentBehaviour != null;
        // TransformData is pooled by FishNet. Copy values; never retain or mutate it.
    }
    private void ObserveReceivedGoals(double now)
    {
        int phase = _round != null ? (int)_round.currentState : -1;
        if (phase != _phase || now - _lastSecondaryFrame > HorizontalMovementObservation.MaxFrameGap)
        { _phase = phase; ResetReceived(now); return; }
        _lastSecondaryFrame = now;
        MatchBoosts.Kind kind = MatchBoosts.Instance != null ? MatchBoosts.Instance.ServerKind(Owner) : MatchBoosts.Kind.None;
        double speed = GetServerHorizontalSpeed();
        double height = ApprovedMovementLimits.JumpHeight * (kind == MatchBoosts.Kind.Jump ? ApprovedMovementLimits.JumpMultiplier : 1f);
        double launch = _geometryValid ? System.Math.Sqrt(-2 * _gravity * height) : 0;
        double up = ReceivedMovementObservation.UpwardCeiling(launch, speed, _slope);
        _secondarySpeedPeak = System.Math.Max(_secondarySpeedPeak, speed);
        _secondaryUpPeak = System.Math.Max(_secondaryUpPeak, up);
        if (Owner.IsLocalClient) { _received.InvalidateAir("trusted-host-no-remote-goals"); return; }
        if (!_pendingGoal)
        {
            if (now - _arrival > ReceivedMovementObservation.MaximumAirGap) _received.InvalidateAir("no-fresh-goals-not-hover-evidence");
            return;
        }
        if (now < _nextReceivedSample) return;
        _nextReceivedSample = now + ReceivedMovementObservation.Interval; _pendingGoal = false;
        bool rootSupported = !_parentedGoal && transform.parent == null &&
            Vector3.Distance(transform.lossyScale, Vector3.one) < .001f && Vector3.Dot(transform.up, Vector3.up) > .999f;
        bool finiteGoal = ServerGameplayValidation.Finite(_goal.x) && ServerGameplayValidation.Finite(_goal.y) && ServerGameplayValidation.Finite(_goal.z);
        if (!_geometryValid || !rootSupported || !finiteGoal)
        { _received.Reset(); _received.InvalidateAir("unsupported-server-geometry-or-parent"); return; }
        bool ready = _terrain != null && _terrain.IsGenerated && _terrain.IsColliderReadyAt(_goal);
        var support = _support.Evaluate(_goal + Vector3.up * _feetOffset, _radius, _step, transform, ready);
        var result = _received.Observe(new ReceivedMovementObservation.Sample { Time = _arrival,
            X = _goal.x, Y = _goal.y, Z = _goal.z, Speed = _secondarySpeedPeak, Launch = _secondaryUpPeak,
            Gravity = _gravity, Step = _step, LevelTolerance = _levelTolerance, Ground = support });
        _secondarySpeedPeak = speed; _secondaryUpPeak = up;
        if (!result.HasValue) return;
        var e = result.Value;
        int slot = e.Rule == "repeated-unexplained-displacement" ? 0 : e.Rule == "excessive-unsupported-ascent" ? 1 : 2;
        if (now < _nextSecondaryLog[slot]) { if (_secondarySuppressed[slot] < int.MaxValue) _secondarySuppressed[slot]++; return; }
        _nextSecondaryLog[slot] = now + LogInterval;
        Debug.LogWarning($"[Movement AC] action=ReportOnly owner={OwnerId} object={ObjectId} rule={e.Rule} serverUptime={now:F2} " +
            $"observed={e.Observed:F3} allowed={e.Allowed:F3} elapsed={e.Elapsed:F2}s samples={e.Samples} " +
            $"distance={e.Distance:F2}m source=received-goal arrivalAge={now - _arrival:F3}s tick={_receivedTick} " +
            $"support={support} quality={_received.Quality} effect={kind} armor={_health.CurrentArmor} " +
            $"approvedSpeed={speed:F2} upCeiling={up:F2} gravity={_gravity:F2} step={_step:F2} terrainReady={ready} root={_goal} " +
            $"crouched={GetComponent<PlayerCameraSetup>().Crouched} phase={phase} grace=complete " +
            $"coalesced={_coalesced} suppressed={_secondarySuppressed[slot]}; no punitive suspicion added.", this);
        _coalesced = _secondarySuppressed[slot] = 0;
    }
    private void OnDestroy()
    {
        if (_networkTransform != null) _networkTransform.OnDataReceived -= OnReceivedGoal;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [ContextMenu("Development/Log movement sample quality")]
    private void DevelopmentSampleQuality() => Debug.Log($"[Movement AC SAMPLE] owner={OwnerId} object={ObjectId} " +
        $"server={IsServerInitialized} quality={_received.Quality} pending={_pendingGoal} tickSeen={_haveTick} " +
        $"lastArrivalAge={(_haveTick ? (Now - _arrival).ToString("F2") : "none")} coalesced={_coalesced}; no verdict.", this);
    // Server-only test fixtures: no client RPC exists for granting boosts.
    [ContextMenu("Development/Grant Speed (8 seconds)")]
    private void TestSpeed() => GrantTestBoost(MatchBoosts.Kind.Speed);
    [ContextMenu("Development/Grant Shield (8 seconds)")]
    private void TestShield() => GrantTestBoost(MatchBoosts.Kind.Shield);
    [ContextMenu("Development/Grant Jump (8 seconds)")]
    private void TestJump() => GrantTestBoost(MatchBoosts.Kind.Jump);
    [ContextMenu("Development/Grant Damage (8 seconds)")]
    private void TestDamage() => GrantTestBoost(MatchBoosts.Kind.Damage);
    [ContextMenu("Development/Grant Armor")]
    private void TestArmor() => GrantTestBoost(MatchBoosts.Kind.Armor);
    [ContextMenu("Development/Grant Invisibility (8 seconds)")]
    private void TestInvisibility() => GrantTestBoost(MatchBoosts.Kind.Invisibility);
    private void GrantTestBoost(MatchBoosts.Kind kind)
    {
        if (IsServerInitialized && _health != null && _health.CanAct && MatchBoosts.Instance != null)
            MatchBoosts.Instance.DevelopmentGrantBoost(Owner, kind);
    }
#endif
}
