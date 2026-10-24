using UnityEngine;

// Movement-derived footsteps and server-result shots; never determines whether a shot fired.
public sealed class PlayerAudio : MonoBehaviour
{
    private AudioSource _shots, _steps;
    private AudioClip _shot, _shotgun, _step;
    private Health _health;
    private CharacterController _capsule;
    private Vector3 _previous;
    private float _distance;
    private void Awake()
    {
        _health = GetComponent<Health>(); _capsule = GetComponent<CharacterController>();
        _shots = gameObject.AddComponent<AudioSource>(); _steps = gameObject.AddComponent<AudioSource>();
        foreach (AudioSource source in new[] { _shots, _steps })
        { source.playOnAwake = false; source.spatialBlend = 1f; source.minDistance = 2f; source.maxDistance = 60f; source.rolloffMode = AudioRolloffMode.Linear; }
        _shots.volume = 0.35f; _steps.volume = 0.15f;
        _shot = Resources.Load<AudioClip>("IntegrationAudio/Shot");
        _shotgun = Resources.Load<AudioClip>("IntegrationAudio/Shotgun");
        _step = Resources.Load<AudioClip>("IntegrationAudio/Step");
        _previous = transform.position;
    }
    public void Shot(bool shotgun)
    {
        AudioClip clip = shotgun && _shotgun != null ? _shotgun : _shot;
        if (clip != null) _shots.PlayOneShot(clip);
    }
    private void Update()
    {
        Vector3 delta = transform.position - _previous; _previous = transform.position;
        if (_health == null || !_health.CanAct || delta.sqrMagnitude > 9f) { _distance = 0; return; }
        _distance += new Vector2(delta.x, delta.z).magnitude;
        if (_distance < 2f || _step == null || _capsule == null) return;
        bool grounded = false;
        foreach (RaycastHit hit in Physics.RaycastAll(transform.position + Vector3.up * 0.2f, Vector3.down, 0.6f, ~0, QueryTriggerInteraction.Ignore))
            if (!hit.transform.IsChildOf(transform) && hit.collider is MeshCollider) { grounded = true; break; }
        if (!grounded) { _distance = 0f; return; }
        _distance = 0f; _steps.PlayOneShot(_step);
    }
}
