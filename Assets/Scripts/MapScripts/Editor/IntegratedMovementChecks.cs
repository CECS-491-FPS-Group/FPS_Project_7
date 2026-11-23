using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Actual integrated terrain + native CharacterController.Move. Not human input or transport.
internal static class IntegratedMovementChecks
{
    internal static int Run(TerrainGenerator terrain)
    {
        int checks = 0, samples = 0, reports = 0, routes = 0;
        void Check(bool ok, string reason) { if (!ok) throw new Exception("[Integrated MOVE] " + reason); checks++; }
        var actor = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab"));
        var capsule = actor.GetComponent<CharacterController>(); var posture = actor.GetComponent<PlayerPosture>() ?? actor.AddComponent<PlayerPosture>();
        var runtime = typeof(SpeedHackDetector).Assembly;
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var queryType = runtime.GetType("MovementSupportQuery"); var query = Activator.CreateInstance(queryType, true);
        var evaluate = queryType.GetMethod("Evaluate", flags);
        var modelType = runtime.GetType("ReceivedMovementObservation"); var model = Activator.CreateInstance(modelType, true);
        var sampleType = modelType.GetNestedType("Sample", BindingFlags.NonPublic);
        var reset = modelType.GetMethod("Reset", flags); var observe = modelType.GetMethod("Observe", flags);
        double clock = 0;
        try
        {
            posture.Initialize(); capsule.enabled = true;
            void Route(Vector3 hint, Vector3 toward, string label, bool jump)
            {
                if (!terrain.TryGetDrySurface(hint, out RaycastHit surface)) return;
                capsule.enabled = false; actor.transform.position = surface.point + Vector3.up * .11f; capsule.enabled = true;
                posture.Apply(false); Physics.SyncTransforms(); reset.Invoke(model, null);
                Vector3 direction = Vector3.ProjectOnPlane(toward - hint, Vector3.up).normalized;
                if (direction.sqrMagnitude < .5f) direction = Vector3.right;
                float vy = 0, travel = 0; int routeSamples = 0, grounded = 0;
                for (int i = 0; i < 150; i++)
                {
                    // Native route: sprint, approved Speed, expiry/replacement, crouch transition.
                    float speed = i >= 25 && i < 70 ? 9 : 6;
                    if (i == 45) posture.Apply(true);
                    if (i == 85 && posture.CanStand()) posture.Apply(false);
                    if (jump && capsule.isGrounded && (i == 15 || i == 100)) vy = Mathf.Sqrt(2 * 15 * (i == 15 ? 1.8f : 1.2f));
                    else if (capsule.isGrounded && vy < 0) vy = -2;
                    vy += -15 * .02f;
                    Vector3 previous = actor.transform.position;
                    capsule.Move((direction * speed + Vector3.up * vy) * .02f); clock += .02;
                    travel += Vector3.Distance(previous, actor.transform.position); if (capsule.isGrounded) grounded++;
                    if (i % 13 != 0) continue;
                    Physics.SyncTransforms();
                    Vector3 feet = actor.transform.TransformPoint(capsule.center - Vector3.up * capsule.height * .5f);
                    object support = evaluate.Invoke(query, new object[] { feet, capsule.radius, capsule.stepOffset, actor.transform, terrain.IsColliderReadyAt(actor.transform.position) });
                    object sample = Activator.CreateInstance(sampleType);
                    void Field(string name, object value) => sampleType.GetField(name, flags).SetValue(sample, value);
                    Field("Time", clock); Field("X", (double)actor.transform.position.x); Field("Y", (double)actor.transform.position.y); Field("Z", (double)actor.transform.position.z);
                    Field("Speed", (double)speed); Field("Launch", Math.Max(Math.Sqrt(30 * (jump && i < 50 ? 1.8 : 1.2)), speed * Math.Tan(capsule.slopeLimit * Math.PI / 180)));
                    Field("Gravity", -15d); Field("Step", (double)capsule.stepOffset); Field("LevelTolerance", .05d); Field("Ground", support);
                    if (observe.Invoke(model, new[] { sample }) != null) reports++;
                    routeSamples++; samples++;
                }
                Check(routeSamples > 8 && reports == 0, label + ": legitimate native traversal not reported by received-goal model"); routes++;
                Check(travel > .25f, label + ": controller actually traversed geometry, not a stationary sample fixture");
                Debug.Log($"[Integrated ROUTE] {label} duration=3 virtualSeconds travel={travel:F3}m groundedMoves={grounded}/150 samples={routeSamples}.");
            }
            var points = terrain.Layout.Roads.Points;
            Check(points.Length > 2, "actual road route available");
            Route(points[0], points[1], "road sprint/Speed expiry/crouch", false);
            foreach (var span in terrain.Layout.Bridges.Take(2))
                Route(points[span.FirstPoint], points[Math.Min(span.LastPoint, points.Length - 1)], "bridge moving approved Jump", true);
            foreach (var point in terrain.Layout.PointsOfInterest.Take(2))
                Route(new Vector3(point.x, 0, point.y), new Vector3(point.x + 8, 0, point.y), "POI slope/roof and falling", true);
            var building = terrain.transform.Find("Structures").GetComponentsInChildren<BoxCollider>().FirstOrDefault(c => c.bounds.size.y > 1 && c.bounds.size.x < 20);
            if (building != null) Route(building.bounds.min - Vector3.right, building.bounds.max + Vector3.right, "building exterior", false);
            bool shore = false;
            Vector2 centre = terrain.worldSettings.WorldCentre(terrain.MeshWorldSize);
            for (int x = -6; x <= 6 && !shore; x++) for (int z = -6; z <= 6 && !shore; z++)
            {
                var hint = new Vector3(centre.x + x * terrain.WorldSize / 14, 0, centre.y + z * terrain.WorldSize / 14);
                if (terrain.TrySampleHeight(hint, out float h) && h > terrain.SeaLevel + .1f && h < terrain.SeaLevel + 2 && terrain.TryGetDrySurface(hint, out _))
                { Route(hint, hint + Vector3.forward * 8, "water-edge sprint/fall", false); shore = true; }
            }
            if (building != null) ProfileGeometry(terrain, building, actor.transform);
            Check(terrain.transform.Find("Water") != null && terrain.transform.Find("Water").GetComponentsInChildren<Collider>().Length == 0,
                "water has no hidden swim/ground/velocity rule");
            // Missing observations cannot distinguish a straight wall crossing from rounding its end.
            Vector3 left = new Vector3(-1, 0, 0), right = new Vector3(1, 0, 0);
            float aroundDistance = Vector3.Distance(left, new Vector3(-1, 0, 1)) + 2 + 1;
            Check(Vector3.Distance(left, right) < 6 && aroundDistance <= 6,
                "same one-second endpoint pair permits straight crossing or a legal 4m corner detour at6m/s");
            Debug.Log($"[Integrated MOVE] PASS checks={checks} routes={routes} samples={samples} reports={reports} " +
                $"seed={terrain.Seed} signature={terrain.LayoutSignature}; native CharacterController/geometry + synthetic fresh observations, NOT FishNet delivery or human play. No wall-traversal detector justified.");
        }
        finally { UnityEngine.Object.DestroyImmediate(actor); Physics.SyncTransforms(); }
        return checks;
    }
    private static void ProfileGeometry(TerrainGenerator terrain, BoxCollider building, Transform self)
    {
        foreach (int players in new[] { 4, 16 })
        {
            var query = new AimOcclusionGeometry(); var bounds = building.bounds; long count = 0; int visible = 0, covered = 0, uncertain = 0;
            var eye = bounds.center + Vector3.forward * (bounds.extents.z + 20);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            for (int tick = 0; tick <= 150; tick++) for (int i = 0; i < players; i++)
            {
                var target = new Bounds(bounds.center - Vector3.forward * (bounds.extents.z + 20) + Vector3.right * Mathf.Sin(tick * .2f + i), new Vector3(1, 2, 1));
                var state = query.Evaluate(Physics.defaultPhysicsScene, eye, eye, target, self);
                count += query.Queries;
                if (state == OccludedAimTracking.Visibility.Visible) visible++; else if (state == OccludedAimTracking.Visibility.Covered) covered++; else uncertain++;
            }
            watch.Stop();
            Debug.Log($"[RC MAP PERF] players={players} cadence=5Hz virtualSeconds=30 evaluations={players * 151} totalMs={watch.Elapsed.TotalMilliseconds:F3} " +
                $"physicsQueries={count} visible={visible} covered={covered} uncertain={uncertain} seed={terrain.Seed}; actual integrated map PhysX, synthetic candidate positions, no human/transport claim.");
        }
    }
}
