using TMPro;
using UnityEngine;

public class RespawnUI : MonoBehaviour
{
    [SerializeField] private GameObject respawnPanel;
    [SerializeField] private TMP_Text respawnText;

    private void Awake()
    {
        Hide();
    }

    public void ShowCountdown(int seconds)
    {
        if (respawnPanel != null)
            respawnPanel.SetActive(true);

        if (respawnText != null)
            respawnText.text = "Wait for player spawn in " + seconds +" seconds";
    }

    public void Hide()
    {
        if (respawnPanel != null)
            respawnPanel.SetActive(false);
    }
}
