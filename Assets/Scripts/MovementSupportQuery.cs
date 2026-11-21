using UnityEngine;

// Conservative broad collision proximity, NOT an authoritative grounded/LOS/path test.
internal sealed class MovementSupportQuery
{
    private readonly Collider[] _hits = new Collider[32];
    internal ReceivedMovementObservation.Support Evaluate(Vector3 feet, float radius, float step,
        Transform actor, bool terrainReady)
    {
        if (!terrainReady || !Finite(feet) || !Finite(radius) || radius <= 0 || !Finite(step) || step < 0)
            return ReceivedMovementObservation.Support.Unknown;
        float margin = Mathf.Max(.1f, step);
        int count = Physics.OverlapCapsuleNonAlloc(feet + Vector3.up * margin,
            feet - Vector3.up * (radius + margin), radius + margin, _hits,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        bool uncertain = count == _hits.Length, near = false;
        for (int i = 0; i < count; i++)
        {
            Collider hit = _hits[i]; _hits[i] = null;
            if (hit == null || hit.transform.IsChildOf(actor)) continue;
            // Actors and moving/kinematic surfaces are ambiguous, never evidence of flight.
            if (hit.attachedRigidbody != null || hit.GetComponentInParent<Health>() != null || hit is CharacterController)
                uncertain = true;
            near = true;
        }
        return uncertain ? ReceivedMovementObservation.Support.Unknown : near ? ReceivedMovementObservation.Support.NearGeometry : ReceivedMovementObservation.Support.Clear;
    }
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
}
