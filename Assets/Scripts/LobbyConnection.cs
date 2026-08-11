using UnityEngine;
using TMPro;    // We need this to read TextMeshPro Input Field
using FishNet;

public class  LobbyConnection : MonoBehaviour
{
    [Header("UI References")]
    public TMP_InputField ipInputField; // Input field for IP address

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