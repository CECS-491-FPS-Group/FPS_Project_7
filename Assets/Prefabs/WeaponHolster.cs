using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class WeaponHolster : MonoBehaviour
{
    // Put your three weapon GameObjects here.
    public GameObject[] weapons = new GameObject[3];

    private Camera playerCamera;
    private PlayerVision playerVision;

    private TMP_Text ammoText;
    private TMP_Text weaponNumberText;

    private HitscanShooter activeWeapon;
    private ProjectileShooter activeProjectileWeapon;
    private int currentWeaponIndex;

    private void Awake()
    {
        playerCamera = GetComponentInChildren<Camera>(true);
        playerVision = GetComponentInChildren<PlayerVision>(true);

        // Finds the TextMeshPro UI object named "AmmoCount".
        GameObject ammoCountObject = GameObject.Find("AmmoCount");

        if (ammoCountObject != null)
        {
            ammoText = ammoCountObject.GetComponent<TMP_Text>();
        }
        else
        {
            Debug.LogWarning("Could not find a UI object named AmmoCount.");
        }

        // Finds the TextMeshPro UI object named "WeaponNumber".
        GameObject weaponNumberObject = GameObject.Find("WeaponNumber");

        if (weaponNumberObject != null)
        {
            weaponNumberText = weaponNumberObject.GetComponent<TMP_Text>();
        }
        else
        {
            Debug.LogWarning("Could not find a UI object named WeaponNumber.");
        }
    }

    private void Start()
    {
        EquipWeapon(0);
    }

    private void Update()
    {
        if (Keyboard.current != null)
        {
            if (Keyboard.current.digit1Key.wasPressedThisFrame)
            {
                EquipWeapon(0);
            }

            if (Keyboard.current.digit2Key.wasPressedThisFrame)
            {
                EquipWeapon(1);
            }

            if (Keyboard.current.digit3Key.wasPressedThisFrame)
            {
                EquipWeapon(2);
            }
        }

        UpdateAmmoDisplay();
    }

    public void EquipWeapon(int weaponIndex)
    {
        if (weaponIndex < 0 ||
            weaponIndex >= weapons.Length ||
            weapons[weaponIndex] == null)
        {
            return;
        }

        for (int i = 0; i < weapons.Length; i++)
        {
            if (weapons[i] != null)
            {
                weapons[i].SetActive(i == weaponIndex);
            }
        }

        currentWeaponIndex = weaponIndex;

        // A weapon is either a hitscan weapon or a projectile weapon.
        activeWeapon = weapons[currentWeaponIndex]
            .GetComponent<HitscanShooter>();

        activeProjectileWeapon = weapons[currentWeaponIndex]
            .GetComponent<ProjectileShooter>();

        if (activeWeapon != null)
        {
            activeWeapon.SetWeaponReferences(
                playerCamera,
                playerVision
            );
        }

        if (activeProjectileWeapon != null)
        {
            activeProjectileWeapon.SetWeaponReferences(
                playerCamera,
                playerVision
            );
        }

        UpdateWeaponNumberDisplay();
    }

    private void UpdateAmmoDisplay()
    {
        if (ammoText == null)
            return;

        if (activeWeapon != null)
        {
            if (activeWeapon.IsReloading)
            {
                ammoText.text = "RELOADING...";
            }
            else
            {
                ammoText.text =
                    activeWeapon.CurrentAmmo +
                    " / " +
                    activeWeapon.MagazineSize;
            }
        }
        else if (activeProjectileWeapon != null)
        {
            // Projectile weapons have no ammo system yet.
            ammoText.text = "-- / --";
        }
    }

    private void UpdateWeaponNumberDisplay()
    {
        if (weaponNumberText == null)
            return;

        // Array positions 0, 1, 2 display as 1, 2, 3.
        weaponNumberText.text = (currentWeaponIndex + 1).ToString();
    }
}