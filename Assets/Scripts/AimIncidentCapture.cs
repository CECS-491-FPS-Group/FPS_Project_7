using System;
using System.Collections.Generic;
using static AimIncidentData;

// One match, <=16 active shooter contexts. Capture and review never call enforcement.
public sealed class AimIncidentCapture
{
    private sealed class Encounter
    {
        public Actor shooter, target;
        public string id = Guid.NewGuid().ToString("N");
        public readonly Queue<Frame> lead = new Queue<Frame>(52);
        public readonly List<Frame> frames = new List<Frame>(MaximumFrames);
        public readonly List<Alert> alerts = new List<Alert>(MaximumAlerts);
        public string incidentId;
        public double closeAt, limit, lastTime = -1;
        public bool finished;
        public int revision;
    }
    public readonly string MatchId = Guid.NewGuid().ToString("N");
    public readonly string Source, Scene, StartedUtc, Clock;
    private readonly Dictionary<string, Encounter> _contexts = new Dictionary<string, Encounter>();
    private readonly HashSet<string> _flagged = new HashSet<string>();
    private readonly Action<Incident> _save;
    private int _incidents;
    private bool _ended;
    public int ActiveContexts => _contexts.Count;
    public int FlaggedPlayers => _flagged.Count;
    public int Incidents => _incidents;
    public AimIncidentCapture(string source, string scene, Action<Incident> save)
    {
        if (source != "client-aim" && source != "scripted-fixture") throw new ArgumentException("Invalid source");
        Source = source; Scene = scene; _save = save;
        StartedUtc = DateTime.UtcNow.ToString("O"); Clock = source == "client-aim" ? "server monotonic seconds from GAME start" : "scripted seconds from fixture start";
    }
    public bool IsFlagged(string participant) => _flagged.Contains(participant);
    public void Observe(Actor shooter, Actor target, Frame frame, Alert? alert = null)
    {
        if (_ended || !Id(shooter.participantId) || !Id(target.participantId) || shooter.participantId == target.participantId ||
            !Finite(frame.time) || frame.time < 0 || !frame.shooter.Valid || !frame.target.Valid || !frame.eye.Valid || !frame.aim.Valid) return;
        if (!_contexts.TryGetValue(shooter.participantId, out var context))
        {
            if (_contexts.Count >= 16) return;
            _contexts.Add(shooter.participantId, context = new Encounter { shooter = shooter, target = target });
        }
        if (context.target.participantId != target.participantId || context.target.life != target.life ||
            context.shooter.life != shooter.life || context.target.objectId != target.objectId || context.shooter.objectId != shooter.objectId)
        {
            Finish(context, frame.time, "encounter/life changed", true);
            _contexts[shooter.participantId] = context = new Encounter { shooter = shooter, target = target };
        }
        if (frame.time <= context.lastTime || frame.time - context.lastTime < .19) return;
        if (context.incidentId != null && !context.finished && frame.time > context.closeAt + .001)
            Finish(context, context.closeAt, "follow-up complete", false);
        frame.gapBefore |= context.lastTime >= 0 && frame.time - context.lastTime > GapSeconds;
        context.lastTime = frame.time;
        while (context.lead.Count > 0 && (context.lead.Count >= 51 || frame.time - context.lead.Peek().time > LeadSeconds)) context.lead.Dequeue();
        context.lead.Enqueue(frame);
        if (context.incidentId != null && !context.finished && context.frames.Count < MaximumFrames && frame.time <= context.limit + .001)
            context.frames.Add(frame);
        if (alert.HasValue && !context.finished && (context.incidentId != null || _incidents < MaximumIncidents))
        {
            if (context.incidentId == null)
            {
                context.incidentId = Guid.NewGuid().ToString("N"); context.frames.AddRange(context.lead);
                context.limit = context.frames[0].time + MaximumSeconds;
                _flagged.Add(shooter.participantId); _incidents++;
            }
            var evidence = alert.Value; evidence.time = frame.time;
            if (context.alerts.Count < MaximumAlerts) context.alerts.Add(evidence);
            context.closeAt = Math.Min(context.limit, frame.time + FollowSeconds);
            Save(context, false, false, "capturing follow-up");
        }
        if (context.incidentId != null && !context.finished && (frame.time >= context.closeAt || context.frames.Count >= MaximumFrames))
            Finish(context, Math.Min(frame.time, context.closeAt), frame.time >= context.limit ? "capture duration cap" : "follow-up complete", frame.time >= context.limit);
    }
    public void Advance(double time)
    {
        if (!Finite(time)) return;
        foreach (var context in _contexts.Values)
            if (context.incidentId != null && !context.finished && time >= context.closeAt)
                Finish(context, context.closeAt, context.closeAt >= context.limit ? "capture duration cap" : "follow-up complete", context.closeAt >= context.limit);
    }
    public void Leave(string participant, double time, string reason)
    {
        var remove = new List<string>();
        foreach (var entry in _contexts)
            if (entry.Value.shooter.participantId == participant || entry.Value.target.participantId == participant)
            { Finish(entry.Value, time, reason, true); remove.Add(entry.Key); }
        foreach (var key in remove) _contexts.Remove(key);
    }
    public void End(double time, string reason)
    {
        if (_ended) return;
        foreach (var context in _contexts.Values) Finish(context, time, reason, true);
        _contexts.Clear(); _ended = true;
    }
    private void Finish(Encounter context, double time, string reason, bool partial)
    {
        if (context.incidentId == null || context.finished) return;
        context.finished = true;
        double last = context.frames[context.frames.Count - 1].time;
        context.closeAt = Math.Min(context.limit, Math.Max(last, time));
        Save(context, true, partial || context.closeAt - last > GapSeconds, reason);
    }
    private void Save(Encounter context, bool final, bool partial, string reason)
    {
        _save?.Invoke(new Incident { matchId = MatchId, incidentId = context.incidentId, encounterId = context.id,
            revision = ++context.revision, scene = Scene, source = Source, startedUtc = StartedUtc, clock = Clock,
            shooter = context.shooter, target = context.target, alertTime = context.alerts[0].time,
            endTime = final ? context.closeAt : context.frames[context.frames.Count - 1].time,
            final = final, partial = partial, endReason = reason, frames = context.frames.ToArray(), alerts = context.alerts.ToArray() });
    }
}
