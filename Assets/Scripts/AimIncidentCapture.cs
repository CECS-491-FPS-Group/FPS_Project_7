using System;
using System.Collections.Generic;
using static AimIncidentData;

// One match, <=16 active shooter contexts. Capture and review never call enforcement.
public sealed class AimIncidentCapture
{
    private sealed class Encounter
    {
        public Actor shooter, target;
        public string id;
        public readonly Queue<Frame> lead = new Queue<Frame>(52);
        public List<Frame> frames;
        public List<Alert> alerts;
        public string incidentId;
        public double closeAt, limit, lastTime = -1, lastAlert = double.NegativeInfinity;
        public bool finished;
        public int revision;
    }
    public readonly string MatchId = Guid.NewGuid().ToString("N");
    public readonly string Source, Scene, StartedUtc, Clock;
    private readonly Dictionary<string, Encounter> _contexts = new Dictionary<string, Encounter>();
    private readonly HashSet<string> _flagged = new HashSet<string>();
    private readonly Dictionary<string, int> _playerIncidents = new Dictionary<string, int>();
    public string BudgetStatus { get; private set; } = "available";
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
        BudgetStatus = "available";
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
            if (context.incidentId == null)
            {
                // Clean candidate churn needs only one reusable lead ring, not160-frame clip allocations.
                context.shooter = shooter; context.target = target; context.lead.Clear();
                context.lastTime = -1; context.lastAlert = double.NegativeInfinity;
            }
            else
            {
                Finish(context, frame.time, "encounter/life changed", true);
                _contexts[shooter.participantId] = context = new Encounter { shooter = shooter, target = target };
            }
        }
        if (context.finished && alert.HasValue && frame.time - context.lastAlert > LeadSeconds + FollowSeconds)
        {
            var next = new Encounter { shooter = shooter, target = target, lastTime = context.lastTime };
            foreach (var sample in context.lead) next.lead.Enqueue(sample);
            _contexts[shooter.participantId] = context = next;
        }
        if (frame.time <= context.lastTime || (frame.time - context.lastTime < .19 && !alert.HasValue)) return;
        if (context.incidentId != null && !context.finished && frame.time > context.closeAt + .001)
            Finish(context, context.closeAt, "follow-up complete", false);
        frame.gapBefore |= context.lastTime >= 0 && frame.time - context.lastTime > GapSeconds;
        context.lastTime = frame.time;
        while (context.lead.Count > 0 && (context.lead.Count >= 51 || frame.time - context.lead.Peek().time > LeadSeconds)) context.lead.Dequeue();
        context.lead.Enqueue(frame);
        if (context.incidentId != null && !context.finished && context.frames.Count < MaximumFrames && frame.time <= context.limit + .001)
            context.frames.Add(frame);
        bool playerRoom = !_playerIncidents.TryGetValue(shooter.participantId, out int playerClips) || playerClips < MaximumPlayerIncidents;
        if (alert.HasValue && context.incidentId == null)
            BudgetStatus = !playerRoom ? "player clip budget reached" : _incidents >= MaximumIncidents ? "match clip budget reached" : "available";
        if (alert.HasValue && !context.finished && (context.incidentId != null || (_incidents < MaximumIncidents && playerRoom)))
        {
            if (context.incidentId == null)
            {
                context.id = Guid.NewGuid().ToString("N"); context.incidentId = Guid.NewGuid().ToString("N");
                context.frames = new List<Frame>(MaximumFrames); context.alerts = new List<Alert>(MaximumAlerts);
                context.frames.AddRange(context.lead);
                context.limit = context.frames[0].time + MaximumSeconds;
                _flagged.Add(shooter.participantId); _incidents++;
                _playerIncidents[shooter.participantId] = playerClips + 1;
            }
            var evidence = alert.Value; evidence.time = frame.time;
            if (context.alerts.Count < MaximumAlerts) context.alerts.Add(evidence);
            context.closeAt = Math.Min(context.limit, frame.time + FollowSeconds);
            Save(context, false, false, "capturing follow-up");
        }
        if (alert.HasValue) context.lastAlert = frame.time;
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
        // Immutable snapshot boundary: the worker may serialize after the next main-thread sample.
        // Review flags/notes never enter the movement policy or mutate live gameplay state.
        _save?.Invoke(new Incident { matchId = MatchId, incidentId = context.incidentId, encounterId = context.id,
            revision = ++context.revision, scene = Scene, source = Source, startedUtc = StartedUtc, clock = Clock,
            shooter = context.shooter, target = context.target, alertTime = context.alerts[0].time,
            endTime = final ? context.closeAt : context.frames[context.frames.Count - 1].time,
            final = final, partial = partial, endReason = reason, frames = context.frames.ToArray(), alerts = context.alerts.ToArray() });
    }
}
