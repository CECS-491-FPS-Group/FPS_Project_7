using UnityEngine;
using TMPro;
using FishNet;
using FishNet.Managing.Scened;
using FishNet.Transporting;

public class  LobbyConnection : MonoBehaviour
{
    [Header("UI References")]
    public TMP_InputField ipInputField; // Input field for IP address

    private void Awake()
    {
        // Subscribe to the server state event so we know exactly when it finishes starting
        InstanceFinder.ServerManager.OnServerConnectionState += OnServerStateChanged;
    }

    private void OnDestroy()
    {
        // Always unsubscribe from events when the object is destroyed to prevent memory leaks
        if (InstanceFinder.ServerManager != null)
        {
            InstanceFinder.ServerManager.OnServerConnectionState -= OnServerStateChanged;
        }
    }

    private void OnServerStateChanged(ServerConnectionStateArgs args)
    {
        // Check if the server has started successfully, then load the lobby
        if (args.ConnectionState == LocalConnectionState.Started)
        {
            SceneLoadData sld = new SceneLoadData("LobbyScene_v1");
            sld.ReplaceScenes = ReplaceOption.All;
            InstanceFinder.SceneManager.LoadGlobalScenes(sld);
        }
        else if (args.ConnectionState == LocalConnectionState.Stopped)
        {
            Debug.Log("Server stopped.");
        }
    }

    // This gets called when the Host button is clicked
    public void StartHost()
    {
        // Start the server to host the game
        InstanceFinder.ServerManager.StartConnection();
        // Immediately connect the host as a local player
        InstanceFinder.ClientManager.StartConnection();

        Debug.Log("Hosting game on localholst...");
    }

    // This gets called when the Join button is clicked
    public void JoinGame()
    {
        string ipAddress = ipInputField.text; // Get the IP address from the input field

        // If the field is empty, default to localhost
        if (string.IsNullOrEmpty(ipAddress))
        {
            ipAddress = "localhost";
        }

        // Tell FishNet's transport layer what IP to look for
        InstanceFinder.TransportManager.Transport.SetClientAddress(ipAddress);

        // Attempt to connect as a client
        InstanceFinder.ClientManager.StartConnection();

        Debug.Log($"Attempting to join game at IP: {ipAddress}");
    }
}