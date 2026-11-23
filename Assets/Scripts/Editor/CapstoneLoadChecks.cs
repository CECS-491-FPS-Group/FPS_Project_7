using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using static AimIncidentData;

// Scripted load cases on the actual observation/capture/archive code; no human-play claim.
internal static class CapstoneLoadChecks
{
    private static readonly BindingFlags Flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private static int step, assertions, configured;
    private static AimIncidentCapture capture;
    private static int expectedFiles;
    internal static void Begin()
    {
        // Scene NetworkObjects can be disposed by the preceding offline-review transition.
        // Read the actual authoritative LobbyScene_v1 component, not MainMenu's advertisement limit.
        string guid = UnityEditor.AssetDatabase.AssetPathToGUID("Assets/Scripts/NetworkLobbyUI.cs");
        var block = System.Text.RegularExpressions.Regex.Split(System.IO.File.ReadAllText("Assets/Scenes/LobbyScene_v1.unity"), "(?m)^---")
            .Single(text => text.Contains("guid: " + guid + ","));
        configured = int.Parse(System.Text.RegularExpressions.Regex.Match(block, "maximumPlayers: ([0-9]+)").Groups[1].Value);
        if (configured < 1 || configured > 16) throw new Exception("Unsupported lobby setting");
        step = assertions = 0;
    }
    private static void Check(bool ok, string label) { if (!ok) throw new Exception("[RC LOAD] " + label); assertions++; }
    private static object Metric(string name) => typeof(AimIncidentArchive).GetProperty("Metrics", Flags).GetValue(null).GetType().GetField(name).GetValue(typeof(AimIncidentArchive).GetProperty("Metrics", Flags).GetValue(null));
    internal static bool Tick()
    {
        if (AimIncidentArchive.Busy) return false;
        if (capture != null)
        {
            var files = AimIncidentArchive.Files().Select(AimIncidentArchive.Load).Where(d => d.matchId == capture.MatchId).ToArray();
            Check(files.Length == expectedFiles && files.All(d => d.final), "all qualifying participants retain final files without starvation");
            Debug.Log($"[RC LOAD WRITE] players={(step <= 3 ? configured : 16)} expectedFiles={expectedFiles} writes={Metric("Writes")} " +
                $"workerTotalMs={Metric("TotalMs")} workerMaxMs={Metric("MaximumMs")} queueHigh={Metric("HighWater")} coalesced={Metric("Coalesced")}; " +
                "actual async serialization/prune/atomic IO on Temp, not live play. Main thread did not await writer.");
            capture = null;
        }
        if (step >= 6)
        {
            Churn(); Debug.Log($"[RC LOAD] PASS assertions={assertions} configured={configured} maximum=16; synthetic intervals and real Temp IO."); return true;
        }
        int players = step < 3 ? configured : 16, active = step % 3 == 0 ? 0 : step % 3 == 1 ? 1 : players;
        typeof(AimIncidentArchive).GetMethod("ResetMetrics", Flags).Invoke(null, null);
        Run(players, active); step++; return false;
    }
    private static Frame Point(double time, float x, float yaw, float targetX) => new Frame { time = time,
        shooterKnown = true, targetKnown = true, aimKnown = true, fresh = true, shooter = new Point(x, 0, 0),
        eye = new Point(x, 1, 0), target = new Point(targetX, 1, 50), aim = new Point(Mathf.Sin(yaw * Mathf.Deg2Rad), 0, Mathf.Cos(yaw * Mathf.Deg2Rad)),
        visibility = "Covered", decision = "load fixture; not live", cover = "synthetic box", shooterEffect = "None", targetEffect = "None" };
    private static void Run(int players, int active)
    {
        var actors = Enumerable.Range(0, players).Select(i => new Actor { participantId = Guid.NewGuid().ToString("N"), connectionId = i }).ToArray();
        var models = Enumerable.Range(0, players).Select(_ => new OccludedAimTracking()).ToArray();
        // Synthetic encounter begins at12s (grace through14s); later qualifying clips
        // are still capturing at round-end30s. Earlier pose samples provide lead-in.
        foreach (var model in models) model.Reset(12);
        int finalSnapshots = 0;
        capture = new AimIncidentCapture("scripted-fixture", "load fixture", data => { if (data.final) finalSnapshots++; AimIncidentArchive.Enqueue(data); });
        var nextReport = new double[players]; int flags = 0; double finishMs;
        long memory = GC.GetTotalMemory(true); int collections = GC.CollectionCount(0);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        // Last observation29.8s, then round-end at30s must finish ACTIVE clips, not time a no-op End.
        for (int tick = 0; tick < 150; tick++)
        for (int i = 0; i < players; i++)
        {
            double time = tick * .2, targetYaw = 5 * Math.Sin(time); double aim = i < active ? targetYaw : 0;
            bool report = models[i].Observe(new OccludedAimTracking.Sample { Time = time, Fresh = true, Cover = OccludedAimTracking.Visibility.Covered,
                AimYaw = aim, TargetYaw = targetYaw });
            bool emit = report && time >= nextReport[i];
            if (emit) { nextReport[i] = time + 10; flags++; }
            capture.Observe(actors[i], actors[(i + 1) % players], Point(time, i * 20, (float)aim, i * 20 + Mathf.Tan((float)targetYaw * Mathf.Deg2Rad) * 50),
                emit ? new Alert { duration = models[i].Duration, matched = models[i].MatchedTime, samples = models[i].Samples,
                    aimTravel = models[i].AimTravel, targetTravel = models[i].TargetTravel, queries = 15 } : (Alert?)null);
        }
        watch.Stop(); double sampleMs = watch.Elapsed.TotalMilliseconds; int previouslyFinal = finalSnapshots;
        watch.Restart(); capture.End(30, "load fixture ended"); watch.Stop(); finishMs = watch.Elapsed.TotalMilliseconds;
        Check(finalSnapshots - previouslyFinal == active, "round-end cost includes every active clip's final snapshot/submission");
        expectedFiles = active; Check(capture.FlaggedPlayers == active, "clean players stay unflagged with concurrent qualifying actors");
        Debug.Log($"[RC LOAD] players={players} cadence=5Hz virtualSeconds=30 observations={players * 150} active={active} alerts={flags} " +
            $"observeCaptureTotalMs={sampleMs:F3} finalizeSubmitMs={finishMs:F3} heapDeltaBeforeGC={GC.GetTotalMemory(false) - memory} gen0Collections={GC.CollectionCount(0) - collections}; " +
            "core aim + capture + submit/validation, excludes physics/world/transport; actual async IO measured separately.");
    }
    private static void Churn()
    {
        var actors = Enumerable.Range(0, 16).Select(i => new Actor { participantId = Guid.NewGuid().ToString("N") }).ToArray();
        var model = new AimIncidentCapture("scripted-fixture", "clean candidate churn", _ => { });
        long before = GC.GetTotalMemory(true); int collections = GC.CollectionCount(0); var watch = System.Diagnostics.Stopwatch.StartNew();
        for (int tick = 0; tick <= 150; tick++) for (int i = 0; i < 16; i++)
            model.Observe(actors[i], actors[(i + 1 + tick % 15) % 16], Point(tick * .2, i * 20, 0, i * 20 + 1));
        watch.Stop();
        Debug.Log($"[RC CHURN] players=16 cadence=5Hz virtualSeconds=30 observations=2416 totalMs={watch.Elapsed.TotalMilliseconds:F3} " +
            $"heapDeltaBeforeGC={GC.GetTotalMemory(false) - before} gen0Collections={GC.CollectionCount(0) - collections} incidents={model.Incidents}; " +
            "clean target changes every sample, fixture not live allocation profile.");
        Check(model.Incidents == 0, "rapid clean candidate changes cannot make an incident");
    }
}
