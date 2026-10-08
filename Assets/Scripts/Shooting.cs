using UnityEngine;
using UnityEngine.InputSystem;
using StarterAssets;

[RequireComponent(typeof(AudioSource))]
public class HitscanShooter : MonoBehaviour
{
    public int damage = 40;
    public float range = 100f;
    public AudioClip gunshotClip;
    [Range(0f, 1f)] public float gunshotVolume = 1f;

    [Header("Effects")]
    [SerializeField] private GameObject bloodEffectPrefab;

    private Camera cam;
    private AudioSource audioSource;
    private PlayerBoosts playerBoosts;

    private void Awake()
    {
        cam = GetComponent<Camera>();

        playerBoosts =
            GetComponentInParent<PlayerBoosts>();
        if (cam == null)
        {
            cam = Camera.main;
        }

        audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
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
        if (cam == null)
        {
            return;
        }

        if (gunshotClip != null)
        {
            audioSource.PlayOneShot(gunshotClip, gunshotVolume);
        }

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
                int actualDamage =
                    damage;

                if (playerBoosts != null)
                {
                    actualDamage =
                        Mathf.RoundToInt(
                            damage *
                            playerBoosts.DamageMultiplier
                        );
                }

                health.TakeDamage(
                    actualDamage,
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