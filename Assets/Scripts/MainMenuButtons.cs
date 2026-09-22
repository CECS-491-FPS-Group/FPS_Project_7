using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuButtons : MonoBehaviour
{
    public GameObject mainMenuCanvas;
    public GameObject theaterMenuCanvas;

    void Start()
    {
        
    }

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

    public void OpenTheater()
    {
        mainMenuCanvas.SetActive(false);
        theaterMenuCanvas.SetActive(true);
    }

    public void CloseTheater()
    {
        theaterMenuCanvas.SetActive(false);
        mainMenuCanvas.SetActive(true);
    }
}