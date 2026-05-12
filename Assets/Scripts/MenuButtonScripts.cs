using UnityEngine;

public class MenuButtonScripts : MonoBehaviour
{

    public void quitGame()
        {
            Debug.Log("Quitting Game...");
            Application.Quit();
        }
}
