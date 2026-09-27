using FishNet.Object;
using FishNet.Object.Synchronizing;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class LobbyCodeUI : NetworkBehaviour
{
    [Header("UI references")]
    [SerializeField] private TMP_Text lobbyCodeText;
    [SerializeField] private Button visibilityButton;
    [SerializeField] private Image eyeIcon;

    [Header("Visibility button icons")]
    [Tooltip("Open-eye icon shown while the code is hidden. Clicking it reveals the code.")]
    [SerializeField] private Sprite showCodeIcon;
    [Tooltip("Slashed-eye icon shown while the code is visible. Clicking it hides the code.")]
    [SerializeField] private Sprite hideCodeIcon;

    [Header("Lobby code")]
    [SerializeField] private string maskCharacter = "•";

    private readonly SyncVar<string> _lobbyCode = new SyncVar<string>();
    private bool _isCodeVisible;

    public string CurrentLobbyCode => _lobbyCode.Value;

    private void Awake()
    {
        _lobbyCode.OnChange += LobbyCode_OnChange;

        if (visibilityButton != null)
            visibilityButton.onClick.AddListener(ToggleCodeVisibility);

        _isCodeVisible = false;
        RefreshUI();
    }

    private void OnDestroy()
    {
        _lobbyCode.OnChange -= LobbyCode_OnChange;

        if (visibilityButton != null)
            visibilityButton.onClick.RemoveListener(ToggleCodeVisibility);
    }

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(maskCharacter))
            maskCharacter = "•";
    }

    private void OnEnable()
    {
        _isCodeVisible = false;
        RefreshUI();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        // The Main Menu already reserved this exact code with a unique port.
        // The fallback keeps direct LobbyScene development tests working.
        _lobbyCode.Value = LobbyCodeUtility.IsValid(LocalLobbySession.LobbyCode)
            ? LocalLobbySession.LobbyCode
            : LobbyCodeUtility.Generate();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        _isCodeVisible = false;
        RefreshUI();
    }

    private void OnDisable()
    {
        _isCodeVisible = false;
    }

    public void ToggleCodeVisibility()
    {
        if (string.IsNullOrEmpty(_lobbyCode.Value))
            return;

        _isCodeVisible = !_isCodeVisible;
        RefreshUI();
    }

    private void LobbyCode_OnChange(
        string previousCode,
        string nextCode,
        bool asServer)
    {
        _isCodeVisible = false;
        RefreshUI();
    }

    private void RefreshUI()
    {
        string currentCode = _lobbyCode.Value;
        bool codeIsReady = !string.IsNullOrEmpty(currentCode);

        if (lobbyCodeText != null)
        {
            lobbyCodeText.text = codeIsReady && _isCodeVisible
                ? currentCode
                : CreateMask(LobbyCodeUtility.CodeLength);
        }

        if (visibilityButton != null)
            visibilityButton.interactable = codeIsReady;

        if (eyeIcon != null)
        {
            Sprite nextIcon = _isCodeVisible ? hideCodeIcon : showCodeIcon;

            if (nextIcon != null)
                eyeIcon.sprite = nextIcon;
        }
    }

    private string CreateMask(int length)
    {
        char character = string.IsNullOrEmpty(maskCharacter)
            ? '*'
            : maskCharacter[0];

        return new string(character, Mathf.Max(1, length));
    }
}
