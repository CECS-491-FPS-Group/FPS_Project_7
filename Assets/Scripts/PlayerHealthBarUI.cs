using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FishNet.Object;

public class PlayerHealthBarUI : MonoBehaviour
{
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private Slider armorSlider;
    [SerializeField] private TMP_Text armorText;

    private Slider healthSlider;
    private Health playerHealth;

    private void Awake()
    {
        healthSlider = GetComponent<Slider>();

        if (healthSlider != null)
        {
            healthSlider.minValue = 0f;
            healthSlider.maxValue = 1f;
            healthSlider.value = 1f;
        }

        if (armorSlider != null)
        {
            armorSlider.minValue = 0f;
            armorSlider.value = 0f;
            armorSlider.gameObject.SetActive(false);
        }

        if (armorText != null)
            armorText.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (playerHealth == null)
        {
            FindLocalPlayerHealth();
            return;
        }

        if (healthSlider != null && playerHealth.MaximumHealth > 0f)
        {
            float healthPercent = Mathf.Clamp01(
                playerHealth.CurrentHealth / playerHealth.MaximumHealth
            );

            healthSlider.value = healthPercent;

            if (healthText != null)
                healthText.text =
                    "Health: " + Mathf.RoundToInt(healthPercent * 100f) + "%";
        }

        bool hasArmor = playerHealth.CurrentArmor > 0f;

        if (armorSlider != null)
        {
            armorSlider.gameObject.SetActive(hasArmor);

            if (hasArmor)
            {
                armorSlider.maxValue = Mathf.Max(
                    1f,
                    playerHealth.MaximumArmor
                );
                armorSlider.value = playerHealth.CurrentArmor;
            }
        }

        if (armorText != null)
        {
            armorText.gameObject.SetActive(hasArmor);

            if (hasArmor)
            {
                armorText.text =
                    "Military Vest: " +
                    Mathf.CeilToInt(playerHealth.CurrentArmor) +
                    "/" +
                    Mathf.CeilToInt(playerHealth.MaximumArmor);
            }
        }
    }

    private void FindLocalPlayerHealth()
    {
        Health[] healthObjects =
            FindObjectsByType<Health>(FindObjectsSortMode.None);

        foreach (Health health in healthObjects)
        {
            NetworkObject networkObject =
                health.GetComponentInParent<NetworkObject>();

            if (networkObject != null && networkObject.IsOwner)
            {
                playerHealth = health;
                return;
            }
        }
    }
}