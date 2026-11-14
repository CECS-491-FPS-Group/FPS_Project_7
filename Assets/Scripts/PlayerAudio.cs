using UnityEngine;

// Server-result shots/HP and movement-derived footsteps. Presentation never authorizes gameplay.
public sealed class PlayerAudio : MonoBehaviour
{
    private AudioSource _shots, _steps, _damage;
    private AudioClip _shot, _shotgun, _step;
    private Health _health;
    private CharacterController _capsule;
    private Transform _emitter;
    private Vector3 _previous;
    private float _distance;
    private readonly RaycastHit[] _groundHits = new RaycastHit[8];
    public enum HealthCue { None, Hurt, Death }

    private void Awake()
    {
#if UNITY_SERVER
        enabled = false;
        return;
#else
        _health = GetComponent<Health>(); _capsule = GetComponent<CharacterController>();
        var emitter = new GameObject("Shot and Health Audio");
        _emitter = emitter.transform;
        emitter.transform.SetParent(transform, false);
        emitter.transform.localPosition = Vector3.up * 1.5f;
        _shots = emitter.AddComponent<AudioSource>();
        _damage = emitter.AddComponent<AudioSource>();
        _steps = gameObject.AddComponent<AudioSource>();
        foreach (AudioSource source in new[] { _shots, _steps, _damage })
        {
            source.playOnAwake = false; source.spatialBlend = 1f;
            source.minDistance = 2f; source.maxDistance = 40f;
            source.rolloffMode = AudioRolloffMode.Linear; source.dopplerLevel = 0f;
        }
        _shots.minDistance = 6f; _shots.maxDistance = 90f;
        _shots.rolloffMode = AudioRolloffMode.Custom;
        _shots.SetCustomCurve(AudioSourceCurveType.CustomRolloff, ShotRolloff(6f, 90f));
        _shot = Resources.Load<AudioClip>("IntegrationAudio/Shot");
        _shotgun = Resources.Load<AudioClip>("IntegrationAudio/Shotgun");
        _step = Resources.Load<AudioClip>("IntegrationAudio/Step");
        // Warm the branch's generated SFX once, rather than allocating on each hit.
        _ = GameplayAudioClips.Hurt; _ = GameplayAudioClips.Death;
        _previous = transform.position;
        ApplyVolume();
#endif
    }

    private void OnEnable()
    {
        if (_shots == null) return;
        GameAudioSettings.Changed += ApplyVolume;
        if (_health != null) _health.HealthPresentationChanged += OnHealthChanged;
        ApplyVolume();
        _previous = transform.position; _distance = 0;
    }
    private void OnDisable()
    {
        GameAudioSettings.Changed -= ApplyVolume;
        if (_health != null) _health.HealthPresentationChanged -= OnHealthChanged;
    }
    private void ApplyVolume()
    {
        if (_shots == null) return;
        _shots.volume = .35f * GameAudioSettings.SfxVolume;
        _steps.volume = .15f * GameAudioSettings.SfxVolume;
        _damage.volume = GameAudioSettings.SfxVolume;
    }

    public void Shot(bool shotgun)
    {
        if (_shots == null) return;
        AudioClip clip = shotgun && _shotgun != null ? _shotgun : _shot;
        _shots.spatialBlend = _health != null && _health.IsOwner ? 0f : 1f;
        if (clip != null) _shots.PlayOneShot(clip);
    }

    // Host uses the server-side HP callback; remote instances use its client callback.
    // FishNet may also deliver a client callback on the host: it must not play twice.
    public static HealthCue SelectHealthCue(bool clientReady, bool serverReady, bool asServer, int previous, int next)
    {
        if (!clientReady || asServer != serverReady || previous <= 0 || next >= previous) return HealthCue.None;
        return next <= 0 ? HealthCue.Death : HealthCue.Hurt;
    }
    private void OnHealthChanged(int previous, int next, bool asServer)
    {
        HealthCue cue = SelectHealthCue(_health.IsClientInitialized, _health.IsServerInitialized, asServer, previous, next);
        if (cue == HealthCue.None) return;
        _damage.spatialBlend = _health.IsOwner ? 0f : 1f;
        _damage.PlayOneShot(cue == HealthCue.Death ? GameplayAudioClips.Death : GameplayAudioClips.Hurt,
            cue == HealthCue.Death ? 1f : .8f);
    }

    // Quadratic positional falloff adapted from ShotAudioBroadcaster@11c62b6,
    // without its client-triggered RPC or per-shot GameObject allocation.
    public static AnimationCurve ShotRolloff(float minDistance, float maxDistance)
    {
        float near = Mathf.Clamp01(minDistance / Mathf.Max(maxDistance, .01f));
        var curve = new AnimationCurve(new Keyframe(0, 1));
        for (int i = 0; i <= 10; i++)
        {
            float t = Mathf.Lerp(near, 1, i / 10f);
            float falloff = 1 - (t - near) / Mathf.Max(1 - near, .0001f);
            curve.AddKey(t, falloff * falloff);
        }
        return curve;
    }

    private void Update()
    {
        if (_emitter != null) _emitter.localPosition = Vector3.up * (1.5f * (GetComponent<PlayerPosture>()?.Ratio ?? 1));
        Vector3 delta = transform.position - _previous; _previous = transform.position;
        if (_health == null || !_health.IsClientInitialized || !_health.CanAct ||
            float.IsNaN(delta.sqrMagnitude) || float.IsInfinity(delta.sqrMagnitude) || delta.sqrMagnitude > 9f)
        { _distance = 0; return; }
        _distance += new Vector2(delta.x, delta.z).magnitude;
        if (_distance < 2f || _step == null || _capsule == null) return;
        bool grounded = false;
        int hits = Physics.RaycastNonAlloc(transform.position + Vector3.up * .2f, Vector3.down, _groundHits,
            .6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits; i++)
            if (!_groundHits[i].transform.IsChildOf(transform) && _groundHits[i].normal.y > .4f &&
                _groundHits[i].collider.GetComponentInParent<FishNet.Object.NetworkObject>() == null)
            { grounded = true; break; }
        if (!grounded) { _distance = 0f; return; }
        _distance = 0f; _steps.PlayOneShot(_step);
    }
}
