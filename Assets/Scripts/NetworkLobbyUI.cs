using UnityEngine;
using TMPro;    // For the text UI
using FishNet.Object; // Core FishNet component
using FishNet.Object.Synchronizing; // Gives us access to SuncLists
using FishNet.Connection;
using FishNet.Transporting;

// Notice this inherits from NetworkBehaviour, not MonoBehaviour. This is because we want to use FishNet's networking features.
public class NetworkLobbyUI : NetworkBehaviour
{
    [Header("Drag your 4 PlayerNameText objects here")]
    public TextMeshProUGUI[] playerSlots; // Array to hold references to the UI text elements for player names

    public TextMeshProUGUI[] statusSlots; // Drag your "Ready/Not Ready" text objects here

    // A SyncList automatically shares its data with every client in the game
    private readonly SyncList<string> _playerNames = new SyncList<string>();
    private readonly SyncList<bool> _playerReady = new SyncList<bool>();
    private readonly SyncList<int> _playerClientIds = new SyncList<int>();

    private void Awake()
    {
        // Tell the script to update the UI whenever a name OR a ready status change
        _playerNames.OnChange += PlayerNames_OnChange;
        _playerReady.OnChange += PlayerReady_OnChange;
    }

    // This runs ONLY on the Server (the Host)
    public override void OnStartServer()
    {
        base.OnStartServer();

        // Add the Host to the List immediately
        _playerClientIds.Add(0);
        _playerNames.Add("<Host> Player 0");
        _playerReady.Add(false);    // default to not ready

        // Tell the server to listen for new people joining
        ServerManager.OnRemoteConnectionState += OnClientConnectionState;
    }

    // The Server runs this whenever someone connects or disconnects
    private void OnClientConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
    {
        if (args.ConnectionState == RemoteConnectionState.Started)
        {
            // A remove client joined! Add them to the synced list.
            _playerClientIds.Add(conn.ClientId);
            _playerNames.Add($"Player {conn.ClientId}");
            _playerReady.Add(false);
        }
        else if (args.ConnectionState == RemoteConnectionState.Stopped)
        {
            // Find exactly where they are in the list and remove them
            int index = _playerClientIds.IndexOf(conn.ClientId);
            if (index != -1)
            {
                _playerClientIds.RemoveAt(index);
                _playerNames.RemoveAt(index);
                _playerReady.RemoveAt(index);
            }
        }
    }

    // This runs on EVERY computer whenever the SyncList is updated by the server
    private void PlayerNames_OnChange(SyncListOperation op, int index, string oldItem, string newItem, bool asServer)
    {
        UpdateUI();
    }

    private void PlayerReady_OnChange(SyncListOperation op, int index, bool oldItem, bool newItem, bool asServer)
    {
        UpdateUI();
    }

    private void UpdateUI()
    {
        // First, reset all text slots to the default state
        for (int i = 0; i < playerSlots.Length; i++)
        {
            playerSlots[i].text = "Waiting . . .";
            statusSlots[i].text = "";   // hide the status if no one is in the slot
        }

        // Next, loop through our syunced list and fill in the player names
        for (int i = 0; i < _playerNames.Count; i++)
        {
            // Make sure we don't try to fill more slots than we physically have in the UI
            if (i < playerSlots.Length)
            {
                playerSlots[i].text = _playerNames[i];
                
                // Check if this specific player is ready using Unity's Rich Text colors
                if (_playerReady[i] == true)
                {
                    statusSlots[i].text = "<color=green>Ready</color>";
                }
                else
                {
                    statusSlots[i].text = "<color=red>Not Ready</color>";
                }
            }
        }
    }
    
    // READY UP LOGIC
    // The player clicks the button and runs this locally
    public void ReadyUpClicked()
    {
        // Tell the server who clicked the button
        CmdToggleReady(LocalConnection);
    }
    
    // The Server intercepts the message and runs this code
    // Require Ownership = false allows any client to press the button
    [ServerRpc(RequireOwnership = false)]
    public void CmdToggleReady(NetworkConnection caller)
    {
        // The server findsout which slot the caller belongs to
        int index = _playerClientIds.IndexOf(caller.ClientId);

        if (index != -1)
        {
            // Toggle their ready status (if it was false, make it true, and vice versa)
            _playerReady[index] = !_playerReady[index];
        }
    }
}