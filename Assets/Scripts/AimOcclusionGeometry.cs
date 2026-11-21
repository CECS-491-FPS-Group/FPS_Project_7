using UnityEngine;

// Conservative physical cover. Only one static convex box covering every padded
// bound corner supports Covered. Mesh/mixed/moving/partial cover remains uncertain.
public sealed class AimOcclusionGeometry
{
    private readonly RaycastHit[] _hits = new RaycastHit[32];
    private readonly Collider[] _overlaps = new Collider[32];
    public int Queries { get; private set; }
    public BoxCollider Cover { get; private set; }
    private bool ClearOrigin(PhysicsScene scene, Vector3 origin, Transform self, Transform target)
    {
        Queries++;
        int count = scene.OverlapSphere(origin, .01f, _overlaps, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        bool clear = count < _overlaps.Length;
        for (int i = 0; i < count; i++)
        {
            Collider c = _overlaps[i]; _overlaps[i] = null;
            if (c == null || (self != null && c.transform.IsChildOf(self)) || (target != null && c.transform.IsChildOf(target))) continue;
            if ((c.ClosestPoint(origin) - origin).sqrMagnitude < .00000001f) clear = false;
        }
        return clear;
    }
    private int Blocker(PhysicsScene scene, Vector3 origin, Vector3 point, Transform self, Transform target, out Collider blocker)
    {
        blocker = null; Vector3 delta = point - origin; float distance = delta.magnitude;
        if (distance < .1f) return -1;
        Queries++;
        int count = scene.Raycast(origin, delta / distance, _hits, distance - .02f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        if (count == _hits.Length) return -1;
        float nearest = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            var hit = _hits[i]; _hits[i] = default;
            if ((self != null && hit.transform.IsChildOf(self)) || (target != null && hit.transform.IsChildOf(target))) continue;
            if (hit.distance < nearest) { nearest = hit.distance; blocker = hit.collider; }
        }
        if (blocker == null) return 0;
        // Another actor/corpse/dynamic obstacle is timing ambiguity, not solid-cover evidence.
        if (blocker.attachedRigidbody != null || blocker.GetComponentInParent<Health>() != null ||
            blocker.GetComponentInParent<Animator>() != null) return -1;
        return 1;
    }
    public OccludedAimTracking.Visibility Evaluate(PhysicsScene scene, Vector3 eye, Vector3 weapon, Bounds bounds, Transform self = null, Transform target = null)
    {
        Queries = 0; Cover = null;
        if (!scene.IsValid() || !Finite(eye) || !Finite(weapon) || !Finite(bounds.center) || !Finite(bounds.size) ||
            bounds.size.x <= 0 || bounds.size.y <= 0 || bounds.size.z <= 0 ||
            !ClearOrigin(scene, eye, self, target) || !ClearOrigin(scene, weapon, self, target)) return OccludedAimTracking.Visibility.Uncertain;
        // Known interior capsule/body points: an unobstructed point is evidence of exposure.
        Vector3 centre = bounds.center;
        for (int i = 0; i < 5; i++)
        {
            Vector3 point = centre;
            if (i == 1) point.y += bounds.extents.y * .5f;
            if (i == 2) point.y -= bounds.extents.y * .5f;
            if (i == 3) point.x += bounds.extents.x * .5f;
            if (i == 4) point.x -= bounds.extents.x * .5f;
            int result = Blocker(scene, eye, point, self, target, out _);
            if (result < 0) return OccludedAimTracking.Visibility.Uncertain;
            if (result == 0) return OccludedAimTracking.Visibility.Visible;
        }
        bounds.Expand(.2f); // edge/timing margin: never interpret a narrow exposed edge as fully hidden
        BoxCollider cover = null;
        int origins = (eye - weapon).sqrMagnitude < .000001f ? 1 : 2;
        for (int origin = 0; origin < origins; origin++)
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = centre + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            int result = Blocker(scene, origin == 0 ? eye : weapon, corner, self, target, out Collider blocker);
            if (result != 1 || !(blocker is BoxCollider box)) return OccludedAimTracking.Visibility.Uncertain;
            if (cover != null && cover != box) return OccludedAimTracking.Visibility.Uncertain;
            cover = box;
        }
        Cover = cover;
        return cover != null ? OccludedAimTracking.Visibility.Covered : OccludedAimTracking.Visibility.Uncertain;
    }
    private static bool Finite(Vector3 v) => ServerGameplayValidation.Finite(v.x) && ServerGameplayValidation.Finite(v.y) && ServerGameplayValidation.Finite(v.z);
}
