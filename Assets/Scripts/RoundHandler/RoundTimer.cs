using System.Collections.Generic;
using Unity.VectorGraphics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class RoundTimer : MonoBehaviour
{
    // 20s pregame, 360s (5m) game, 10s postgame
    public bool timerIsRunning;
    float timeRemaining;
    public string timeText;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        timerIsRunning = true;
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
                timeRemaining = 0;
                timerIsRunning = false;
            }
        }
    }

    // just doing a bit of floating point math every frame B)
    // prio 1 for optimization, just getting it to work for now
    void DisplayTime(float timeToDisplay)
    {
        timeToDisplay += 1;

        float minutes = Mathf.FloorToInt(timeToDisplay / 60); 
        float seconds = Mathf.FloorToInt(timeToDisplay % 60);

        timeText = string.Format("{0:00}:{1:00}", minutes, seconds);
    }
}
