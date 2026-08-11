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

    // A SyncList automatically shares its data with every client in the game
    private readonly SyncList<string> _playerNames = new SyncList<string>();

    private void Awake()
    {
        // Tell the script: "Any time this list changes, run the UpdateUI function"
        _playerNames.OnChange += PlayerNames_OnChange;
    }

    // This runs ONLY on the Server (the Host)
    public override void OnStartServer()
    {
        base.OnStartServer();

        // Add the Host to the List immediately
        _playerNames.Add("<Host> Player");

        // Tell the server to listen for new people joining
        ServerManager.OnRemoteConnectionState += OnClientConnectionState;
    }

    // The Server runs this whenever someone connects or disconnects
    private void OnClientConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
    {
        if (args.ConnectionState == RemoteConnectionState.Started)
        {
            // A new client joined! Add them to the syunced list
            _playerNames.Add($"Player {conn.ClientId}");
        }
    }

    // This runs on EVERY computer whenever the SyncList is updated by the server
    private void PlayerNames_OnChange(SyncListOperation op, int index, string oldItem, string newItem, bool asServer)
    {
        UpdateUI();
    }

    private void UpdateUI()
    {
        // First, reset all text slots to the default state
        foreach (var text in playerSlots)
        {
            text.text = "Waiting . . .";
        }

        // Next, loop through our syunced list and fill in the player names
        for (int i = 0; i < _playerNames.Count; i++)
        {
            // Make sure we don't try to fill more slots than we physically have in the UI
            if (i < playerSlots.Length)
            {
                playerSlots[i].text = _playerNames[i];
            }
        }
    }
}