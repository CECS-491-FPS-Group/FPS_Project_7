using System.Collections.Generic;
using FishNet.Component.Transforming;
using FishNet.Connection;
using UnityEngine;
using UnityEngine.SceneManagement;

// Server-only observation attached at runtime. Not a NetworkBehaviour or enforcement source.
[DisallowMultipleComponent]
public sealed class OccludedAimObserver : MonoBehaviour
{
    private static readonly List<OccludedAimObserver> Actors = new();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Actors.Clear();
    private const int MaximumActors = 16;
    private HitscanShooter _shooter;
    private Health _health;
    private PlayerCameraSetup _posture;
    private CharacterController _capsule;
    private NetworkTransform _networkTransform;
    private NetworkConnection _owner;
    private uint _life, _tick, _sequence;
    private bool _haveTick, _haveSequence, _stance, _running;
    private ServerGameplayValidation.RequestBudget _budget;
    private readonly AimOcclusionGeometry _geometry = new();
    private readonly OccludedAimTracking _tracking = new();
    private Vector3 _aim, _previousPosition, _goal;
    private double _aimAt = -100, _goalAt = -100, _nextSample, _lastFrame, _graceUntil, _shotAt = -100, _hurtAt = -100, _nextLog;
    private uint _aimVersion, _usedAim, _goalVersion, _usedGoal;
    private OccludedAimObserver _target;
    private uint _targetLife;
    private NetworkConnection _targetOwner;
    private BoxCollider _cover;
    private Matrix4x4 _coverMatrix;
    private Vector3 _coverSize, _coverCentre;
    private TerrainGenerator _terrain;
    private static double Now => Time.realtimeSinceStartupAsDouble;
    internal void Begin(HitscanShooter shooter)
    {
        End(); _shooter = shooter; _health = GetComponent<Health>(); _posture = GetComponent<PlayerCameraSetup>();
        _capsule = GetComponent<CharacterController>(); _networkTransform = GetComponent<NetworkTransform>();
        _terrain = FindFirstObjectByType<TerrainGenerator>();
        _running = true; ResetSession();
        if (Actors.Count >= MaximumActors) { _running = false; Debug.LogWarning("[Aim AC] observer unavailable: capacity 16; not a player verdict.", this); return; }
        Actors.Add(this);
        if (_networkTransform != null) _networkTransform.OnDataReceived += ReceivedPosition;
        _health.HealthPresentationChanged += HealthChanged;
    }
    internal void End()
    {
        _running = false; Actors.Remove(this);
        if (_networkTransform != null) _networkTransform.OnDataReceived -= ReceivedPosition;
        if (_health != null) _health.HealthPresentationChanged -= HealthChanged;
        _owner = null; _budget = null; _haveTick = _haveSequence = false;
        _aimVersion = _goalVersion = _usedAim = _usedGoal = 0;
        _aim = _goal = default; _aimAt = _goalAt = -100;
        Clear(Now); _target = null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        DevelopmentStopAimFixture();
#endif
    }
    private void OnDestroy() => End();
    private void OnDisable() => End();
    private void ResetSession()
    {
        _owner = _shooter.Owner; _life = _health.ServerLifeVersion; _stance = _posture.Crouched;
        _budget = new ServerGameplayValidation.RequestBudget(6, 4);
        _haveTick = _haveSequence = false; _aimAt = _goalAt = _shotAt = _hurtAt = -100;
        _aimVersion = _usedAim = _goalVersion = _usedGoal = 0;
        _nextLog = _nextSample = 0;
        _previousPosition = transform.position; _lastFrame = Now;
        Clear(Now); _graceUntil = Now + 1.5;
    }
    private void Clear(double now)
    {
        _target = null; _targetOwner = null; _targetLife = 0; _usedGoal = 0;
        _cover = null; _tracking.Reset(now);
    }
    internal void Receive(Vector3 direction, uint sequence)
    {
        if (!_running) return;
        if (!ReferenceEquals(_owner, _shooter.Owner)) ResetSession();
        double now = Now;
        if (!_budget.TryConsume(now) || !_health.CanAct ||
            !ServerGameplayValidation.Finite(direction.x) || !ServerGameplayValidation.Finite(direction.y) || !ServerGameplayValidation.Finite(direction.z) ||
            direction.sqrMagnitude < .99f || direction.sqrMagnitude > 1.01f ||
            Mathf.Abs(Mathf.Asin(Mathf.Clamp(direction.y, -1, 1)) * Mathf.Rad2Deg) > _shooter.ApprovedAimPitch + .1f ||
            (_haveSequence && unchecked((int)(sequence - _sequence)) <= 0)) return;
        _sequence = sequence; _haveSequence = true;
        _aim = direction.normalized; _aimAt = now; _aimVersion++;
    }
    private void ReceivedPosition(NetworkTransform.TransformData previous, NetworkTransform.TransformData next)
    {
        if (!_running || !_shooter.IsServerInitialized || !_health.CanAct || !_owner.IsActive || !_owner.IsAuthenticated ||
            _owner.IsLocalClient || next.ParentBehaviour != null ||
            (_haveTick && !ReceivedMovementObservation.IsNewTick(true, _tick, next.Tick))) return;
        if (!Finite(next.Position)) { _goalAt = -100; Clear(Now); return; }
        _haveTick = true; _tick = next.Tick;
        if ((next.Position - _goal).sqrMagnitude <= .000001f) return;
        _goal = next.Position; _goalAt = Now; _goalVersion++;
    }
    internal void AcceptedShot() => _shotAt = Now;
    private void HealthChanged(int previous, int next, bool asServer) { if (asServer && next < previous) _hurtAt = Now; }
    private bool Eligible => _running && _shooter != null && _shooter.IsServerInitialized && _shooter.IsSpawned &&
        _health.CanAct && _owner != null && _owner.IsActive && _owner.IsAuthenticated &&
        ReferenceEquals(_owner, _shooter.Owner) && _life == _health.ServerLifeVersion && _stance == _posture.Crouched &&
        Vector3.Distance(transform.position, _previousPosition) <= 4 &&
        MatchBoosts.Instance != null && MatchBoosts.Instance.ServerMatchActive &&
        MatchBoosts.Instance.ServerKind(_owner) != MatchBoosts.Kind.Invisibility &&
        Finite(transform.position) &&
        transform.parent == null && Vector3.Distance(transform.lossyScale, Vector3.one) < .001f &&
        Vector3.Dot(transform.up, Vector3.up) > .999f && _capsule != null && _capsule.enabled &&
        _terrain != null && _terrain.IsGenerated && _terrain.IsColliderReadyAt(transform.position);
    private void LateUpdate()
    {
        if (!_running || _shooter == null || !_shooter.IsServerInitialized) return;
        double now = Now;
        if (!ReferenceEquals(_owner, _shooter.Owner)) ResetSession();
        bool reset = !Eligible || _life != _health.ServerLifeVersion || _stance != _posture.Crouched || now - _lastFrame > .65;
        _life = _health.ServerLifeVersion; _stance = _posture.Crouched; _lastFrame = now;
        if (reset) { Clear(now); _previousPosition = transform.position; _graceUntil = now + 1.5; return; }
        if (now < _nextSample) return;
        _nextSample = now + .2;
        Vector3 position = transform.position;
        float travel = Vector3.Distance(position, _previousPosition); _previousPosition = position;
        // Viewer movement produces bearing changes too. This first rule requires a stationary observer.
        if (travel > 4) _graceUntil = now + 1.5;
        if (travel > .05f || now - _aimAt > .65) { Clear(now); return; }
        if (now < _graceUntil || _aimVersion == _usedAim) return;
        _usedAim = _aimVersion;
        Vector3 eye = _posture.ServerShotOrigin;
        OccludedAimObserver best = null; float bestAngle = 10, secondAngle = 180;
        foreach (var actor in Actors)
        {
            if (actor == this || !actor.Eligible || actor.gameObject.scene != gameObject.scene || now < actor._graceUntil) continue;
            Vector3 delta = actor._capsule.bounds.center - eye;
            if (delta.sqrMagnitude > _shooter.ApprovedAimRange * _shooter.ApprovedAimRange || delta.sqrMagnitude < 1) continue;
            float angle = Vector3.Angle(_aim, delta);
            if (angle < bestAngle) { secondAngle = bestAngle; bestAngle = angle; best = actor; }
            else if (angle < secondAngle) secondAngle = angle;
        }
        if (best == null || secondAngle - bestAngle < 2) { Clear(now); return; }
        if (_target != best || _targetLife != best._life || !ReferenceEquals(_targetOwner, best._owner))
        {
            Clear(now); _target = best; _targetLife = best._life; _targetOwner = best._owner; _usedGoal = best._goalVersion; return;
        }
        bool fresh = best._owner.IsLocalClient || (best._goalVersion != _usedGoal && now - best._goalAt <= .65 &&
            Vector3.Distance(best._goal, best.transform.position) < .9f);
        if (!fresh && now - best._goalAt <= .65) return; // wait for a new pair, never count a duplicate
        _usedGoal = best._goalVersion;
        float distance = Vector3.Distance(eye, best._capsule.bounds.center);
        // Potential cues, not an assertion of hearing. Moving targets nearby could be making footsteps.
        const float cueMargin = 2; // emitter/eye/body offsets and receipt timing
        bool cue = distance <= PlayerAudio.NearbyCueRange + cueMargin ||
            (distance <= PlayerAudio.ShotCueRange + cueMargin && now - best._shotAt < 2.5) ||
            (distance <= PlayerAudio.NearbyCueRange + cueMargin && now - best._hurtAt < 2.5);
        if (!fresh || cue) { _tracking.Reset(now); _cover = null; return; }
        Vector3 targetEye = best._posture.ServerShotOrigin; // apply approved stance before sampling collision bounds
        Bounds bounds = best._capsule.bounds;
        if (!best._owner.IsLocalClient)
        {
            // A smoothed body and its latest received position may straddle an edge.
            // Require coverage of both instead of calling that timing mismatch hidden.
            Bounds receivedBounds = bounds; receivedBounds.center += best._goal - best.transform.position;
            bounds.Encapsulate(receivedBounds);
        }
        OccludedAimTracking.Visibility visibility = _geometry.Evaluate(Physics.defaultPhysicsScene, eye, eye, bounds, transform, best.transform);
        BoxCollider cover = _geometry.Cover;
        if (cover != _cover || (cover != null && (cover.transform.localToWorldMatrix != _coverMatrix || cover.size != _coverSize || cover.center != _coverCentre)))
        {
            _tracking.Reset(now); _cover = cover;
            if (cover != null) { _coverMatrix = cover.transform.localToWorldMatrix; _coverSize = cover.size; _coverCentre = cover.center; }
        }
        Vector3 bearing = (bounds.center - eye).normalized;
        bool report = _tracking.Observe(new OccludedAimTracking.Sample { Time = now, Fresh = fresh, PossibleCue = cue, Cover = visibility,
            AimYaw = Yaw(_aim), AimPitch = Pitch(_aim), TargetYaw = Yaw(bearing), TargetPitch = Pitch(bearing) });
        if (!report || now < _nextLog) return;
        _nextLog = now + 10;
        Debug.LogWarning($"[Aim AC] action=ReportOnly rule=correlated-occluded-aim source=client-aim owner={_owner.ClientId} object={_shooter.ObjectId} " +
            $"target={best._owner.ClientId}/{best._shooter.ObjectId} duration={_tracking.Duration:F2}s samples={_tracking.Samples} matched={_tracking.MatchedTime:F2}s " +
            $"aimTravel={_tracking.AimTravel:F2}deg targetTravel={_tracking.TargetTravel:F2}deg error={bestAngle:F2}deg distance={distance:F1}m " +
            $"visibility={visibility} cover={cover.name} aimAge={now - _aimAt:F3}s targetAge={(best._owner.IsLocalClient ? 0 : now - best._goalAt):F3}s " +
            $"crouched={_stance}/{best._stance} effect={MatchBoosts.Instance.ServerKind(_owner)}/{MatchBoosts.Instance.ServerKind(best._owner)} " +
            $"cue=none-in-conservative-range queries={_geometry.Queries}; review signal only, no punitive evidence.", this);
    }
    public static float Yaw(Vector3 v) => Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;
    public static float Pitch(Vector3 v) => Mathf.Asin(Mathf.Clamp(v.normalized.y, -1, 1)) * Mathf.Rad2Deg;
    private static bool Finite(Vector3 v) => ServerGameplayValidation.Finite(v.x) && ServerGameplayValidation.Finite(v.y) && ServerGameplayValidation.Finite(v.z);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private Coroutine _fixture;
    private UnityEngine.SceneManagement.Scene _fixtureScene;
    [ContextMenu("Development/Log aim observation state (server only)")]
    private void DevelopmentAimQuality()
    {
        if (!_running || !_shooter.IsServerInitialized) return;
        Debug.Log($"[Aim AC SAMPLE] owner={_shooter.OwnerId} eligible={Eligible} aimSeen={_haveSequence} aimAge={Now - _aimAt:F3}s " +
            $"sequence={_sequence} target={(_target != null ? _target._shooter.OwnerId.ToString() : "none")} " +
            $"cover={(_cover != null ? _cover.name : "none/uncertain")} samples={_tracking.Samples} duration={_tracking.Duration:F2}s " +
            $"lastQueries={_geometry.Queries}; client-reported aim, sample-quality context only, not a verdict.", this);
    }
    [ContextMenu("Development/Run occluded aim fixture (server only)")]
    private void DevelopmentAimFixture()
    {
        if (!_running || !_shooter.IsServerInitialized || _fixture != null) return;
        _fixture = StartCoroutine(RunFixture());
    }
    [ContextMenu("Development/Stop occluded aim fixture")]
    private void DevelopmentStopAimFixture()
    {
        if (_fixture != null) { StopCoroutine(_fixture); _fixture = null; }
        if (_fixtureScene.IsValid()) UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(_fixtureScene);
        _fixtureScene = default;
    }
    private System.Collections.IEnumerator RunFixture()
    {
        _fixtureScene = UnityEngine.SceneManagement.SceneManager.CreateScene("Aim fixture " + GetInstanceID(),
            new UnityEngine.SceneManagement.CreateSceneParameters(UnityEngine.SceneManagement.LocalPhysicsMode.Physics3D));
        var wall = new GameObject("Synthetic solid cover", typeof(BoxCollider));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(wall, _fixtureScene);
        wall.transform.position = new Vector3(0, 2, 25); wall.GetComponent<BoxCollider>().size = new Vector3(20, 10, 1);
        var scene = _fixtureScene.GetPhysicsScene(); scene.Simulate(.02f);
        var geometry = new AimOcclusionGeometry(); var tracking = new OccludedAimTracking(); tracking.Reset(0);
        Debug.Log("[Aim AC FIXTURE] started: isolated synthetic target/aim/physics, not human-play evidence; 12s auto cleanup.", this);
        for (int i = 0; i <= 60; i++)
        {
            double time = i * .2; Vector3 eye = Vector3.up;
            var bounds = new Bounds(new Vector3(5 * Mathf.Sin((float)time), 1, 50), new Vector3(1, 2, 1));
            Vector3 bearing = (bounds.center - eye).normalized;
            var visibility = geometry.Evaluate(scene, eye, eye, bounds);
            if (tracking.Observe(new OccludedAimTracking.Sample { Time = time, Fresh = true, Cover = visibility,
                AimYaw = Yaw(bearing), AimPitch = Pitch(bearing), TargetYaw = Yaw(bearing), TargetPitch = Pitch(bearing) }))
                Debug.LogWarning($"[Aim AC FIXTURE] action=ReportOnly rule=correlated-occluded-aim source=scripted-fixture contextOwner={_shooter.OwnerId} target=synthetic " +
                    $"duration={tracking.Duration:F2}s samples={tracking.Samples} matched={tracking.MatchedTime:F2}s visibility={visibility} queries={geometry.Queries}; no connection or policy action.", this);
            yield return new WaitForSecondsRealtime(.2f);
        }
        UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(_fixtureScene); _fixtureScene = default; _fixture = null;
        Debug.Log("[Aim AC FIXTURE] finished/cleaned; live actors unchanged.", this);
    }
#endif
}
