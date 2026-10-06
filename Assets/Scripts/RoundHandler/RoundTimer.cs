using System;
using FishNet;
using FishNet.Broadcast;
using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;

public class RoundTimer : MonoBehaviour
{
    [Min(0)] public int preGameLength = 15;
    [Min(0)] public int gameLength = 30;
    [Min(0)] public int postGameLength = 15;

    [Header("Passive Income")]
    [Min(0)] public int passiveIncomeAmount = 50;
    [Min(0.1f)] public float passiveIncomeInterval = 30f;

    public enum gameStates { PREGAME = 0, GAME = 1, POSTGAME = 2 }

    [HideInInspector] public bool timerIsRunning;
    [HideInInspector] public string timeText;
    [HideInInspector] public gameStates currentState;
    [HideInInspector] public string currentStateName;

    public struct RoundSnapshot : IBroadcast
    {
        public gameStates Phase;
        public double Deadline;
        public bool Running;
    }

    private NetworkManager _networkManager;
    private double _deadline;
    private double _nextSnapshotTime;
    private bool _serverStartedRound;
    private double _nextIncomeTime = double.PositiveInfinity;

    private double ServerTime => _networkManager.TimeManager.TicksToTime(_networkManager.TimeManager.Tick);

    private void Start()
    {
        timerIsRunning = false;
        currentStateName = "Waiting";
        timeText = "00:00";
        _networkManager = InstanceFinder.NetworkManager;
        if (_networkManager == null)
        {
            Debug.LogError("[RoundTimer] No NetworkManager found.", this);
            enabled = false;
            return;
        }

        _networkManager.ClientManager.RegisterBroadcast<RoundSnapshot>(OnSnapshot);
    }

    private void OnDestroy()
    {
        if (_networkManager != null)
            _networkManager.ClientManager.UnregisterBroadcast<RoundSnapshot>(OnSnapshot);
    }

    private void Update()
    {
        if (_networkManager == null) return;

        double now = ServerTime;
        if (_networkManager.IsServerStarted)
        {
            if (!_serverStartedRound)
            {
                _serverStartedRound = true;
                BeginPhase(gameStates.PREGAME, now, preGameLength);
            }

            // Carry the deadline forward so a slow frame does not extend a phase.
            while (timerIsRunning && now >= _deadline)
            {
                switch (currentState)
                {
                    case gameStates.PREGAME:
                        BeginPhase(gameStates.GAME, _deadline, gameLength);
                        break;
                    case gameStates.GAME:
                        BeginPhase(gameStates.POSTGAME, _deadline, postGameLength);
                        break;
                    case gameStates.POSTGAME:
                        timerIsRunning = false;
                        SendSnapshot();
                        break;
                }
            }

            // Phase transitions take precedence, including at the GAME end boundary.
            if (timerIsRunning && currentState == gameStates.GAME && now >= _nextIncomeTime)
            {
                if (TrackPlayerCurrency.instance != null)
                    TrackPlayerCurrency.instance.AddCurrencyToRegisteredPlayers(passiveIncomeAmount);

                // One payout at most per frame; never burst-catch-up missed intervals.
                _nextIncomeTime = now + Math.Max(0.1d, passiveIncomeInterval);
            }

            // Continue after stopping so late listeners also receive Postgame 00:00.
            if (now >= _nextSnapshotTime) SendSnapshot();
        }

        double remaining = timerIsRunning ? Math.Max(0d, _deadline - now) : 0d;
        int seconds = (int)Math.Ceiling(remaining);
        timeText = string.Format("{0:00}:{1:00}", seconds / 60, seconds % 60);
    }

    private void BeginPhase(gameStates phase, double startTime, int duration)
    {
        SetPhase(phase);
        _deadline = startTime + Math.Max(0, duration);
        _nextIncomeTime = phase == gameStates.GAME
            ? startTime + Math.Max(0.1d, passiveIncomeInterval)
            : double.PositiveInfinity;
        timerIsRunning = true;
        SendSnapshot();
    }

    private void SetPhase(gameStates phase)
    {
        currentState = phase;
        currentStateName = phase == gameStates.PREGAME ? "Pregame" :
            phase == gameStates.GAME ? "Game" : "Postgame";
    }

    private void SendSnapshot()
    {
        _networkManager.ServerManager.Broadcast(new RoundSnapshot
        {
            Phase = currentState,
            Deadline = _deadline,
            Running = timerIsRunning
        }, true, Channel.Reliable);
        _nextSnapshotTime = ServerTime + 1d;
    }

    private void OnSnapshot(RoundSnapshot snapshot, Channel channel)
    {
        // The Host uses its authoritative state, not delayed copies of its own messages.
        if (_networkManager.IsServerStarted) return;
        SetPhase(snapshot.Phase);
        _deadline = snapshot.Deadline;
        timerIsRunning = snapshot.Running;
    }
}
