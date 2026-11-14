using FishNet.Object;
using StarterAssets;
using UnityEngine;
using UnityEngine.InputSystem;
using PlayerInputComponent = UnityEngine.InputSystem.PlayerInput;

// Only the owning player reads devices. A local menu never pauses the server clock.
[DefaultExecutionOrder(-50)]
public sealed class LocalPlayerControls : MonoBehaviour
{
    private Health _health;
    private PlayerCameraSetup _setup;
    private PlayerInputComponent _input;
    private StarterAssetsInputs _values;
    private InputActionAsset _ownedAsset;
    private float _nextStance;
    private GameObject _menu;
    public bool MenuOpen => _menu != null && _menu.activeSelf;
    public InputActionAsset Actions => _input.actions;
    public bool Held(string name) => !MenuOpen && Actions.FindAction(name)?.IsPressed() == true;
    public bool Pressed(string name) => !MenuOpen && Actions.FindAction(name)?.WasPressedThisFrame() == true;
    public bool Sprinting => _values.sprint;

    private void Awake()
    {
        _health = GetComponent<Health>(); _setup = GetComponent<PlayerCameraSetup>();
        _input = GetComponent<PlayerInputComponent>(); _values = GetComponent<StarterAssetsInputs>();
        _ownedAsset = LocalInputBindings.Create();
        _input.actions = _ownedAsset;
    }
    private void Update()
    {
        if (!_health.IsClientInitialized || !_health.IsOwner) return;
        if (!_health.CanAct) { LocalBindingsMenu.CloseFor(Actions); CloseMenu(); return; }
        // Escape remains a reserved way out even while PlayerInput is disabled for a menu.
        bool pause = MenuOpen ? Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame : Pressed("Pause");
        if (pause && !LocalBindingsMenu.Rebinding) { if (MenuOpen) CloseMenu(); else OpenMenu(); }
        bool desired = !MenuOpen && Held("Crouch");
        if (desired != _setup.Crouched && Time.unscaledTime >= _nextStance)
        {
            _nextStance = Time.unscaledTime + .25f;
            _setup.RequestLocalStance(desired);
        }
        if (MenuOpen) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
    }
    private void OpenMenu()
    {
        if (_menu == null)
        {
            _menu = IntegrationUi.Canvas("Local Match Menu", 110);
            var panel = IntegrationUi.Panel(_menu.transform, "Menu", new Vector2(440, 330));
            IntegrationUi.Text(panel, "Menu — match continues", new Vector2(0, 125), new Vector2(400, 35), 24);
            IntegrationUi.Button(panel, "Resume", new Vector2(0, 65), CloseMenu);
            IntegrationUi.Button(panel, "Controls / Rebind", new Vector2(0, 10), () => LocalBindingsMenu.Show(Actions));
            IntegrationUi.Button(panel, "Save movement replay", new Vector2(0, -45), () => GetComponent<LocalReplayCapture>()?.Save());
            IntegrationUi.Button(panel, _health.NetworkManager.IsServerStarted ? "Stop host and return" : "Leave to MainMenu",
                new Vector2(0, -100), ClientDisconnectRecovery.ReturnToMenu);
        }
        _menu.SetActive(true);
        GetComponent<FirstPersonController>().enabled = false;
        _values.move = _values.look = Vector2.zero; _values.jump = _values.sprint = false;
        _values.cursorLocked = false; _values.cursorInputForLook = false;
    }
    private void CloseMenu()
    {
        if (_menu != null) _menu.SetActive(false);
        if (_values == null) return;
        _values.cursorLocked = true; _values.cursorInputForLook = true;
        if (_health != null && _health.IsClientInitialized && _health.IsOwner && _health.CanAct)
        { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
    }
    private void OnDestroy()
    {
        if (_input != null) LocalBindingsMenu.CloseFor(_input.actions);
        if (_menu != null) Destroy(_menu);
        if (_ownedAsset != null) Destroy(_ownedAsset);
    }
}
