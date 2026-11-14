using FishNet.Object;
using FishNet.Connection;
using FishNet.Object.Synchronizing;
using FishNet.Component.Transforming;
using StarterAssets;
using UnityEngine;
using PlayerInputComponent = UnityEngine.InputSystem.PlayerInput;

// Run before PlayerInput (-100) so remote copies never claim local devices.
[DefaultExecutionOrder(-200)]
public class PlayerCameraSetup : NetworkBehaviour
{
    [Header("Components to Disable for Other Players")]
    public Camera playerCamera;
    public AudioListener audioListener;
    public Canvas playerUI;

    [Header("Third-person Visual")]
    public Transform soldierVisual;
    private Renderer[] _bodyRenderers;

    private PlayerInputComponent _playerInput;
    private StarterAssetsInputs _inputs;
    private FirstPersonController _controller;
    private Health _health;
    private bool _movementEnabled;
    private Collider[] _deathColliders;
    private bool[] _collisionBeforeDeath;
    private bool _collisionSuppressed;
    private readonly SyncVar<bool> _crouching = new();
    private PlayerPosture _posture;
    private LocalPlayerControls _controls;
    private ServerGameplayValidation.RequestBudget _stanceRequests;
    private double _nextStanceLog;
    public bool Crouched => _crouching.Value;
    public Vector3 ServerShotOrigin => _posture.AimOrigin;

    private void Awake()
    {
        _playerInput = GetComponent<PlayerInputComponent>();
        _inputs = GetComponent<StarterAssetsInputs>();
        _controller = GetComponent<FirstPersonController>();
        _health = GetComponent<Health>();
        if (GetComponent<ApprovedMovementBoost>() == null) gameObject.AddComponent<ApprovedMovementBoost>();
        SetupSoldierVisual();
        SetLocalControl(false);
        _posture = gameObject.AddComponent<PlayerPosture>();
        _controls = gameObject.AddComponent<LocalPlayerControls>();
        gameObject.AddComponent<PlayerDeathVisual>();
    }

    private void SetupSoldierVisual()
    {
        if (soldierVisual == null) return;

        foreach (Animator animator in soldierVisual.GetComponentsInChildren<Animator>(true))
            animator.applyRootMotion = false;

        _bodyRenderers = soldierVisual.GetComponentsInChildren<Renderer>(true);
        CharacterController capsule = GetComponent<CharacterController>();
        if (capsule != null && _bodyRenderers.Length > 0)
        {
            Bounds bounds = _bodyRenderers[0].bounds;
            foreach (Renderer body in _bodyRenderers) bounds.Encapsulate(body.bounds);
            if (bounds.size.y > 0.001f)
            {
                float scale = capsule.height * Mathf.Abs(transform.lossyScale.y) / bounds.size.y;
                Vector3 feet = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                Vector3 targetFeet = transform.TransformPoint(capsule.center - Vector3.up * capsule.height * 0.5f);
                Vector3 scaledFeet = soldierVisual.position + (feet - soldierVisual.position) * scale;
                soldierVisual.localScale *= scale;
                soldierVisual.position += targetFeet - scaledFeet;
            }
        }

        // Replace only the placeholder's appearance, preserving its collider.
        Transform placeholder = transform.Find("Capsule");
        if (placeholder != null && placeholder.TryGetComponent(out Renderer renderer))
            renderer.enabled = false;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        SetLocalControl(base.IsOwner);
        CosmeticCorpse.IgnoreForPlayer(gameObject);
        _posture.Initialize();

        string devices = "none";
        if (_playerInput && _playerInput.devices.Count > 0)
        {
            devices = string.Empty;
            for (int i = 0; i < _playerInput.devices.Count; i++)
                devices += (i == 0 ? "" : ", ") + _playerInput.devices[i].displayName;
        }

        Debug.Log($"[PlayerCameraSetup] OwnerId={OwnerId} LocalClientId={LocalConnection.ClientId} " +
            $"IsOwner={IsOwner} PlayerInputEnabled={(_playerInput && _playerInput.enabled)} Devices=[{devices}]", this);
    }

    public override void OnStopClient()
    {
        SetLocalControl(false);
        _posture.Apply(false);
        base.OnStopClient();
    }

    private void SetLocalControl(bool enabled)
    {
        SetMovementControl(enabled && (_health == null || _health.CanAct));
        if (playerCamera) playerCamera.enabled = enabled;
        if (audioListener) audioListener.enabled = false;
        if (enabled) IntegrationAudio.RegisterOwnerCamera(playerCamera);
        else IntegrationAudio.ReleaseOwnerCamera(playerCamera);
        if (playerUI) playerUI.enabled = enabled;
        UpdateBodyVisibility();
    }

