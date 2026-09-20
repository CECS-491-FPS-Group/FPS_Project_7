using System.Collections;
using UnityEngine;

public class Health : MonoBehaviour
{
    public int maxHp = 100;
    public int currentHp;

    // Used by the player health-bar UI.
    public float MaximumHealth => maxHp;
    public float CurrentHealth => currentHp;

    [Header("Respawn")]
    public bool respawnOnDeath = true;
    public float respawnDelay = 5f;
    public bool showRespawnUI = true;

    public bool IsDead => isDead;

    private Vector3 startingPosition;
    private bool isDead;

    private RandomRespawnArea randomRespawnArea;

    private void Awake()
    {
        currentHp = maxHp;
        startingPosition = transform.position;

        randomRespawnArea =
            FindFirstObjectByType<RandomRespawnArea>();
    }

    public void TakeDamage(int damage)
    {
        if (isDead)
            return;

        currentHp = Mathf.Max(
            0,
            currentHp - damage
        );

        Debug.Log(
            gameObject.name +
            " took " +
            damage +
            " damage. HP left: " +
            currentHp
        );

        if (currentHp <= 0)
            Die();
    }

    private void Die()
    {
        if (isDead)
            return;

        isDead = true;

        Debug.Log(gameObject.name + " died.");

        if (respawnOnDeath)
            StartCoroutine(RespawnRoutine());
        else
            Destroy(gameObject);
    }

    private IEnumerator RespawnRoutine()
    {
        RespawnUI respawnUI = null;

        if (showRespawnUI)
        {
            respawnUI =
                FindFirstObjectByType<RespawnUI>();
        }

        SetObjectActive(false);

        float timeRemaining = respawnDelay;

        while (timeRemaining > 0f)
        {
            if (respawnUI != null)
            {
                respawnUI.ShowCountdown(
                    Mathf.CeilToInt(timeRemaining)
                );
            }

            timeRemaining -= Time.deltaTime;
            yield return null;
        }

        CharacterController controller =
            GetComponent<CharacterController>();

        if (controller != null)
            controller.enabled = false;

        Vector3 newPosition;

        if (randomRespawnArea != null)
        {
            newPosition =
                randomRespawnArea.GetRandomSpawnPosition(
                    controller
                );
        }
        else
        {
            newPosition = startingPosition;

            Debug.LogWarning(
                "RandomRespawnArea was not found."
            );
        }

        transform.SetPositionAndRotation(
            newPosition,
            Quaternion.Euler(
                0f,
                Random.Range(0f, 360f),
                0f
            )
        );

        if (controller != null)
            controller.enabled = true;

        currentHp = maxHp;
        isDead = false;

        SetObjectActive(true);

        if (respawnUI != null)
            respawnUI.Hide();

        Debug.Log(
            gameObject.name +
            " respawned."
        );
    }

    private void SetObjectActive(bool active)
    {
        foreach (
            Renderer renderer
            in GetComponentsInChildren<Renderer>(true)
        )
        {
            renderer.enabled = active;
        }

        foreach (
            Collider collider
            in GetComponentsInChildren<Collider>(true)
        )
        {
            collider.enabled = active;
        }

        SetBehaviourEnabled(
            "FirstPersonController",
            active
        );

        SetBehaviourEnabled(
            "HitscanShooter",
            active
        );

        SetBehaviourEnabled(
            "BotAI",
            active
        );

        SetBehaviourEnabled(
            "AIBotAnimation",
            active
        );
    }

    private void SetBehaviourEnabled(
        string typeName,
        bool enabledState
    )
    {
        foreach (
            MonoBehaviour behaviour
            in GetComponentsInChildren<MonoBehaviour>(true)
        )
        {
            if (
                behaviour != null &&
                behaviour.GetType().Name == typeName
            )
            {
                behaviour.enabled = enabledState;
            }
        }

        foreach (
            MonoBehaviour behaviour
            in GetComponentsInParent<MonoBehaviour>(true)
        )
        {
            if (
                behaviour != null &&
                behaviour.GetType().Name == typeName
            )
            {
                behaviour.enabled = enabledState;
            }
        }
    }
}