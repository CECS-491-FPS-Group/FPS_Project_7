using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class LobbyRoomRowUI : MonoBehaviour
{
    [Header("Row references")]
    [SerializeField] private TMP_Text roomNameText;
    [SerializeField] private TMP_Text lobbyCodeText;
    [SerializeField] private TMP_Text playerCountText;
    [SerializeField] private Button joinButton;

    private string _lobbyCode = string.Empty;
    private Action<string> _joinAction;

    private void Awake()
    {
        if (joinButton != null)
            joinButton.onClick.AddListener(JoinClicked);
    }

    private void OnDestroy()
    {
        if (joinButton != null)
            joinButton.onClick.RemoveListener(JoinClicked);
    }

    public void Configure(LocalLobbyRecord room, Action<string> joinAction)
    {
        _lobbyCode = room.code;
        _joinAction = joinAction;

        if (roomNameText != null)
            roomNameText.text = room.roomName;

        if (lobbyCodeText != null)
            lobbyCodeText.text = room.code;

        if (playerCountText != null)
            playerCountText.text = $"{room.playerCount}/{room.maxPlayers}";

        if (joinButton != null)
            joinButton.interactable = room.playerCount < room.maxPlayers;
    }

    private void JoinClicked()
    {
        _joinAction?.Invoke(_lobbyCode);
    }
}
