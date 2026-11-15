using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

// Finds only the newly elected replacement host. Gameplay still uses
// FishNet/Tugboat; this class exchanges small LAN discovery messages.
public sealed class LanHostMigrationDiscovery : IDisposable
{
    public const int DiscoveryPort = 47778;

    private const string Protocol = "FPS7-HOST-MIGRATION-1";
    private const string QueryKind = "query";
    private const string ReplyKind = "reply";
    private const float RequestIntervalSeconds = 0.5f;

    [Serializable]
    private sealed class Packet
    {
        public string protocol;
        public string kind;
        public string nonce;
        public string version;
        public string sessionId;
        public string code;
        public int generation;
        public int port;
    }

    private readonly bool _isHost;
    private readonly string _sessionId;
    private readonly int _generation;
    private readonly string _lobbyCode;
    private readonly ushort _hostPort;
    private readonly string _nonce = Guid.NewGuid().ToString("N");

    private UdpClient _socket;
    private float _nextRequestTime;
    private string _foundAddress = string.Empty;
    private ushort _foundPort;

    public string Error { get; private set; } = string.Empty;

    public LanHostMigrationDiscovery(
        bool isHost,
        string sessionId,
        int generation,
        string lobbyCode,
        ushort hostPort = 0)
    {
        _isHost = isHost;
        _sessionId = sessionId;
        _generation = generation;
        _lobbyCode = LobbyCodeUtility.Normalize(lobbyCode);
        _hostPort = hostPort;

        try
        {
            _socket = new UdpClient(AddressFamily.InterNetwork);

            if (_isHost)
            {
                _socket.Client.SetSocketOption(
                    SocketOptionLevel.Socket,
                    SocketOptionName.ReuseAddress,
                    true);
            }

            _socket.Client.Bind(new IPEndPoint(
                IPAddress.Any,
                _isHost ? DiscoveryPort : 0));
            _socket.EnableBroadcast = true;
            _socket.Client.Blocking = false;
        }
        catch (SocketException exception)
        {
            Fail(exception);
        }
    }

    public void Poll()
    {
        if (_socket == null)
            return;

        if (!_isHost &&
            string.IsNullOrEmpty(_foundAddress) &&
            Time.realtimeSinceStartup >= _nextRequestTime)
        {
            SendQuery();
            _nextRequestTime =
                Time.realtimeSinceStartup + RequestIntervalSeconds;
        }

        try
        {
            for (int i = 0; i < 32 && _socket.Available > 0; i++)
            {
                IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = _socket.Receive(ref sender);
                if (data == null || data.Length == 0 || data.Length > 2048)
                    continue;

                Packet packet;
                try
                {
                    packet = JsonUtility.FromJson<Packet>(
                        Encoding.UTF8.GetString(data));
                }
                catch (ArgumentException)
                {
                    continue;
                }

                if (_isHost)
                    TryReply(packet, sender);
                else
                    TryAcceptReply(packet, sender);
            }
        }
        catch (SocketException exception)
        {
            if (exception.SocketErrorCode != SocketError.WouldBlock &&
                exception.SocketErrorCode != SocketError.ConnectionReset)
            {
                Fail(exception);
            }
        }
    }

    public bool TryGetEndpoint(out string address, out ushort port)
    {
        address = _foundAddress;
        port = _foundPort;
        return !string.IsNullOrWhiteSpace(address) && port > 0;
    }

    private void SendQuery()
    {
        Packet query = new Packet
        {
            protocol = Protocol,
            kind = QueryKind,
            nonce = _nonce,
            version = Application.version,
            sessionId = _sessionId,
            code = _lobbyCode,
            generation = _generation,
            port = 0
        };

        byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(query));
        foreach (string target in GetBroadcastTargets())
        {
            try
            {
                _socket.Send(
                    bytes,
                    bytes.Length,
                    new IPEndPoint(
                        IPAddress.Parse(target),
                        DiscoveryPort));
            }
            catch (SocketException)
            {
                // One adapter may reject broadcasts. Keep trying the others.
            }
        }
    }

    private void TryReply(Packet query, IPEndPoint sender)
    {
        if (!MatchesPlan(query) ||
            !string.Equals(query.kind, QueryKind, StringComparison.Ordinal) ||
            !Guid.TryParseExact(query.nonce, "N", out _))
        {
            return;
        }

        Packet reply = new Packet
        {
            protocol = Protocol,
            kind = ReplyKind,
            nonce = query.nonce,
            version = Application.version,
            sessionId = _sessionId,
            code = _lobbyCode,
            generation = _generation,
            port = _hostPort
        };

        byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(reply));
        _socket.Send(bytes, bytes.Length, sender);
    }

    private void TryAcceptReply(Packet reply, IPEndPoint sender)
    {
        if (!MatchesPlan(reply) ||
            !string.Equals(reply.kind, ReplyKind, StringComparison.Ordinal) ||
            !string.Equals(reply.nonce, _nonce, StringComparison.Ordinal) ||
            reply.port < 1 || reply.port > ushort.MaxValue)
        {
            return;
        }

        // The network address always comes from the UDP sender. A packet is
        // never allowed to claim an arbitrary address in its JSON data.
        _foundAddress = sender.Address.ToString();
        _foundPort = (ushort)reply.port;
    }

    private bool MatchesPlan(Packet packet)
    {
        return packet != null &&
               string.Equals(packet.protocol, Protocol, StringComparison.Ordinal) &&
               string.Equals(packet.version, Application.version, StringComparison.Ordinal) &&
               string.Equals(packet.sessionId, _sessionId, StringComparison.Ordinal) &&
               string.Equals(packet.code, _lobbyCode, StringComparison.Ordinal) &&
               packet.generation == _generation;
    }

    private static IEnumerable<string> GetBroadcastTargets()
    {
        var targets = new HashSet<string>(StringComparer.Ordinal)
        {
            "255.255.255.255",
            "127.0.0.1"
        };

        try
        {
            foreach (NetworkInterface adapter in
                     NetworkInterface.GetAllNetworkInterfaces())
            {
                if (adapter.OperationalStatus != OperationalStatus.Up)
                    continue;

                foreach (UnicastIPAddressInformation address in
                         adapter.GetIPProperties().UnicastAddresses)
                {
                    if (address.Address.AddressFamily !=
                            AddressFamily.InterNetwork ||
                        address.IPv4Mask == null)
                    {
                        continue;
                    }

                    byte[] ip = address.Address.GetAddressBytes();
                    byte[] mask = address.IPv4Mask.GetAddressBytes();
                    if (ip[0] == 127)
                        continue;

                    for (int i = 0; i < 4; i++)
                        ip[i] = (byte)(ip[i] | (mask[i] ^ 255));

                    targets.Add(new IPAddress(ip).ToString());
                }
            }
        }
        catch (NetworkInformationException)
        {
        }
        catch (PlatformNotSupportedException)
        {
        }

        return targets;
    }

    private void Fail(SocketException exception)
    {
        Error =
            "LAN migration discovery unavailable: " +
            exception.SocketErrorCode + ".";
        Debug.LogWarning("[HostMigration] " + Error);
        Dispose();
    }

    public void Dispose()
    {
        _socket?.Close();
        _socket = null;
        _foundAddress = string.Empty;
        _foundPort = 0;
    }
}
