using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

// Discovery only. Gameplay connections always use FishNet/Tugboat.
public sealed class LanLobbyDiscovery : IDisposable
{
    public const int DiscoveryPort = 47777;
    private const string Protocol = "FPS7-LAN-1";
    private const float Lifetime = 8f;
    [Serializable] private sealed class Packet
    {
        public string protocol, nonce, version, code, name;
        public int port, players, capacity;
    }
    private sealed class Entry { public LocalLobbyRecord Room; public float Seen; }
    private readonly Dictionary<string, Entry> _rooms = new Dictionary<string, Entry>();
    private readonly string _nonce = Guid.NewGuid().ToString("N");
    private readonly bool _host;
    private UdpClient _socket;
    public string Error { get; private set; }

    public LanLobbyDiscovery(bool host)
    {
        _host = host;
        try
        {
            _socket = new UdpClient(AddressFamily.InterNetwork);
            if (host) _socket.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _socket.Client.Bind(new IPEndPoint(IPAddress.Any, host ? DiscoveryPort : 0));
            _socket.EnableBroadcast = true;
            _socket.Client.Blocking = false;
        }
        catch (SocketException ex) { Fail(ex); }
    }

    public void Request()
    {
        if (_host || _socket == null) return;
        byte[] bytes = Encoding.UTF8.GetBytes(Protocol + ":" + _nonce);
        var targets = new HashSet<string> { "255.255.255.255", "127.0.0.1" };
        // Directed broadcasts also cover PCs with multiple active adapters.
        try
        {
            foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (adapter.OperationalStatus != OperationalStatus.Up) continue;
                foreach (UnicastIPAddressInformation address in adapter.GetIPProperties().UnicastAddresses)
                {
                    if (address.Address.AddressFamily != AddressFamily.InterNetwork || address.IPv4Mask == null) continue;
                    byte[] ip = address.Address.GetAddressBytes(), mask = address.IPv4Mask.GetAddressBytes();
                    if (ip[0] == 127) continue;
                    for (int i = 0; i < 4; i++) ip[i] = (byte)(ip[i] | (mask[i] ^ 255));
                    targets.Add(new IPAddress(ip).ToString());
                }
            }
        }
        catch (NetworkInformationException) { }
        catch (PlatformNotSupportedException) { }
        foreach (string target in targets)
        {
            try { _socket.Send(bytes, bytes.Length, new IPEndPoint(IPAddress.Parse(target), DiscoveryPort)); }
            catch (SocketException) { /* One adapter may not permit broadcasts; try the others. */ }
        }
    }

    // Called on Unity's main thread; bounded, non-blocking, no worker callbacks.
    public void Poll(LocalLobbyRecord advertisedRoom = null)
    {
        if (_socket == null) return;
        try
        {
            for (int i = 0; i < 32 && _socket.Available > 0; i++)
            {
                IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = _socket.Receive(ref sender);
                if (data.Length > 2048) continue;
                string message = Encoding.UTF8.GetString(data);
                if (_host)
                {
                    if (advertisedRoom == null || !advertisedRoom.isListed ||
                        !message.StartsWith(Protocol + ":", StringComparison.Ordinal)) continue;
                    string nonce = message.Substring(Protocol.Length + 1);
                    if (!Guid.TryParseExact(nonce, "N", out _)) continue;
                    var packet = new Packet { protocol = Protocol, nonce = nonce, version = Application.version,
                        code = advertisedRoom.code, name = advertisedRoom.roomName, port = advertisedRoom.port,
                        players = advertisedRoom.playerCount, capacity = advertisedRoom.maxPlayers };
                    byte[] reply = Encoding.UTF8.GetBytes(JsonUtility.ToJson(packet));
                    _socket.Send(reply, reply.Length, sender);
                }
                else
                {
                    Packet packet;
                    try { packet = JsonUtility.FromJson<Packet>(message); }
                    catch (ArgumentException) { continue; }
                    if (packet == null || packet.protocol != Protocol || packet.nonce != _nonce ||
                        packet.version != Application.version || !LobbyCodeUtility.IsValid(packet.code) ||
                        packet.port < 1 || packet.port > 65535 || packet.capacity < 1 || packet.capacity > 128 ||
                        packet.players < 0 || packet.players > packet.capacity ||
                        string.IsNullOrEmpty(packet.name) || packet.name.Length > 128) continue;
                    // Never accept an address claimed by the JSON payload.
                    string address = sender.Address.ToString();
                    _rooms[address + ":" + packet.port] = new Entry { Seen = Time.realtimeSinceStartup,
                        Room = new LocalLobbyRecord { address = address, port = packet.port, code = packet.code,
                            roomName = packet.name, playerCount = packet.players, maxPlayers = packet.capacity,
                            isListed = true, state = LocalLobbyRegistry.StateOpen, gameVersion = packet.version } };
                }
            }
        }
        catch (SocketException ex)
        {
            if (ex.SocketErrorCode != SocketError.WouldBlock && ex.SocketErrorCode != SocketError.ConnectionReset) Fail(ex);
        }
    }

    public List<LocalLobbyRecord> GetRooms()
    {
        var result = new List<LocalLobbyRecord>();
        foreach (string key in new List<string>(_rooms.Keys))
        {
            Entry entry = _rooms[key];
            if (Time.realtimeSinceStartup - entry.Seen > Lifetime) _rooms.Remove(key);
            else result.Add(entry.Room.Copy());
        }
        result.Sort((a, b) => string.CompareOrdinal(a.address + ":" + a.port, b.address + ":" + b.port));
        return result;
    }

    private void Fail(SocketException ex)
    {
        Error = "LAN discovery unavailable: " + ex.SocketErrorCode + ". Direct IP joining remains available.";
        Debug.LogWarning("[LAN] " + Error);
        Dispose();
    }
    public void Dispose() { _socket?.Close(); _socket = null; _rooms.Clear(); }
}
