using StarterAssets;
using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using FishNet.Component.Transforming;
using UnityEngine.InputSystem;
#endif

// Owner movement stays local; only the server's broadcast selects the modifier.
public sealed class ApprovedMovementBoost : MonoBehaviour
{
    private FirstPersonController _controller;
    private Health _health;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private bool _developmentExcessSpeed;
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
        _controller.MoveSpeed = ApprovedMovementLimits.WalkSpeed * speed;
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
#endif
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // Local owner mutation only: intentionally simulates a modified client. Detection has no client RPC.
    private void DevelopmentControls()
    {
        if (!_health.CanAct) { _developmentExcessSpeed = false; return; }
        if (Keyboard.current == null) return;
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
#endif
}
