using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerVision : MonoBehaviour
{
    public float mouseSensitivity = 0.1f;

    // Shared recoil recovery speed.
    public float recoilRecoverySpeed = 8f;

    private Camera playerCamera;

    private float yaw;
    private float pitch;
    private float recoilOffset;

    // Scope state
    private float normalFieldOfView;
    private float targetFieldOfView;
    private float scopeTransitionSpeed = 12f;

    private void Awake()
    {
        playerCamera = GetComponentInChildren<Camera>(true);

        if (playerCamera != null)
        {
            normalFieldOfView = playerCamera.fieldOfView;
            targetFieldOfView = normalFieldOfView;
        }
    }

    private void Start()
    {
        yaw = transform.eulerAngles.y;
        pitch = transform.eulerAngles.x;

        if (pitch > 180f)
        {
            pitch -= 360f;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        UpdateScopeZoom();

        if (Mouse.current == null)
            return;

        if (Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return;
        }

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (Cursor.lockState != CursorLockMode.Locked)
            return;

        Vector2 mouseDelta = Mouse.current.delta.ReadValue();

        yaw += mouseDelta.x * mouseSensitivity;
        pitch -= mouseDelta.y * mouseSensitivity;

        pitch = Mathf.Clamp(pitch, -80f, 80f);

        recoilOffset = Mathf.Lerp(
            recoilOffset,
            0f,
            recoilRecoverySpeed * Time.deltaTime
        );

        float finalPitch = pitch + recoilOffset;

        transform.rotation = Quaternion.Euler(
            finalPitch,
            yaw,
            0f
        );
    }

    public void AddRecoil(float recoilAmount)
    {
        recoilOffset -= recoilAmount;
    }

    public void SetScope(
        bool isScoped,
        float zoomMultiplier,
        float transitionSpeed
    )
    {
        if (playerCamera == null)
            return;

        scopeTransitionSpeed = transitionSpeed;

        if (isScoped)
        {
            targetFieldOfView =
                normalFieldOfView / Mathf.Max(zoomMultiplier, 1f);
        }
        else
        {
            targetFieldOfView = normalFieldOfView;
        }
    }

    private void UpdateScopeZoom()
    {
        if (playerCamera == null)
            return;

        playerCamera.fieldOfView = Mathf.Lerp(
            playerCamera.fieldOfView,
            targetFieldOfView,
            scopeTransitionSpeed * Time.deltaTime
        );
    }
}