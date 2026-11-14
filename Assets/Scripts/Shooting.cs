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
    [System.Serializable]
    public class WeaponProfile
    {
        public string label;
        public int damage = 40, magazine = 30, pellets = 1;
        public float rate = 5f, reload = 3f, range = 100f, spread = 0f, zoom = 2f;
        public bool automatic;
    }
    public WeaponProfile[] weapons =
    {
        new WeaponProfile { label = "Sidearm" },
        new WeaponProfile { label = "Rifle", damage = 20, rate = 10, reload = 2.5f, automatic = true, spread = 0.4f },
        new WeaponProfile { label = "Shotgun", damage = 10, magazine = 8, pellets = 8, rate = 1, spread = 4, zoom = 1.3f }
    };
    private readonly SyncVar<int> _selectedWeapon = new();
    private int[] _magazines;
    private float _bloom;
    private double _lastBloomTime;
    private double _nextSwitchTime;
    private bool _scoped;
    private WeaponProfile[] _serverWeapons;
    private bool _localConfigurationValid, _serverConfigurationValid;
    private float _serverPitch, _serverYaw;
    private float _normalFov;
    private double _nextRejectLog, _serverRequestRate;
    private GameplayPlayerSpawner _requestGate;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private int _developmentProbe;
    private double _nextDevelopmentProbe;
