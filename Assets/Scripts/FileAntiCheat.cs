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
        public double deadline, nextReport;
        public bool received, missingLogged, invalidLogged, enforcementHandled, hostExemptionLogged;
        public uint sequence;
    }
    private NetworkManager _manager;
    private readonly Dictionary<NetworkConnection, Session> _sessions = new Dictionary<NetworkConnection, Session>();
    private BuildIntegrity.Manifest _approved;
    private CancellationTokenSource _cancel;
    private Task<Report> _work;
    private int _generation, _workGeneration;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private uint _sequence;
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
            _sessions.Add(connection, new Session { deadline = Now + Deadline });
    }
    private void RemoteState(NetworkConnection connection, RemoteConnectionStateArgs args)
    {
        if (args.ConnectionState == RemoteConnectionState.Stopped) _sessions.Remove(connection);
    }
    private void ClientAuthenticated()
    {
        _generation++;
        _cancel?.Cancel();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        _sequence = 0;
#endif
        _initialPending = true;
    }
    private void ClientState(ClientConnectionStateArgs args)
    {
        if (args.ConnectionState != LocalConnectionState.Stopped) return;
        _generation++;
        _cancel?.Cancel();
        _initialPending = false;
    }

    private void Update()
    {
        foreach (var pair in _sessions)
        {
            Session s = pair.Value;
            if (!s.received && !s.missingLogged && Now >= s.deadline)
            {
                s.missingLogged = true;
                Log(pair.Key, "startup", _approved == null ? "ServerConfigurationError; MissingReport" : "MissingReport");
            }
        }
        if (_work != null && _work.IsCompleted)
        {
            if (!_work.IsCanceled && !_work.IsFaulted && _workGeneration == _generation &&
                _manager.IsClientStarted && _manager.ClientManager.Connection.IsAuthenticated)
            {
                _manager.ClientManager.Broadcast(_work.Result);
                Debug.Log($"[File AC] client report sent kind={(_work.Result.sequence == 0 ? "startup" : "recheck")} sequence={_work.Result.sequence}");
            }
            else if (_work.IsFaulted) Debug.LogWarning("[File AC] LocalConfigurationError; worker failed; report unavailable.");
            _work = null;
            _cancel?.Dispose();
            _cancel = null;
        }
        if (_initialPending && _work == null) { _initialPending = false; BeginCheck(0); }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Keyboard.current != null && Keyboard.current.f9Key.wasPressedThisFrame && _work == null &&
            _manager.IsClientStarted && _manager.ClientManager.Connection.IsAuthenticated && Now >= _clientNext)
            BeginCheck(++_sequence);
#endif
    }

    private void BeginCheck(uint sequence)
    {
        _clientNext = Now + RecheckInterval;
        _workGeneration = _generation;
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
        if ((s.received && (Now < s.nextReport || report.sequence <= s.sequence)) || (!s.received && report.sequence != 0))
        {
            if (!s.invalidLogged) { s.invalidLogged = true; Log(sender, kind, "InvalidReport frequency/sequence"); }
            return;
        }
#if !(UNITY_EDITOR || DEVELOPMENT_BUILD)
        if (report.sequence != 0) return;
#endif
        bool late = s.missingLogged || (!s.received && Now >= s.deadline);
        if (late) s.missingLogged = true; // A later recheck must not erase a missed startup deadline.
        s.received = true;
        s.sequence = report.sequence;
        s.nextReport = Now + RecheckInterval;
        string result;
        bool eligible = false;
        // Only the actual local Editor connection may be skipped; a remote claim is not trusted.
        if (report.mode > 2 || report.schema != BuildIntegrity.Schema) result = "InvalidReport mode/schema";
        else if (report.mode == 1) result = Application.isEditor && sender.IsLocalClient ? "EditorSkipped (not verified)" : "UnsupportedBuild (remote Editor claim)";
        else if (_approved == null) result = "ServerConfigurationError";
        else if (report.mode == 2) result = "ClientConfigurationError (manifest missing/unreadable/invalid; not verified)";
        else if (report.backend != 1 ||
            report.platform != (_approved.platform == "Windows" ? 1 : 2) ||
            report.buildA != Convert.ToUInt64(_approved.buildId.Substring(0, 16), 16) ||
            report.buildB != Convert.ToUInt64(_approved.buildId.Substring(16, 16), 16)) result = "UnsupportedBuild";
        else
        {
            result = BuildIntegrity.Compare(_approved, report.first, report.second, report.third);
            eligible = BuildIntegrity.CanEnforce(result, late);
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
        }
        Log(sender, kind, (late ? "LateReport result=" : "") + result);
        if (eligible) ApplyPolicy(sender, staticMode, "StaticIntegrity", $"kind={kind} {result}");
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
        // Existing FishNet API: false flushes pending reliable data, with no client ACK wait.
        sender.Disconnect(false);
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
