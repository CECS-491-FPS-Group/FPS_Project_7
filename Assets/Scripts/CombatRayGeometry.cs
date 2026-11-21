using UnityEngine;

// Shared hard shot geometry. No penetration: nearest non-self/non-dead solid wins.
public sealed class CombatRayGeometry
{
    private readonly Collider[] _overlaps = new Collider[32];
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
