using System;
using System.Collections;
using System.Collections.Generic;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Managing.Scened;
using FishNet.Transporting;
using UnityEngine;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkManager))]
// Coordinates graceful and unexpected LAN lobby host migration. The elected
// client starts a replacement FishNet server, and the remaining clients find
// it over LAN before reconnecting.
public sealed class LanHostMigrationManager : MonoBehaviour
{
    private const string LobbySceneName = "LobbyScene_v1";

    [Serializable]
    public struct MigrationPlayer
    {
        public string PlayerToken;
        public int JoinOrder;
        public bool IsCurrentHost;
    }

    public struct MigrationRosterSnapshot : IBroadcast
    {
        public string SessionId;
        public int Generation;
        public uint Revision;
        public MigrationPlayer[] Players;
        public string LobbyCode;
        public string RoomName;
        public int MaxPlayers;
        public bool IsListed;
        public ushort PreferredPort;
    }

    public struct GracefulMigrationPlan : IBroadcast
    {
        public string SessionId;
        public int Generation;
        public string SuccessorToken;
        public MigrationPlayer[] Players;
        public string LobbyCode;
        public string RoomName;
        public int MaxPlayers;
        public bool IsListed;
        public ushort PreferredPort;
    }

    public static LanHostMigrationManager Instance { get; private set; }

    public static bool MigrationInProgress =>
        Instance != null && Instance._migrationInProgress;

    public event Action<MigrationRosterSnapshot> ClientSnapshotChanged;

    [Header("LAN host migration")]
    [Tooltip("Time given to deliver the handoff plan before the old host stops.")]
    [SerializeField, Min(0.25f)]
    private float handoffNoticeSeconds = 0.75f;

    [Tooltip("Maximum time clients wait for the replacement host.")]
    [SerializeField, Min(5f)]
    private float migrationTimeoutSeconds = 15f;

    [Tooltip("Small delay that lets the old host release its gameplay port.")]
    [SerializeField, Min(0f)]
    private float successorStartDelaySeconds = 0.5f;

    [Header("Unexpected host loss")]
    [Tooltip("How long a disconnected client checks whether the original host is still responding before starting migration.")]
    [SerializeField, Min(0.5f)]
    private float hostLossConfirmationSeconds = 2.5f;

    [SerializeField]
    private string fallbackMainMenuSceneName = "MainMenu";

    public float HandoffNoticeSeconds => handoffNoticeSeconds;

    public bool HasClientSnapshot =>
        !string.IsNullOrWhiteSpace(_clientSnapshot.SessionId) &&
        _clientSnapshot.Players != null;

    public MigrationRosterSnapshot LatestClientSnapshot => _clientSnapshot;
    public string ElectedSuccessorToken { get; private set; } = string.Empty;
    public bool IsLocalPlayerElectedSuccessor =>
        HasClientSnapshot &&
        !string.IsNullOrWhiteSpace(LocalLobbySession.PlayerToken) &&
        string.Equals(
            ElectedSuccessorToken,
            LocalLobbySession.PlayerToken,
            StringComparison.Ordinal);

    private readonly Dictionary<string, MigrationPlayer> _serverPlayers =
        new Dictionary<string, MigrationPlayer>(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _knownJoinOrders =
        new Dictionary<string, int>(StringComparer.Ordinal);

    private NetworkManager _networkManager;
    private RejoinIdentityManager _identityManager;
    private MigrationRosterSnapshot _clientSnapshot;
    private GracefulMigrationPlan _pendingPlan;
    private LocalLobbyRecord _replacementRoom;
    private LanHostMigrationDiscovery _migrationDiscovery;
    private Coroutine _migrationRoutine;
    private Coroutine _failureRoutine;

    private string _serverSessionId = string.Empty;
    private int _serverGeneration;
    private int _nextJoinOrder;
    private uint _serverRevision;

    private bool _subscribed;
    private bool _hasPendingPlan;
    private bool _migrationInProgress;
    private bool _departingHost;
    private bool _prepareServerFromMigrationPlan;
    private bool _startingReplacementServer;
    private bool _replacementLobbyLoadPending;
    private bool _migrationConnectAttemptInProgress;
    private bool _checkingOriginalHost;
    private bool _applicationQuitting;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            enabled = false;
            return;
        }

        Instance = this;
        _networkManager = GetComponent<NetworkManager>();
        _identityManager = GetComponent<RejoinIdentityManager>();
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void Start()
    {
        // A second chance for scene setups where FishNet finishes initializing
        // after this component's OnEnable method.
        Subscribe();
    }

    private void Update()
    {
        if (_migrationDiscovery == null)
            return;

        _migrationDiscovery.Poll();
    }

    private void OnApplicationQuit()
    {
        _applicationQuitting = true;
    }

    private void OnDisable()
    {
        CancelReplacementLobbyLoad();
        Unsubscribe();
    }

