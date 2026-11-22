using System;
using System.Collections.Generic;
using System.IO;
using static AimIncidentData;

internal static class AimIncidentChecks
{
    private static int count;
    private static void Check(bool ok, string label) { if (!ok) throw new Exception(label); count++; Console.WriteLine("PASS " + label); }
    private static Actor Actor(int connection = 1) => new Actor { participantId = Guid.NewGuid().ToString("N"), connectionId = connection, objectId = connection, life = 1 };
    private static Frame Frame(double time) => new Frame { time = time, shooterKnown = true, targetKnown = true, aimKnown = true,
        shooter = new Point(1, 2, 3), target = new Point(5, 2, 50), eye = new Point(1, 3, 3), aim = new Point(0, 0, 1),
        visibility = "Covered", decision = "window-building", cover = "wall", shooterEffect = "None", targetEffect = "None", fresh = true };
    private static Alert Evidence => new Alert { duration = 6, matched = 5.6, samples = 31, aimTravel = 20, targetTravel = 20, queries = 15 };
    public static void Main()
    {
        var files = new List<Incident>(); var a = Actor(); var b = Actor(2);
        var capture = new AimIncidentCapture("client-aim", "RoundImplementation", files.Add);
        for (int i = 0; i <= 60; i++) capture.Observe(a, b, Frame(i * .2));
        Check(files.Count == 0 && capture.FlaggedPlayers == 0, "clean observations never flag or save");
        capture.Observe(a, b, Frame(12.2), Evidence);
        var first = files[0]; Validate(first);
        Check(capture.IsFlagged(a.participantId) && !capture.IsFlagged(b.participantId), "only reporting shooter flagged in this match");
        Check(first.frames.Length >= 50 && first.frames[0].time >= 2.19 && first.frames[0].time < 2.6, "ten-second rolling lead-in preserved before alert");
        Check(first.source == "client-aim" && first.matchId == capture.MatchId && first.shooter.participantId == a.participantId, "scoped source and identities stored");
        Check(!first.final && first.alerts[0].samples == 31, "immediate recoverable snapshot contains evidence");
        for (int i = 62; i <= 111; i++) capture.Observe(a, b, Frame(i * .2), i == 111 ? Evidence : (Alert?)null);
        Check(files.Count == 2 && files[1].incidentId == first.incidentId && files[1].alerts.Length == 2, "overlapping encounter alerts group in stable incident");
        Check(first.frames.Length < files[1].frames.Length && first.alerts.Length == 1, "queued snapshots immutable across follow-up");
        capture.End(23, "round ended"); var final = files[files.Count - 1]; Validate(final);
        Check(final.final && final.partial && final.endReason == "round ended" && capture.ActiveContexts == 0, "round end finalizes usable partial and clears context");
        Check(final.matchId == first.matchId && final.incidentId == first.incidentId && final.revision > first.revision, "identity survives revisions and round end");
        int before = files.Count; capture.End(24, "again"); capture.Observe(a, b, Frame(25), Evidence);
        Check(files.Count == before, "repeated end and post-round input cannot append incidents");
        var other = new AimIncidentCapture("client-aim", "RoundImplementation", files.Add);
        var reused = Actor(1); other.Observe(reused, b, Frame(0), Evidence);
        Check(other.MatchId != capture.MatchId && reused.participantId != a.participantId && !other.IsFlagged(a.participantId), "reused connection ID has no prior-match flag");
        other.Leave(reused.participantId, .5, "disconnected"); Validate(files[files.Count - 1]);
        Check(files[files.Count - 1].partial && other.ActiveContexts == 0, "disconnect finalizes short partial");
        var gapFiles = new List<Incident>(); var gap = new AimIncidentCapture("scripted-fixture", "synthetic", gapFiles.Add);
        gap.Observe(a, b, Frame(0)); gap.Observe(a, b, Frame(1), Evidence); gap.Observe(a, b, Frame(1), Evidence);
        gap.Observe(a, b, Frame(3)); gap.End(5, "fixture ended"); var g = gapFiles[gapFiles.Count - 1]; Validate(g);
        Check(g.frames.Length == 3 && g.frames[1].gapBefore && g.frames[2].gapBefore, "duplicates excluded and missing elapsed data marked");
        Check(FrameAt(g, .5) == -1 && FrameAt(g, 2) == -1 && FrameAt(g, 4) == -1, "scrubbing through internal/tail gaps cannot invent actor positions");
        Check(FrameAt(g, 1) == 1 && FrameAt(g, -1) == -1, "observed snapshot selected exactly");
        Check(g.source == "scripted-fixture" && g.clock.StartsWith("scripted"), "fixture source and clock separate from live");
        var bounded = new List<Incident>(); var limit = new AimIncidentCapture("client-aim", "scene", bounded.Add);
        for (int i = 0; i < 500; i++) limit.Observe(a, b, Frame(i * .2), i % 50 == 0 ? Evidence : (Alert?)null);
        limit.End(100, "end"); var last = bounded[bounded.Count - 1]; Validate(last);
        Check(last.frames.Length <= MaximumFrames && last.frames[last.frames.Length - 1].time - last.frames[0].time <= MaximumSeconds + .001,
            "continuous repeated alerts bounded to thirty seconds and160 frames");
        Check(limit.Incidents == 1 && last.alerts.Length <= MaximumAlerts, "continuous encounter cannot create unbounded incidents/alerts");
        var capacity = new AimIncidentCapture("client-aim", "scene", _ => { });
        for (int i = 0; i < 40; i++) capacity.Observe(Actor(i), b, Frame(0), Evidence);
        Check(capacity.ActiveContexts == 16 && capacity.Incidents == 16, "supported lobby active context cap enforced");
        var perMatch = new AimIncidentCapture("client-aim", "scene", _ => { });
        for (int i = 0; i < 50; i++) { var p = Actor(i); perMatch.Observe(p, b, Frame(0), Evidence); perMatch.Leave(p.participantId, .2, "left"); }
        Check(perMatch.Incidents == MaximumIncidents && perMatch.FlaggedPlayers == MaximumIncidents, "connection churn bounded per match");
        int replacements = 0; var life = new AimIncidentCapture("client-aim", "scene", _ => replacements++);
        life.Observe(a, b, Frame(0), Evidence); b.life++; life.Observe(a, b, Frame(.2), Evidence);
        Check(replacements == 3 && life.Incidents == 2, "respawn life separates encounters and closes old partial");
        var quiet = new AimIncidentCapture("client-aim", "scene", files.Add); quiet.Observe(a, b, Frame(0), Evidence); quiet.Advance(13);
        Check(files[files.Count - 1].final && files[files.Count - 1].partial, "missing follow-up finalizes on bounded deadline even without samples");
        bool rejected = false; g.frames[0].time = double.NaN; try { Validate(g); } catch (InvalidDataException) { rejected = true; }
        Check(rejected, "nonfinite persisted evidence rejected");
        Console.WriteLine($"AimIncident: {count} assertions passed; no Unity/network/policy dependency.");
    }
}
