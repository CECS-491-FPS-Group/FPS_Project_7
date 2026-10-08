using UnityEngine;
using UnityEngine.SceneManagement;
public class MainMenuButtons : MonoBehaviour
{
    public AudioClip clickSound;
    [Range(0f, 1f)] public float clickVolume = 1f;

    private void Awake()
    {
        ButtonClickSound buttonClickSound = GetComponent<ButtonClickSound>();
        if (buttonClickSound == null)
        {
            buttonClickSound = gameObject.AddComponent<ButtonClickSound>();
        }

        buttonClickSound.Configure(clickSound, clickVolume);
    }

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
