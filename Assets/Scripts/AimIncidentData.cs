using System;
using System.IO;

// Plain bounded data. No networking, Unity objects, policy or client file access.
public static class AimIncidentData
{
    public const int MaximumFrames = 160, MaximumAlerts = 4, MaximumIncidents = 32;
    public const int MaximumPlayerIncidents = 2; // 16 players each retain a fair share of the32-clip match budget.
    public const double LeadSeconds = 10, FollowSeconds = 12, MaximumSeconds = 30, GapSeconds = .65;
    [Serializable] public struct Point
    {
        public float x, y, z;
        public Point(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public bool Valid => Finite(x) && Finite(y) && Finite(z) && Math.Abs(x) <= 1000000 && Math.Abs(y) <= 1000000 && Math.Abs(z) <= 1000000;
    }
    [Serializable] public struct Actor
    {
        public string participantId;
        public int connectionId, objectId;
        public uint life;
    }
    [Serializable] public struct Frame
    {
        public double time, aimAge, targetAge;
        public Point shooter, target, eye, aim;
        public bool shooterKnown, targetKnown, aimKnown, fresh, cue, gapBefore;
        public string visibility, decision, cover, shooterEffect, targetEffect;
        public bool shooterCrouched, targetCrouched;
    }
    [Serializable] public struct Alert
    {
        public double time, duration, matched, aimTravel, targetTravel, error;
        public int samples, queries;
    }
    [Serializable] public sealed class Incident
    {
        public int version = 1, revision;
        public string matchId, incidentId, encounterId, source, rule = "correlated-occluded-aim";
        public string scene, startedUtc, clock, endReason;
        public Actor shooter, target;
        public double alertTime, endTime;
        public bool final, partial;
        public Frame[] frames;
        public Alert[] alerts;
    }
    [Serializable] public sealed class Review
    {
        public string incidentId, note = "", reviewedUtc;
        public bool reviewed;
        public int revision;
    }
    public static bool Id(string value) => Guid.TryParseExact(value, "N", out _);
    public static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    private static bool Text(string value, int maximum) => value != null && value.Length <= maximum;
    public static void Validate(Incident data)
    {
        if (data == null || data.version != 1 || !Id(data.matchId) || !Id(data.incidentId) || !Id(data.encounterId) ||
            !Id(data.shooter.participantId) || !Id(data.target.participantId) || data.shooter.participantId == data.target.participantId ||
            (data.source != "client-aim" && data.source != "scripted-fixture") || data.rule != "correlated-occluded-aim" ||
            !Text(data.scene, 128) || !Text(data.startedUtc, 64) || !Text(data.clock, 64) || !Text(data.endReason, 64) ||
            data.revision < 1 || data.frames == null || data.frames.Length < 1 || data.frames.Length > MaximumFrames ||
            data.alerts == null || data.alerts.Length < 1 || data.alerts.Length > MaximumAlerts ||
            !Finite(data.alertTime) || !Finite(data.endTime) || data.alertTime < 0 || data.endTime < data.alertTime ||
            data.endTime - data.frames[0].time > MaximumSeconds + .001)
            throw new InvalidDataException("Invalid incident header or bounds.");
        double previous = -1, start = data.frames[0].time;
        foreach (var frame in data.frames)
        {
            if (!Finite(frame.time) || frame.time < 0 || frame.time <= previous || frame.time - start > MaximumSeconds + .001 ||
                frame.time > data.endTime + .001 || !frame.shooter.Valid || !frame.target.Valid || !frame.eye.Valid || !frame.aim.Valid ||
                !Finite(frame.aimAge) || !Finite(frame.targetAge) || frame.aimAge < 0 || frame.targetAge < 0 ||
                !Text(frame.visibility, 24) || !Text(frame.decision, 64) || !Text(frame.cover, 64) ||
                !Text(frame.shooterEffect, 24) || !Text(frame.targetEffect, 24)) throw new InvalidDataException("Invalid incident frame.");
            previous = frame.time;
        }
        previous = -1;
        foreach (var alert in data.alerts)
        {
            if (!Finite(alert.time) || alert.time < data.alertTime || alert.time < previous || alert.time > data.endTime + .001 ||
                !Finite(alert.duration) || alert.duration < 0 || !Finite(alert.matched) || alert.matched < 0 ||
                !Finite(alert.aimTravel) || alert.aimTravel < 0 || !Finite(alert.targetTravel) || alert.targetTravel < 0 ||
                !Finite(alert.error) || alert.error < 0 || alert.samples < 0 || alert.queries < 0)
                throw new InvalidDataException("Invalid incident alert.");
            previous = alert.time;
        }
    }
    // Step playback: never interpolate missing observations or claim a camera view.
    public static int FrameAt(Incident data, double time)
    {
        int low = 0, high = data.frames.Length - 1;
        if (time < data.frames[0].time || time > data.endTime) return -1;
        while (low < high) { int mid = (low + high + 1) / 2; if (data.frames[mid].time <= time) low = mid; else high = mid - 1; }
        if (time - data.frames[low].time > .3 &&
            (low == data.frames.Length - 1 || data.frames[low + 1].gapBefore || data.frames[low + 1].time - data.frames[low].time > GapSeconds)) return -1;
        return low;
    }
}
