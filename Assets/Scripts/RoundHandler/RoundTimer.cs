using System.Collections.Generic;
using FishNet.Serializing.Helping;
using Unity.VectorGraphics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;



public class RoundTimer : MonoBehaviour
{
    public int preGameLength;
    public int gameLength;
    public int postGameLength;
    // 20s pregame, 360s (5m) game, 10s postgame
    [HideInInspector]
    public enum gameStates
    {
        PREGAME = 0,
        GAME = 1,
        POSTGAME = 2
    }
    [HideInInspector]
    public bool timerIsRunning;
    [HideInInspector]
    public string timeText;
    [HideInInspector]
    public gameStates currentState;
    [HideInInspector]
    public string currentStateName;
    float timeRemaining;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        startGame();
    }

    // Update is called once per frame
    void Update()
    {
        if (timerIsRunning)
        {
            if (timeRemaining > 0)
            {
                timeRemaining -= Time.deltaTime;
                DisplayTime(timeRemaining);
            }
            else
            {
                updateGameState();
            }
        }
    }

    void startGame()
    {
        timerIsRunning = true;
        // start pregame when scene is loaded
        currentState = gameStates.PREGAME;
        currentStateName = "Pregame";
        timeRemaining = preGameLength; 
    }

    // just doing a bit of floating point math every frame B) very cool and efficient
    // prio 1 for optimization later
    // just getting it to work for now lol
    void DisplayTime(float timeToDisplay)
    {
        timeToDisplay += 1;

        float minutes = Mathf.FloorToInt(timeToDisplay / 60);
        float seconds = Mathf.FloorToInt(timeToDisplay % 60);

        timeText = string.Format("{0:00}:{1:00}", minutes, seconds);
    }

    void updateGameState()
    {
        switch (currentState)
        {
            // if we're in the pregame, switch to main game
            case gameStates.PREGAME:
                timeRemaining = gameLength;
                currentState = gameStates.GAME;
                currentStateName = "Game";
                break;
            
            // if we're in the main game, switch to postgame
            case gameStates.GAME:
                timeRemaining = postGameLength;
                currentState = gameStates.POSTGAME;
                currentStateName = "Postgame";
                break;

            // if we're in the postgame, reload the scene
            case gameStates.POSTGAME:
                SceneManager.LoadScene("RoundImplementation");
                break;
        }
    }
}
