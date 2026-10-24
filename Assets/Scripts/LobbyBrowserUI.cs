using System.Collections.Generic;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class LobbyBrowserUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private LobbyConnection lobbyConnection;
    [SerializeField] private TMP_InputField lobbyCodeInput;
    [SerializeField] private Transform roomListContent;
    [SerializeField] private LobbyRoomRowUI roomRowPrefab;
    [SerializeField] private GameObject emptyRoomsMessage;
    [SerializeField] private TMP_Text browserStatusText;

    [Header("Refresh")]
    [SerializeField, Min(0.5f)] private float automaticRefreshSeconds = 2f;

    private readonly List<LobbyRoomRowUI> _spawnedRows =
        new List<LobbyRoomRowUI>();

    private float _nextRefreshTime;
    private LanLobbyDiscovery _discovery;

    private void OnEnable()
    {
        _discovery?.Dispose();
        _discovery = new LanLobbyDiscovery(false);
        _nextRefreshTime = 0f;
        RefreshRooms();
    }

    private void Update()
    {
        _discovery?.Poll();
        if (Time.unscaledTime >= _nextRefreshTime)
            RefreshRooms();
    }

    public void RefreshRooms()
    {
        _discovery?.Request();
        _nextRefreshTime = Time.unscaledTime + automaticRefreshSeconds;

        if (roomListContent == null || roomRowPrefab == null)
            return;

        ClearRows();

        if (!LocalLobbyRegistry.TryGetListedRooms(
                out List<LocalLobbyRecord> rooms,
                out string error))
        {
            rooms = new List<LocalLobbyRecord>();
        }

        for (int i = 0; i < rooms.Count; i++)
        {
            LobbyRoomRowUI row = Instantiate(roomRowPrefab, roomListContent);
            row.Configure(rooms[i], JoinRoomFromList);
            _spawnedRows.Add(row);
        }

        foreach (LocalLobbyRecord room in _discovery?.GetRooms() ?? new List<LocalLobbyRecord>())
        {
            // The local registry already represents same-PC hosts. Remote hosts
            // are keyed by endpoint, never by lobby code alone.
            if (rooms.Exists(local => local.code == room.code && local.port == room.port && IsLocalAddress(room.address))) continue;
            LobbyRoomRowUI row = Instantiate(roomRowPrefab, roomListContent);
            row.Configure(room, ignored =>
            {
                if (lobbyCodeInput != null) lobbyCodeInput.SetTextWithoutNotify(room.code);
                if (lobbyConnection != null) lobbyConnection.JoinDiscoveredRoom(room);
            });
            _spawnedRows.Add(row);
        }

        if (emptyRoomsMessage != null)
            emptyRoomsMessage.SetActive(_spawnedRows.Count == 0);

        SetStatus(!string.IsNullOrEmpty(error) ? error : _discovery?.Error ?? string.Empty);
    }

    private static bool IsLocalAddress(string address)
    {
        if (System.Net.IPAddress.TryParse(address, out var ip) && System.Net.IPAddress.IsLoopback(ip)) return true;
        try
        {
            foreach (var adapter in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                foreach (var local in adapter.GetIPProperties().UnicastAddresses)
                    if (local.Address.ToString() == address) return true;
        }
        catch (System.Net.NetworkInformation.NetworkInformationException) { }
        return false;
    }

    private void OnDisable() { _discovery?.Dispose(); _discovery = null; ClearRows(); }
    private void OnApplicationQuit() { _discovery?.Dispose(); }

    public void JoinEnteredCode()
    {
        if (lobbyConnection == null)
        {
            SetStatus("LobbyConnection is not assigned.");
            return;
        }

        string code = lobbyCodeInput == null ? string.Empty : lobbyCodeInput.text;
        lobbyConnection.JoinByCode(code);
    }

    private void JoinRoomFromList(string code)
    {
        if (lobbyCodeInput != null)
            lobbyCodeInput.SetTextWithoutNotify(code);

        if (lobbyConnection != null)
            lobbyConnection.JoinByCode(code);
        else
            SetStatus("LobbyConnection is not assigned.");
    }

    private void ClearRows()
    {
        for (int i = 0; i < _spawnedRows.Count; i++)
        {
            if (_spawnedRows[i] != null)
            {
                _spawnedRows[i].gameObject.SetActive(false);
                Destroy(_spawnedRows[i].gameObject);
            }
        }

        _spawnedRows.Clear();
    }

    private void SetStatus(string message)
    {
        if (browserStatusText != null)
            browserStatusText.text = message;
    }
}
