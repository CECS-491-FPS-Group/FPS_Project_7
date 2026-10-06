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
        
        FindTimer();
    }

    void Update()
    {
        if (!timerText) FindTimer();
        if (!timerText || !displayText) return;

        displayText.text = timerText.currentStateName + "\n" + timerText.timeText;
    }

    private void FindTimer()
    {
        // Accept either the RoundHandler parent or its Timer child as the reference.
        if (timer == null) timer = GameObject.Find("RoundHandler");
        if (timer != null) timerText = timer.GetComponentInChildren<RoundTimer>(true);
    }
}