    private void OnDestroy()
    {
        CancelReplacementLobbyLoad();
        Unsubscribe();
        DisposeMigrationDiscovery();
        ClearServerState();
        ClearClientState();

        if (Instance == this)
            Instance = null;
    }

    public bool TryGetElectedSuccessor(out string playerToken)
    {
        playerToken = ElectedSuccessorToken;
        return IsValidPlayerToken(playerToken);
    }

    // RejoinManager and LobbyReturnController may receive FishNet's stopped
    // event before this component. Let either one start the same idempotent
    // host-loss check so event subscription order cannot open the wrong UI.
    public static bool TryStartUnexpectedHostRecovery()
    {
        return Instance != null &&
               Instance.TryBeginUnexpectedHostRecovery();
    }

    // Called by LobbyReturnController before a host intentionally leaves.
    // False means there is no eligible client, so the normal shutdown is used.
    public bool TryBeginGracefulHostMigration()
    {
        if (_networkManager == null ||
            !_networkManager.IsServerStarted ||
            !_networkManager.IsClientStarted ||
            !LocalLobbySession.IsHosting ||
            !LobbyCodeUtility.IsValid(LocalLobbySession.LobbyCode))
        {
            return false;
        }

        EnsureServerSession();
        MigrationPlayer[] players = GetSortedServerPlayers();
        string successorToken = FindSuccessorToken(players);
        if (!IsValidPlayerToken(successorToken))
            return false;

        GracefulMigrationPlan plan = new GracefulMigrationPlan
        {
            SessionId = _serverSessionId,
            Generation = _serverGeneration + 1,
            SuccessorToken = successorToken,
            Players = players,
            LobbyCode = LocalLobbySession.LobbyCode,
            RoomName = LocalLobbySession.RoomName,
            MaxPlayers = Mathf.Clamp(LocalLobbySession.MaxPlayers, 1, 16),
            IsListed = LocalLobbySession.IsListed,
            PreferredPort = LocalLobbySession.Port
        };

        if (!TryValidatePlan(plan, out GracefulMigrationPlan safePlan))
        {
            Debug.LogWarning(
                "[HostMigration] The host could not create a valid handoff plan.",
                this);
            return false;
        }

        _departingHost = true;
        _networkManager.ServerManager.Broadcast(
            safePlan,
            true,
            Channel.Reliable);

        Debug.Log(
            $"[HostMigration] Handoff plan sent: " +
            $"session={ShortToken(safePlan.SessionId)}, " +
            $"generation={safePlan.Generation}, " +
            $"nextHost={ShortToken(safePlan.SuccessorToken)}.",
            this);

        return true;
    }

    private bool TryBeginUnexpectedHostRecovery()
    {
        if (_migrationInProgress)
            return true;

        if (_applicationQuitting ||
            _networkManager == null ||
            _networkManager.IsServerStarted ||
            LocalLobbySession.IsHosting ||
            LocalLobbySession.IsIntentionalDisconnect ||
            !LocalLobbySession.CanReconnect ||
            !HasClientSnapshot ||
            !ContainsPlayer(
                _clientSnapshot.Players,
                LocalLobbySession.PlayerToken))
        {
            return false;
        }

        string successorToken =
            FindSuccessorToken(_clientSnapshot.Players);
        if (!IsValidPlayerToken(successorToken))
            return false;

        GracefulMigrationPlan plan = new GracefulMigrationPlan
        {
            SessionId = _clientSnapshot.SessionId,
            Generation = _clientSnapshot.Generation + 1,
            SuccessorToken = successorToken,
            Players = _clientSnapshot.Players,
            LobbyCode = _clientSnapshot.LobbyCode,
            RoomName = _clientSnapshot.RoomName,
            MaxPlayers = _clientSnapshot.MaxPlayers,
            IsListed = _clientSnapshot.IsListed,
            PreferredPort = _clientSnapshot.PreferredPort
        };

        if (!TryValidatePlan(plan, out GracefulMigrationPlan safePlan))
            return false;

        _pendingPlan = safePlan;
        _hasPendingPlan = true;
        _migrationInProgress = true;
        _checkingOriginalHost = true;

        Debug.Log(
            "[HostMigration] Connection stopped unexpectedly. " +
            "Checking whether the original host is still on the LAN.",
            this);

        if (_migrationRoutine == null)
        {
            _migrationRoutine = StartCoroutine(
                CheckOriginalHostThenMigrate());
        }

        return true;
    }

