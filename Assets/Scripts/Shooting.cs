using UnityEngine;
using UnityEngine.InputSystem;

public class HitscanShooter : MonoBehaviour
{
    public int damage = 40;
    public float range = 100f;

    [Header("Effects")]
    [SerializeField] private GameObject bloodEffectPrefab;

    private Camera cam;

    private void Awake()
    {
        cam = GetComponent<Camera>();
    }

    private void Update()
    {
        if (
            Mouse.current != null &&
            Mouse.current.leftButton.wasPressedThisFrame
        )
        {
            Shoot();
        }
    }

    private void Shoot()
    {
        Vector3 crosshairPosition =
            new Vector3(
                Screen.width / 2f,
                Screen.height / 2f,
                0f
            );

        Ray ray =
            cam.ScreenPointToRay(
                crosshairPosition
            );

        if (
            Physics.Raycast(
                ray,
                out RaycastHit hit,
                range
            )
        )
        {
            Health health =
                hit.collider.GetComponentInParent<Health>();

            if (health != null)
            {
                health.TakeDamage(
                    damage,
                    transform.root.gameObject
                );

                SpawnBloodEffect(
                    hit.point,
                    hit.normal
                );
            }
        }

        Debug.DrawRay(
            ray.origin,
            ray.direction * range,
            Color.red,
            1f
        );
    }

    private void SpawnBloodEffect(
        Vector3 hitPoint,
        Vector3 hitNormal
    )
    {
        if (bloodEffectPrefab == null)
            return;

        Vector3 spawnPosition =
            hitPoint +
            hitNormal * 0.01f;

        Quaternion rotation =
            Quaternion.LookRotation(
                hitNormal
            );

        GameObject bloodEffect =
            Instantiate(
                bloodEffectPrefab,
                spawnPosition,
                rotation
            );

        Destroy(
            bloodEffect,
            2f
        );
    }
}