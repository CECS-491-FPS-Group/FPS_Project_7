using UnityEngine;
using TMPro;

public class ShowCredits : MonoBehaviour
{
    GameObject playerObject;
    TextMeshProUGUI displayText;
    public GameObject roundHandler;
    TrackPlayerCurrency currencyTracker;
    // https://discussions.unity.com/t/traverse-up-the-hierarchy-to-find-first-parent-with-specific-tag/7956/5
    // lol
    public static GameObject FindParentWithTag(GameObject childObject, string tag)
    {
        Transform t = childObject.transform;
        while (t.parent != null)
        {
            if (t.parent.tag == tag)
            {
                return t.parent.gameObject;
            }
            t = t.parent.transform;
        }
        return null; // Could not find a parent with given tag.
    }

    void displayCredits()
    {
        int credits = currencyTracker.displayCredits(playerObject);
        displayText.text = "$" + credits.ToString();
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        playerObject = FindParentWithTag(gameObject, "Player");
        displayText = GetComponent<TextMeshProUGUI>();
        currencyTracker = roundHandler.GetComponent<TrackPlayerCurrency>();
    }

    // Update is called once per frame
    void Update()
    {
        displayCredits();
    }
}
