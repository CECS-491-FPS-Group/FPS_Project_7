using TMPro;
using UnityEngine;

public class ResultsUI : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject resultsPanel;

    [Header("Result Text")]
    [SerializeField] private TMP_Text soldiersKilledText;
    [SerializeField] private TMP_Text playerDeathsText;
    [SerializeField] private TMP_Text totalXPText;
    [SerializeField] private TMP_Text nextMatchText;

    private MatchManager matchManager;

    private void Awake()
    {
        if (resultsPanel != null)
            resultsPanel.SetActive(false);
    }

    private void Update()
    {
        if (matchManager == null)
        {
            matchManager =
                FindFirstObjectByType<MatchManager>();

            return;
        }

        if (resultsPanel == null)
            return;

        // Hide the results panel when a new match starts.
        if (!matchManager.MatchFinished)
        {
            if (resultsPanel.activeSelf)
                resultsPanel.SetActive(false);

            return;
        }

        // Show the results panel after Round 3.
        if (!resultsPanel.activeSelf)
            resultsPanel.SetActive(true);

        if (soldiersKilledText != null)
        {
            soldiersKilledText.text =
                "Soldiers Killed: " +
                matchManager.SoldiersKilled;
        }

        if (playerDeathsText != null)
        {
            playerDeathsText.text =
                "Player Deaths: " +
                matchManager.PlayerDeaths;
        }

        if (totalXPText != null)
        {
            totalXPText.text =
                "Total XP: " +
                matchManager.TotalXP;
        }

        if (nextMatchText != null)
        {
            int seconds =
                Mathf.Max(
                    0,
                    matchManager.SecondsRemaining
                );

            if (seconds > 0)
            {
                nextMatchText.text =
                    "Wait for next match to start in " +
                    seconds +
                    " sec";
            }
            else
            {
                nextMatchText.text =
                    "Waiting for next match...";
            }
        }
    }
}