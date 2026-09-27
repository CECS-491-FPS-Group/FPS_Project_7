using UnityEngine;

public static class LocalLobbySession
{
    public static bool IsHosting { get; private set; }
    public static string LobbyCode { get; private set; } = string.Empty;
    public static string OwnerToken { get; private set; } = string.Empty;
    public static string Address { get; private set; } = string.Empty;
    public static ushort Port { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetWhenPlaySessionStarts()
    {
        Clear();
    }

    public static void BeginHosting(LocalLobbyRecord room)
    {
        IsHosting = true;
        LobbyCode = room.code;
        OwnerToken = room.ownerToken;
        Address = room.address;
        Port = (ushort)room.port;
    }

    public static void BeginJoining(LocalLobbyRecord room)
    {
        IsHosting = false;
        LobbyCode = room.code;
        OwnerToken = string.Empty;
        Address = room.address;
        Port = (ushort)room.port;
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
        LobbyCode = string.Empty;
        OwnerToken = string.Empty;
        Address = string.Empty;
        Port = 0;
    }
}
