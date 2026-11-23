using UnityEngine;

// Shared hard shot geometry. No penetration: nearest non-self/non-dead solid wins.
public sealed class CombatRayGeometry
{
    private readonly Collider[] _overlaps = new Collider[32];
    private readonly RaycastHit[] _segmentHits = new RaycastHit[32];
    // A clear endpoint alone cannot establish that a body-mounted origin is reachable.
    // Query both directions so a one-sided procedural mesh cannot be crossed backwards.
    public bool WorldSegmentClear(Vector3 from, Vector3 to, Transform self)
    {
        Vector3 delta = to - from; float distance = delta.magnitude;
        if (!ServerGameplayValidation.Finite(distance)) return false;
        if (distance < .0001f) return true;
        return ClearDirection(from, delta / distance, distance, self) && ClearDirection(to, -delta / distance, distance, self);
    }
    private bool ClearDirection(Vector3 origin, Vector3 direction, float distance, Transform self)
    {
        int count = Physics.RaycastNonAlloc(origin, direction, _segmentHits, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        bool clear = count < _segmentHits.Length;
        for (int i = 0; i < count; i++)
        {
            Collider hit = _segmentHits[i].collider; _segmentHits[i] = default;
            if (hit != null && !hit.transform.IsChildOf(self) && hit.GetComponentInParent<Health>() == null) clear = false;
        }
        return clear;
    }
    public bool OriginClear(Vector3 origin, Transform shooter)
    {
        int count = Physics.OverlapSphereNonAlloc(origin, .01f, _overlaps, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        bool clear = count < _overlaps.Length;
        for (int i = 0; i < count; i++)
        {
            Collider hit = _overlaps[i]; _overlaps[i] = null;
            if (hit == null || hit.transform.IsChildOf(shooter) || hit.GetComponentInParent<Health>() != null) continue;
            if ((hit.ClosestPoint(origin) - origin).sqrMagnitude <= .00000001f) clear = false;
        }
        return clear;
    }
    public static bool FirstHit(Vector3 origin, Vector3 direction, float range, Transform shooter, out RaycastHit closest)
    {
        RaycastHit[] hits = Physics.RaycastAll(origin, direction, range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        closest = default; float distance = float.PositiveInfinity;
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform.IsChildOf(shooter) || hit.distance >= distance) continue;
            Health player = hit.collider.GetComponentInParent<Health>();
            if (player != null && player.IsDead) continue;
            closest = hit; distance = hit.distance;
        }
        return closest.collider != null;
    }
}
