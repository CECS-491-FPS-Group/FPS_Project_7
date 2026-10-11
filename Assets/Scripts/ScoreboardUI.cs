using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class ScoreboardUI : MonoBehaviour
{
    [SerializeField] private GameObject scoreboardPanel;
    [SerializeField] private TMP_Text playerScoreText;
    [SerializeField] private TMP_Text soldierScoreText;

    private MatchManager matchManager;

    private void Awake()
    {
        if (scoreboardPanel != null)
            scoreboardPanel.SetActive(false);
    }

    private void Update()
    {
        if (matchManager == null)
        {
            matchManager =
                FindFirstObjectByType<MatchManager>();

            return;
        }

        bool holdingG =
            Keyboard.current != null &&
            Keyboard.current.gKey.isPressed;

        if (scoreboardPanel != null)
            scoreboardPanel.SetActive(holdingG);

        if (!holdingG)
            return;

        if (playerScoreText != null)
        {
            playerScoreText.text =
                "YOU:\n\n" +
                "Kills: " +
                matchManager.SoldiersKilled +
                "\nDeaths: " +
                matchManager.PlayerDeaths +
                "\nXP: " +
                matchManager.TotalXP;
        }

        if (soldierScoreText != null)
        {
            soldierScoreText.text =
                "SOLDIERS:\n\n" +
                "Kills: " +
                matchManager.PlayerDeaths +
                "\nDeaths: " +
                matchManager.SoldiersKilled;
        }
    }
}