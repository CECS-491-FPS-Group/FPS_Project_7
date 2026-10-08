using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FishNet.Object;

public class PlayerHealthBarUI : MonoBehaviour
{
    [SerializeField] private TMP_Text healthText;

    private Slider healthSlider;
    private Health playerHealth;

    private void Awake()
    {
        healthSlider = GetComponent<Slider>();

        healthSlider.minValue = 0f;
        healthSlider.maxValue = 1f;
        healthSlider.value = 1f;
    }

    private void Update()
    {
        if (playerHealth == null)
        {
            FindLocalPlayerHealth();
            return;
        }

        if (playerHealth.MaximumHealth <= 0f)
            return;

        float healthPercent =
            Mathf.Clamp01(
                playerHealth.CurrentHealth /
                playerHealth.MaximumHealth
            );

        healthSlider.value = healthPercent;

        if (healthText != null)
        {
            int percentage =
                Mathf.RoundToInt(healthPercent * 100f);

            healthText.text =
                "Health: " + percentage + "%";
        }
    }

    private void FindLocalPlayerHealth()
    {
        Health[] healthObjects =
            FindObjectsByType<Health>(
                FindObjectsSortMode.None
            );

        foreach (Health health in healthObjects)
        {
            NetworkObject networkObject =
                health.GetComponentInParent<NetworkObject>();

            if (
                networkObject != null &&
                networkObject.IsOwner
            )
            {
                playerHealth = health;
                return;
            }
        }
    }
}