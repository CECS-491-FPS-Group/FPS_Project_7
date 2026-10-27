using UnityEngine;

[DisallowMultipleComponent]
// Restores the Host/Join view after leaving LobbyScene_v1.
public sealed class MainMenuReturnController : MonoBehaviour
{
    [Header("Host/Join View")]
    [SerializeField] private GameObject hostOrJoin;
    [SerializeField] private GameObject hostJoinControls;
    [SerializeField] private GameObject hostOptionOverlay;
    [SerializeField] private GameObject lobbyBrowserOverlay;

    [Header("Optional")]
    [Tooltip("Assign only a panel that contains the first-menu controls. " +
             "Do not assign MainMenuCanvas if HostOrJoin is its child.")]
    [SerializeField] private GameObject firstMainMenuScreen;

    private void Awake()
    {
        if (LobbyReturnController.ConsumeHostJoinReturnRequest())
            ShowHostJoin();
    }

    public void ShowHostJoin()
    {
        if (hostOrJoin == null)
        {
            Debug.LogError(
                "MainMenuReturnController needs the HostOrJoin object assigned.");
            return;
        }

        if (firstMainMenuScreen != null)
            firstMainMenuScreen.SetActive(false);

        hostOrJoin.SetActive(true);

        if (hostJoinControls != null)
            hostJoinControls.SetActive(true);

        if (hostOptionOverlay != null)
            hostOptionOverlay.SetActive(false);

        if (lobbyBrowserOverlay != null)
            lobbyBrowserOverlay.SetActive(false);
    }
}
