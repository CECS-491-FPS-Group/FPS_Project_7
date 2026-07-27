using UnityEngine;
using UnityEngine.SceneManagement;
public class MainMenuButtons : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void OpenLobby()
    {
        SceneManager.LoadScene("LobbyScene_v1");
    }

    public void OpenMainMenu()
    {
        SceneManager.LoadScene("MainMenu");
    }
}
