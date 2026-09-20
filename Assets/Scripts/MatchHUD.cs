using TMPro;
using UnityEngine;

public class MatchHUD : MonoBehaviour
{
    [SerializeField] private TMP_Text roundText;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text centerMessageText;

    private MatchManager matchManager;

    private void Awake()
    {
        if (centerMessageText != null)
            centerMessageText.gameObject.SetActive(false);
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

            if (centerMessageText != null)
            {
                centerMessageText.text =
                    "Next round starts in " +
                    FormatTime(
                        matchManager.SecondsRemaining
                    );
            }

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
    }

    private void SetNormalHudVisible(
        bool visible
    )
    {
        if (roundText != null)
            roundText.gameObject.SetActive(visible);

        if (timerText != null)
            timerText.gameObject.SetActive(visible);
    }

    private void SetCenterMessageVisible(
        bool visible
    )
    {
        if (centerMessageText != null)
        {
            centerMessageText.gameObject.SetActive(
                visible
            );
        }
    }

    private string FormatTime(
        int totalSeconds
    )
    {
        int minutes =
            totalSeconds / 60;

        int seconds =
            totalSeconds % 60;

        return minutes.ToString("00") +
               ":" +
               seconds.ToString("00");
    }
}