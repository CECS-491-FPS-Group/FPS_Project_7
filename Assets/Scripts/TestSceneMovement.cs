using UnityEngine;
using UnityEngine.InputSystem;

// Simple WASD movement for offline test scenes where the camera has no player controller.
public class TestSceneMovement : MonoBehaviour
{
    public float moveSpeed = 5f;

    private void Update()
    {
        if (Keyboard.current == null || Cursor.lockState != CursorLockMode.Locked)
            return;

        float x = (Keyboard.current.dKey.isPressed ? 1f : 0f) - (Keyboard.current.aKey.isPressed ? 1f : 0f);
        float z = (Keyboard.current.wKey.isPressed ? 1f : 0f) - (Keyboard.current.sKey.isPressed ? 1f : 0f);

        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
        Vector3 move = right * x + forward * z;

        transform.position += move.normalized * moveSpeed * Time.deltaTime;
    }
}