    private void Subscribe()
    {
        if (_subscribed || _networkManager == null ||
            _networkManager.ClientManager == null ||
            _networkManager.ServerManager == null)
        {
            return;
        }

        _identityManager = GetComponent<RejoinIdentityManager>();
        if (_identityManager == null)
        {
            Debug.LogError(
                "[HostMigration] RejoinIdentityManager must be on the same NetworkManager.",
                this);
            return;
        }

        _networkManager.ClientManager.RegisterBroadcast<MigrationRosterSnapshot>(
            OnClientRosterSnapshot);
        _networkManager.ClientManager.RegisterBroadcast<GracefulMigrationPlan>(
            OnClientMigrationPlan);
        _networkManager.ClientManager.OnClientConnectionState +=
            OnClientConnectionState;
        _networkManager.ServerManager.OnServerConnectionState +=
            OnServerConnectionState;
        _identityManager.ServerIdentityRegistered += OnServerIdentityRegistered;
        _identityManager.ServerIdentityDisconnected += OnServerIdentityDisconnected;
        _subscribed = true;

        if (_networkManager.IsServerStarted)
        {
            EnsureServerSession();

            if (!_startingReplacementServer)
                StartCurrentHostPresenceResponder();
        }
    }

    private void Unsubscribe()
    {
        if (!_subscribed || _networkManager == null)
            return;

        _networkManager.ClientManager.UnregisterBroadcast<MigrationRosterSnapshot>(
            OnClientRosterSnapshot);
        _networkManager.ClientManager.UnregisterBroadcast<GracefulMigrationPlan>(
            OnClientMigrationPlan);
        _networkManager.ClientManager.OnClientConnectionState -=
            OnClientConnectionState;
        _networkManager.ServerManager.OnServerConnectionState -=
            OnServerConnectionState;

        if (_identityManager != null)
        {
            _identityManager.ServerIdentityRegistered -= OnServerIdentityRegistered;
            _identityManager.ServerIdentityDisconnected -= OnServerIdentityDisconnected;
        }

        _subscribed = false;
    }

    private void OnServerConnectionState(ServerConnectionStateArgs args)
    {
        if (args.ConnectionState == LocalConnectionState.Started)
        {
            EnsureServerSession();

            if (_startingReplacementServer)
                LoadFreshReplacementLobby();
            else
                StartCurrentHostPresenceResponder();
        }
        else if (args.ConnectionState == LocalConnectionState.Stopped)
        {
            CancelReplacementLobbyLoad();
            DisposeMigrationDiscovery();
            ClearServerState();
        }
    }

    private void OnClientConnectionState(ClientConnectionStateArgs args)
    {
        if (args.ConnectionState == LocalConnectionState.Started)
        {
            if (!_migrationInProgress)
            {
                _departingHost = false;
                return;
            }

            LocalLobbySession.MarkConnected();
            CompleteMigration();
            return;
        }

        if (args.ConnectionState != LocalConnectionState.Stopped)
            return;

        _migrationConnectAttemptInProgress = false;

        if (_departingHost)
        {
            _departingHost = false;
            ClearClientState();
            return;
        }

        if (!_migrationInProgress)
        {
            if (TryBeginUnexpectedHostRecovery())
                return;

            if (LocalLobbySession.IsIntentionalDisconnect)
                ClearClientState();
            return;
        }

        if (_checkingOriginalHost || !_hasPendingPlan)
            return;

        if (_migrationRoutine == null)
            _migrationRoutine = StartCoroutine(RunPendingMigration());
    }

    private IEnumerator CheckOriginalHostThenMigrate()
    {
        GracefulMigrationPlan plan = _pendingPlan;

        DisposeMigrationDiscovery();
        _migrationDiscovery = new LanHostMigrationDiscovery(
            false,
            plan.SessionId,
            plan.Generation - 1,
            plan.LobbyCode);

        if (!string.IsNullOrEmpty(_migrationDiscovery.Error))
        {
            CancelUnexpectedRecoveryAndShowRejoin(
                "The original host could not be checked on this device.");
            yield break;
        }

        float deadline =
            Time.realtimeSinceStartup + hostLossConfirmationSeconds;

        while (_migrationInProgress &&
               _checkingOriginalHost &&
               Time.realtimeSinceStartup < deadline)
        {
            if (_migrationDiscovery.TryGetEndpoint(
                    out string address,
                    out ushort port))
            {
                Debug.Log(
                    $"[HostMigration] Original host is still responding at " +
                    $"{address}:{port}. Opening the normal rejoin prompt.",
                    this);

                CancelUnexpectedRecoveryAndShowRejoin(string.Empty);
                yield break;
            }

            yield return null;
        }

        if (!_migrationInProgress || !_checkingOriginalHost)
        {
            _migrationRoutine = null;
            yield break;
        }

        DisposeMigrationDiscovery();
        _checkingOriginalHost = false;

        Debug.LogWarning(
            $"[HostMigration] Original host did not answer for " +
            $"{hostLossConfirmationSeconds:0.0} seconds. " +
            $"Starting emergency migration to generation {plan.Generation}.",
            this);

        yield return RunPendingMigration();
        _migrationRoutine = null;
    }