    private void Update()
    {
        UpdateDeathCollision();
        if (IsServerInitialized && _health != null && !_health.CanAct) _crouching.Value = false;
        if (IsClientInitialized || IsServerInitialized) _posture.Apply(_crouching.Value && (_health == null || _health.CanAct));
        bool allowed = IsClientInitialized && IsOwner && (_health == null || _health.CanAct) && (_controls == null || !_controls.MenuOpen);
        if (allowed != _movementEnabled) SetMovementControl(allowed);
        UpdateBodyVisibility();
    }

    private void OnDestroy() => IntegrationAudio.ReleaseOwnerCamera(playerCamera);

    public override void OnStartServer()
    {
        base.OnStartServer(); _crouching.Value = false;
        _stanceRequests = new ServerGameplayValidation.RequestBudget(8, 8);
        _posture.Initialize();
    }
    public void RequestLocalStance(bool crouched) { if (IsClientInitialized && IsOwner) RequestStance(crouched); }
    [ServerRpc(RequireOwnership = true)]
    private void RequestStance(bool crouched, NetworkConnection sender = null)
    {
        if (!IsServerInitialized || !IsSpawned || sender == null || sender != Owner || !sender.IsActive ||
            !sender.IsAuthenticated || _health == null || !_health.CanAct ||
            _stanceRequests == null || !_stanceRequests.TryConsume(Time.realtimeSinceStartupAsDouble)) return;
        if (!crouched && !_posture.CanStand())
        {
            if (Time.realtimeSinceStartupAsDouble >= _nextStanceLog)
            { _nextStanceLog = Time.realtimeSinceStartupAsDouble + 2; Debug.Log($"[Stance] owner={OwnerId} stand blocked by clearance."); }
            return;
        }
        bool changed = _crouching.Value != crouched;
        _crouching.Value = crouched;
        if (changed && Time.realtimeSinceStartupAsDouble >= _nextStanceLog)
        { _nextStanceLog = Time.realtimeSinceStartupAsDouble + 2; Debug.Log($"[Stance] owner={OwnerId} crouched={crouched} server approved."); }
    }

    private void UpdateDeathCollision()
    {
        if (_health == null || (!IsServerInitialized && !IsClientInitialized)) return;
        if (_health.IsDead)
        {
            if (!_collisionSuppressed)
            {
                GetComponent<PlayerDeathVisual>()?.Show();
                // Capture after FishNet configures the remote CharacterController.
                _deathColliders = GetComponentsInChildren<Collider>(true);
                _collisionBeforeDeath = new bool[_deathColliders.Length];
                for (int i = 0; i < _deathColliders.Length; i++)
                    _collisionBeforeDeath[i] = _deathColliders[i].enabled;
                _collisionSuppressed = true;
            }
            foreach (Collider collider in _deathColliders)
                if (collider != null) collider.enabled = false;
        }
        else if (_collisionSuppressed)
        {
            GetComponent<PlayerDeathVisual>()?.Clear();
            for (int i = 0; i < _deathColliders.Length; i++)
                if (_deathColliders[i] != null) _deathColliders[i].enabled = _collisionBeforeDeath[i];
            _collisionSuppressed = false;
            CosmeticCorpse.IgnoreForPlayer(gameObject);
        }
    }

    private void UpdateBodyVisibility()
    {
        bool visible = IsClientInitialized && !IsOwner && (_health == null || !_health.IsDead) &&
            (MatchBoosts.Instance == null || !MatchBoosts.Instance.ClientInvisible(OwnerId));
        if (_bodyRenderers != null)
            foreach (Renderer body in _bodyRenderers)
                if (body != null && body.enabled != visible) body.enabled = visible;
    }

    public bool ApplyRespawnPosition(Vector3 position)
    {
        if (!IsClientInitialized || !IsOwner) return false;
        SetMovementControl(false);
        CharacterController capsule = GetComponent<CharacterController>();
        bool wasEnabled = capsule != null && capsule.enabled;
        if (capsule != null) capsule.enabled = false;
        transform.position = position;
        if (_controller != null) _controller.ResetMovementState();
        if (capsule != null) capsule.enabled = wasEnabled;
        NetworkTransform networkTransform = GetComponent<NetworkTransform>();
        if (networkTransform != null)
        {
            networkTransform.Teleport();
            networkTransform.ForceSend();
        }
        return true;
    }

    private void SetMovementControl(bool enabled)
    {
        _movementEnabled = enabled;
        // Disabling PlayerInput also unpairs its devices. Do this before clearing
        // cached values because canceled input actions can update those values.
        if (_playerInput) _playerInput.enabled = false;
        if (_inputs)
        {
            _inputs.move = Vector2.zero;
            _inputs.look = Vector2.zero;
            _inputs.jump = false;
            _inputs.sprint = false;
            _inputs.enabled = enabled;
        }

        if (_playerInput) _playerInput.enabled = enabled;
        if (_controller) _controller.enabled = enabled;
        if (enabled) CosmeticCorpse.IgnoreForPlayer(gameObject);
    }
}
