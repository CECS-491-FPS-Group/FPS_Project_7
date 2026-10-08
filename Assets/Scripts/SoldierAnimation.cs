using UnityEngine;

// Presentation only: samples the root already moved by the controller/NetworkTransform.
[DisallowMultipleComponent]
public sealed class SoldierAnimation : MonoBehaviour
{
    [SerializeField] private Transform soldierVisual;
    [SerializeField, Min(0f)] private float startSpeed = 0.2f;
    [SerializeField, Min(0f)] private float stopSpeed = 0.1f;
    [SerializeField, Min(0.01f)] private float smoothingTime = 0.12f;
    [SerializeField, Min(0.1f)] private float referenceRunSpeed = 4f;
    [SerializeField, Min(0.1f)] private float teleportDistance = 3f;

    private static readonly int Speed = Animator.StringToHash("Speed");
    private static readonly int PlaybackSpeed = Animator.StringToHash("PlaybackSpeed");
    private Animator _animator;
    private Health _health;
    private Vector3 _previousPosition;
    private float _speed;
    private bool _moving;
    private bool _hasSample;

    private void Awake()
    {
        _health = GetComponent<Health>();
        if (soldierVisual != null) _animator = soldierVisual.GetComponentInChildren<Animator>(true);
        if (_animator != null) _animator.applyRootMotion = false;
    }

    private void OnEnable() => ResetSample();

    private void ResetSample()
    {
        _previousPosition = transform.position;
        _speed = 0f;
        _moving = false;
        _hasSample = false;
        if (_animator == null) return;
        _animator.SetFloat(Speed, 0f);
        _animator.SetFloat(PlaybackSpeed, 1f);
    }

    private void LateUpdate()
    {
        if (_animator == null) return;
        Vector3 delta = transform.position - _previousPosition;
        _previousPosition = transform.position;
        if (!_hasSample || delta.sqrMagnitude > teleportDistance * teleportDistance ||
            (_health != null && !_health.CanAct))
        {
            ResetSample();
            _hasSample = true;
            return;
        }
        if (Time.deltaTime <= 0f) return;

        float measured = new Vector2(delta.x, delta.z).magnitude / Time.deltaTime;
        _speed = Mathf.Lerp(_speed, measured,
            1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.01f, smoothingTime)));
        // Hysteresis prevents small interpolation corrections from toggling locomotion.
        _moving = _moving ? _speed > stopSpeed : _speed > startSpeed;
        _animator.SetFloat(Speed, _moving ? _speed : 0f);
        _animator.SetFloat(PlaybackSpeed, _moving
            ? Mathf.Clamp(_speed / Mathf.Max(0.1f, referenceRunSpeed), 0.6f, 1.8f) : 1f);
    }
}
