using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static AimIncidentData;

// Editor-only complete-path fixtures. All archive writes isolated under project Temp.
internal static class AimIncidentIntegrationChecks
{
    private static readonly BindingFlags Hidden = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    private static readonly FieldInfo FolderField = typeof(AimIncidentArchive).GetField("_folder", BindingFlags.Static | BindingFlags.NonPublic);
    private static string originalFolder, folder, fixtureMatch, bridgeMatch;
    private static Incident fixture;
    private static Incident gapFixture;
    private static NetworkManager manager;
    private static AimIncidentReviewMenu menu;
    private static int phase, assertions, retentionIndex;
    internal static void Prepare()
    {
        if (AimIncidentArchive.Busy) throw new Exception("Previous archive worker still running");
        originalFolder = (string)FolderField.GetValue(null);
        folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/AntiCheatIncidentChecks", Guid.NewGuid().ToString("N")));
        FolderField.SetValue(null, folder); phase = assertions = retentionIndex = 0;
    }
    internal static void Start(NetworkManager network, OccludedAimObserver observer)
    {
        manager = network;
        fixtureMatch = (string)typeof(OccludedAimObserver).GetField("_lastFixtureMatch", Hidden).GetValue(observer);
        manager.ServerManager.Despawn(observer.gameObject);
        BridgeCases(); Benchmark(); BenchmarkBridge();
    }
    private static void Check(bool valid, string label)
    { if (!valid) throw new Exception("[Incident CHECK] " + label); assertions++; }
    private static void Invoke(object instance, string method, params object[] args) => instance.GetType().GetMethod(method, Hidden).Invoke(instance, args);
    private static object Read(object instance, string field) => instance.GetType().GetField(field, Hidden).GetValue(instance);
    private static NetworkConnection Owner(int id)
    {
        var owner = new NetworkConnection(manager, id, 0, true);
        Invoke(owner, "ConnectionAuthenticated"); Invoke(owner, "SetLoadedStartScenes", true); return owner;
    }
    private static GameObject Spawn(NetworkConnection owner, int x)
    {
        var player = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab"));
        player.transform.position = new Vector3(x, 300, 16000); manager.ServerManager.Spawn(player, owner); return player;
    }
    private static Frame Frame(double time) => new Frame { time = time, shooterKnown = true, targetKnown = true, aimKnown = true,
        aim = new Point(0, 0, 1), target = new Point(0, 0, 50), eye = new Point(0, 1, 0),
        visibility = "Covered", decision = "injected server harness sample", cover = "synthetic wall", shooterEffect = "None", targetEffect = "None" };
    private static Alert Evidence => new Alert { duration = 6, matched = 5.6, samples = 31, queries = 15 };
    private static void BridgeCases()
    {
        var recorder = manager.GetComponent<AimIncidentRecorder>();
        var a = Owner(7010); var b = Owner(7011);
        var shooter = Spawn(a, 16000); var target = Spawn(b, 16050); GameObject rejoined = null;
        try
        {
            Invoke(recorder, "BeginMatch", shooter.scene);
            var match = (AimIncidentCapture)Read(recorder, "_match"); bridgeMatch = match.MatchId;
            var observer = shooter.GetComponent<OccludedAimObserver>(); var targetObserver = target.GetComponent<OccludedAimObserver>();
            Invoke(recorder, "Record", observer, targetObserver, Frame(0), null);
            Check(match.Incidents == 0 && match.FlaggedPlayers == 0, "clean capture bridge creates no flag");
            Invoke(recorder, "Record", observer, targetObserver, Frame(0), (Alert?)Evidence);
            Check(match.Incidents == 1 && match.FlaggedPlayers == 1, "existing alert enters match flag via server bridge");
            Invoke(recorder, "RemoteState", a, new RemoteConnectionStateArgs(RemoteConnectionState.Stopped, a.ClientId, 0));
            Check(match.ActiveContexts == 0, "disconnect callback closes incident and context");
            manager.ServerManager.Despawn(shooter); shooter = null;
            rejoined = Spawn(Owner(7010), 16000);
            Invoke(recorder, "Record", rejoined.GetComponent<OccludedAimObserver>(), targetObserver, Frame(0), (Alert?)Evidence);
            Check(match.Incidents == 2 && match.FlaggedPlayers == 2, "reused connection number gets new participant in same match");
            Invoke(recorder, "FinishMatch", "round ended");
            Check(Read(recorder, "_match") == null && ((System.Collections.IDictionary)Read(recorder, "_participants")).Count == 0,
                "round boundary releases match/participant state");
            Invoke(recorder, "BeginMatch", rejoined.scene);
            var next = (AimIncidentCapture)Read(recorder, "_match");
            Check(next.MatchId != bridgeMatch && next.FlaggedPlayers == 0, "new round has independent match identity/flags");
            Invoke(recorder, "FinishScene", rejoined.scene); Check(Read(recorder, "_match") == null, "scene-end finalizes match");
        }
        finally
        {
            if (shooter != null) manager.ServerManager.Despawn(shooter);
            if (target != null) manager.ServerManager.Despawn(target);
            if (rejoined != null) manager.ServerManager.Despawn(rejoined);
        }
    }
    private static void Benchmark()
    {
        Incident largest = null; int snapshots = 0;
        var actors = Enumerable.Range(0, 16).Select(i => new Actor { participantId = Guid.NewGuid().ToString("N"), connectionId = i }).ToArray();
        long before = GC.GetTotalMemory(true);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var capture = new AimIncidentCapture("scripted-fixture", "cost fixture", data => { largest = data; snapshots++; });
        for (int tick = 0; tick <= 150; tick++)
        for (int i = 0; i < 16; i++) capture.Observe(actors[i], actors[(i + 1) % 16], Frame(tick * .2), tick == 50 || tick == 100 ? (Alert?)Evidence : null);
        watch.Stop(); long retained = GC.GetTotalMemory(true) - before;
        capture.End(30, "benchmark ended");
        Check(capture.ActiveContexts == 0 && capture.Incidents == 16 && largest.frames.Length <= MaximumFrames, "supported-size capture remains bounded");
        double captureMs = watch.Elapsed.TotalMilliseconds; watch.Restart(); string json = JsonUtility.ToJson(largest); watch.Stop();
        Debug.Log($"[Incident PERF] fixture actors=16 cadence=5Hz virtualSeconds=30 observations=2416 snapshots={snapshots} " +
            $"captureMs={captureMs:F3} approximateManagedHeapDelta={retained} framesPerLargestClip={largest.frames.Length} " +
            $"serializeOneMs={watch.Elapsed.TotalMilliseconds:F3} bytes={System.Text.Encoding.UTF8.GetByteCount(json)}; no live transport, physics or disk in capture timing.");
    }
    private static void BenchmarkBridge()
    {
        var recorder = manager.GetComponent<AimIncidentRecorder>(); var players = new List<GameObject>();
        try
        {
            for (int i = 0; i < 16; i++) players.Add(Spawn(Owner(7100 + i), 17000 + i * 4));
            Invoke(recorder, "BeginMatch", players[0].scene);
            var record = typeof(AimIncidentRecorder).GetMethod("Record", Hidden);
            var start = typeof(AimIncidentRecorder).GetField("_start", Hidden);
            long before = GC.GetTotalMemory(true); var watch = System.Diagnostics.Stopwatch.StartNew();
            for (int tick = 0; tick <= 150; tick++)
            {
                start.SetValue(recorder, Time.realtimeSinceStartupAsDouble - tick * .2);
                for (int i = 0; i < 16; i++) record.Invoke(recorder, new object[] { players[i].GetComponent<OccludedAimObserver>(),
                    players[(i + 1) % 16].GetComponent<OccludedAimObserver>(), Frame(0), null });
            }
            watch.Stop(); long bytes = GC.GetTotalMemory(true) - before;
            var match = (AimIncidentCapture)Read(recorder, "_match");
            Check(match.ActiveContexts == 16 && match.Incidents == 0, "actual16-prefab clean bridge produces no flags");
            Debug.Log($"[Incident PERF] server-prefab fixture actors=16 observations=2416 virtualSeconds=30 bridgeMs={watch.Elapsed.TotalMilliseconds:F3} " +
                $"approximateManagedHeapDelta={bytes}; includes reflection/GetComponent/approved-posture sampling, excludes spawn, live transport, physics queries, GC measurement and disk.");
            Invoke(recorder, "FinishMatch", "benchmark ended");
        }
        finally { foreach (var player in players) if (player != null) manager.ServerManager.Despawn(player); }
    }
    internal static bool Tick()
    {
        if (AimIncidentArchive.Busy) return false;
        if (phase == 0)
        {
            var data = AimIncidentArchive.Files().Select(AimIncidentArchive.Load).ToArray();
            fixture = data.Single(d => d.matchId == fixtureMatch);
            Check(fixture.source == "scripted-fixture" && fixture.final && fixture.partial && fixture.frames.Length >= 50, "actual alert saves source/lead/follow-up partial");
            Check(fixture.shooter.participantId != fixture.target.participantId && fixture.shooter.connectionId == -1,
                "synthetic actors never flag context host identity");
            var reused = data.Where(d => d.matchId == bridgeMatch).ToArray();
            Check(reused.Length == 2 && reused.All(d => d.shooter.connectionId == 7010) && reused[0].shooter.participantId != reused[1].shooter.participantId,
                "save/load preserves same-match reused-ID separation");
            Check(reused.All(d => d.final && d.partial) && reused.Any(d => d.endReason == "connection left") && reused.Any(d => d.endReason == "round ended"),
                "disconnect and round partial reasons survive disk");
            Check(new FileInfo(Path.Combine(folder, AimIncidentArchive.Leaf(fixture))).Length <= AimIncidentArchive.MaximumBytes, "saved file byte bound");
            manager.ServerManager.StopConnection(true); phase = 1; return false;
        }
        if (phase == 1)
        {
            if (manager.IsServerStarted) return false;
            LocalReplayMenu.Show();
            var theater = UnityEngine.Object.FindFirstObjectByType<LocalReplayMenu>();
            Check(theater != null, "ordinary Theater still opens");
            var entry = theater.GetComponentsInChildren<Button>().Single(b => b.GetComponentInChildren<TMP_Text>()?.text == "Server aim incidents");
            entry.onClick.Invoke(); menu = UnityEngine.Object.FindFirstObjectByType<AimIncidentReviewMenu>();
            Check(menu != null, "Theater entry opens incident review");
            bool selected = (bool)typeof(AimIncidentReviewMenu).GetMethod("Select", Hidden).Invoke(menu, new object[] { fixture.matchId, fixture.shooter.participantId, fixture.incidentId });
            Check(selected, "match/player/incident selection finds correct persisted identity");
            Invoke(menu, "Scrub", fixture.alertTime);
            int index = (int)typeof(AimIncidentReviewMenu).GetProperty("SelectedFrame", Hidden).GetValue(menu);
            Check(index >= 0 && fixture.frames[index].aimKnown, "playback selects recorded reported-aim frame");
            ((Button)menu.GetComponentsInChildren<Button>().Single(b => b.GetComponentInChildren<TMP_Text>()?.text == "Play / Pause")).onClick.Invoke();
            phase = 2; return false;
        }
        if (phase == 2)
        {
            if ((double)Read(menu, "_time") <= fixture.alertTime) return false;
            Check((double)Read(menu, "_time") > fixture.alertTime, "existing-style playback advances evidence timeline");
            var note = menu.GetComponentInChildren<TMP_InputField>(); note.text = "Fixture reviewed; not human-play evidence.";
            menu.GetComponentsInChildren<Button>().Single(b => b.GetComponentInChildren<TMP_Text>()?.text == "Reviewed + Save note").onClick.Invoke();
            var review = AimIncidentArchive.LoadReview(fixture);
            Check(review.reviewed && review.revision == fixture.revision && review.note == note.text, "Reviewed and short local note survive disk");
            var bytes = File.ReadAllBytes(Path.Combine(folder, AimIncidentArchive.Leaf(fixture)));
            Check(AimIncidentArchive.Load(AimIncidentArchive.Leaf(fixture)).revision == fixture.revision && bytes.Length > 0, "review does not mutate evidence");
            Check(!manager.IsServerStarted, "review cannot start network or change enforcement");
            gapFixture = JsonUtility.FromJson<Incident>(JsonUtility.ToJson(fixture)); gapFixture.incidentId = Guid.NewGuid().ToString("N");
            gapFixture.frames = new[] { fixture.frames[0], fixture.frames[10], fixture.frames[40], fixture.frames[60] };
            for (int i = 1; i < gapFixture.frames.Length; i++) gapFixture.frames[i].gapBefore = true;
            AimIncidentArchive.Enqueue(gapFixture); phase = 6; return false;
        }
        if (phase == 6)
        {
            Invoke(menu, "Refresh");
            Check((bool)typeof(AimIncidentReviewMenu).GetMethod("Select", Hidden).Invoke(menu, new object[] { gapFixture.matchId, gapFixture.shooter.participantId, gapFixture.incidentId }),
                "gap fixture selectable through same catalog");
            Invoke(menu, "Scrub", 4d);
            Check((int)typeof(AimIncidentReviewMenu).GetProperty("SelectedFrame", Hidden).GetValue(menu) == -1 &&
                ((TMP_Text)Read(menu, "_details")).text.Contains("GAP"), "review hides actor/ray at missing interval and labels gap");
            UnityEngine.Object.Destroy(menu.gameObject); var theater = UnityEngine.Object.FindFirstObjectByType<LocalReplayMenu>();
            if (theater != null) UnityEngine.Object.Destroy(theater.gameObject);
            CapstoneLoadChecks.Begin(); phase = 7; return false;
        }
        if (phase == 7)
        {
            if (!CapstoneLoadChecks.Tick()) return false;
            FolderField.SetValue(null, Path.Combine(folder, "retention")); phase = 3; return false;
        }
        if (phase == 3)
        {
            if (retentionIndex < 35)
            {
                var data = JsonUtility.FromJson<Incident>(JsonUtility.ToJson(fixture));
                data.incidentId = Guid.NewGuid().ToString("N"); AimIncidentArchive.Enqueue(data); retentionIndex++; return false;
            }
            Check(AimIncidentArchive.Files().Length == AimIncidentArchive.RetainedFiles, "disk retention count bounded after35 incidents");
            string old = AimIncidentArchive.Files()[0]; var evidence = AimIncidentArchive.Load(old);
            AimIncidentArchive.SaveReview(evidence, true, "expired review"); File.SetLastWriteTimeUtc(Path.Combine(AimIncidentArchive.Folder, old), DateTime.UtcNow.AddDays(-8));
            var next = JsonUtility.FromJson<Incident>(JsonUtility.ToJson(fixture)); next.incidentId = Guid.NewGuid().ToString("N"); AimIncidentArchive.Enqueue(next);
            phase = 4; return false;
        }
        if (phase == 4)
        {
            Check(Directory.GetFiles(AimIncidentArchive.Folder, "*.review.json").Length == 0, "expired evidence deletes attached review note");
            bool rejected = false; try { AimIncidentArchive.Load("../anything.json"); } catch (InvalidDataException) { rejected = true; }
            Check(rejected, "review cannot select arbitrary local path");
            string oversized = Path.Combine(AimIncidentArchive.Folder, Guid.NewGuid().ToString("N") + "_" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllBytes(oversized, new byte[AimIncidentArchive.MaximumBytes + 1]); rejected = false;
            try { AimIncidentArchive.Load(Path.GetFileName(oversized)); } catch (InvalidDataException) { rejected = true; }
            finally { File.Delete(oversized); }
            Check(rejected, "oversized file rejected before JSON playback");
            var recorder = manager.GetComponent<AimIncidentRecorder>(); recorder.enabled = false;
            string blocked = Path.Combine(folder, "blocked-folder"); File.WriteAllText(blocked, "not a directory"); FolderField.SetValue(null, blocked);
            AimIncidentArchive.Enqueue(fixture); phase = 5; return false;
        }
        Check(AimIncidentArchive.TakeFailure() != null, "worker disk failure contained without gameplay exception");
        manager.GetComponent<AimIncidentRecorder>().enabled = true;
        Restore(); Debug.Log($"[Incident CHECK] PASS assertions={assertions}; complete scripted/server-local save-select-play-review path, not live multiplayer evidence."); return true;
    }
    internal static void Restore()
    {
        if (menu != null) UnityEngine.Object.Destroy(menu.gameObject);
        FolderField.SetValue(null, originalFolder);
    }
}
