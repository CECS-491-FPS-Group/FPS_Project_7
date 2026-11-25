using FishNet;
using FishNet.Transporting;
using FishNet.Transporting.Tugboat;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class LocalLobbyPublisher : MonoBehaviour
{
    private const float HeartbeatIntervalSeconds = 3f;

    private LocalLobbyRecord _room;
    private bool _isPublishing;
    private float _nextHeartbeatTime;
    private LanLobbyDiscovery _discovery;
    private Tugboat _transport;

    private void OnEnable()
    {
        if (InstanceFinder.ServerManager != null)
            InstanceFinder.ServerManager.OnServerConnectionState += OnServerStateChanged;
    }

    private void OnDisable()
    {
        if (InstanceFinder.ServerManager != null)
            InstanceFinder.ServerManager.OnServerConnectionState -= OnServerStateChanged;

        StopPublishing();
    }

    private void Update()
    {
        if (_isPublishing && _room != null && InstanceFinder.ServerManager.Started)
        {
            _room.playerCount = InstanceFinder.ServerManager.Clients.Count;
            _room.port = _transport.GetPort();
            _discovery?.Poll(_room);
        }
        if (!_isPublishing || Time.unscaledTime < _nextHeartbeatTime)
            return;

        PublishHeartbeat();
    }

    private void OnApplicationQuit()
    {
        StopPublishing();
    }

    public void PrepareRoom(LocalLobbyRecord room)
    {
        StopPublishing();
        _transport = GetComponent<Tugboat>();
        _room = room?.Copy();
    }

    public void CancelRoom()
    {
        StopPublishing();
    }

    private void OnServerStateChanged(ServerConnectionStateArgs args)
    {
        if (_room == null || args.TransportIndex != _transport.Index)
            return;

        if (args.ConnectionState == LocalConnectionState.Started)
        {
            _isPublishing = true;
            _discovery?.Dispose();
            if (_room.isListed)
            {
                _discovery = new LanLobbyDiscovery(true);
                if (string.IsNullOrEmpty(_discovery.Error))
                    Debug.Log($"[LAN] Public room discovery UDP {LanLobbyDiscovery.DiscoveryPort}, gameplay UDP {_transport.GetPort()}.");
            }
            PublishHeartbeat();
        }
        else if (args.ConnectionState == LocalConnectionState.Stopped)
        {
            StopPublishing();
        }
    }

    private void PublishHeartbeat()
    {
        if (_room == null)
            return;

        int connectedPlayers = 1;

        if (InstanceFinder.ServerManager != null)
            connectedPlayers = Mathf.Max(1, InstanceFinder.ServerManager.Clients.Count);

        if (!LocalLobbyRegistry.TryUpdateRoom(
                _room.code,
                _room.ownerToken,
                LocalLobbyRegistry.StateOpen,
                connectedPlayers,
                out string error))
        {
            if (!string.IsNullOrEmpty(error))
                Debug.LogWarning(error);
        }

        _nextHeartbeatTime = Time.unscaledTime + HeartbeatIntervalSeconds;
    }

    private void StopPublishing()
    {
        _discovery?.Dispose();
        _discovery = null;
        if (_room == null)
            return;

        LocalLobbyRegistry.TryRemoveRoom(
            _room.code,
            _room.ownerToken,
            out string error);

        if (!string.IsNullOrEmpty(error))
            Debug.LogWarning(error);

        LocalLobbySession.ClearHostedRoom(_room.ownerToken);
        _room = null;
        _isPublishing = false;
    }
}
