using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine.InputSystem;
#endif

// Server-owned policy, defaulting to reporting only. No authentication or gameplay gates.
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkManager))]
public sealed class FileAntiCheat : MonoBehaviour
{
    public enum EnforcementMode { ReportOnly, Disconnect }
    [Header("Server policy (leave ReportOnly until runtime validation passes)")]
    [SerializeField] private EnforcementMode staticMode = EnforcementMode.ReportOnly;
    [SerializeField] private EnforcementMode dynamicMode = EnforcementMode.ReportOnly;
    [SerializeField, Range(5f, 10f)] private float dynamicSuspicionThreshold = 8f;
    public struct Report : IBroadcast
    {
        public uint sequence;
        public byte schema, platform, backend, mode; // mode: 0=measured, 1=Editor skip, 2=local configuration error
        public ulong buildA, buildB;
        public BuildIntegrity.Digest first, second, third;
    }
    private sealed class Session
    {
        public IntegrityReportLifecycle reports;
        public bool invalidLogged, enforcementHandled, hostExemptionLogged;
    }
    private NetworkManager _manager;
    private readonly Dictionary<NetworkConnection, Session> _sessions = new(ConnectionReferenceComparer.Instance);
    private BuildIntegrity.Manifest _approved;
    private CancellationTokenSource _cancel;
    private Task<Report> _work;
    private int _generation, _workGeneration;
    private uint _workSequence;
    private double _workDeadline, _clientStartupDeadline;
    private bool _workTimedOut;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private uint _sequence;
    private string _developmentStartup;
    private double _developmentSendAfter;
    private Report? _developmentLastReport;
    private bool _developmentNetworkConfigured;
#endif
    private double _clientNext;
    private bool _initialPending;
    private const double Deadline = 60, RecheckInterval = 15;
    private static double Now => Time.realtimeSinceStartupAsDouble;

    private void Awake()
    {
        _manager = GetComponent<NetworkManager>();
        _manager.ServerManager.RegisterBroadcast<Report>(Receive);
        _manager.ServerManager.OnAuthenticationResult += Authenticated;
        _manager.ServerManager.OnRemoteConnectionState += RemoteState;
        _manager.ServerManager.OnServerConnectionState += ServerState;
        _manager.ClientManager.OnAuthenticated += ClientAuthenticated;
        _manager.ClientManager.OnClientConnectionState += ClientState;
    }

    private void ServerState(ServerConnectionStateArgs args)
    {
        if (args.ConnectionState == LocalConnectionState.Stopped) { _sessions.Clear(); _approved = null; }
        if (args.ConnectionState != LocalConnectionState.Started) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        DevelopmentConfigureNetwork();
#endif
        _sessions.Clear();
        _approved = null;
        staticMode = ReadMode("FPS_STATIC_AC_MODE", staticMode);
        dynamicMode = ReadMode("FPS_DYNAMIC_AC_MODE", dynamicMode);
        string threshold = Environment.GetEnvironmentVariable("FPS_DYNAMIC_AC_THRESHOLD");
        if (!string.IsNullOrWhiteSpace(threshold))
        {
            if (float.TryParse(threshold, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float parsed))
                dynamicSuspicionThreshold = parsed;
            else { dynamicSuspicionThreshold = 8f; Debug.LogWarning("[AC Policy] Invalid dynamic threshold; using 8."); }
        }
        Debug.Log($"[AC Policy] server static={staticMode} dynamic={dynamicMode} dynamicThreshold={HorizontalMovementObservation.EnforcementThreshold(dynamicSuspicionThreshold):F2}");
        try
        {
            string path = Environment.GetEnvironmentVariable("FPS_INTEGRITY_MANIFEST");
            if (string.IsNullOrWhiteSpace(path))
                path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "IntegrityServer", "approved.json");
            var manifest = JsonUtility.FromJson<BuildIntegrity.Manifest>(BuildIntegrity.ReadManifest(path));
            BuildIntegrity.Validate(manifest);
            _approved = manifest;
            Debug.Log($"[File AC] approved build={manifest.buildId} platform={manifest.platform} backend={manifest.backend} path={path}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[File AC] ServerConfigurationError ({e.GetType().Name}); no trusted baseline loaded. Not a cheating verdict.");
        }
    }

    private void Authenticated(NetworkConnection connection, bool authenticated)
    {
        if (authenticated && !_sessions.ContainsKey(connection))
            _sessions.Add(connection, new Session { reports = new IntegrityReportLifecycle(Now, Deadline, RecheckInterval) });
    }
    private void RemoteState(NetworkConnection connection, RemoteConnectionStateArgs args)
    {
        if (args.ConnectionState == RemoteConnectionState.Stopped) _sessions.Remove(connection);
    }
    private void ClientAuthenticated()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        DevelopmentConfigureNetwork();
