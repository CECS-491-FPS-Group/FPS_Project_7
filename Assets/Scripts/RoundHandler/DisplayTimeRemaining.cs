using TMPro;
using UnityEngine;

public class DisplayTimeRemaining : MonoBehaviour
{
    public GameObject timer;
    TextMeshProUGUI displayText;
    RoundTimer timerText;

    void Start()
    {
        displayText = GetComponent<TextMeshProUGUI>();
        
        // Dynamically reconnect to the scene object
        if (timer == null) timer = GameObject.Find("RoundHandler");
        if (timer != null) timerText = timer.GetComponent<RoundTimer>();
    }

    void Update()
    {
        // Safety check to prevent the infinite crash loop
        if (!timerText) return;
        
        if (timerText.timerIsRunning)
        {
            displayText.text = timerText.currentStateName + "\n" + timerText.timeText;
        }
    }
}