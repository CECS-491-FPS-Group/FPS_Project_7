using UnityEngine;
using TMPro;

public class ShowCredits : MonoBehaviour
{
    GameObject playerObject;
    TextMeshProUGUI displayText;
    public GameObject roundHandler;
    TrackPlayerCurrency currencyTracker;

    public static GameObject FindParentWithTag(GameObject childObject, string tag)
    {
        Transform t = childObject.transform;
        while (t.parent != null)
        {
            if (t.parent.tag == tag) return t.parent.gameObject;
            t = t.parent.transform;
        }
        return null;
    }

    void Start()
    {
        playerObject = FindParentWithTag(gameObject, "Player");
        displayText = GetComponent<TextMeshProUGUI>();
        
        // Dynamically reconnect to the scene object
        if (roundHandler == null) roundHandler = GameObject.Find("RoundHandler");
        if (roundHandler != null) currencyTracker = roundHandler.GetComponent<TrackPlayerCurrency>();
    }

    void Update()
    {
        // Safety check to prevent the infinite crash loop
        if (!currencyTracker || !playerObject) return;
        displayText.text = "$" + currencyTracker.displayCredits(playerObject).ToString();
    }
}