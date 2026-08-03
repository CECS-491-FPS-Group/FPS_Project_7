using UnityEngine;
using UnityEngine.SceneManagement;

public class LobbyBehavior : MonoBehaviour
{
    public bool isReady;
    public GameObject readyButton;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        isReady = false;
        // readyButton = GameObject.Find("LobbyPanel").Find("ReadyButton").GetComponent<Button>();
    }

    public void onReady()
    {
        if (!isReady)
        {
            isReady = true;
        }
        else
        {
            isReady = false;
        }
    }

    public void startGame()
    {
        if (isReady)
        {
            SceneManager.LoadScene("Assets/Starter Assets/Sample/FirstPersonController/Playground.unity");
        }
    }
}
