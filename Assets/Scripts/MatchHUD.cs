using TMPro;
using UnityEngine;

public class MatchHUD : MonoBehaviour
{
    [SerializeField] private TMP_Text roundText;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text centerMessageText;
    [SerializeField] private TMP_Text killStreakText;

    [Header("Live Score")]
    [SerializeField] private TMP_Text playerScoreText;
    [SerializeField] private TMP_Text botScoreText;

    private MatchManager matchManager;

    private void Awake()
    {
        if (centerMessageText != null)
            centerMessageText.gameObject.SetActive(false);

        if (killStreakText != null)
            killStreakText.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (matchManager == null)
        {
            matchManager =
                FindFirstObjectByType<MatchManager>();

            return;
        }

        if (matchManager.MatchFinished)
        {
            SetNormalHudVisible(false);
            SetCenterMessageVisible(false);
            return;
        }

        if (matchManager.IntermissionActive)
        {
            SetNormalHudVisible(false);
            SetCenterMessageVisible(true);
            return;
        }

        SetNormalHudVisible(true);
        SetCenterMessageVisible(false);

        if (roundText != null)
        {
            roundText.text =
                "Round " +
                matchManager.CurrentRound +
                " / 3";
        }

        if (timerText != null)
        {
            timerText.text =
                FormatTime(
                    matchManager.SecondsRemaining
                );
        }

        if (playerScoreText != null)
        {
            playerScoreText.text =
                "You: " +
                matchManager.SoldiersKilled;
        }

        if (botScoreText != null)
        {
            botScoreText.text =
                "Bot: " +
                matchManager.PlayerDeaths;
        }

        if (killStreakText != null)
        {
            bool hasKillStreak =
                matchManager.KillStreak > 0;

            killStreakText.gameObject.SetActive(
                hasKillStreak
            );

            if (hasKillStreak)
            {
                killStreakText.text =
                    "Kill Streak: " +
                    matchManager.KillStreak;
            }
        }
    }

    private void SetNormalHudVisible(bool visible)
    {
        if (roundText != null)
            roundText.gameObject.SetActive(visible);

        if (timerText != null)
            timerText.gameObject.SetActive(visible);

        if (playerScoreText != null)
            playerScoreText.gameObject.SetActive(visible);

        if (botScoreText != null)
            botScoreText.gameObject.SetActive(visible);

        if (!visible && killStreakText != null)
            killStreakText.gameObject.SetActive(false);
    }

    private void SetCenterMessageVisible(bool visible)
    {
        if (centerMessageText != null)
        {
            centerMessageText.gameObject.SetActive(visible);

            if (visible)
            {
                centerMessageText.text =
                    "Next round starts in " +
                    FormatTime(
                        matchManager.SecondsRemaining
                    );
            }
        }
    }

    private string FormatTime(int totalSeconds)
    {
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;

        return minutes.ToString("00") +
               ":" +
               seconds.ToString("00");
    }
}