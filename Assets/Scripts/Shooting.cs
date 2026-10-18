using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(100)] // Sample aim after Starter Assets applies mouse look.
public class HitscanShooter : NetworkBehaviour
{
    [Min(1)] public int damage = 40;
    [Min(0.1f)] public float range = 100f;
    [Min(0.1f)] public float shotsPerSecond = 5f;
    [Range(0f, 180f)] public float maxYawDifference = 45f;
    [Range(0f, 89.9f)] public float maxPitch = 89f;
    public bool automaticFire;
    [Min(1)] public int magazineSize = 30;
    [Min(0.1f)] public float reloadDuration = 3f;

    private readonly SyncVar<int> _ammo = new(new SyncTypeSettings(ReadPermission.OwnerOnly));
    private readonly SyncVar<bool> _reloading = new(new SyncTypeSettings(ReadPermission.OwnerOnly));
    private readonly SyncVar<double> _reloadDeadline = new(new SyncTypeSettings(ReadPermission.OwnerOnly));
    public int CurrentAmmo => _ammo.Value;
    public bool IsReloading => _reloading.Value;
    public double ReloadSecondsRemaining => IsReloading && IsClientInitialized
        ? System.Math.Max(0d, _reloadDeadline.Value - ServerTime) : 0d;
    private double ServerTime => TimeManager.TicksToTime(TimeManager.Tick);
    private bool _wasDead;
    private double _nextLocalShotTime;

    private Camera _camera;
    private Health _health;
    private CombatPresentation _presentation;
    private double _nextServerShotTime;

    private void Awake()
    {
        _camera = GetComponent<Camera>();
        _health = GetComponentInParent<Health>();
        _presentation = GetComponentInParent<CombatPresentation>();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        _nextServerShotTime = 0d;
        _ammo.Value = Mathf.Max(1, magazineSize);
        _reloading.Value = false;
        _reloadDeadline.Value = 0d;
        _wasDead = false;
    }

    private void Update() => ReconcileServerWeapon();

    private void ReconcileServerWeapon()
    {
        if (!IsServerInitialized || !IsSpawned || _health == null) return;
        if (_health.IsDead) _wasDead = true;
        if (_health.IsDead || _health.MatchEnded)
        {
            if (IsReloading) Debug.Log($"[Weapon] Owner={OwnerId} reload cancelled.", this);
            _reloading.Value = false;
            _reloadDeadline.Value = 0d;
            return;
        }
        // Health becomes alive only after its server-approved respawn acknowledgement.
        if (_wasDead)
        {
            _wasDead = false;
            _ammo.Value = Mathf.Max(1, magazineSize);
            _reloading.Value = false;
            _reloadDeadline.Value = 0d;
            Debug.Log($"[Weapon] Owner={OwnerId} respawn magazine={CurrentAmmo}.", this);
        }
        if (IsReloading && ServerTime >= _reloadDeadline.Value)
        {
            _ammo.Value = Mathf.Max(1, magazineSize);
            _reloading.Value = false;
            _reloadDeadline.Value = 0d;
            Debug.Log($"[Weapon] Owner={OwnerId} reload complete; ammo={CurrentAmmo}.", this);
        }
    }

    [ServerRpc(RequireOwnership = true)]
    private void RequestReload(NetworkConnection sender = null)
    {
        if (!IsServerInitialized || !IsSpawned || sender == null || sender != Owner ||
            !sender.IsActive || !sender.IsAuthenticated) return;
        ReconcileServerWeapon();
        if (_health == null || !_health.CanAct || IsReloading || CurrentAmmo >= magazineSize ||
            magazineSize < 1 || !Finite(reloadDuration) || reloadDuration <= 0f) return;
        _reloading.Value = true;
        _reloadDeadline.Value = ServerTime + reloadDuration;
        Debug.Log($"[Weapon] Owner={OwnerId} reload started ({reloadDuration}s); ammo={CurrentAmmo}.", this);
    }

    public override void OnStopClient()
    {
        _nextLocalShotTime = 0d;
        base.OnStopClient();
    }

