using FishNet.Object;
using StarterAssets;
using UnityEngine;
using PlayerInputComponent = UnityEngine.InputSystem.PlayerInput;

// Run before PlayerInput (-100) so remote copies never claim local devices.
[DefaultExecutionOrder(-200)]
public class PlayerCameraSetup : NetworkBehaviour
{
    [Header("Components to Disable for Other Players")]
    public Camera playerCamera;
    public AudioListener audioListener;
    public Canvas playerUI;

    private PlayerInputComponent _playerInput;
    private StarterAssetsInputs _inputs;
    private FirstPersonController _controller;

    private void Awake()
    {
        _playerInput = GetComponent<PlayerInputComponent>();
        _inputs = GetComponent<StarterAssetsInputs>();
        _controller = GetComponent<FirstPersonController>();
        SetLocalControl(false);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (base.IsOwner)
        {
            // Force FishNet to respect the Prefab's starting height of Y: 689
            transform.position = new Vector3(0, 689f, 0);
        }

        SetLocalControl(base.IsOwner);

        string devices = "none";
        if (_playerInput && _playerInput.devices.Count > 0)
        {
            devices = string.Empty;
            for (int i = 0; i < _playerInput.devices.Count; i++)
                devices += (i == 0 ? "" : ", ") + _playerInput.devices[i].displayName;
        }

        Debug.Log($"[PlayerCameraSetup] OwnerId={OwnerId} LocalClientId={LocalConnection.ClientId} " +
            $"IsOwner={IsOwner} PlayerInputEnabled={(_playerInput && _playerInput.enabled)} Devices=[{devices}]", this);
    }

    public override void OnStopClient()
    {
        SetLocalControl(false);
        base.OnStopClient();
    }

    private void SetLocalControl(bool enabled)
    {
        // Disabling PlayerInput also unpairs its devices. Do this before clearing
        // cached values because canceled input actions can update those values.
        if (_playerInput) _playerInput.enabled = false;
        if (_inputs)
        {
            _inputs.move = Vector2.zero;
            _inputs.look = Vector2.zero;
            _inputs.jump = false;
            _inputs.sprint = false;
            _inputs.enabled = enabled;
        }

        if (_playerInput) _playerInput.enabled = enabled;
        if (_controller) _controller.enabled = enabled;
        if (playerCamera) playerCamera.enabled = enabled;
        if (audioListener) audioListener.enabled = enabled;
        if (playerUI) playerUI.enabled = enabled;
    }
}
