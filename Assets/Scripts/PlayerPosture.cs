using StarterAssets;
using UnityEngine;

// Source crouch scale .4 is confined to capsule/visuals, never the network root.
[DefaultExecutionOrder(50)]
public sealed class PlayerPosture : MonoBehaviour
{
    private CharacterController _capsule;
    private PlayerCameraSetup _setup;
    private Transform _eye;
    private float _height, _step;
    private Vector3 _centre, _eyePosition, _visualScale, _aimOffset;
    private bool _initialized;
    private CapsuleCollider[] _hitCapsules;
    private float[] _hitHeights;
    private Vector3[] _hitCentres;
    public bool Crouched { get; private set; }
    public float Ratio => Crouched ? CrouchedHeight(_height, _capsule.radius) / _height : 1;
    public static float CrouchedHeight(float height, float radius) => Mathf.Max(height * .4f, radius * 2);
    public Vector3 AimOrigin { get { Initialize(); return transform.TransformPoint(new Vector3(_aimOffset.x, _aimOffset.y * Ratio, _aimOffset.z)); } }

    public void Initialize()
    {
        if (_initialized) return;
        _initialized = true;
        _capsule = GetComponent<CharacterController>(); _setup = GetComponent<PlayerCameraSetup>();
        _height = _capsule.height; _centre = _capsule.center; _step = _capsule.stepOffset;
        _eye = GetComponent<FirstPersonController>().CinemachineCameraTarget.transform;
        _eyePosition = _eye.localPosition;
        _aimOffset = transform.InverseTransformPoint(_setup.playerCamera.transform.position);
        _hitCapsules = GetComponentsInChildren<CapsuleCollider>(true);
        _hitHeights = new float[_hitCapsules.Length]; _hitCentres = new Vector3[_hitCapsules.Length];
        for (int i = 0; i < _hitCapsules.Length; i++)
        { _hitHeights[i] = _hitCapsules[i].height; _hitCentres[i] = _hitCapsules[i].center; }
        if (_setup.soldierVisual != null) _visualScale = _setup.soldierVisual.localScale;
    }
    public void Apply(bool crouched)
    {
        Initialize(); Crouched = crouched;
        float height = _height * Ratio;
        _capsule.height = height;
        _capsule.center = _centre - Vector3.up * ((_height - height) * .5f);
        _capsule.stepOffset = Mathf.Min(_step, height * .5f);
        for (int i = 0; i < _hitCapsules.Length; i++)
        {
            float target = Mathf.Max(_hitHeights[i] * Ratio, _hitCapsules[i].radius * 2);
            _hitCapsules[i].height = target;
            _hitCapsules[i].center = _hitCentres[i] - Vector3.up * ((_hitHeights[i] - target) * .5f);
        }
        _eye.localPosition = new Vector3(_eyePosition.x, _eyePosition.y * Ratio, _eyePosition.z);
        if (_setup.soldierVisual != null)
            _setup.soldierVisual.localScale = new Vector3(_visualScale.x, _visualScale.y * Ratio, _visualScale.z);
    }
    public bool CanStand()
    {
        Initialize();
        float radius = _capsule.radius * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z));
        float half = Mathf.Max(_height * Mathf.Abs(transform.lossyScale.y) * .5f - radius, 0);
        Vector3 centre = transform.TransformPoint(_centre);
        foreach (Collider hit in Physics.OverlapCapsule(centre + Vector3.up * half, centre - Vector3.up * half,
            Mathf.Max(.01f, radius - .02f), Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            if (!hit.transform.IsChildOf(transform)) return false;
        return true;
    }
    private void LateUpdate()
    {
        if (_setup != null && (_setup.IsServerInitialized || _setup.IsClientInitialized))
            _setup.playerCamera.transform.position = AimOrigin;
    }
}