    private void LateUpdate()
    {
        if (!IsClientInitialized || !IsOwner || _camera == null || !_camera.isActiveAndEnabled) return;
        if (_health == null || !_health.CanAct)
        {
            _nextLocalShotTime = 0d;
            return;
        }
        if (_presentation != null) _presentation.PrepareAim(maxPitch);
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            RequestReload();
            return;
        }
        if (Mouse.current == null || IsReloading || CurrentAmmo <= 0) return;
        bool wantsFire = automaticFire ? Mouse.current.leftButton.isPressed : Mouse.current.leftButton.wasPressedThisFrame;
        if (wantsFire && (!automaticFire || Time.unscaledTimeAsDouble >= _nextLocalShotTime))
        {
            _nextLocalShotTime = Time.unscaledTimeAsDouble + 1d / Mathf.Max(0.1f, shotsPerSecond);
            RequestShot(_camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f)).direction);
        }
    }

    [ServerRpc(RequireOwnership = true)]
    private void RequestShot(Vector3 direction, NetworkConnection sender = null)
    {
        if (!IsServerInitialized) return;
        if (sender == null || !sender.IsActive || !sender.IsAuthenticated || sender != Owner || !IsSpawned)
        {
            Reject("invalid ownership");
            return;
        }
        ReconcileServerWeapon();
        if (_health == null || !_health.IsServerInitialized || !_health.CanAct)
        {
            Reject("shooter has no living server health");
            return;
        }
        if (!Finite(direction.x) || !Finite(direction.y) || !Finite(direction.z) ||
            direction.sqrMagnitude < 0.99f || direction.sqrMagnitude > 1.01f)
        {
            Reject("invalid aim direction");
            return;
        }
        direction.Normalize();
        float pitch = Mathf.Abs(Mathf.Asin(Mathf.Clamp(direction.y, -1f, 1f)) * Mathf.Rad2Deg);
        Vector3 horizontalAim = Vector3.ProjectOnPlane(direction, Vector3.up);
        Vector3 horizontalFacing = Vector3.ProjectOnPlane(NetworkObject.transform.forward, Vector3.up);
        if (pitch > maxPitch + 0.1f || horizontalAim.sqrMagnitude < 0.000001f ||
            Vector3.Angle(horizontalFacing, horizontalAim) > maxYawDifference)
        {
            Reject("aim inconsistent with player facing/pitch limits");
            return;
        }
        if (damage <= 0 || !Finite(range) || range <= 0f || !Finite(shotsPerSecond) || shotsPerSecond <= 0f)
        {
            Reject("invalid server weapon configuration");
            return;
        }

        if (IsReloading || CurrentAmmo <= 0)
        {
            Reject(IsReloading ? "reloading" : "empty magazine");
            return;
        }
        double now = TimeManager.TicksToTime(TimeManager.Tick);
        if (now < _nextServerShotTime)
        {
            Reject("cooldown");
            return;
        }
        _nextServerShotTime = now + 1d / shotsPerSecond;
        _ammo.Value--;
        Debug.Log($"[Weapon] Owner={OwnerId} accepted shot; ammo={CurrentAmmo}/{magazineSize}.", this);

        // Origin is the server's camera mount, never a client-supplied position.
        // RaycastAll lets us exclude self without ignoring intervening world geometry.
        RaycastHit[] hits = Physics.RaycastAll(transform.position, direction, range, ~0, QueryTriggerInteraction.Ignore);
        Transform shooterRoot = NetworkObject.transform;
        RaycastHit closest = default;
        float closestDistance = float.PositiveInfinity;
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform.IsChildOf(shooterRoot) || hit.distance >= closestDistance) continue;
            closest = hit;
            closestDistance = hit.distance;
        }

        Debug.Log($"[Combat] Accepted shot shooter={OwnerId} hit={(closest.collider != null ? closest.collider.name : "miss")}.", this);
        if (closest.collider == null)
        {
            PresentAcceptedShot(false);
            return;
        }
        Health victim = closest.collider.GetComponentInParent<Health>();
        if (victim == null || victim == _health || !victim.IsSpawned ||
            !victim.CompareTag("Player") || !victim.Owner.IsActive || victim.Owner == Owner)
        {
            PresentAcceptedShot(false);
            if (victim == null) PresentImpact(false, closest.point, closest.normal);
            return;
        }

        int previousHp = victim.CurrentHealth;
        victim.TakeDamage(damage, Owner);
        bool damagedPlayer = victim.CurrentHealth < previousHp;
        PresentAcceptedShot(damagedPlayer);
        if (damagedPlayer) PresentImpact(true, closest.point, closest.normal);
    }

    [ObserversRpc(BufferLast = false, RunLocally = false)]
    private void PresentAcceptedShot(bool damagedPlayer)
    {
        if (IsOwner)
        {
            if (_presentation != null) _presentation.AcceptedShot(damagedPlayer);
        }
        else
        {
            SoldierAnimation soldier = GetComponentInParent<SoldierAnimation>();
            if (soldier != null) soldier.PlayAcceptedShot();
        }
    }

    [ObserversRpc(BufferLast = false, RunLocally = false)]
    private void PresentImpact(bool blood, Vector3 point, Vector3 normal)
    {
        if (_presentation != null) _presentation.ShowImpact(blood, point, normal);
    }

    private void Reject(string reason)
    {
        Debug.Log($"[Combat] Rejected shot shooter={OwnerId}: {reason}.", this);
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
