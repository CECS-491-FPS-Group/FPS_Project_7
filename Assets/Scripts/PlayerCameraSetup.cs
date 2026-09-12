using FishNet.Object;
using UnityEngine;

public class PlayerCameraSetup : NetworkBehaviour
{
    [Header("Components to Disable for Other Players")]
    public Camera playerCamera;
    public AudioListener audioListener;
    public Canvas playerUI;

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (!base.IsOwner)
        {
            if (playerCamera) playerCamera.enabled = false;
            if (audioListener) audioListener.enabled = false;
            if (playerUI) playerUI.enabled = false;
        }
        else
        {
            // Force FishNet to respect the Prefab's starting height of Y: 689
            transform.position = new Vector3(0, 689f, 0);
        }
    }
}