#endif
        _generation++;
        _cancel?.Cancel();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        _sequence = 0;
        _developmentLastReport = null;
        _developmentStartup = Application.isEditor ? null : Environment.GetEnvironmentVariable("FPS_INTEGRITY_TEST_STARTUP");
        _developmentSendAfter = _developmentStartup == "Delay" ? Now + 65 : 0;
        if (!string.IsNullOrEmpty(_developmentStartup)) Debug.Log($"[File AC TEST] startup fixture={_developmentStartup}; development only, no admission exemption.");
#endif
        _clientStartupDeadline = Now + Deadline;
        _initialPending = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (_developmentStartup == "Suppress") _initialPending = false;
#endif
    }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void DevelopmentConfigureNetwork()
    {
        if (_developmentNetworkConfigured) return;
        _developmentNetworkConfigured = true;
        string profile = Environment.GetEnvironmentVariable("FPS_MOVEMENT_TEST_NETWORK");
        if (string.IsNullOrEmpty(profile) || profile == "Off") return;
        if (profile != "100ms" && profile != "150ms-loss2")
        { Debug.LogWarning("[Network TEST] Unknown profile; no simulation setting changed."); return; }
        var simulator = _manager.TransportManager.LatencySimulator;
        simulator.SetLatency(profile == "100ms" ? 100 : 150);
        simulator.SetPacketLoss(profile == "100ms" ? 0 : .02);
        simulator.SetOutOfOrder(0);
        simulator.SetEnabled(true);
        Debug.Log($"[Network TEST] profile={profile} outgoing-only instance simulation. Apply to both endpoints for both directions; no jitter; reliable loss adds 30% delay, not packet drop.");
    }
#endif
    private void ClientState(ClientConnectionStateArgs args)
    {
        if (args.ConnectionState != LocalConnectionState.Stopped) return;
        _generation++;
        _cancel?.Cancel();
        _initialPending = false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        _developmentLastReport = null;
#endif
    }

    private void Update()
    {
        foreach (var pair in _sessions)
        {
            Session s = pair.Value;
            if (s.reports.Expire(Now, _approved != null)) Log(pair.Key, "startup", s.reports.Result);
        }
        // One worker at most, including retired generations. Cancellation is best effort
        // for OS I/O; never start an unbounded series of replacement workers.
        if (_work != null && !_work.IsCompleted && !_workTimedOut && Now >= _workDeadline)
        {
            _workTimedOut = true; _cancel?.Cancel();
            if (_workGeneration == _generation) SendUnavailable(_workSequence, "local hash worker timed out");
        }
        if (_initialPending && _work != null && Now >= _clientStartupDeadline)
        {
            _initialPending = false;
            SendUnavailable(0, "previous hash worker still retiring");
        }
        bool deliveryReady = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        deliveryReady = _workGeneration != _generation || _workSequence != 0 || Now >= _developmentSendAfter;
#endif
        if (_work != null && _work.IsCompleted && deliveryReady)
        {
            if (!_workTimedOut && !_work.IsCanceled && !_work.IsFaulted && _workGeneration == _generation &&
                _manager.IsClientStarted && _manager.ClientManager.Connection.IsAuthenticated)
            {
                Report report = _work.Result;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (report.sequence == 0 && report.mode == 0)
                {
                    if (_developmentStartup == "Malformed") report.first.slot = 2;
                    else if (_developmentStartup == "Unsupported") report.buildA ^= 1;
                    else if (_developmentStartup == "Unreadable") report.first = new BuildIntegrity.Digest { slot = 0, status = 2 };
                }
#endif
                SendReport(report);
            }
            else if (_work.IsFaulted)
            {
                _ = _work.Exception; // Observe retired faults as well; never send an old session's result.
                if (!_workTimedOut && _workGeneration == _generation) SendUnavailable(_workSequence, "local hash worker failed");
            }
            _work = null;
            _cancel?.Dispose();
            _cancel = null;
        }
        if (_initialPending && _work == null) { _initialPending = false; BeginCheck(0); }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Keyboard.current != null && Keyboard.current.f9Key.wasPressedThisFrame && _work == null &&
            _manager.IsClientStarted && _manager.ClientManager.Connection.IsAuthenticated && Now >= _clientNext)
        {
            if ((Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed) && _developmentLastReport.HasValue)
            {
                _clientNext = Now + RecheckInterval;
                Debug.Log("[File AC TEST] replay last report unchanged; expected InvalidReport frequency/sequence, no new verification.");
                SendReport(_developmentLastReport.Value);
            }
            else if (_sequence < uint.MaxValue) BeginCheck(++_sequence);
        }
