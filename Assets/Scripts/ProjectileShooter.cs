using UnityEngine;
using UnityEngine.InputSystem;

public class ProjectileShooter : MonoBehaviour
{
    private Camera cam;
    private PlayerVision playerVision;

    // Projectile
    public GameObject projectilePrefab;
    public float projectileSpeed = 25f;

    // Firing
    public float fireRate = 2f;

    private float nextFireTime;

    // Recoil
    public float recoilAmount = 1f;

    private void Awake()
    {
        playerVision = GetComponentInParent<PlayerVision>();
    }

    public void SetWeaponReferences(Camera playerCamera, PlayerVision vision)
    {
        cam = playerCamera;
        playerVision = vision;
    }

    private void Update()
    {
        if (Mouse.current == null)
            return;

        bool wantsToShoot = Mouse.current.leftButton.wasPressedThisFrame;

        if (wantsToShoot && Time.time >= nextFireTime)
        {
            Shoot();
        }
    }

    private void Shoot()
    {
        if (cam == null)
        {
            Debug.LogWarning("This weapon has no player camera assigned.");
            return;
        }

        if (projectilePrefab == null)
        {
            Debug.LogWarning("This weapon has no projectile prefab assigned.");
            return;
        }

        float timeBetweenShots = 1f / Mathf.Max(fireRate, 0.01f);
        nextFireTime = Time.time + timeBetweenShots;

        Vector3 crosshairPosition = new Vector3(
            Screen.width / 2f,
            Screen.height / 2f,
            0f
        );

        Ray ray = cam.ScreenPointToRay(crosshairPosition);

        GameObject projectileObject = Instantiate(
            projectilePrefab,
            ray.origin + ray.direction * 0.5f,
            Quaternion.LookRotation(ray.direction)
        );

        Rigidbody body = projectileObject.GetComponent<Rigidbody>();

        if (body != null)
        {
            body.linearVelocity = ray.direction * projectileSpeed;
        }

        if (playerVision != null)
        {
            playerVision.AddRecoil(recoilAmount);
        }
    }
}
