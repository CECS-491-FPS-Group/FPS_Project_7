using FishNet.Connection;
using FishNet.Object;
using UnityEngine;
using UnityEngine.InputSystem;

public class HitscanShooter : NetworkBehaviour
{
    [Min(1)] public int damage = 40;
    [Min(0.1f)] public float range = 100f;
    [Min(0.1f)] public float shotsPerSecond = 5f;
    [Range(0f, 180f)] public float maxYawDifference = 45f;
    [Range(0f, 89.9f)] public float maxPitch = 89f;

    private Camera _camera;
    private Health _health;
    private double _nextServerShotTime;

    private void Awake()
    {
        _camera = GetComponent<Camera>();
        _health = GetComponentInParent<Health>();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        _nextServerShotTime = 0d;
    }

    private void LateUpdate()
    {
        if (!IsClientInitialized || !IsOwner || _camera == null || !_camera.isActiveAndEnabled) return;
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            RequestShot(_camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f)).direction);
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
        if (_health == null || !_health.IsServerInitialized || _health.CurrentHealth <= 0)
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

        double now = TimeManager.TicksToTime(TimeManager.Tick);
        if (now < _nextServerShotTime)
        {
            Reject("cooldown");
            return;
        }
        _nextServerShotTime = now + 1d / shotsPerSecond;

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
        if (closest.collider == null) return;
        Health victim = closest.collider.GetComponentInParent<Health>();
        if (victim == null || victim == _health || !victim.IsSpawned ||
            !victim.CompareTag("Player") || !victim.Owner.IsActive || victim.Owner == Owner) return;

        victim.TakeDamage(damage, OwnerId);
    }

    private void Reject(string reason)
    {
        Debug.Log($"[Combat] Rejected shot shooter={OwnerId}: {reason}.", this);
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
