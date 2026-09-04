using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.LowLevel;

public class TrackPlayerCurrency : MonoBehaviour
{
    // yippee i love singletons!
    public static TrackPlayerCurrency instance {get; private set;}
    public Dictionary<GameObject, int> playerCredits;
    GameObject[] playerList;

    void initialize()
    {
        if (instance != null && instance != this) 
        { 
            Destroy(this); 
        } 
        else 
        { 
            // new singleton created. Initialize me!
            instance = this;
            initializePlayerList();
            initializeCreditsDict();
        } 
    }
    void initializePlayerList()
    {
        playerList = GameObject.FindGameObjectsWithTag("Player");

        // marking each player as don't destroy on load
        // this makes the dictionary of currency work
        // this is admittedly a weird place to do it. happy debugging!
        // this has the fun side effect of loading in ANOTHER player when the scene restarts
        // foreach (GameObject player in playerList)
        // {
        //     DontDestroyOnLoad(player);
        // }
    }

    void initializeCreditsDict()
    {
        playerCredits = new Dictionary<GameObject, int>();
        for (int i = 0; i < playerList.Length; i++)
        {
            playerCredits.Add(playerList[i], 0);
        }
    }

    public void testIncrementPlayerCredits()
    {
        foreach (GameObject player in playerList)
        {
            playerCredits[player] += 67;
        }
    }

    public int displayCredits(GameObject player)
    {
        return playerCredits[player];
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        initialize();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
