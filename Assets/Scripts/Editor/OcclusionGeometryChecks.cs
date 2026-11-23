using System;
using UnityEngine;

// Native PhysX regressions, using the runtime geometry and temporal logic.
internal static class OcclusionGeometryChecks
{
    internal static int Run(GameObject actor)
    {
        int checks = 0;
        void Check(bool condition, string label) { if (!condition) throw new InvalidOperationException("[Occlusion CHECK] " + label); checks++; }
        Vector3 offset = new Vector3(12000, 300, 12000), eye = offset + Vector3.up;
        var fixture = new GameObject("Occlusion physics regressions");
        var wallObject = new GameObject("Solid wall", typeof(BoxCollider)); wallObject.transform.SetParent(fixture.transform);
        var wall = wallObject.GetComponent<BoxCollider>(); wall.size = new Vector3(20, 10, 1); wall.transform.position = offset + new Vector3(0, 2, 25);
        var target = new GameObject("Target collision fixture", typeof(CapsuleCollider)); target.transform.SetParent(fixture.transform);
        target.transform.position = offset + new Vector3(0, 1, 50); target.GetComponent<CapsuleCollider>().height = 2;
        var query = new AimOcclusionGeometry(); var hard = new CombatRayGeometry();
        OccludedAimTracking.Visibility Classify() { Physics.SyncTransforms(); return query.Evaluate(Physics.defaultPhysicsScene, eye, eye, target.GetComponent<Collider>().bounds, actor.transform, target.transform); }
        try
        {
            Check(Classify() == OccludedAimTracking.Visibility.Covered, "one static box fully hides padded target");
            Check(query.Evaluate(Physics.defaultPhysicsScene, eye, eye + Vector3.right * 30, target.GetComponent<Collider>().bounds,
                actor.transform, target.transform) == OccludedAimTracking.Visibility.Uncertain, "different weapon/eye exposure is uncertain");
            wall.size = new Vector3(10, 10, 1); wall.transform.position += Vector3.left * 5;
            var adjacent = UnityEngine.Object.Instantiate(wallObject, fixture.transform); adjacent.transform.position += Vector3.right * 10;
            Check(Classify() == OccludedAimTracking.Visibility.Uncertain, "mixed occluders cannot prove convex coverage");
            UnityEngine.Object.DestroyImmediate(adjacent); wall.size = new Vector3(20, 10, 1); wall.transform.position += Vector3.right * 5;
            Physics.SyncTransforms();
            Check(CombatRayGeometry.FirstHit(eye, Vector3.forward, 100, actor.transform, out var hit) && hit.collider == wall, "hard shot stops at solid wall");
            // Demonstrate Unity's origin-inside blind spot, then shared hard guard blocks it.
            Vector3 inside = new Vector3(wall.bounds.center.x, eye.y, wall.bounds.center.z);
            Check(CombatRayGeometry.FirstHit(inside, Vector3.forward, 100, actor.transform, out hit) && hit.collider == target.GetComponent<Collider>(), "legacy ray alone misses enclosing wall");
            Check(!hard.OriginClear(inside, actor.transform), "origin overlap prevents enclosed-wall bypass");
            Check(hard.OriginClear(eye, actor.transform), "legitimate eye near no geometry remains clear");
            wall.transform.position += Vector3.right * 30;
            Check(Classify() == OccludedAimTracking.Visibility.Visible, "real opening exposes centre");
            Check(CombatRayGeometry.FirstHit(eye, Vector3.forward, 100, actor.transform, out hit) && hit.collider == target.GetComponent<Collider>(), "clear opening ray reaches target");
            wall.transform.position = offset + new Vector3(-9.9f, 2, 25);
            Check(Classify() != OccludedAimTracking.Visibility.Covered, "partially exposed target never confidently hidden");
            wall.transform.position = offset + new Vector3(0, 2, 25);
            var body = wallObject.AddComponent<Rigidbody>(); body.isKinematic = true;
            Check(Classify() == OccludedAimTracking.Visibility.Uncertain, "moving door/body makes coverage uncertain");
            UnityEngine.Object.DestroyImmediate(body);
            wall.isTrigger = true; Check(Classify() == OccludedAimTracking.Visibility.Visible, "trigger cannot be solid cover"); wall.isTrigger = false;
            wallObject.layer = 2; Check(Classify() == OccludedAimTracking.Visibility.Visible, "cosmetic corpse layer ignored"); wallObject.layer = 0;
            target.GetComponent<CapsuleCollider>().height = 1;
            Check(Classify() == OccludedAimTracking.Visibility.Covered, "short crouched target behind full wall remains covered");
            target.transform.position += Vector3.up * 20;
            Check(Classify() != OccludedAimTracking.Visibility.Covered, "elevated exposed target does not accumulate");
            target.transform.position = offset + new Vector3(0, 1, 50); target.GetComponent<CapsuleCollider>().height = 2;
            Physics.SyncTransforms();
            var tracking = new OccludedAimTracking(); tracking.Reset(0); int reports = 0;
            var watch = System.Diagnostics.Stopwatch.StartNew(); long queries = 0;
            for (int i = 0; i <= 60; i++)
            {
                float t = i * .2f; target.transform.position = offset + new Vector3(5 * Mathf.Sin(t), 1, 50);
                var state = Classify(); queries += query.Queries;
                Vector3 bearing = (target.transform.position - eye).normalized;
                if (tracking.Observe(new OccludedAimTracking.Sample { Time = t, Fresh = true, Cover = state,
                    AimYaw = OccludedAimObserver.Yaw(bearing), TargetYaw = OccludedAimObserver.Yaw(bearing) })) reports++;
            }
            Check(reports > 0, "synthetic known aim/target trajectory exercises shared physics plus tracking");
            watch.Stop();
            Debug.Log($"[Occlusion PERF] synthetic 61 classifications / 12 virtual seconds: queries={queries}, elapsedMs={watch.Elapsed.TotalMilliseconds:F3}; includes SyncTransforms, NOT live transport timing.");
            // Lobby supports 1..16 (default4). One candidate/player/5Hz, 20 or80 evals per virtual second.
            watch.Restart(); queries = 0;
            for (int i = 0; i < 800; i++) { Classify(); queries += query.Queries; }
            watch.Stop();
            Debug.Log($"[Occlusion PERF] synthetic max16 actors x5Hz x10 virtual seconds: classifications=800 queries={queries} elapsedMs={watch.Elapsed.TotalMilliseconds:F3}; static fixture, no live clients.");
            var cleanModel = new OccludedAimTracking(); cleanModel.Reset(0); int cleanReports = 0, saved = 0, seenVisible = 0, seenCovered = 0;
            var cleanCapture = new AimIncidentCapture("scripted-fixture", "clean corner fixture", _ => saved++);
            var a = new AimIncidentData.Actor { participantId = Guid.NewGuid().ToString("N") };
            var b = new AimIncidentData.Actor { participantId = Guid.NewGuid().ToString("N") };
            float heldYaw = Mathf.Atan2(17 + 2 * Mathf.Sin(.3f), 50) * Mathf.Rad2Deg;
            for (int i = 0; i <= 60; i++)
            {
                float t = i * .2f, x = t < 2 ? 23 - 3 * t : 17 + 2 * Mathf.Sin(t - 2);
                target.transform.position = offset + new Vector3(x, 1, 50);
                target.GetComponent<CapsuleCollider>().height = t >= 4 && t < 5 ? 1 : 2;
                var state = Classify(); if (state == OccludedAimTracking.Visibility.Visible) seenVisible++; if (state == OccludedAimTracking.Visibility.Covered) seenCovered++;
                float bearing = OccludedAimObserver.Yaw(target.transform.position - eye), aimYaw = t < 2.3f ? bearing : heldYaw;
                bool cue = t >= 1.4f && t <= 3.9f;
                if (cleanModel.Observe(new OccludedAimTracking.Sample { Time = t, Fresh = true, Cover = state, PossibleCue = cue,
                    AimYaw = aimYaw, TargetYaw = bearing })) cleanReports++;
                cleanCapture.Observe(a, b, new AimIncidentData.Frame { time = t, shooterKnown = true, targetKnown = true, aimKnown = true,
                    shooter = new AimIncidentData.Point(eye.x, eye.y, eye.z), target = new AimIncidentData.Point(target.transform.position.x, target.transform.position.y, target.transform.position.z),
                    visibility = state.ToString(), decision = "clean hold/brief follow/audio fixture", cover = "wall", cue = cue, shooterEffect = "None", targetEffect = "None" });
            }
            cleanCapture.End(12, "clean sequence ended");
            Check(seenVisible > 0 && seenCovered > 0, "actual corner geometry transitions exposed to covered");
            Check(cleanReports == 0, "clean hold then brief follow then held hidden angle with cue/partial posture never alerts");
            Check(saved == 0 && cleanCapture.FlaggedPlayers == 0, "ordinary clean geometry sequence creates no review flag/file");
        }
        finally { UnityEngine.Object.DestroyImmediate(fixture); Physics.SyncTransforms(); }
        return checks;
    }
}
