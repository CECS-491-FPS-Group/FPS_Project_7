using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(AudioSource))]
public class HitscanShooter : MonoBehaviour
{
    public int damage = 40;
    public float range = 100f;
    public AudioClip gunshotClip;
    [Range(0f, 1f)] public float gunshotVolume = 1f;

    private Camera cam;
    private AudioSource audioSource;

    public float recoilAmount = 2f;
    public float recoilSpeed = 10f;
    public float returnSpeed = 5f;

    private Vector3 originalRotation;
    private float currentRecoil;

    public GameObject hitMarker;
    public int maxHitMarkers = 8;
    private Queue<GameObject> hitMarkers = new Queue<GameObject>();

    private void Awake()
    {
        cam = GetComponentInChildren<Camera>();
        if (cam == null)
        {
            cam = Camera.main;
        }

        audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        if (cam != null)
        {
            originalRotation = cam.transform.localEulerAngles;
        }
    }

    private void Update()
    {
        if (Mouse.current != null && Mouse.current.leftButton.isPressed)
        {
            Shoot();
        }

        HandleRecoil();
    }



    private void ApplyRecoil()
    {
        currentRecoil -= recoilAmount;
    }


    private void HandleRecoil()
    {
        if (cam == null)
        {
            return;
        }

        currentRecoil = Mathf.Lerp(
            currentRecoil,
            0,
            recoilSpeed * Time.deltaTime
        );

        cam.transform.localRotation = Quaternion.Euler(
            originalRotation.x + currentRecoil,
            originalRotation.y,
            originalRotation.z
        );
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

        Vector3 crosshairPosition = new Vector3(
            Screen.width / 2f,
            Screen.height / 2f,
            0f
        );

        Ray ray = cam.ScreenPointToRay(crosshairPosition);

        if (Physics.Raycast(ray, out RaycastHit hit, range))
        {
            // Spawn hit marker
            if (hitMarker != null)
            {
                GameObject marker = Instantiate(
                    hitMarker,
                    hit.point + hit.normal * 0.01f,
                    Quaternion.LookRotation(-hit.normal)
                );

                hitMarkers.Enqueue(marker);

                // Remove oldest marker after 8
                if (hitMarkers.Count > maxHitMarkers)
                {
                    GameObject oldest = hitMarkers.Dequeue();

                    if (oldest != null)
                    {
                        Destroy(oldest);
                    }
                }
            }

            // Damage enemies
            Health health = hit.collider.GetComponentInParent<Health>();

            if (health != null)
            {
                health.TakeDamage(damage);
            }
        }


        ApplyRecoil();

        Debug.DrawRay(ray.origin, ray.direction * range, Color.red, 1f);
    }
}