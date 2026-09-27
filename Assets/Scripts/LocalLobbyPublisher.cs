using FishNet;
using FishNet.Transporting;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class LocalLobbyPublisher : MonoBehaviour
{
    private const float HeartbeatIntervalSeconds = 3f;

    private LocalLobbyRecord _room;
    private bool _isPublishing;
    private float _nextHeartbeatTime;

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
        _room = room?.Copy();
    }

    public void CancelRoom()
    {
        StopPublishing();
    }

    private void OnServerStateChanged(ServerConnectionStateArgs args)
    {
        if (_room == null)
            return;

        if (args.ConnectionState == LocalConnectionState.Started)
        {
            _isPublishing = true;
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
