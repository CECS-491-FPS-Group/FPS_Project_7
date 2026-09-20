using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;

[RequireComponent(typeof(NetworkObject))]
public class MatchManager : NetworkBehaviour
{
    [Header("Match Settings")]
    [SerializeField] private int totalRounds = 3;
    [SerializeField] private float roundLength = 300f;
    [SerializeField] private float intermissionLength = 15f;
    [SerializeField] private float postMatchLength = 20f;

    [Header("XP Settings")]
    [SerializeField] private int xpPerSoldierKill = 5;

    private readonly SyncVar<int> currentRound =
        new SyncVar<int>();

    private readonly SyncVar<int> secondsRemaining =
        new SyncVar<int>();

    private readonly SyncVar<bool> matchActive =
        new SyncVar<bool>();

    private readonly SyncVar<bool> intermissionActive =
        new SyncVar<bool>();

    private readonly SyncVar<bool> matchFinished =
        new SyncVar<bool>();

    private readonly SyncVar<int> soldiersKilled =
        new SyncVar<int>();

    private readonly SyncVar<int> playerDeaths =
        new SyncVar<int>();

    private readonly SyncVar<int> totalXP =
        new SyncVar<int>();

    public int CurrentRound =>
        currentRound.Value;

    public int SecondsRemaining =>
        secondsRemaining.Value;

    public bool MatchActive =>
        matchActive.Value;

    public bool IntermissionActive =>
        intermissionActive.Value;

    public bool MatchFinished =>
        matchFinished.Value;

    public int SoldiersKilled =>
        soldiersKilled.Value;

    public int PlayerDeaths =>
        playerDeaths.Value;

    public int TotalXP =>
        totalXP.Value;

    public int XPPerSoldierKill =>
        xpPerSoldierKill;

    private float serverSecondsRemaining;
    private int lastDisplayedSecond;

    private bool localFreezeState;
    private bool localFreezeStateInitialized;

    public override void OnStartServer()
    {
        base.OnStartServer();

        currentRound.Value = 1;
        secondsRemaining.Value =
            Mathf.CeilToInt(roundLength);

        matchActive.Value = true;
        intermissionActive.Value = false;
        matchFinished.Value = false;

        soldiersKilled.Value = 0;
        playerDeaths.Value = 0;
        totalXP.Value = 0;

        serverSecondsRemaining =
            roundLength;

        lastDisplayedSecond =
            secondsRemaining.Value;

        SetAllCharactersFrozen(false);
        ApplyDifficultyToAllSoldiers();

        Debug.Log("Round 1 started.");
    }

    private void Update()
    {
        ApplyFreezeStateOnThisClient();

        if (!IsServerInitialized)
            return;

        if (matchFinished.Value)
        {
            UpdatePostMatchCountdown();
            return;
        }

        if (
            !matchActive.Value &&
            !intermissionActive.Value
        )
        {
            return;
        }

        serverSecondsRemaining -=
            Time.deltaTime;

        UpdateDisplayedSeconds();

        if (serverSecondsRemaining > 0f)
            return;

        if (intermissionActive.Value)
        {
            StartNextRound();
        }
        else if (matchActive.Value)
        {
            EndCurrentRound();
        }
    }

    private void UpdateDisplayedSeconds()
    {
        int newDisplayedSecond =
            Mathf.Max(
                0,
                Mathf.CeilToInt(
                    serverSecondsRemaining
                )
            );

        if (
            newDisplayedSecond ==
            lastDisplayedSecond
        )
        {
            return;
        }

        lastDisplayedSecond =
            newDisplayedSecond;

        secondsRemaining.Value =
            newDisplayedSecond;
    }

    private void UpdatePostMatchCountdown()
    {
        serverSecondsRemaining -=
            Time.deltaTime;

        int newDisplayedSecond =
            Mathf.Max(
                0,
                Mathf.CeilToInt(
                    serverSecondsRemaining
                )
            );

        if (
            newDisplayedSecond !=
            lastDisplayedSecond
        )
        {
            lastDisplayedSecond =
                newDisplayedSecond;

            secondsRemaining.Value =
                newDisplayedSecond;
        }

        if (serverSecondsRemaining <= 0f)
        {
            secondsRemaining.Value = 0;
        }
    }

