using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    private PlayerInput playerInput;
    private PlayerInput.GameplayActions gameplayActions;

    void Awake()
    {
        playerInput = new PlayerInput();
        gameplayActions = new PlayerInput.GameplayActions();
    }

    void Update()
    {
        
    }

    private void OnEnable()
    {
        gameplayActions.Enable();
    }
    private void OnDisable()
    {
        gameplayActions.Disable();
    }
}
