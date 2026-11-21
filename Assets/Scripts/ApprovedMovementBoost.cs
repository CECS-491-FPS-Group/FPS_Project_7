using StarterAssets;
using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using FishNet.Component.Transforming;
using UnityEngine.InputSystem;
#endif

[DefaultExecutionOrder(-20)]
// Owner movement stays local; only the server's broadcast selects the modifier.
public sealed class ApprovedMovementBoost : MonoBehaviour
{
    private FirstPersonController _controller;
    private Health _health;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private bool _developmentExcessSpeed;
    private int _developmentMovementProbe;
    private double _developmentProbeAt = -1, _developmentNextTeleport;
    private Vector3 _developmentOrigin;
#endif
    private void Awake()
    {
        _controller = GetComponent<FirstPersonController>(); _health = GetComponent<Health>();
    }
    private void Update()
    {
        if (_controller == null || _health == null || !_health.IsOwner) return;
        MatchBoosts.Kind kind = _health.CanAct && MatchBoosts.Instance != null ? MatchBoosts.Instance.LocalKind : MatchBoosts.Kind.None;
        float speed = kind == MatchBoosts.Kind.Speed ? ApprovedMovementLimits.SpeedMultiplier : 1f;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        DevelopmentControls();
        if (_developmentExcessSpeed) speed *= 3f;
#endif
        bool crouched = GetComponent<PlayerCameraSetup>().Crouched;
        _controller.MoveSpeed = (crouched ? 1f : ApprovedMovementLimits.WalkSpeed) * speed;
        _controller.SprintSpeed = ApprovedMovementLimits.SprintSpeed * speed;
        _controller.JumpHeight = ApprovedMovementLimits.JumpHeight *
            (kind == MatchBoosts.Kind.Jump ? ApprovedMovementLimits.JumpMultiplier : 1f);
    }
    private void OnDisable()
    {
        if (_controller == null) return;
        _controller.MoveSpeed = ApprovedMovementLimits.WalkSpeed;
        _controller.SprintSpeed = ApprovedMovementLimits.SprintSpeed;
        _controller.JumpHeight = ApprovedMovementLimits.JumpHeight;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        _developmentExcessSpeed = false;
        _developmentMovementProbe = 0; _developmentProbeAt = -1;
#endif
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // Local owner mutation only: intentionally simulates a modified client. Detection has no client RPC.
    private void DevelopmentControls()
    {
        if (!_health.CanAct) { _developmentExcessSpeed = false; _developmentMovementProbe = 0; _developmentProbeAt = -1; return; }
        if (Keyboard.current == null) return;
        bool alt = Keyboard.current.leftAltKey.isPressed || Keyboard.current.rightAltKey.isPressed;
        if (alt && Keyboard.current.f7Key.wasPressedThisFrame) { DevelopmentResetMovementProbe(); return; }
        if (alt && Keyboard.current.f8Key.wasPressedThisFrame) { DevelopmentCycleMovementProbe(); return; }
        if (Keyboard.current.f7Key.wasPressedThisFrame)
        {
            _developmentExcessSpeed = !_developmentExcessSpeed;
            Debug.Log($"[Movement AC TEST] owner={_health.OwnerId} excessive speed={_developmentExcessSpeed} (3x local speed).", this);
        }
        if (Keyboard.current.f8Key.wasPressedThisFrame)
        {
            CharacterController capsule = GetComponent<CharacterController>();
            bool enabledBefore = capsule != null && capsule.enabled;
            if (capsule != null) capsule.enabled = false;
            transform.position += Vector3.right * 30f;
            if (capsule != null) capsule.enabled = enabledBefore;
            NetworkTransform networkTransform = GetComponent<NetworkTransform>();
            if (networkTransform != null) { networkTransform.Teleport(); networkTransform.ForceSend(); }
            Debug.Log($"[Movement AC TEST] owner={_health.OwnerId} local horizontal jump +30m X.", this);
        }
    }

    public void DevelopmentCycleMovementProbe()
        => DevelopmentSetMovementProbe((_developmentMovementProbe + 1) % 4);
    public void DevelopmentSetMovementProbe(int next)
    {
        if (_health == null || !_health.IsClientInitialized || !_health.IsOwner || !_health.CanAct) return;
        if (next < 0 || next > 3) return;
        DevelopmentResetMovementProbe();
        _developmentMovementProbe = next; _developmentProbeAt = -1;
        Debug.Log($"[Movement AC TEST] owner={_health.OwnerId} probe={next} (0=off,1=repeated-40m-teleports,2=ascent,3=level-motion); Resume menu to start; auto-stop 12s. Alt+F7 resets.");
    }
    public void DevelopmentResetMovementProbe()
    {
        bool restore = _developmentMovementProbe != 0 && _developmentProbeAt >= 0 && _health != null && _health.CanAct;
        _developmentMovementProbe = 0; _developmentProbeAt = -1; _developmentExcessSpeed = false;
        if (restore) DevelopmentPlace(_developmentOrigin, true);
        if (_controller != null) _controller.ResetMovementState();
        Debug.Log("[Movement AC TEST] probes off; local movement reset; active probe returned to its captured start. No server grace requested.");
    }
    private void LateUpdate()
    {
        if (_health == null || !_health.IsClientInitialized || !_health.IsOwner)
        { _developmentMovementProbe = 0; _developmentExcessSpeed = false; _developmentProbeAt = -1; return; }
        if (_developmentMovementProbe == 0) return;
        if (!_health.CanAct) { _developmentMovementProbe = 0; _developmentProbeAt = -1; return; }
        bool menu = GetComponent<LocalPlayerControls>()?.MenuOpen == true;
        if (menu) { if (_developmentProbeAt >= 0) DevelopmentResetMovementProbe(); return; }
        double now = Time.unscaledTimeAsDouble;
        if (_developmentProbeAt < 0) { _developmentProbeAt = now; _developmentOrigin = transform.position; _developmentNextTeleport = now + 2; }
        double elapsed = now - _developmentProbeAt;
        if (elapsed >= 12) { DevelopmentResetMovementProbe(); return; }
        if (_developmentMovementProbe == 1)
        {
            if (now < _developmentNextTeleport) return;
            _developmentNextTeleport = now + 2;
            bool away = Vector3.Distance(transform.position, _developmentOrigin) < 20;
            DevelopmentPlace(_developmentOrigin + Vector3.right * (away ? 40 : 0), true);
            Debug.Log($"[Movement AC TEST] owner={_health.OwnerId} repeated unexplained displacement at elapsed={elapsed:F1}s.");
        }
        else if (_developmentMovementProbe == 2)
        {
            float launch = Mathf.Sqrt(_controller.JumpHeight * -2 * _controller.Gravity);
            Vector3 point = transform.position; point.y = _developmentOrigin.y + (float)elapsed * launch * 3;
            DevelopmentPlace(point, false);
        }
        else
        {
            // Fresh changing XZ, fixed Y: stationary silence by itself is NOT hover evidence.
            Vector3 point = _developmentOrigin + new Vector3(Mathf.Sin((float)elapsed) * .25f,
                ApprovedMovementLimits.JumpHeight * 6 + 4, Mathf.Cos((float)elapsed) * .25f);
            DevelopmentPlace(point, elapsed < .05);
        }
    }
    private void DevelopmentPlace(Vector3 point, bool teleport)
    {
        CharacterController capsule = GetComponent<CharacterController>();
        bool before = capsule != null && capsule.enabled;
        if (capsule != null) capsule.enabled = false;
        transform.position = point;
        if (capsule != null) capsule.enabled = before;
        if (teleport && TryGetComponent(out NetworkTransform network)) { network.Teleport(); network.ForceSend(); }
    }
#endif
}
