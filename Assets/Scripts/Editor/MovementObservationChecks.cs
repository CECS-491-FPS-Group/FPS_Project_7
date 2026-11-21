using System;
using System.Reflection;
using FishNet.Component.Transforming;
using UnityEngine;

// Native geometry and stop-callback fixtures; no multiplayer/transport acceptance claim.
internal static class MovementObservationChecks
{
    internal static int Run(GameObject actor)
    {
        int checks = 0;
        void Check(bool valid, string message) { if (!valid) throw new InvalidOperationException("[Movement CHECK] " + message); checks++; }
        Type queryType = typeof(SpeedHackDetector).Assembly.GetType("MovementSupportQuery");
        object query = Activator.CreateInstance(queryType, true);
        MethodInfo evaluate = queryType.GetMethod("Evaluate", BindingFlags.Instance | BindingFlags.NonPublic);
        string Support(Vector3 point, bool ready = true) => evaluate.Invoke(query, new object[] { point, .5f, .25f, actor.transform, ready }).ToString();
        var fixture = new GameObject("Movement support fixture");
        Vector3 feet = actor.transform.position + Vector3.down * .07f;
        try
        {
            Check(Support(feet, false) == "Unknown", "unready terrain is ambiguous");
            Check(Support(feet + Vector3.up * 20) == "Clear", "clear elevated sample has no collision support");
            var ground = fixture.AddComponent<BoxCollider>(); ground.size = new Vector3(5, 1, 5);
            fixture.transform.position = feet - Vector3.up * .55f; Physics.SyncTransforms();
            Check(Support(feet) == "NearGeometry", "static floor/bridge deck suppresses hover inference");
            fixture.transform.rotation = Quaternion.Euler(0, 0, 20); Physics.SyncTransforms();
            Check(Support(feet) == "NearGeometry", "slope collision proximity conservative");
            fixture.transform.rotation = Quaternion.identity; fixture.transform.position += Vector3.up * .25f; Physics.SyncTransforms();
            Check(Support(feet) == "NearGeometry", "step collision proximity conservative");
            var body = fixture.AddComponent<Rigidbody>(); body.isKinematic = true;
            Check(Support(feet) == "Unknown", "moving/kinematic support ambiguous");
            UnityEngine.Object.DestroyImmediate(body);
            ground.isTrigger = true;
            Check(Support(feet) == "Clear", "trigger volumes are not physical support");
            ground.isTrigger = false; fixture.layer = 2;
            Check(Support(feet) == "Clear", "cosmetic corpse layer cannot be support");
            fixture.layer = 0;
            var capsule = actor.GetComponent<CharacterController>();
            float before = capsule.center.y - capsule.height * .5f;
            actor.GetComponent<PlayerPosture>().Apply(true);
            Check(Mathf.Abs(capsule.center.y - capsule.height * .5f - before) < .0001f && Support(feet) == "NearGeometry", "crouch preserves support reference");
            actor.GetComponent<PlayerPosture>().Apply(false);
            UnityEngine.Object.DestroyImmediate(ground); Physics.SyncTransforms();
            Check(Support(feet) == "Clear", "self colliders do not imply support");
            for (int i = 0; i < 34; i++)
            {
                var child = new GameObject("Crowded support", typeof(BoxCollider)); child.transform.SetParent(fixture.transform, false);
                child.transform.position = feet;
            }
            Physics.SyncTransforms();
            Check(Support(feet) == "Unknown", "saturated nonalloc query is ambiguous");
        }
        finally { UnityEngine.Object.DestroyImmediate(fixture); }

        var detector = actor.GetComponent<SpeedHackDetector>(); var network = actor.GetComponent<NetworkTransform>();
        var hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        var handler = (NetworkTransform.DataReceivedChanged)Delegate.CreateDelegate(typeof(NetworkTransform.DataReceivedChanged), detector,
            typeof(SpeedHackDetector).GetMethod("OnReceivedGoal", hidden));
        var listeners = typeof(NetworkTransform).GetField("OnDataReceived", hidden);
        int Count() => (listeners.GetValue(network) as Delegate)?.GetInvocationList().Length ?? 0;
        int initial = Count();
        network.OnDataReceived += handler;
        typeof(SpeedHackDetector).GetField("_networkTransform", hidden).SetValue(detector, network);
        typeof(SpeedHackDetector).GetField("_pendingGoal", hidden).SetValue(detector, true);
        detector.OnStopServer();
        Check(Count() == initial && !(bool)typeof(SpeedHackDetector).GetField("_pendingGoal", hidden).GetValue(detector), "stop callback unhooks listener and clears queued sample");
        detector.OnStopServer();
        Check(Count() == initial, "repeated stop callbacks do not accumulate listeners");
        return checks;
    }
}
