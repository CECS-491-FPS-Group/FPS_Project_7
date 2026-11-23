
using UnityEngine;
using UnityEngine.InputSystem;

public enum ProjectileDetonationMode
{
    EveryBounce,
    FinalBounce,
    Timed
}

public class ProjectileShooter : MonoBehaviour
{
    private Camera cam;
    private PlayerVision playerVision;

    // Projectile
    public GameObject projectilePrefab;
    public float projectileSpeed = 25f;
    public float projectileLifetime = 10f;

    // Arc
    public float launchAngle = 10f;

    // Bounce
    public ProjectileDetonationMode detonationMode =
        ProjectileDetonationMode.FinalBounce;

    public int maxBounces = 3;
    public float bounciness = 0.8f;

    // Timed explosion
    public float detonationDelay = 3f;

    // Explosion
    public float explosionRadius = 5f;
    public float explosionDamage = 50f;
    public float explosionForce = 700f;

    // Firing
    public float fireRate = 2f;
    private float nextFireTime;

    // Recoil
    public float recoilAmount = 1f;

    private void Awake()
    {
        playerVision = GetComponentInParent<PlayerVision>();
    }

    public void SetWeaponReferences(
        Camera playerCamera,
        PlayerVision vision)
    {
        cam = playerCamera;
        playerVision = vision;
    }

    private void Update()
    {
        if (Mouse.current == null)
            return;

        bool wantsToShoot =
            Mouse.current.leftButton.wasPressedThisFrame;

        if (wantsToShoot && Time.time >= nextFireTime)
        {
            Shoot();
        }
    }

    private void Shoot()
    {
        if (cam == null)
        {
            Debug.LogWarning(
                "This weapon has no player camera assigned."
            );
            return;
        }

        if (projectilePrefab == null)
        {
            Debug.LogWarning(
                "This weapon has no projectile prefab assigned."
            );
            return;
        }

        float timeBetweenShots =
            1f / Mathf.Max(fireRate, 0.01f);

        nextFireTime = Time.time + timeBetweenShots;

        Vector3 crosshairPosition = new Vector3(
            Screen.width / 2f,
            Screen.height / 2f,
            0f
        );

        Ray ray = cam.ScreenPointToRay(crosshairPosition);

        Vector3 launchDirection =
            Quaternion.AngleAxis(
                -launchAngle,
                cam.transform.right
            ) * ray.direction;

        GameObject projectileObject = Instantiate(
            projectilePrefab,
            ray.origin + ray.direction * 0.5f,
            Quaternion.LookRotation(launchDirection)
        );

        Rigidbody body =
            projectileObject.GetComponent<Rigidbody>();

        if (body == null)
        {
            Debug.LogWarning(
                "Projectile prefab had no Rigidbody, so one was added."
            );

            body = projectileObject.AddComponent<Rigidbody>();
        }

        body.isKinematic = false;
        body.useGravity = true;
        body.collisionDetectionMode =
            CollisionDetectionMode.ContinuousDynamic;

        body.linearVelocity =
            launchDirection * projectileSpeed;

        // Ignore collisions with the player
        Collider projectileCollider =
            projectileObject.GetComponentInChildren<Collider>();

        if (projectileCollider != null)
        {
            Collider[] ownerColliders =
                transform.root.GetComponentsInChildren<Collider>();

            foreach (Collider ownerCollider in ownerColliders)
            {
                Physics.IgnoreCollision(
                    projectileCollider,
                    ownerCollider
                );
            }
        }

        // Add bounce behavior
        ProjectileBounce bounce =
            projectileObject.GetComponent<ProjectileBounce>();

        if (bounce == null)
        {
            bounce =
                projectileObject.AddComponent<ProjectileBounce>();
        }

        bounce.Setup(
            maxBounces,
            bounciness,
            explosionRadius,
            explosionDamage,
            explosionForce,
            projectileLifetime,
            detonationMode,
            detonationDelay
        );

        Debug.Log(
            "Fired projectile with velocity " + body.linearVelocity
        );

        if (playerVision != null)
        {
            playerVision.AddRecoil(recoilAmount);
        }
    }
}
