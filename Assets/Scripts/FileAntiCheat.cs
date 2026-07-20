using UnityEngine;
using FishNet.Object;
using FishNet.Connection;
using System.IO;
using System.Security.Cryptography;
using System.Text;

public class FileAntiCheat : NetworkBehaviour
{
    private string expectedHash = "";
    private string filePath;

    void Awake()
    {
        // Target a dummy "configuration" file in the StreamingAssets folder
        filePath = Path.Combine(Application.streamingAssetsPath, "weapon_stats.json");
        CreateDummyFileIfNotExists();
        
        // The server calculates what the perfect, unhacked hash SHOULD be
        expectedHash = GetFileHash(filePath);
    }

    // 1. As soon as a Client joins, they run this code
    public override void OnStartClient()
    {
        base.OnStartClient();
        
        // The client calculates the hash of their local file
        string myLocalHash = GetFileHash(filePath);
        
        // Send that hash to the server for inspection!
        ServerRpc_VerifyHash(myLocalHash);
    }

    // 2. The Server receives the hash and makes the final decision
    // RequireOwnership = false means ANY client can send this to the server
    [ServerRpc(RequireOwnership = false)]
    private void ServerRpc_VerifyHash(string clientHash, NetworkConnection caller = null)
    {
        if (clientHash == expectedHash)
        {
            Debug.Log($"[ANTI-CHEAT] Client {caller.ClientId} passed the integrity check.");
        }
        else
        {
            Debug.LogWarning($"[ANTI-CHEAT] MODIFIED FILE DETECTED for Client {caller.ClientId}! Booting them from the server.");
            
            // Kick the cheater immediately!
            caller.Disconnect(true);
        }
    }

    // --- Cryptography & Helper Methods ---
    
    private string GetFileHash(string path)
    {
        using (SHA256 sha256 = SHA256.Create())
        {
            using (FileStream fileStream = File.OpenRead(path))
            {
                byte[] hashBytes = sha256.ComputeHash(fileStream);
                
                // Convert the raw bytes into a readable hexadecimal string
                StringBuilder sb = new StringBuilder();
                foreach (byte b in hashBytes)
                {
                    sb.Append(b.ToString("x2"));
                }
                return sb.ToString();
            }
        }
    }

    private void CreateDummyFileIfNotExists()
    {
        if (!File.Exists(filePath))
        {
            Directory.CreateDirectory(Application.streamingAssetsPath);
            // Create a pristine, "unhacked" file if one doesn't exist
            File.WriteAllText(filePath, "{ \"damage\": 10, \"fireRate\": 0.5 }");
        }
    }
}