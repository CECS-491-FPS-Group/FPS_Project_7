using System.Security.Cryptography;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class LobbyCodeUI : NetworkBehaviour
{
    private const string HexCharacters = "0123456789ABCDEF";

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
    [SerializeField, Range(4, 12)] private int codeLength = 6;
    [SerializeField] private string maskCharacter = "•";

    // the server generates this value. fishnet sends the same value to every
    // client observing this lobby object.
    private readonly SyncVar<string> _lobbyCode = new SyncVar<string>();

    // this intentionally is not synchronized. every player controls whether
    // the code is revealed on only their own screen.
    private bool _isCodeVisible;

    public string CurrentLobbyCode => _lobbyCode.Value;

    private void Awake()
    {
        // listen for code updates and connect the visibility button.
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
        // keep inspector values within safe limits.
        codeLength = Mathf.Clamp(codeLength, 4, 12);

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

        // a fresh server session always receives a fresh code.
        _lobbyCode.Value = GenerateHexCode(codeLength);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        // joining players always begin with the code hidden.
        _isCodeVisible = false;
        RefreshUI();
    }

    private void OnDisable()
    {
        // do not leave a revealed code visible if this ui is reopened.
        _isCodeVisible = false;
    }

    public void ToggleCodeVisibility()
    {
        if (string.IsNullOrEmpty(_lobbyCode.Value))
            return;

        _isCodeVisible = !_isCodeVisible;
        RefreshUI();
    }

    private void LobbyCode_OnChange(string previousCode, string nextCode, bool asServer)
    {
        // a new code is treated as private until the local user reveals it.
        _isCodeVisible = false;
        RefreshUI();
    }

    private void RefreshUI()
    {
        // update the text, button, and icon to match the local state.
        string currentCode = _lobbyCode.Value;
        bool codeIsReady = !string.IsNullOrEmpty(currentCode);

        if (lobbyCodeText != null)
        {
            lobbyCodeText.text = codeIsReady && _isCodeVisible
                ? currentCode
                : CreateMask(codeIsReady ? currentCode.Length : codeLength);
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
        // use the first mask character for every hidden position.
        char character = string.IsNullOrEmpty(maskCharacter)
            ? '*'
            : maskCharacter[0];

        return new string(character, Mathf.Max(1, length));
    }

    private static string GenerateHexCode(int length)
    {
        // create secure random bytes and map them to hexadecimal characters.
        byte[] randomBytes = new byte[length];

        using (RandomNumberGenerator generator = RandomNumberGenerator.Create())
        {
            generator.GetBytes(randomBytes);
        }

        char[] codeCharacters = new char[length];

        for (int i = 0; i < length; i++)
            codeCharacters[i] = HexCharacters[randomBytes[i] & 0x0F];

        return new string(codeCharacters);
    }
}
