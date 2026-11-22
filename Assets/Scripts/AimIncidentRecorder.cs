using System;
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;
using UnityEngine.SceneManagement;
using static AimIncidentData;

// Persistent server-side capture bridge. No network messages and no policy references.
[DisallowMultipleComponent]
public sealed class AimIncidentRecorder : MonoBehaviour
{
    private NetworkManager _manager;
    private AimIncidentCapture _match;
    private Scene _scene;
    private double _start, _nextWarning;
    private readonly Dictionary<NetworkConnection, string> _participants = new(ConnectionReferenceComparer.Instance);
    private readonly Dictionary<OccludedAimObserver, OccludedAimObserver> _targets = new();
    private double Now => Time.realtimeSinceStartupAsDouble;
    private double MatchTime => Math.Max(0, Now - _start);
    private void Awake()
    {
        _manager = GetComponent<NetworkManager>();
        _manager.ServerManager.OnRemoteConnectionState += RemoteState;
        _manager.ServerManager.OnServerConnectionState += ServerState;
        SceneManager.sceneUnloaded += SceneUnloaded;
    }
    internal void BeginMatch(Scene scene)
    {
        if (!_manager.IsServerStarted) return;
        try
        {
            FinishMatch("new match"); _start = Now; _scene = scene;
            _match = new AimIncidentCapture("client-aim", scene.name, QueueSave);
            Debug.Log($"[Aim INCIDENT] match={_match.MatchId} started; server-only report flags, no policy action.");
        }
        catch (Exception error) { _match = null; Warn(error.GetType().Name); }
    }
    internal void FinishMatch(string reason)
    {
        try { _match?.End(MatchTime, reason); }
        catch (Exception error) { Warn(error.GetType().Name); }
        _match = null; _participants.Clear(); _targets.Clear();
    }
    internal void FinishScene(Scene scene) { if (_match != null && _scene == scene) FinishMatch("scene ended"); }
    private string Participant(OccludedAimObserver actor)
    {
        var owner = actor.ReviewConnection;
        if (owner == null || !owner.IsActive || !owner.IsAuthenticated) return null;
        if (!_participants.TryGetValue(owner, out string id) && _participants.Count < 16)
        { id = Guid.NewGuid().ToString("N"); _participants.Add(owner, id); }
        return id;
    }
    internal void Record(OccludedAimObserver shooter, OccludedAimObserver target, Frame frame, Alert? alert)
    {
        if (_match == null || !_manager.IsServerStarted || shooter.gameObject.scene != _scene) return;
        try
        {
            if (!shooter.ReviewPresent) { ActorStopped(shooter); return; }
            if (target != null && target.ReviewPresent)
            {
                if (!_targets.ContainsKey(shooter) && _targets.Count >= 16) return;
                _targets[shooter] = target;
            }
            else _targets.TryGetValue(shooter, out target);
            if (target == null || !target.ReviewPresent || target.gameObject.scene != _scene) return;
            string a = Participant(shooter), b = Participant(target); if (a == null || b == null) return;
            // Follow-up can continue after aim leaves the candidate cone, without more physics.
            shooter.FillReviewPoses(target, ref frame);
            frame.time = MatchTime;
            _match.Observe(shooter.ReviewActor(a), target.ReviewActor(b), frame, alert);
        }
        catch (Exception error) { Warn("capture unavailable: " + error.GetType().Name); }
    }
    internal void ActorStopped(OccludedAimObserver actor)
    {
        try
        {
            if (_match != null && actor.ReviewConnection != null && _participants.TryGetValue(actor.ReviewConnection, out string id))
                _match.Leave(id, MatchTime, "actor stopped");
            RemoveTargets(actor.ReviewConnection, actor);
        }
        catch (Exception error) { Warn(error.GetType().Name); }
    }
    private void RemoveTargets(NetworkConnection connection, OccludedAimObserver actor = null)
    {
        var remove = new List<OccludedAimObserver>();
        foreach (var item in _targets)
            if (item.Key == null || item.Value == null || item.Key == actor || item.Value == actor ||
                ReferenceEquals(item.Key.ReviewConnection, connection) || ReferenceEquals(item.Value.ReviewConnection, connection)) remove.Add(item.Key);
        foreach (var key in remove) _targets.Remove(key);
    }
    private void RemoteState(NetworkConnection connection, RemoteConnectionStateArgs state)
    {
        if (state.ConnectionState != RemoteConnectionState.Stopped) return;
        try { if (_participants.TryGetValue(connection, out string id)) _match?.Leave(id, MatchTime, "connection left"); }
        catch (Exception error) { Warn(error.GetType().Name); }
        _participants.Remove(connection); RemoveTargets(connection);
    }
    private void ServerState(ServerConnectionStateArgs state) { if (state.ConnectionState == LocalConnectionState.Stopped) FinishMatch("server stopped"); }
    private void SceneUnloaded(Scene scene) => FinishScene(scene);
    private void Update()
    {
        try { _match?.Advance(MatchTime); } catch (Exception error) { Warn(error.GetType().Name); }
        string failure = AimIncidentArchive.TakeFailure(); if (failure != null) Warn(failure);
        string saved = AimIncidentArchive.TakeSaved(); if (saved != null) Debug.Log("[Aim INCIDENT] saved=" + saved);
    }
    private void Warn(string reason)
    {
        if (Now < _nextWarning) return; _nextWarning = Now + 5;
        Debug.LogWarning("[Aim INCIDENT] " + reason + "; gameplay/policy unaffected.");
    }
    internal static void QueueSave(Incident data)
    {
        try
        {
            if (AimIncidentArchive.Enqueue(data)) Debug.Log($"[Aim INCIDENT] queued match={data.matchId} incident={data.incidentId} " +
                $"shooter={data.shooter.participantId} target={data.target.participantId} source={data.source} revision={data.revision} final={data.final}; ReportOnly.");
        }
        catch (Exception error) { AimIncidentArchive.ReportFailure("snapshot unavailable: " + error.GetType().Name); }
    }
    private void OnDestroy()
    {
        FinishMatch("recorder stopped");
        if (_manager != null)
        {
            _manager.ServerManager.OnRemoteConnectionState -= RemoteState;
            _manager.ServerManager.OnServerConnectionState -= ServerState;
        }
        SceneManager.sceneUnloaded -= SceneUnloaded;
    }
}
