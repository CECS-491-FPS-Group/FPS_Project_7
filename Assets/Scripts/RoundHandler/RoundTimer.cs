using System.Collections.Generic;
using FishNet.Serializing.Helping;
using Unity.VectorGraphics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;



public class RoundTimer : MonoBehaviour
{
    // 20s pregame, 360s (5m) game, 10s postgame
    public enum gameStates
    {
        PREGAME = 0,
        GAME = 1,
        POSTGAME = 2
    }
    
    public bool timerIsRunning;
    float timeRemaining;
    public string timeText;
    public gameStates currentState;
    public string currentStateName;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        timerIsRunning = true;
        // start pregame when scene is loaded
        currentState = gameStates.PREGAME;
        currentStateName = "Pregame";
        timeRemaining = 20; 
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
                // if in pregame, switch to game
                if (currentState == gameStates.PREGAME)
                {
                    timeRemaining = 5;
                    currentState = gameStates.GAME;
                    currentStateName = "Game";
                }

                // if in game, switch to postgame
                else if (currentState == gameStates.GAME)
                {
                    timeRemaining = 10;
                    currentState = gameStates.POSTGAME;
                    currentStateName = "Postgame";
                }

                // if in postgame, reload scene
                else if (currentState == gameStates.POSTGAME)
                {
                    SceneManager.LoadScene("RoundImplementation");
                }
            }
        }
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
}
