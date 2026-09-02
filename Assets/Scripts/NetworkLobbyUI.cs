using FishNet;
using FishNet.Connection;
using FishNet.Managing.Timing;
using FishNet.Managing.Scened;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NetworkLobbyUI : NetworkBehaviour
{
    [Header("Player Slots UI")]
    public TextMeshProUGUI[] playerSlots = new TextMeshProUGUI[0];

    public TextMeshProUGUI[] statusSlots = new TextMeshProUGUI[0];
    public ConnectionBarsUI[] connectionBars = new ConnectionBarsUI[4];

    [Header("Local Button UI")]
    public Image readyButtonImage;
    public TextMeshProUGUI readyButtonText;
    
    [Header("Host Controls")]
    public Button startGameButton;

    [Header("Ping & Lobby Settings")]
    [SerializeField, Range(1000, 60000)] private int maximumReportedPing = 60000;
    [SerializeField, Range(1, 16)] private int maximumPlayers = 4;

    private readonly SyncList<string> _playerNames = new SyncList<string>();
    private readonly SyncList<int> _playerIds = new SyncList<int>();
    private readonly SyncList<bool> _playerReady = new SyncList<bool>();
    private readonly SyncDictionary<int, int> _playerPings = new SyncDictionary<int, int>();

    private TimeManager _timeManager;

    private void Awake()
    {
        _playerNames.OnChange += PlayerNames_OnChange;
        _playerIds.OnChange += PlayerIds_OnChange;
        _playerReady.OnChange += PlayerReady_OnChange;
        _playerPings.OnChange += PlayerPings_OnChange;
    }

    private void OnDestroy()
    {
        _playerNames.OnChange -= PlayerNames_OnChange;
        _playerIds.OnChange -= PlayerIds_OnChange;
        _playerReady.OnChange -= PlayerReady_OnChange;
        _playerPings.OnChange -= PlayerPings_OnChange;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        _playerNames.Clear();
        _playerIds.Clear();
        _playerReady.Clear();
        _playerPings.Clear();

        ServerManager.OnAuthenticationResult += OnAuthenticationResult;
        ServerManager.OnRemoteConnectionState += OnClientConnectionState;

        foreach (NetworkConnection connection in ServerManager.Clients.Values)
        {
            if (connection.IsAuthenticated && connection.IsLocalClient) TryAddPlayer(connection);
        }
        foreach (NetworkConnection connection in ServerManager.Clients.Values)
        {
            if (connection.IsAuthenticated && !connection.IsLocalClient) TryAddPlayer(connection);
        }
    }

    public override void OnStopServer()
    {
        ServerManager.OnAuthenticationResult -= OnAuthenticationResult;
        ServerManager.OnRemoteConnectionState -= OnClientConnectionState;
        base.OnStopServer();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        RefreshLocalHostEntry();
        UpdateUI();

        _timeManager = InstanceFinder.TimeManager;
        if (_timeManager != null) _timeManager.OnRoundTripTimeUpdated += OnLocalRoundTripTimeUpdated;
    }

    public override void OnStopClient()
    {
        if (_timeManager != null) _timeManager.OnRoundTripTimeUpdated -= OnLocalRoundTripTimeUpdated;
        _timeManager = null;
        base.OnStopClient();
    }

    private void TryAddPlayer(NetworkConnection connection)
    {
        if (connection == null || !connection.IsAuthenticated) return;
        if (_playerIds.IndexOf(connection.ClientId) >= 0) return;
        if (_playerIds.Count >= maximumPlayers) return;

        string playerName = connection.IsLocalClient ? $"<Host> Player {connection.ClientId}" : $"Player {connection.ClientId}";

        // Add the silent data first
        _playerIds.Add(connection.ClientId);
        _playerReady.Add(false);
        
        // Add the Name LAST to safely trigger the UpdateUI() loop
        _playerNames.Add(playerName);

        _playerPings[connection.ClientId] = 0;
    }
    
    private void OnAuthenticationResult(NetworkConnection connection, bool authenticated)
    {
        if (authenticated) TryAddPlayer(connection);
    }

    private void OnClientConnectionState(NetworkConnection connection, RemoteConnectionStateArgs args)
    {
        if (args.ConnectionState != RemoteConnectionState.Stopped) return;

        int playerIndex = _playerIds.IndexOf(connection.ClientId);
        if (playerIndex >= 0)
        {
            _playerNames.RemoveAt(playerIndex);
            _playerIds.RemoveAt(playerIndex);
            _playerReady.RemoveAt(playerIndex);
        }
        _playerPings.Remove(connection.ClientId);
    }

    private void RefreshLocalHostEntry()
    {
        if (!InstanceFinder.IsServerStarted || InstanceFinder.ClientManager == null) return;
        NetworkConnection localConnection = InstanceFinder.ClientManager.Connection;
        if (localConnection == null) return;

        int playerIndex = _playerIds.IndexOf(localConnection.ClientId);
        if (playerIndex < 0) return;

        string hostName = $"<Host> Player {localConnection.ClientId}";
        _playerNames[playerIndex] = hostName;
        _playerPings[localConnection.ClientId] = 0;
    }

    private void OnLocalRoundTripTimeUpdated(long roundTripTimeMs)
    {
        long clampedPing = System.Math.Min(System.Math.Max(roundTripTimeMs, 0L), maximumReportedPing);
        ReportPingServerRpc((int)clampedPing);
    }

    [ServerRpc(RequireOwnership = false)]
    private void ReportPingServerRpc(int pingMs, NetworkConnection sender = null)
    {
        if (sender == null) return;
        int playerIndex = _playerIds.IndexOf(sender.ClientId);
        if (playerIndex < 0) return;

        int safePing = Mathf.Clamp(pingMs, 0, maximumReportedPing);
        if (_playerPings.TryGetValue(sender.ClientId, out int oldPing) && oldPing == safePing) return;
        _playerPings[sender.ClientId] = safePing;
    }

    private void PlayerNames_OnChange(SyncListOperation op, int index, string old, string newItem, bool asServer) { UpdateUI(); }
    private void PlayerIds_OnChange(SyncListOperation op, int index, int old, int newItem, bool asServer) { UpdateUI(); }
    private void PlayerReady_OnChange(SyncListOperation op, int index, bool old, bool newItem, bool asServer) { UpdateUI(); }
    private void PlayerPings_OnChange(SyncDictionaryOperation op, int clientId, int ping, bool asServer) { UpdateUI(); }

    private void UpdateUI()
    {
        for (int i = 0; i < playerSlots.Length; i++)
        {
            if (playerSlots[i]) playerSlots[i].text = "Waiting . . .";
            if (i < statusSlots.Length && statusSlots[i]) statusSlots[i].text = "";
        }

        for (int i = 0; i < connectionBars.Length; i++)
        {
            if (connectionBars[i]) connectionBars[i].gameObject.SetActive(false);
        }

        int visiblePlayerCount = Mathf.Min(_playerNames.Count, playerSlots.Length);

        for (int i = 0; i < visiblePlayerCount; i++)
        {
            if (playerSlots[i]) playerSlots[i].text = _playerNames[i];

            if (i < statusSlots.Length && statusSlots[i])
            {
                statusSlots[i].text = _playerReady[i] ? "<color=green>Ready</color>" : "<color=red>Not Ready</color>";
            }

            if (i >= connectionBars.Length || !connectionBars[i]) continue;

            ConnectionBarsUI bars = connectionBars[i];
            bars.gameObject.SetActive(true);

            if (i < _playerIds.Count && _playerPings.TryGetValue(_playerIds[i], out int pingMs))
            {
                bars.SetLatencyMs(pingMs);
            }
            else
            {
                bars.SetUnavailable();
            }
        }

        if (IsClientInitialized && LocalConnection.IsValid && readyButtonText && readyButtonImage)
        {
            int myIndex = _playerIds.IndexOf(LocalConnection.ClientId);
            if (myIndex != -1)
            {
                if (_playerReady[myIndex])
                {
                    readyButtonText.text = "CANCEL";
                    readyButtonImage.color = new Color32(200, 50, 50, 255);
                }
                else
                {
                    readyButtonText.text = "READY";
                    readyButtonImage.color = new Color32(50, 150, 50, 255);
                }
            }
        }
        // Start Game Button Logic
        if (startGameButton)
        {
            // Assume everyone is ready, as long as there is at least 1 person in the lobby
            bool allReady = _playerReady.Count > 0;
            
            // Loop through and check if anyone is slacking
            for (int i = 0; i < _playerReady.Count; i++)
            {
                if (!_playerReady[i])
                {
                    allReady = false;
                    break;
                }
            }
            
            // The button is only clickable if EVERYONE is ready AND this computer is the Host
            startGameButton.interactable = allReady && IsServerStarted;
        }
    }

    public void ReadyUpClicked()
    {
        CmdToggleReady(LocalConnection);
    }

    [ServerRpc(RequireOwnership = false)]
    public void CmdToggleReady(NetworkConnection caller)
    {
        int index = _playerIds.IndexOf(caller.ClientId);
        if (index != -1)
        {
            _playerReady[index] = !_playerReady[index];
        }
    }

    public void StartGameClicked()
    {
        // Double-check that only the server is allowed to trigger a map change
        if (!IsServerStarted) return;
        
        // Use FishNet's Scene Manager to load the actual game map
        // For now we put "MapTestScene", change to the real scene name later
        SceneLoadData sld = new SceneLoadData("Playground");
        sld.ReplaceScenes = ReplaceOption.All;
        InstanceFinder.SceneManager.LoadGlobalScenes(sld);
    }
}