    private void CancelUnexpectedRecoveryAndShowRejoin(string reason)
    {
        DisposeMigrationDiscovery();
        _checkingOriginalHost = false;
        _migrationInProgress = false;
        _hasPendingPlan = false;
        _pendingPlan = default;
        _migrationRoutine = null;

        if (!string.IsNullOrWhiteSpace(reason))
            Debug.LogWarning("[HostMigration] " + reason, this);

        if (!RejoinManager.TryShowDisconnectPrompt())
        {
            BeginMigrationFailure(
                "The normal rejoin prompt was unavailable after the host check.");
        }
    }

    private IEnumerator RunPendingMigration()
    {
        GracefulMigrationPlan plan = _pendingPlan;

        if (string.Equals(
                plan.SuccessorToken,
                LocalLobbySession.PlayerToken,
                StringComparison.Ordinal))
        {
            yield return RunSuccessorMigration(plan);
        }
        else
        {
            yield return RunWaitingClientMigration(plan);
        }

        _migrationRoutine = null;
    }

    private IEnumerator RunSuccessorMigration(GracefulMigrationPlan plan)
    {
        if (successorStartDelaySeconds > 0f)
            yield return new WaitForSecondsRealtime(successorStartDelaySeconds);

        if (!_migrationInProgress)
            yield break;

        if (!LocalLobbyRegistry.TryReserveMigratedRoom(
                plan.LobbyCode,
                plan.RoomName,
                plan.IsListed,
                plan.MaxPlayers,
                plan.PreferredPort,
                out LocalLobbyRecord room,
                out string error))
        {
            BeginMigrationFailure(
                "The replacement host could not reserve the lobby: " + error);
            yield break;
        }

        _replacementRoom = room.Copy();
        LocalLobbySession.BeginMigratedHosting(room);

        LocalLobbyPublisher publisher =
            GetComponent<LocalLobbyPublisher>();
        if (publisher == null)
            publisher = gameObject.AddComponent<LocalLobbyPublisher>();
        publisher.PrepareRoom(room);

        _prepareServerFromMigrationPlan = true;
        _startingReplacementServer = true;

        Debug.Log(
            $"[HostMigration] This client is starting the replacement server " +
            $"on port {room.port}.",
            this);

        if (!_networkManager.ServerManager.StartConnection((ushort)room.port))
        {
            publisher.CancelRoom();
            BeginMigrationFailure(
                "The elected replacement host could not start its server.");
            yield break;
        }

        float deadline =
            Time.realtimeSinceStartup + migrationTimeoutSeconds;

        while (_migrationInProgress &&
               Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        if (_migrationInProgress)
        {
            BeginMigrationFailure(
                "The replacement host started, but its local player could not reconnect.");
        }
    }

    private IEnumerator RunWaitingClientMigration(GracefulMigrationPlan plan)
    {
        DisposeMigrationDiscovery();
        _migrationDiscovery = new LanHostMigrationDiscovery(
            false,
            plan.SessionId,
            plan.Generation,
            plan.LobbyCode);

        if (!string.IsNullOrEmpty(_migrationDiscovery.Error))
        {
            BeginMigrationFailure(_migrationDiscovery.Error);
            yield break;
        }

        Debug.Log(
            "[HostMigration] Waiting for the replacement host on the LAN.",
            this);

        float deadline =
            Time.realtimeSinceStartup + migrationTimeoutSeconds;

        while (_migrationInProgress &&
               Time.realtimeSinceStartup < deadline)
        {
            if (!_migrationConnectAttemptInProgress &&
                _migrationDiscovery.TryGetEndpoint(
                    out string address,
                    out ushort port))
            {
                LocalLobbySession.BeginMigrationReconnect(
                    plan.LobbyCode,
                    address,
                    port,
                    plan.RoomName,
                    plan.MaxPlayers,
                    plan.IsListed);

                Debug.Log(
                    $"[HostMigration] Replacement host found at " +
                    $"{address}:{port}. Reconnecting...",
                    this);

                _migrationConnectAttemptInProgress =
                    _networkManager.ClientManager.StartConnection(
                        address,
                        port);

                if (!_migrationConnectAttemptInProgress)
                    yield return new WaitForSecondsRealtime(0.5f);
            }

            yield return null;
        }

        if (_migrationInProgress)
        {
            BeginMigrationFailure(
                "The replacement host was not available before the migration timed out.");
        }
    }

    private void LoadFreshReplacementLobby()
    {
        if (_replacementLobbyLoadPending ||
            !_startingReplacementServer ||
            _networkManager == null ||
            _networkManager.SceneManager == null)
        {
            return;
        }

        _replacementLobbyLoadPending = true;
        _networkManager.SceneManager.OnLoadEnd +=
            OnReplacementLobbyLoadEnd;

        // The old server deinitialized every scene NetworkObject when it
        // disconnected. Loading a fresh global lobby gives the replacement
        // server and all reconnecting clients new, initialized NetworkObjects.
        SceneLoadData sceneLoadData =
            new SceneLoadData(LobbySceneName);
        sceneLoadData.ReplaceScenes = ReplaceOption.All;
        _networkManager.SceneManager.LoadGlobalScenes(sceneLoadData);

        Debug.Log(
            "[HostMigration] Replacement server is reloading " +
            LobbySceneName + " as a global FishNet scene.",
            this);
    }

    private void OnReplacementLobbyLoadEnd(SceneLoadEndEventArgs args)
    {
        if (!_replacementLobbyLoadPending ||
            !args.QueueData.AsServer)
        {
            return;
        }

        CancelReplacementLobbyLoad();

        if (!_startingReplacementServer ||
            !_networkManager.IsServerStarted)
        {
            return;
        }

        Debug.Log(
            "[HostMigration] Fresh replacement lobby is ready. " +
            "Starting the new host client.",
            this);

        StartReplacementHostClient();
    }

    private void CancelReplacementLobbyLoad()
    {
        if (_networkManager != null &&
            _networkManager.SceneManager != null)
        {
            _networkManager.SceneManager.OnLoadEnd -=
                OnReplacementLobbyLoadEnd;
        }

        _replacementLobbyLoadPending = false;
    }

    private void StartReplacementHostClient()
    {
        if (!_startingReplacementServer ||
            _replacementRoom == null ||
            !_hasPendingPlan)
        {
            return;
        }

        ushort actualPort =
            _networkManager.TransportManager.Transport.GetPort();

        DisposeMigrationDiscovery();
        _migrationDiscovery = new LanHostMigrationDiscovery(
            true,
            _pendingPlan.SessionId,
            _pendingPlan.Generation,
            _pendingPlan.LobbyCode,
            actualPort);

        if (!string.IsNullOrEmpty(_migrationDiscovery.Error))
        {
            BeginMigrationFailure(_migrationDiscovery.Error);
            return;
        }

        _migrationConnectAttemptInProgress =
            _networkManager.ClientManager.StartConnection(
                "127.0.0.1",
                actualPort);

        if (!_migrationConnectAttemptInProgress)
        {
            BeginMigrationFailure(
                "The replacement server started, but its host client could not connect.");
        }
    }

    private void CompleteMigration()
    {
        bool becameHost = LocalLobbySession.IsHosting;
        _migrationInProgress = false;
        _hasPendingPlan = false;
        _startingReplacementServer = false;
        _prepareServerFromMigrationPlan = false;
        _checkingOriginalHost = false;
        CancelReplacementLobbyLoad();
        _migrationConnectAttemptInProgress = false;

        // The host keeps its responder for the whole session. Besides helping
        // waiting clients reconnect, it lets a future disconnect distinguish
        // a living host from a crashed host. Ordinary clients can close theirs.
        if (!becameHost)
            DisposeMigrationDiscovery();

        Debug.Log(
            becameHost
                ? "[HostMigration] Migration complete. This player is now the LAN host."
                : "[HostMigration] Migration complete. Reconnected to the replacement host.",
            this);
    }

    private void BeginMigrationFailure(string reason)
    {
        if (_failureRoutine != null)
            return;

        Debug.LogError("[HostMigration] " + reason, this);
        _failureRoutine = StartCoroutine(
            StopAfterMigrationFailureAndReturn());
    }

    private IEnumerator StopAfterMigrationFailureAndReturn()
    {
        LocalLobbySession.MarkIntentionalDisconnect();
        CancelReplacementLobbyLoad();

        LocalLobbyPublisher publisher =
            GetComponent<LocalLobbyPublisher>();
        if (publisher != null && LocalLobbySession.IsHosting)
            publisher.CancelRoom();

        if (_networkManager.IsServerStarted)
            _networkManager.ServerManager.StopConnection(true);

        if (_networkManager.IsClientStarted)
            _networkManager.ClientManager.StopConnection();

        float deadline = Time.realtimeSinceStartup + 3f;
        while ((_networkManager.IsServerStarted ||
                _networkManager.IsClientStarted) &&
               Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        DisposeMigrationDiscovery();
        _migrationInProgress = false;
        _hasPendingPlan = false;
        _startingReplacementServer = false;
        _prepareServerFromMigrationPlan = false;
        _replacementLobbyLoadPending = false;
        _migrationConnectAttemptInProgress = false;
        _checkingOriginalHost = false;
        LocalLobbySession.Clear();
        LobbyReturnController.RequestHostJoinAfterReturn();

        if (string.IsNullOrWhiteSpace(fallbackMainMenuSceneName))
        {
            Debug.LogError(
                "[HostMigration] A fallback Main Menu Scene Name is required.",
                this);
        }
        else
        {
            UnitySceneManager.LoadScene(fallbackMainMenuSceneName);
        }

        _failureRoutine = null;
    }

    private void EnsureServerSession()
    {
        if (!string.IsNullOrEmpty(_serverSessionId))
            return;

        _serverPlayers.Clear();
        _knownJoinOrders.Clear();
        _serverRevision = 0;
        _nextJoinOrder = 0;

        if (_prepareServerFromMigrationPlan && _hasPendingPlan)
        {
            _serverSessionId = _pendingPlan.SessionId;
            _serverGeneration = _pendingPlan.Generation;

            for (int i = 0; i < _pendingPlan.Players.Length; i++)
            {
                MigrationPlayer player = _pendingPlan.Players[i];
                _knownJoinOrders[player.PlayerToken] = player.JoinOrder;
                _nextJoinOrder = Mathf.Max(
                    _nextJoinOrder,
                    player.JoinOrder + 1);
            }

            _prepareServerFromMigrationPlan = false;

            Debug.Log(
                $"[HostMigration] Replacement server continued session " +
                $"{ShortToken(_serverSessionId)} at generation " +
                $"{_serverGeneration}.",
                this);
            return;
        }

        _serverSessionId = Guid.NewGuid().ToString("N");
        _serverGeneration = 0;

        Debug.Log(
            $"[HostMigration] Server created migration session " +
            $"{ShortToken(_serverSessionId)}.",
            this);
    }

    private void StartCurrentHostPresenceResponder()
    {
        if (_networkManager == null ||
            !_networkManager.IsServerStarted ||
            !LocalLobbySession.IsHosting ||
            string.IsNullOrWhiteSpace(_serverSessionId) ||
            !LobbyCodeUtility.IsValid(LocalLobbySession.LobbyCode) ||
            _networkManager.TransportManager == null ||
            _networkManager.TransportManager.Transport == null)
        {
            return;
        }

        ushort actualPort =
            _networkManager.TransportManager.Transport.GetPort();

        DisposeMigrationDiscovery();
        _migrationDiscovery = new LanHostMigrationDiscovery(
            true,
            _serverSessionId,
            _serverGeneration,
            LocalLobbySession.LobbyCode,
            actualPort);

        if (!string.IsNullOrEmpty(_migrationDiscovery.Error))
        {
            Debug.LogWarning(
                "[HostMigration] Host-loss detection is unavailable: " +
                _migrationDiscovery.Error,
                this);
            DisposeMigrationDiscovery();
            return;
        }

        Debug.Log(
            $"[HostMigration] Host presence responder started for " +
            $"generation {_serverGeneration} on gameplay port {actualPort}.",
            this);
    }

    private void OnServerIdentityRegistered(
        NetworkConnection connection,
        string playerToken,
        bool isReturningPlayer)
    {
        if (_networkManager == null || !_networkManager.IsServerStarted ||
            connection == null || !connection.IsActive ||
            !connection.IsAuthenticated || !IsValidPlayerToken(playerToken))
        {
            return;
        }

        EnsureServerSession();

        bool isHost = IsHostConnection(connection);
        if (_serverPlayers.TryGetValue(
                playerToken,
                out MigrationPlayer player))
        {
            player.IsCurrentHost = isHost;
        }
        else
        {
            if (!_knownJoinOrders.TryGetValue(
                    playerToken,
                    out int joinOrder))
            {
                joinOrder = _nextJoinOrder++;
                _knownJoinOrders[playerToken] = joinOrder;
            }

            player = new MigrationPlayer
            {
                PlayerToken = playerToken,
                JoinOrder = joinOrder,
                IsCurrentHost = isHost
            };
        }

        _serverPlayers[playerToken] = player;
        PublishServerRoster();
    }

    private void OnServerIdentityDisconnected(
        NetworkConnection connection,
        string playerToken)
    {
        if (_networkManager == null || !_networkManager.IsServerStarted ||
            !IsValidPlayerToken(playerToken) ||
            !_serverPlayers.Remove(playerToken))
        {
            return;
        }

        PublishServerRoster();
    }

    private void PublishServerRoster()
    {
        if (_networkManager == null || !_networkManager.IsServerStarted ||
            string.IsNullOrEmpty(_serverSessionId))
        {
            return;
        }

        MigrationRosterSnapshot snapshot = new MigrationRosterSnapshot
        {
            SessionId = _serverSessionId,
            Generation = _serverGeneration,
            Revision = ++_serverRevision,
            Players = GetSortedServerPlayers(),
            LobbyCode = LocalLobbySession.LobbyCode,
            RoomName = LocalLobbySession.RoomName,
            MaxPlayers = Mathf.Clamp(LocalLobbySession.MaxPlayers, 1, 16),
            IsListed = LocalLobbySession.IsListed,
            PreferredPort = LocalLobbySession.Port
        };

        _networkManager.ServerManager.Broadcast(
            snapshot,
            true,
            Channel.Reliable);
    }

    private MigrationPlayer[] GetSortedServerPlayers()
    {
        var players = new List<MigrationPlayer>(_serverPlayers.Values);
        players.Sort(ComparePlayers);
        return players.ToArray();
    }

    private void OnClientRosterSnapshot(
        MigrationRosterSnapshot snapshot,
        Channel channel)
    {
        if (!TryValidateSnapshot(
                snapshot,
                out MigrationRosterSnapshot safeSnapshot))
        {
            Debug.LogWarning(
                "[HostMigration] Client rejected an invalid migration roster.",
                this);
            return;
        }

        bool sameSession = string.Equals(
            _clientSnapshot.SessionId,
            safeSnapshot.SessionId,
            StringComparison.Ordinal);

        if (sameSession &&
            (safeSnapshot.Generation < _clientSnapshot.Generation ||
             (safeSnapshot.Generation == _clientSnapshot.Generation &&
              safeSnapshot.Revision <= _clientSnapshot.Revision)))
        {
            return;
        }

        _clientSnapshot = safeSnapshot;
        ElectedSuccessorToken =
            FindSuccessorToken(safeSnapshot.Players);

        Debug.Log(
            $"[HostMigration] Roster received: " +
            $"session={ShortToken(safeSnapshot.SessionId)}, " +
            $"generation={safeSnapshot.Generation}, " +
            $"players={safeSnapshot.Players.Length}, " +
            $"nextHost={ShortToken(ElectedSuccessorToken)}, " +
            $"thisPlayerIsNext={IsLocalPlayerElectedSuccessor}.",
            this);

        ClientSnapshotChanged?.Invoke(_clientSnapshot);
    }

    private void OnClientMigrationPlan(
        GracefulMigrationPlan plan,
        Channel channel)
    {
        // The old host also owns a local client and may receive its broadcast.
        // It is intentionally leaving, so it must not try to rejoin itself.
        if (_departingHost)
            return;

        if (!TryValidatePlan(
                plan,
                out GracefulMigrationPlan safePlan) ||
            !ContainsPlayer(
                safePlan.Players,
                LocalLobbySession.PlayerToken))
        {
            Debug.LogWarning(
                "[HostMigration] Client rejected an invalid handoff plan.",
                this);
            return;
        }

        if (HasClientSnapshot &&
            (!string.Equals(
                 safePlan.SessionId,
                 _clientSnapshot.SessionId,
                 StringComparison.Ordinal) ||
             safePlan.Generation != _clientSnapshot.Generation + 1))
        {
            Debug.LogWarning(
                "[HostMigration] Client rejected a handoff plan for the wrong session or generation.",
                this);
            return;
        }

        _pendingPlan = safePlan;
        _hasPendingPlan = true;
        _migrationInProgress = true;

        Debug.Log(
            $"[HostMigration] Handoff plan received: " +
            $"generation={safePlan.Generation}, " +
            $"thisPlayerWillHost=" +
            $"{string.Equals(safePlan.SuccessorToken, LocalLobbySession.PlayerToken, StringComparison.Ordinal)}.",
            this);
    }

    private static bool TryValidateSnapshot(
        MigrationRosterSnapshot snapshot,
        out MigrationRosterSnapshot safeSnapshot)
    {
        safeSnapshot = default;

        if (!IsValidPlayerToken(snapshot.SessionId) ||
            snapshot.Generation < 0 ||
            snapshot.Revision == 0 ||
            !LobbyCodeUtility.IsValid(snapshot.LobbyCode) ||
            snapshot.MaxPlayers < 1 || snapshot.MaxPlayers > 16 ||
            snapshot.PreferredPort < 1 ||
            !TryValidatePlayers(
                snapshot.Players,
                false,
                out MigrationPlayer[] players))
        {
            return false;
        }

        string roomName = string.IsNullOrWhiteSpace(snapshot.RoomName)
            ? $"Room {snapshot.LobbyCode}"
            : snapshot.RoomName.Trim();
        if (roomName.Length > 128)
            return false;

        safeSnapshot = new MigrationRosterSnapshot
        {
            SessionId = snapshot.SessionId,
            Generation = snapshot.Generation,
            Revision = snapshot.Revision,
            Players = players,
            LobbyCode = LobbyCodeUtility.Normalize(snapshot.LobbyCode),
            RoomName = roomName,
            MaxPlayers = snapshot.MaxPlayers,
            IsListed = snapshot.IsListed,
            PreferredPort = snapshot.PreferredPort
        };
        return true;
    }

    private static bool TryValidatePlan(
        GracefulMigrationPlan plan,
        out GracefulMigrationPlan safePlan)
    {
        safePlan = default;

        if (!IsValidPlayerToken(plan.SessionId) ||
            plan.Generation < 1 ||
            !IsValidPlayerToken(plan.SuccessorToken) ||
            !LobbyCodeUtility.IsValid(plan.LobbyCode) ||
            plan.MaxPlayers < 1 || plan.MaxPlayers > 16 ||
            plan.PreferredPort < 1 ||
            !TryValidatePlayers(
                plan.Players,
                true,
                out MigrationPlayer[] players))
        {
            return false;
        }

        bool successorFound = false;
        for (int i = 0; i < players.Length; i++)
        {
            if (!string.Equals(
                    players[i].PlayerToken,
                    plan.SuccessorToken,
                    StringComparison.Ordinal))
            {
                continue;
            }

            if (players[i].IsCurrentHost)
                return false;

            successorFound = true;
            break;
        }

        if (!successorFound)
            return false;

        string roomName = string.IsNullOrWhiteSpace(plan.RoomName)
            ? $"Room {plan.LobbyCode}"
            : plan.RoomName.Trim();
        if (roomName.Length > 128)
            return false;

        safePlan = new GracefulMigrationPlan
        {
            SessionId = plan.SessionId,
            Generation = plan.Generation,
            SuccessorToken = plan.SuccessorToken,
            Players = players,
            LobbyCode = LobbyCodeUtility.Normalize(plan.LobbyCode),
            RoomName = roomName,
            MaxPlayers = plan.MaxPlayers,
            IsListed = plan.IsListed,
            PreferredPort = plan.PreferredPort
        };
        return true;
    }

    private static bool TryValidatePlayers(
        MigrationPlayer[] source,
        bool requireHostAndSuccessor,
        out MigrationPlayer[] players)
    {
        players = null;

        int minimumPlayers = requireHostAndSuccessor ? 2 : 0;
        if (source == null ||
            source.Length < minimumPlayers ||
            source.Length > 16)
        {
            return false;
        }

        var tokens = new HashSet<string>(StringComparer.Ordinal);
        players = new MigrationPlayer[source.Length];
        int hostCount = 0;

        for (int i = 0; i < source.Length; i++)
        {
            MigrationPlayer player = source[i];
            if (!IsValidPlayerToken(player.PlayerToken) ||
                player.JoinOrder < 0 ||
                !tokens.Add(player.PlayerToken))
            {
                players = null;
                return false;
            }

            if (player.IsCurrentHost)
                hostCount++;

            players[i] = player;
        }

        if (hostCount > 1 ||
            (requireHostAndSuccessor && hostCount != 1))
        {
            players = null;
            return false;
        }

        Array.Sort(players, ComparePlayers);
        return true;
    }

    private static int ComparePlayers(
        MigrationPlayer left,
        MigrationPlayer right)
    {
        int orderComparison =
            left.JoinOrder.CompareTo(right.JoinOrder);
        return orderComparison != 0
            ? orderComparison
            : string.CompareOrdinal(
                left.PlayerToken,
                right.PlayerToken);
    }

    private static string FindSuccessorToken(MigrationPlayer[] players)
    {
        if (players == null)
            return string.Empty;

        string successor = string.Empty;
        int bestJoinOrder = int.MaxValue;

        for (int i = 0; i < players.Length; i++)
        {
            MigrationPlayer player = players[i];
            if (player.IsCurrentHost ||
                !IsValidPlayerToken(player.PlayerToken))
            {
                continue;
            }

            if (player.JoinOrder < bestJoinOrder ||
                (player.JoinOrder == bestJoinOrder &&
                 string.CompareOrdinal(
                     player.PlayerToken,
                     successor) < 0))
            {
                successor = player.PlayerToken;
                bestJoinOrder = player.JoinOrder;
            }
        }

        return successor;
    }

    private static bool ContainsPlayer(
        MigrationPlayer[] players,
        string playerToken)
    {
        if (!IsValidPlayerToken(playerToken) || players == null)
            return false;

        for (int i = 0; i < players.Length; i++)
        {
            if (string.Equals(
                    players[i].PlayerToken,
                    playerToken,
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private void ClearServerState()
    {
        _serverPlayers.Clear();
        _knownJoinOrders.Clear();
        _serverSessionId = string.Empty;
        _serverGeneration = 0;
        _serverRevision = 0;
        _nextJoinOrder = 0;
    }

    private void ClearClientState()
    {
        _clientSnapshot = default;
        ElectedSuccessorToken = string.Empty;
        _pendingPlan = default;
        _hasPendingPlan = false;
        _migrationInProgress = false;
        _prepareServerFromMigrationPlan = false;
        _startingReplacementServer = false;
        _checkingOriginalHost = false;
        CancelReplacementLobbyLoad();
        _migrationConnectAttemptInProgress = false;
        _replacementRoom = null;
        DisposeMigrationDiscovery();
    }

    private void DisposeMigrationDiscovery()
    {
        _migrationDiscovery?.Dispose();
        _migrationDiscovery = null;
    }

    private static bool IsValidPlayerToken(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               Guid.TryParseExact(value, "N", out _);
    }

    private bool IsHostConnection(NetworkConnection connection)
    {
        if (connection == null)
            return false;

        if (connection.IsLocalClient)
            return true;

        return _networkManager != null &&
               _networkManager.IsServerStarted &&
               _networkManager.IsClientStarted &&
               _networkManager.ClientManager.Connection != null &&
               connection.ClientId ==
               _networkManager.ClientManager.Connection.ClientId;
    }

    private static string ShortToken(string value)
    {
        return string.IsNullOrEmpty(value)
            ? "<none>"
            : value.Substring(0, Math.Min(8, value.Length));
    }
}
