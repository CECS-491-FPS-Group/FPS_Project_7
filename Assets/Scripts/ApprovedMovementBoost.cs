using StarterAssets;
using UnityEngine;

// Owner movement stays local; only the server's broadcast selects the modifier.
public sealed class ApprovedMovementBoost : MonoBehaviour
{
    private FirstPersonController _controller;
    private Health _health;
    private float _walk, _sprint, _jump;
    private void Awake()
    {
        _controller = GetComponent<FirstPersonController>(); _health = GetComponent<Health>();
        if (_controller != null) { _walk = _controller.MoveSpeed; _sprint = _controller.SprintSpeed; _jump = _controller.JumpHeight; }
    }
    private void Update()
    {
        if (_controller == null || _health == null || !_health.IsOwner) return;
        MatchBoosts.Kind kind = _health.CanAct && MatchBoosts.Instance != null ? MatchBoosts.Instance.LocalKind : MatchBoosts.Kind.None;
        _controller.MoveSpeed = _walk * (kind == MatchBoosts.Kind.Speed ? 1.5f : 1f);
        _controller.SprintSpeed = _sprint * (kind == MatchBoosts.Kind.Speed ? 1.5f : 1f);
        _controller.JumpHeight = _jump * (kind == MatchBoosts.Kind.Jump ? 1.5f : 1f);
    }
    private void OnDisable()
    {
        if (_controller == null) return;
        _controller.MoveSpeed = _walk; _controller.SprintSpeed = _sprint; _controller.JumpHeight = _jump;
    }
}