#endif
    public string WeaponName => Profile?.label ?? "Weapon unavailable";
    private WeaponProfile Profile
    {
        get
        {
            var definitions = IsServerInitialized ? _serverWeapons : weapons;
            bool valid = IsServerInitialized ? _serverConfigurationValid : _localConfigurationValid;
            return valid && definitions != null && _selectedWeapon.Value >= 0 && _selectedWeapon.Value < definitions.Length
                ? definitions[_selectedWeapon.Value] : null;
        }
    }

    public static string ConfigurationError(WeaponProfile[] definitions, float yaw, float pitch)
    {
        if (!ServerGameplayValidation.ValidAimConfiguration(yaw, pitch)) return "invalid finite yaw/pitch envelope";
        if (definitions == null || definitions.Length == 0 || definitions.Length > ServerGameplayValidation.MaximumWeaponDefinitions)
            return "weapon definitions must contain 1..64 entries";
        for (int i = 0; i < definitions.Length; i++)
        {
            WeaponProfile p = definitions[i];
            if (p == null) return $"weapon[{i}] is null";
            string error = ServerGameplayValidation.WeaponConfigurationError(p.damage, p.magazine, p.pellets,
                p.rate, p.reload, p.range, p.spread, p.zoom);
            if (error != null) return $"weapon[{i}]: {error}";
        }
        return null;
    }

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
    private LocalPlayerControls _controls;
    private PlayerCameraSetup _posture;
    private double _nextServerShotTime;

    private void Awake()
    {
        _camera = GetComponent<Camera>();
        _health = GetComponentInParent<Health>();
        _presentation = GetComponentInParent<CombatPresentation>();
        _controls = GetComponentInParent<LocalPlayerControls>();
        _posture = GetComponentInParent<PlayerCameraSetup>();
        _normalFov = _camera != null ? _camera.fieldOfView : 60f;
        _selectedWeapon.OnChange += OnWeaponChanged;
        _localConfigurationValid = ConfigurationError(weapons, maxYawDifference, maxPitch) == null;
        ApplyProfile();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        _nextServerShotTime = 0d;
        _nextSwitchTime = 0d; _bloom = 0; _lastBloomTime = 0;
        _nextRejectLog = 0d;
        _requestGate = NetworkManager.GetComponent<GameplayPlayerSpawner>();
        string error = ConfigurationError(weapons, maxYawDifference, maxPitch);
        _serverConfigurationValid = error == null;
        if (!_serverConfigurationValid)
        {
            Debug.LogError($"[Weapon Config] owner={OwnerId} invalid: {error}; combat disabled. Developer configuration error, not a cheating verdict.", this);
            _ammo.Value = 0; _reloading.Value = false; _reloadDeadline.Value = 0;
            return;
        }
        // Freeze this server's approved values; client/Inspector presentation fields cannot become limits.
        _serverWeapons = new WeaponProfile[weapons.Length];
        for (int i = 0; i < weapons.Length; i++)
        {
            WeaponProfile p = weapons[i];
            _serverWeapons[i] = new WeaponProfile { label = p.label, damage = p.damage, magazine = p.magazine,
                pellets = p.pellets, rate = p.rate, reload = p.reload, range = p.range, spread = p.spread,
                zoom = p.zoom, automatic = p.automatic };
        }
        _serverPitch = maxPitch; _serverYaw = maxYawDifference;
        _selectedWeapon.Value = 0;
        ApplyProfile();
        double fastest = 0d;
        foreach (WeaponProfile weapon in _serverWeapons)
            if (weapon != null && Finite(weapon.rate) && weapon.rate > fastest) fastest = weapon.rate;
        _serverRequestRate = ServerGameplayValidation.CombatRequestRate(fastest);
        _ammo.Value = Mathf.Max(1, magazineSize);
        _reloading.Value = false;
        _reloadDeadline.Value = 0d;
        _wasDead = false;
        _magazines = new int[_serverWeapons.Length];
        for (int i = 0; i < _serverWeapons.Length; i++) _magazines[i] = _serverWeapons[i].magazine;
    }

    private void OnDestroy() => _selectedWeapon.OnChange -= OnWeaponChanged;
    private void OnWeaponChanged(int previous, int next, bool asServer) => ApplyProfile();
    private void ApplyProfile()
    {
        WeaponProfile p = Profile;
        damage = p?.damage ?? 0; magazineSize = p?.magazine ?? 0;
        shotsPerSecond = p?.rate ?? 0; reloadDuration = p?.reload ?? 0;
        range = p?.range ?? 0; automaticFire = p?.automatic ?? false;
        _scoped = false;
        if (_camera != null) _camera.fieldOfView = _normalFov;
    }

    [ServerRpc(RequireOwnership = true)]
    private void RequestWeapon(int index, NetworkConnection sender = null)
    {
        if (!IsServerInitialized || !IsSpawned || sender != Owner || sender == null ||
            !sender.IsActive || !sender.IsAuthenticated) return;
        if (!_serverConfigurationValid || !AdmitRequest(sender, "switch")) return;
        if (_health == null || !_health.CanAct ||
            index < 0 || index >= _serverWeapons.Length || index == _selectedWeapon.Value || ServerTime < _nextSwitchTime) return;
        ReconcileServerWeapon();
        _magazines[_selectedWeapon.Value] = CurrentAmmo;
        _reloading.Value = false; _reloadDeadline.Value = 0;
        _selectedWeapon.Value = index;
        ApplyProfile();
        _ammo.Value = _magazines[index];
        _bloom = 0f;
        _nextSwitchTime = ServerTime + 0.25d;
        // Do not reset shot cooldown: switching cannot bypass the last weapon's fire rate.
    }

    private void Update() => ReconcileServerWeapon();

    private void ReconcileServerWeapon()
    {
        if (!IsServerInitialized || !IsSpawned || !_serverConfigurationValid || _health == null) return;
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
            _ammo.Value = Profile.magazine;
            for (int i = 0; i < _magazines.Length; i++) _magazines[i] = _serverWeapons[i].magazine;
            _reloading.Value = false;
            _reloadDeadline.Value = 0d;
            Debug.Log($"[Weapon] Owner={OwnerId} respawn magazine={CurrentAmmo}.", this);
        }
        if (IsReloading && ServerTime >= _reloadDeadline.Value)
        {
            _ammo.Value = Profile.magazine;
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
        if (!_serverConfigurationValid || !AdmitRequest(sender, "reload")) return;
        ReconcileServerWeapon();
        if (_health == null || !_health.CanAct || IsReloading || CurrentAmmo >= Profile.magazine) return;
        _reloading.Value = true;
        _reloadDeadline.Value = ServerTime + Profile.reload;
        Debug.Log($"[Weapon] Owner={OwnerId} reload started ({Profile.reload}s); ammo={CurrentAmmo}.", this);
    }

    public override void OnStopClient()
    {
        _nextLocalShotTime = 0d;
        base.OnStopClient();
    }

    private void LateUpdate()
    {
        if (!IsClientInitialized || !IsOwner || _camera == null || !_camera.isActiveAndEnabled) return;
        if (_health == null || !_health.CanAct || Profile == null || (_controls != null && _controls.MenuOpen))
        {
            _nextLocalShotTime = 0d;
            _scoped = false;
            _camera.fieldOfView = _normalFov;
            return;
        }
        if (Keyboard.current != null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Keyboard.current.f10Key.wasPressedThisFrame) DevelopmentRequestProbe();
#endif
        }
        if (_controls != null)
        {
            if (_controls.Pressed("Weapon1")) RequestWeapon(0);
            if (_controls.Pressed("Weapon2")) RequestWeapon(1);
            if (_controls.Pressed("Weapon3")) RequestWeapon(2);
        }
        _scoped = _controls != null ? _controls.Held("Aim") : Mouse.current != null && Mouse.current.rightButton.isPressed;
        _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView,
            _scoped ? _normalFov / Profile.zoom : _normalFov,
            1f - Mathf.Exp(-12f * Time.deltaTime));
        if (_presentation != null) _presentation.PrepareAim(maxPitch);
        if (_controls != null ? _controls.Pressed("Reload") : Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            RequestReload();
            return;
        }
        if (IsReloading || CurrentAmmo <= 0) return;
        bool wantsFire = _controls != null ? automaticFire ? _controls.Held("Shoot") : _controls.Pressed("Shoot") :
            Mouse.current != null && (automaticFire ? Mouse.current.leftButton.isPressed : Mouse.current.leftButton.wasPressedThisFrame);
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
        if (!_serverConfigurationValid || !AdmitRequest(sender, "shot")) return;
        Vector3 origin = _posture != null ? _posture.ServerShotOrigin : transform.position;
        if (!Finite(origin.x) || !Finite(origin.y) || !Finite(origin.z)) { Reject("invalid server origin"); return; }
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
        if (pitch > _serverPitch + 0.1f || horizontalAim.sqrMagnitude < 0.000001f ||
            Vector3.Angle(horizontalFacing, horizontalAim) > _serverYaw)
        {
            Reject("aim inconsistent with player facing/pitch limits");
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
        _nextServerShotTime = now + 1d / Profile.rate;
        _ammo.Value--;
        Debug.Log($"[Weapon] Owner={OwnerId} accepted shot; ammo={CurrentAmmo}/{magazineSize}.", this);

        _bloom = Mathf.Max(0f, _bloom - (float)System.Math.Max(0d, now - _lastBloomTime - 0.15d) * 8f);
        float spread = Profile.pellets > 1 ? Profile.spread : (_bloom <= 0f ? 0f : Profile.spread + _bloom);
        bool damagedPlayer = false;
        // A killstreak earned by one pellet affects the next trigger, not this shot's remaining pellets.
        int acceptedDamage = (int)System.Math.Round((double)Profile.damage * (MatchBoosts.Instance != null ? MatchBoosts.Instance.DamageMultiplier(Owner) : 1f));
        Quaternion aim = Quaternion.LookRotation(direction);
        for (int pellet = 0; pellet < Mathf.Clamp(Profile.pellets, 1, 16); pellet++)
        {
            Vector2 offset = Random.insideUnitCircle * spread;
            Vector3 pelletDirection = aim * Quaternion.Euler(offset.y, offset.x, 0f) * Vector3.forward;
            damagedPlayer |= FireServerRay(pelletDirection, origin, acceptedDamage);
        }
        _bloom = Mathf.Min(4f, _bloom + 0.4f);
        _lastBloomTime = now;
        PresentAcceptedShot(damagedPlayer, Profile.pellets > 1);
    }

    private bool FireServerRay(Vector3 direction, Vector3 origin, int acceptedDamage)
    {
        // Origin is the server's camera mount, never a client-supplied position.
        // RaycastAll lets us exclude self without ignoring intervening world geometry.
        RaycastHit[] hits = Physics.RaycastAll(origin, direction, Profile.range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        Transform shooterRoot = NetworkObject.transform;
        RaycastHit closest = default;
        float closestDistance = float.PositiveInfinity;
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform.IsChildOf(shooterRoot) || hit.distance >= closestDistance) continue;
            // Invisible dead bodies must not shield living players behind them.
            Health hitPlayer = hit.collider.GetComponentInParent<Health>();
            if (hitPlayer != null && hitPlayer.IsDead) continue;
            closest = hit;
            closestDistance = hit.distance;
        }

        Debug.Log($"[Combat] Accepted shot shooter={OwnerId} hit={(closest.collider != null ? closest.collider.name : "miss")}.", this);
        if (closest.collider == null)
        {
            return false;
        }
        Health victim = closest.collider.GetComponentInParent<Health>();
        if (victim == null || victim == _health || !victim.IsSpawned ||
            !victim.CompareTag("Player") || !victim.Owner.IsActive || victim.Owner == Owner)
        {
            if (victim == null) PresentImpact(false, closest.point, closest.normal);
            return false;
        }

        int previousHp = victim.CurrentHealth;
        int previousArmor = victim.CurrentArmor;
        victim.TakeDamage(acceptedDamage, Owner);
        bool damagedPlayer = victim.CurrentHealth < previousHp;
        if (damagedPlayer) PresentImpact(true, closest.point, closest.normal);
        return damagedPlayer || victim.CurrentArmor < previousArmor;
    }

    [ObserversRpc(BufferLast = false, RunLocally = false)]
    private void PresentAcceptedShot(bool damagedPlayer, bool shotgunAudio)
    {
        if (_presentation != null) _presentation.PlayShotAudio(shotgunAudio);
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

    private bool AdmitRequest(NetworkConnection sender, string action)
    {
        if (_requestGate != null && _requestGate.TryConsumeCombatRequest(sender, _serverRequestRate)) return true;
        Reject(_requestGate == null ? "server request gate unavailable" : "request budget exceeded", action);
        return false;
    }

    private void Reject(string reason, string action = "shot")
    {
        if (ServerTime < _nextRejectLog) return;
        _nextRejectLog = ServerTime + 1d;
        Debug.Log($"[Combat] Rejected {action} shooter={OwnerId}: {reason}.", this);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [ContextMenu("Development/Validate weapon configuration fixtures")]
    private void DevelopmentConfigurationProbe()
    {
        if (!IsServerInitialized) { Debug.Log("[Weapon Config TEST] Select a server-spawned shooter."); return; }
        var invalid = new WeaponProfile { rate = float.NaN };
        Debug.Log($"[Weapon Config TEST] live={(ConfigurationError(weapons, maxYawDifference, maxPitch) ?? "valid")} " +
            $"null={(ConfigurationError(null, 45, 89) != null ? "rejected" : "FAILED")} " +
            $"NaN={(ConfigurationError(new[] { invalid }, 45, 89) != null ? "rejected" : "FAILED")}; " +
            "validator fixtures only; no live config changed, no player verdict.");
    }

    // Fixed local probes through the existing RPCs/broadcasts, with no privileged server API.
    private void DevelopmentRequestProbe()
    {
        if (Time.unscaledTimeAsDouble < _nextDevelopmentProbe) return;
        _nextDevelopmentProbe = Time.unscaledTimeAsDouble + 2d;
        int phase = _developmentProbe++ % 3;
        if (phase == 0)
        {
            Debug.Log($"[Request TEST] owner={OwnerId} F10 invalid NaN shot (no valid shot sent).");
            RequestShot(new Vector3(float.NaN, 0f, 1f));
        }
        else if (phase == 1)
        {
            Debug.Log($"[Request TEST] owner={OwnerId} F10 128 invalid switches, then zero-direction shot.");
            for (int i = 0; i < 128; i++) RequestWeapon(-1);
            RequestShot(Vector3.zero);
        }
        else
        {
            Debug.Log($"[Request TEST] owner={OwnerId} F10 16 scoreboard and 16 balance queries.");
            for (int i = 0; i < 16; i++)
            {
                ClientManager.Broadcast(new MatchScoreboard.SnapshotRequest());
                ClientManager.Broadcast(new TrackPlayerCurrency.BalanceRequest());
            }
        }
    }
#endif

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
