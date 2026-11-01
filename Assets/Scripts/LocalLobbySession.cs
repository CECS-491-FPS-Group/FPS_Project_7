using System;
using UnityEngine;

// Keeps the current room connection information while Unity changes scenes.
public static class LocalLobbySession
{
    public static bool IsHosting { get; private set; }
    public static bool HasConnected { get; private set; }
    public static bool IsIntentionalDisconnect { get; private set; }
    public static string LobbyCode { get; private set; } = string.Empty;
    public static string OwnerToken { get; private set; } = string.Empty;
    public static string PlayerToken { get; private set; } = string.Empty;
    public static string Address { get; private set; } = string.Empty;
    public static ushort Port { get; private set; }

    // A room code is deliberately not required here. Players who joined with
    // the LAN/direct-IP field still need to be able to rejoin that address.
    public static bool CanReconnect =>
        !IsHosting &&
        HasConnected &&
        !IsIntentionalDisconnect &&
        !string.IsNullOrWhiteSpace(Address) &&
        Port > 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetWhenPlaySessionStarts()
    {
        Clear();
    }

    public static void BeginHosting(LocalLobbyRecord room)
    {
        BeginNewPlayerSession();
        IsHosting = true;
        HasConnected = false;
        IsIntentionalDisconnect = false;
        LobbyCode = room.code;
        OwnerToken = room.ownerToken;
        Address = room.address;
        Port = (ushort)room.port;
    }

    public static void BeginJoining(LocalLobbyRecord room)
    {
        BeginNewPlayerSession();
        IsHosting = false;
        HasConnected = false;
        IsIntentionalDisconnect = false;
        LobbyCode = room.code;
        OwnerToken = string.Empty;
        Address = room.address;
        Port = (ushort)room.port;
    }

    public static void MarkConnected()
    {
        HasConnected = true;
        IsIntentionalDisconnect = false;
    }

    public static void MarkIntentionalDisconnect()
    {
        IsIntentionalDisconnect = true;
    }

    private static void BeginNewPlayerSession()
    {
        // This token remains unchanged during an unexpected disconnect/rejoin,
        // but a completely new host or join attempt receives a fresh identity.
        // It is intentionally process-local so an Editor client and a standalone
        // build running on the same computer never share the same token.
        PlayerToken = Guid.NewGuid().ToString("N");
    }

    public static void ClearHostedRoom(string ownerToken)
    {
        if (!IsHosting || OwnerToken != ownerToken)
            return;

        Clear();
    }

    public static void Clear()
    {
        IsHosting = false;
        HasConnected = false;
        IsIntentionalDisconnect = false;
        LobbyCode = string.Empty;
        OwnerToken = string.Empty;
        PlayerToken = string.Empty;
        Address = string.Empty;
        Port = 0;
    }
}
