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
        timerText = timer.GetComponent<RoundTimer>();
    }

    // Update is called once per frame
    void Update()
    {
        if (timerText.timerIsRunning)
        {
            displayText.text = timerText.currentStateName + "\n" + timerText.timeText;
        }
    }
}