#endif
    }

    private void SendReport(Report report)
    {
        if (!_manager.IsClientStarted || !_manager.ClientManager.Connection.IsAuthenticated) return;
        _clientNext = Now + RecheckInterval;
        _manager.ClientManager.Broadcast(report);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        _developmentLastReport = report;
#endif
        Debug.Log($"[File AC] client report sent kind={(report.sequence == 0 ? "startup" : "recheck")} sequence={report.sequence}; awaiting server comparison (not a verification result).");
    }
    private void SendUnavailable(uint sequence, string reason)
    {
        Debug.LogWarning($"[File AC] ClientConfigurationError: {reason}; not verified, no automatic retry.");
        SendReport(new Report { sequence = sequence, schema = BuildIntegrity.Schema, mode = 2 });
    }

    private void BeginCheck(uint sequence)
    {
        _clientNext = Now + RecheckInterval;
        _workGeneration = _generation;
        _workSequence = sequence; _workDeadline = Now + Deadline; _workTimedOut = false;
        string root = Application.dataPath;
        byte platform = Application.platform == RuntimePlatform.WindowsPlayer ? (byte)1 :
            Application.platform == RuntimePlatform.OSXPlayer ? (byte)2 : (byte)0;
#if ENABLE_IL2CPP
        byte backend = 2;
#else
        byte backend = 1;
#endif
        bool editor = Application.isEditor;
        _cancel = new CancellationTokenSource();
        CancellationToken token = _cancel.Token;
        _work = Task.Run(() =>
        {
            Report report = new Report { sequence = sequence, schema = BuildIntegrity.Schema, platform = platform, backend = backend };
            if (editor) { report.mode = 1; return report; }
            try
            {
                var manifest = JsonUtility.FromJson<BuildIntegrity.Manifest>(BuildIntegrity.ReadManifest(BuildIntegrity.ConfinedPath(root, BuildIntegrity.ClientManifest)));
                BuildIntegrity.Validate(manifest);
                report.buildA = Convert.ToUInt64(manifest.buildId.Substring(0, 16), 16);
                report.buildB = Convert.ToUInt64(manifest.buildId.Substring(16, 16), 16);
                // Expected hashes in the client copy are deliberately ignored.
                report.first = BuildIntegrity.Measure(root, 0, token);
                report.second = BuildIntegrity.Measure(root, 1, token);
                report.third = BuildIntegrity.Measure(root, 2, token);
                return report;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception) { report.mode = 2; return report; }
        }, token);
    }

    private void Receive(NetworkConnection sender, Report report, Channel channel)
    {
        if (sender == null || !sender.IsActive || !sender.IsAuthenticated || !_sessions.TryGetValue(sender, out Session s) || s.enforcementHandled) return;
        string kind = report.sequence == 0 ? "startup" : "recheck";
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        const bool rechecks = true;
#else
        const bool rechecks = false;
#endif
        if (!s.reports.TryAccept(report.sequence, Now, rechecks, out string rejection))
        {
            if (!s.invalidLogged) { s.invalidLogged = true; Log(sender, kind, rejection); }
            return;
        }
        string result = EvaluateReport(report, _approved, Application.isEditor && sender.IsLocalClient);
        bool eligible = BuildIntegrity.CanEnforce(result, s.reports.Late);
        s.reports.Complete(result);
        if (result == "HashMismatch" || result == "MissingRequiredFile" || result == "UnreadableFile")
        {
            BuildIntegrity.Digest[] slots = { report.first, report.second, report.third };
            for (int i = 0; i < slots.Length; i++)
            {
                var expected = BuildIntegrity.FromHash(i, _approved.files[i].sha256);
                var actual = slots[i];
                string status = actual.status == 1 ? "missing" : actual.status == 2 ? "unreadable" :
                    actual.a == expected.a && actual.b == expected.b && actual.c == expected.c && actual.d == expected.d ? "match" : "mismatch";
                result += $" {BuildIntegrity.RequiredPaths[i]}={status}";
            }
        }
        Log(sender, kind, s.reports.Late ? s.reports.Result + (result.StartsWith("Verified") ? "" : " details=" + result) : result);
        if (eligible) ApplyPolicy(sender, staticMode, "StaticIntegrity", $"kind={kind} {result}");
    }

    // Same classifier used by the server and native integration fixtures. No RPC/file input.
    public static string EvaluateReport(Report report, BuildIntegrity.Manifest approved, bool localEditor)
    {
        // Only the actual local Editor connection may be skipped; a remote claim is not trusted.
        if (report.mode > 2 || report.schema != BuildIntegrity.Schema) return "InvalidReport mode/schema";
        if (report.mode == 1) return localEditor ? "EditorSkipped (not verified)" : "UnsupportedBuild (remote Editor claim)";
        if (approved == null) return "ServerConfigurationError";
        if (report.mode == 2) return "ClientConfigurationError (manifest/check unavailable; not verified)";
        if (report.backend != 1 ||
            report.platform != (approved.platform == "Windows" ? 1 : 2) ||
            report.buildA != Convert.ToUInt64(approved.buildId.Substring(0, 16), 16) ||
            report.buildB != Convert.ToUInt64(approved.buildId.Substring(16, 16), 16)) return "UnsupportedBuild";
        return BuildIntegrity.Compare(approved, report.first, report.second, report.third);
    }

    private static EnforcementMode ReadMode(string variable, EnforcementMode fallback)
    {
        string value = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        if (value == "ReportOnly") return EnforcementMode.ReportOnly;
        if (value == "Disconnect") return EnforcementMode.Disconnect;
        Debug.LogWarning($"[AC Policy] Invalid {variable}; using ReportOnly.");
        return EnforcementMode.ReportOnly;
    }

    internal void EvaluateDynamic(NetworkConnection sender, string rule, double suspicion, string evidence)
    {
        if (HorizontalMovementObservation.CanEnforce(rule, suspicion, dynamicSuspicionThreshold))
            ApplyPolicy(sender, dynamicMode, "DynamicMovement", evidence + $" threshold={HorizontalMovementObservation.EnforcementThreshold(dynamicSuspicionThreshold):F2}");
    }

    private void ApplyPolicy(NetworkConnection sender, EnforcementMode mode, string category, string evidence)
    {
        if (mode != EnforcementMode.Disconnect || !_manager.IsServerStarted || sender == null ||
            !sender.IsActive || !sender.IsAuthenticated || sender.Disconnecting ||
            !_sessions.TryGetValue(sender, out Session session) || session.enforcementHandled) return;
        if (sender.IsLocalClient)
        {
            if (session.hostExemptionLogged) return;
            session.hostExemptionLogged = true;
            Debug.LogWarning($"[AC Policy] owner={sender.ClientId} category={category} action=HostReportOnly evidence={evidence}; trusted player-host exempt from disconnect.");
            return;
        }
        session.enforcementHandled = true; // Shared by static/dynamic; cleared by existing connection cleanup.
        Debug.LogWarning($"[AC Policy] owner={sender.ClientId} category={category} action=Disconnect evidence={evidence}");
        // Queue only a fixed server-originated code to this connection. Installed FishNet
        // flushes this tick and closes after ~100ms/minimum two ticks; IsActive becomes
        // false immediately, so gameplay requests are rejected during that interval.
        try
        {
            _manager.ServerManager.Broadcast(sender, new ClientDisconnectRecovery.Notice {
                Reason = category == "StaticIntegrity" ? DisconnectNoticeState.Integrity : DisconnectNoticeState.Movement
            }, true, Channel.Reliable);
        }
        finally { sender.Disconnect(false); } // Best effort; no ACK, never skip disconnect.
    }

    private static void Log(NetworkConnection sender, string kind, string result)
    {
        Debug.Log($"[File AC] owner={sender.ClientId} kind={kind} result={result}");
    }

    private void OnDestroy()
    {
        _cancel?.Cancel();
        _manager.ServerManager.UnregisterBroadcast<Report>(Receive);
        _manager.ServerManager.OnAuthenticationResult -= Authenticated;
        _manager.ServerManager.OnRemoteConnectionState -= RemoteState;
        _manager.ServerManager.OnServerConnectionState -= ServerState;
        _manager.ClientManager.OnAuthenticated -= ClientAuthenticated;
        _manager.ClientManager.OnClientConnectionState -= ClientState;
        _sessions.Clear();
    }
}
