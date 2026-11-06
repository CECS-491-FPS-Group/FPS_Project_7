using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class HitscanShooter : MonoBehaviour
{
    public enum ScopeZoom
    {
        Zoom1_5x,
        Zoom2x,
        Zoom4x,
        Zoom8x,
        Zoom12x
    }

    private Camera cam;
    private PlayerVision playerVision;

    // Damage
    public int damage = 40;
    public float range = 100f;

    // Firing
    public bool isAutomatic = true;
    public float fireRate = 10f;

    private float nextFireTime;

    // Shotgun
    public bool isShotgun = false;
    public int pelletsPerShot = 8;

    // Accuracy
    public float hipSpread = 1.5f;
    public float scopedSpread = 0.2f;
    public float bloomPerShot = 0.4f;
    public float maxBloom = 4f;
    public float bloomRecoveryDelay = 0.15f;
    public float bloomRecoveryRate = 8f;
    public float scopedBloomMultiplier = 0.5f;
    public bool firstShotAccurate = true;

    private float currentBloom;
    private float lastShotTime;

    // Ammo / Reload
    public int magazineSize = 30;
    public float reloadTime = 3f;

    private int currentAmmo;
    private bool isReloading;

    // Recoil
    public float recoilAmount = 2f;

    // Scope
    public bool canScope = false;
    public ScopeZoom scopeZoom = ScopeZoom.Zoom2x;
    public float scopeTransitionSpeed = 12f;

    private bool isScoped;

    // Hitmarkers
    public GameObject hitMarker;
    public int maxHitMarkers = 8;
    public float hitMarkerMinDistance = 0.1f;

    private Queue<GameObject> hitMarkers = new Queue<GameObject>();

    // Audio
    public AudioClip gunshotClip;
    [Range(0f, 1f)] public float gunshotVolume = 1f;
    public AudioClip hitConfirmClip;
    [Range(0f, 1f)] public float hitConfirmVolume = 0.7f;

    private AudioSource audioSource;
    private ShotAudioBroadcaster shotBroadcaster;
    private float nextHitConfirmTime = float.NegativeInfinity;
    private static AudioClip generatedHitConfirmClip;

    private const float HitConfirmCooldown = 0.12f;

    // Read by WeaponHolster for the HUD.
    public int CurrentAmmo => currentAmmo;
    public int MagazineSize => magazineSize;
    public bool IsReloading => isReloading;
    public float CurrentSpread => GetCurrentSpread();

    private void Awake()
    {
        currentAmmo = magazineSize;
        playerVision = GetComponentInParent<PlayerVision>();
        shotBroadcaster = GetComponentInParent<ShotAudioBroadcaster>();

        // Legacy scenes have a single shooter with no WeaponHolster to hand it a camera.
        // Players with a holster must not self-assign one, or leftover shooters on the
        // prefab would fire alongside the equipped weapon.
        if (cam == null && GetComponentInParent<WeaponHolster>() == null)
        {
            cam = GetComponentInChildren<Camera>();
            if (cam == null)
            {
                cam = Camera.main;
            }
        }

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        if (hitConfirmClip == null)
        {
            if (generatedHitConfirmClip == null)
            {
                generatedHitConfirmClip = CreateHitConfirmClip();
            }

            hitConfirmClip = generatedHitConfirmClip;
        }
    }

    private void OnDisable()
    {
        // Switching away from a scoped weapon returns the camera to normal.
        if (isScoped && playerVision != null)
        {
            playerVision.SetScope(false, 1f, scopeTransitionSpeed);
        }

        isScoped = false;
        currentBloom = 0f;
    }

    public void SetWeaponReferences(Camera playerCamera, PlayerVision vision)
    {
        cam = playerCamera;
        playerVision = vision;
    }

    private void Update()
    {
        HandleScopeInput();
        UpdateBloom();

        if (Keyboard.current != null &&
            Keyboard.current.rKey.wasPressedThisFrame)
        {
            StartReload();
        }

        if (isReloading || Mouse.current == null)
            return;

        bool wantsToShoot = isAutomatic
            ? Mouse.current.leftButton.isPressed
            : Mouse.current.leftButton.wasPressedThisFrame;

        if (wantsToShoot && Time.time >= nextFireTime)
        {
            if (currentAmmo > 0)
            {
                Shoot();
            }
            else
            {
                StartReload();
            }
        }
    }

    private void HandleScopeInput()
    {
        if (!canScope || Mouse.current == null)
            return;

        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            isScoped = !isScoped;

            if (playerVision != null)
            {
                playerVision.SetScope(
                    isScoped,
                    GetScopeMultiplier(),
                    scopeTransitionSpeed
                );
            }
        }
    }

    private float GetScopeMultiplier()
    {
        switch (scopeZoom)
        {
            case ScopeZoom.Zoom1_5x:
                return 1.5f;

            case ScopeZoom.Zoom2x:
                return 2f;

            case ScopeZoom.Zoom4x:
                return 4f;

            case ScopeZoom.Zoom8x:
                return 8f;

            case ScopeZoom.Zoom12x:
                return 12f;

            default:
                return 1f;
        }
    }

    private void UpdateBloom()
    {
        if (currentBloom <= 0f)
            return;

        if (Time.time >= lastShotTime + bloomRecoveryDelay)
        {
            currentBloom = Mathf.Max(
                0f,
                currentBloom - bloomRecoveryRate * Time.deltaTime
            );
        }
    }

    private float GetCurrentSpread()
    {
        if (isScoped)
        {
            return scopedSpread + currentBloom * scopedBloomMultiplier;
        }

        return hipSpread + currentBloom;
    }

    private void Shoot()
    {
        if (cam == null)
        {
            Debug.LogWarning("This weapon has no player camera assigned.");
            return;
        }

        currentAmmo--;

        float timeBetweenShots = 1f / Mathf.Max(fireRate, 0.01f);
        nextFireTime = Time.time + timeBetweenShots;
        lastShotTime = Time.time;

        Vector3 crosshairPosition = new Vector3(
            Screen.width / 2f,
            Screen.height / 2f,
            0f
        );

        Ray centerRay = cam.ScreenPointToRay(crosshairPosition);

        // Spread is read before this shot's bloom is added.
        float spread = GetCurrentSpread();

        // The first shot after a pause (no bloom built up) has no spread.
        // Shotguns are excluded so their pellets still fan out.
        if (firstShotAccurate && !isShotgun && currentBloom <= 0f)
        {
            spread = 0f;
        }

        int bulletCount = isShotgun ? Mathf.Max(1, pelletsPerShot) : 1;

        // One sound per trigger pull, so shotgun pellets don't stack into a single loud blast.
        if (gunshotClip != null)
        {
            audioSource.PlayOneShot(gunshotClip, gunshotVolume * GameAudioSettings.SfxVolume);
        }

        if (shotBroadcaster != null)
        {
            shotBroadcaster.NotifyShot(gameObject);
        }

        bool damagedSomething = false;

        for (int i = 0; i < bulletCount; i++)
        {
            damagedSomething |= FireBullet(centerRay, spread);
        }

        if (damagedSomething)
        {
            PlayHitConfirm();
        }

        currentBloom = Mathf.Min(currentBloom + bloomPerShot, maxBloom);

        if (playerVision != null)
        {
            playerVision.AddRecoil(recoilAmount);
        }

        if (currentAmmo <= 0)
        {
            StartReload();
        }
    }

    private bool FireBullet(Ray centerRay, float spreadDegrees)
    {
        Vector3 direction = ApplySpread(centerRay.direction, spreadDegrees);
        Ray ray = new Ray(centerRay.origin, direction);
        bool damaged = false;

        if (Physics.Raycast(ray, out RaycastHit hit, range))
        {
            SpawnHitMarker(hit);

            Health health = hit.collider.GetComponentInParent<Health>();

            if (health != null)
            {
                health.TakeDamage(damage);
                damaged = true;
            }
        }

        Debug.DrawRay(ray.origin, ray.direction * range, Color.red, 1f);
        return damaged;
    }

    private void PlayHitConfirm()
    {
        if (Time.time < nextHitConfirmTime)
            return;

        audioSource.PlayOneShot(hitConfirmClip, hitConfirmVolume * GameAudioSettings.SfxVolume);
        nextHitConfirmTime = Time.time + HitConfirmCooldown;
    }

    private static AudioClip CreateHitConfirmClip()
    {
        const int sampleRate = 44100;
        const float duration = 0.14f;
        const float startFrequency = 1450f;
        const float endFrequency = 920f;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];
        float frequencySlope = (endFrequency - startFrequency) / duration;

        for (int i = 0; i < sampleCount; i++)
        {
            float time = (float)i / sampleRate;
            float phase = 2f * Mathf.PI * (startFrequency * time + 0.5f * frequencySlope * time * time);
            float envelope = Mathf.Exp(-22f * time) * Mathf.Clamp01(time / 0.002f);
            float transient = 0.22f * Mathf.Sin(2f * Mathf.PI * 2300f * time) * Mathf.Exp(-65f * time);
            samples[i] = envelope * (0.72f * Mathf.Sin(phase) + transient);
        }

        AudioClip clip = AudioClip.Create("Hit Confirm", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private Vector3 ApplySpread(Vector3 forward, float spreadDegrees)
    {
        if (spreadDegrees <= 0f)
            return forward;

        Vector2 offset = Random.insideUnitCircle * spreadDegrees;

        Quaternion spreadRotation =
            Quaternion.AngleAxis(offset.x, cam.transform.up) *
            Quaternion.AngleAxis(-offset.y, cam.transform.right);

        return spreadRotation * forward;
    }

    private void SpawnHitMarker(RaycastHit hit)
    {
        if (hitMarker == null)
            return;

        foreach (GameObject marker in hitMarkers)
        {
            if (marker != null &&
                Vector3.Distance(marker.transform.position, hit.point)
                < hitMarkerMinDistance)
            {
                return;
            }
        }

        GameObject markerObject = Instantiate(
            hitMarker,
            hit.point + hit.normal * 0.005f,
            Quaternion.LookRotation(-hit.normal)
        );

        hitMarkers.Enqueue(markerObject);

        if (hitMarkers.Count > maxHitMarkers)
        {
            GameObject oldest = hitMarkers.Dequeue();

            if (oldest != null)
            {
                Destroy(oldest);
            }
        }
    }

    private void StartReload()
    {
        if (isReloading || currentAmmo >= magazineSize)
            return;

        StartCoroutine(Reload());
    }

    private IEnumerator Reload()
    {
        isReloading = true;

        yield return new WaitForSeconds(reloadTime);

        currentAmmo = magazineSize;
        isReloading = false;
    }
}