    private void EndCurrentRound()
    {
        if (
            currentRound.Value >=
            totalRounds
        )
        {
            FinishMatch();
            return;
        }

        matchActive.Value = false;
        intermissionActive.Value = true;

        serverSecondsRemaining =
            intermissionLength;

        secondsRemaining.Value =
            Mathf.CeilToInt(
                serverSecondsRemaining
            );

        lastDisplayedSecond =
            secondsRemaining.Value;

        SetAllCharactersFrozen(true);

        Debug.Log(
            "Round " +
            currentRound.Value +
            " ended. Intermission started."
        );
    }

    private void StartNextRound()
    {
        RespawnAllCharacters();

        currentRound.Value++;

        matchActive.Value = true;
        intermissionActive.Value = false;

        serverSecondsRemaining =
            roundLength;

        secondsRemaining.Value =
            Mathf.CeilToInt(
                serverSecondsRemaining
            );

        lastDisplayedSecond =
            secondsRemaining.Value;

        SetAllCharactersFrozen(false);
        ApplyDifficultyToAllSoldiers();

        Debug.Log(
            "Round " +
            currentRound.Value +
            " started."
        );
    }

    private void FinishMatch()
    {
        matchActive.Value = false;
        intermissionActive.Value = false;
        matchFinished.Value = true;

        serverSecondsRemaining =
            postMatchLength;

        secondsRemaining.Value =
            Mathf.CeilToInt(
                serverSecondsRemaining
            );

        lastDisplayedSecond =
            secondsRemaining.Value;

        SetAllCharactersFrozen(true);

        Debug.Log(
            "Match finished. Results displayed for " +
            postMatchLength +
            " seconds."
        );
    }

    private void ApplyFreezeStateOnThisClient()
    {
        bool shouldFreeze =
            intermissionActive.Value ||
            matchFinished.Value;

        if (
            localFreezeStateInitialized &&
            localFreezeState == shouldFreeze
        )
        {
            return;
        }

        localFreezeState =
            shouldFreeze;

        localFreezeStateInitialized = true;

        SetAllCharactersFrozen(shouldFreeze);
    }

    private void SetAllCharactersFrozen(
        bool frozen
    )
    {
        Health[] healthObjects =
            FindObjectsByType<Health>(
                FindObjectsSortMode.None
            );

        foreach (Health health in healthObjects)
        {
            if (health != null)
            {
                health.SetMatchFrozen(frozen);
            }
        }
    }

    private void RespawnAllCharacters()
    {
        Health[] healthObjects =
            FindObjectsByType<Health>(
                FindObjectsSortMode.None
            );

        foreach (Health health in healthObjects)
        {
            if (health != null)
            {
                health.ForceRespawnForRound();
            }
        }
    }

    public void RegisterDeath(
        GameObject victim,
        GameObject killer
    )
    {
        if (!IsServerInitialized)
            return;

        if (victim == null)
            return;

        BotAI victimBot =
            victim.GetComponentInParent<BotAI>();

        bool victimIsPlayer =
            victim.CompareTag("Player");

        bool killerIsPlayer =
            killer != null &&
            killer.CompareTag("Player");

        BotAI killerBot = null;

        if (killer != null)
        {
            killerBot =
                killer.GetComponentInParent<BotAI>();
        }

        if (
            victimBot != null &&
            killerIsPlayer
        )
        {
            soldiersKilled.Value++;

            totalXP.Value +=
                xpPerSoldierKill;

            Debug.Log(
                "Soldier killed. Total kills: " +
                soldiersKilled.Value +
                ". Total XP: " +
                totalXP.Value
            );
        }

        if (
            victimIsPlayer &&
            killerBot != null
        )
        {
            playerDeaths.Value++;

            Debug.Log(
                "Player death recorded. Total deaths: " +
                playerDeaths.Value
            );
        }
    }

    private void ApplyDifficultyToAllSoldiers()
    {
        BotAI[] soldiers =
            FindObjectsByType<BotAI>(
                FindObjectsSortMode.None
            );

        foreach (BotAI soldier in soldiers)
        {
            if (soldier != null)
            {
                soldier.ApplyRoundDifficulty(
                    currentRound.Value
                );
            }
        }
    }
}