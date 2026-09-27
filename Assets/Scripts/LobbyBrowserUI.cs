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

    private void OnEnable()
    {
        _nextRefreshTime = 0f;
        RefreshRooms();
    }

    private void Update()
    {
        if (Time.unscaledTime >= _nextRefreshTime)
            RefreshRooms();
    }

    public void RefreshRooms()
    {
        _nextRefreshTime = Time.unscaledTime + automaticRefreshSeconds;

        if (roomListContent == null || roomRowPrefab == null)
            return;

        ClearRows();

        if (!LocalLobbyRegistry.TryGetListedRooms(
                out List<LocalLobbyRecord> rooms,
                out string error))
        {
            SetStatus(error);

            if (emptyRoomsMessage != null)
                emptyRoomsMessage.SetActive(true);

            return;
        }

        for (int i = 0; i < rooms.Count; i++)
        {
            LobbyRoomRowUI row = Instantiate(roomRowPrefab, roomListContent);
            row.Configure(rooms[i], JoinRoomFromList);
            _spawnedRows.Add(row);
        }

        if (emptyRoomsMessage != null)
            emptyRoomsMessage.SetActive(rooms.Count == 0);

        SetStatus(string.Empty);
    }

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
