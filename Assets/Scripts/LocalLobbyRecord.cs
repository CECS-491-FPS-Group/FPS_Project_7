using System;

[Serializable]
public sealed class LocalLobbyRecord
{
    public string code;
    public string roomName;
    public string address;
    public int port;
    public int playerCount;
    public int maxPlayers;
    public bool isListed;
    public string state;
    public string ownerToken;
    public long lastHeartbeatUtcTicks;
    public string gameVersion;

    public LocalLobbyRecord Copy()
    {
        return new LocalLobbyRecord
        {
            code = code,
            roomName = roomName,
            address = address,
            port = port,
            playerCount = playerCount,
            maxPlayers = maxPlayers,
            isListed = isListed,
            state = state,
            ownerToken = ownerToken,
            lastHeartbeatUtcTicks = lastHeartbeatUtcTicks,
            gameVersion = gameVersion
        };
    }
}
