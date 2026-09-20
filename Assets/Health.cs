using System.Collections;
using UnityEngine;

public class Health : MonoBehaviour
{
    public int maxHp = 100;
    public int currentHp;

    public float MaximumHealth => maxHp;
    public float CurrentHealth => currentHp;

    [Header("Respawn")]
    public bool respawnOnDeath = true;
    public float respawnDelay = 5f;
    public bool showRespawnUI = true;

    public bool IsDead => isDead;

    private Vector3 startingPosition;
    private bool isDead;
    private bool matchFrozen;

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
        TakeDamage(damage, null);
    }

    public void TakeDamage(
        int damage,
        GameObject attacker
    )
    {
        if (isDead || matchFrozen)
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
            Die(attacker);
    }

    private void Die(GameObject attacker)
    {
        if (isDead)
            return;

        isDead = true;

        MatchManager matchManager =
            FindFirstObjectByType<MatchManager>();

        if (matchManager != null)
        {
            matchManager.RegisterDeath(
                gameObject,
                attacker
            );
        }

        Debug.Log(
            gameObject.name +
            " died."
        );

        if (respawnOnDeath)
        {
            StartCoroutine(
                RespawnRoutine()
            );
        }
        else
        {
            Destroy(gameObject);
        }
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

        float timeRemaining =
            respawnDelay;

        while (timeRemaining > 0f)
        {
            if (respawnUI != null)
            {
                respawnUI.ShowCountdown(
                    Mathf.CeilToInt(
                        timeRemaining
                    )
                );
            }

            timeRemaining -=
                Time.deltaTime;

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
            newPosition =
                startingPosition;

            Debug.LogWarning(
                "RandomRespawnArea was not found."
            );
        }

        transform.SetPositionAndRotation(
            newPosition,
            Quaternion.Euler(
                0f,
                Random.Range(
                    0f,
                    360f
                ),
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

    public void SetMatchFrozen(bool frozen)
    {
        matchFrozen = frozen;

        bool enableBehaviours =
            !frozen &&
            !isDead;

        SetBehaviourEnabled(
            "FirstPersonController",
            enableBehaviours
        );

        SetBehaviourEnabled(
            "HitscanShooter",
            enableBehaviours
        );

        SetBehaviourEnabled(
            "BotAI",
            enableBehaviours
        );

        SetBehaviourEnabled(
            "AIBotAnimation",
            enableBehaviours
        );
    }

    public void ForceRespawnForRound()
    {
        StopAllCoroutines();

        RespawnUI respawnUI =
            FindFirstObjectByType<RespawnUI>();

        if (respawnUI != null)
            respawnUI.Hide();

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
            newPosition =
                startingPosition;

            Debug.LogWarning(
                "RandomRespawnArea was not found."
            );
        }

        transform.SetPositionAndRotation(
            newPosition,
            Quaternion.Euler(
                0f,
                Random.Range(
                    0f,
                    360f
                ),
                0f
            )
        );

        if (controller != null)
            controller.enabled = true;

        currentHp = maxHp;
        isDead = false;

        SetObjectActive(true);
    }

    private void SetObjectActive(bool active)
    {
        foreach (
            Renderer renderer
            in GetComponentsInChildren<Renderer>(
                true
            )
        )
        {
            renderer.enabled = active;
        }

        foreach (
            Collider collider
            in GetComponentsInChildren<Collider>(
                true
            )
        )
        {
            collider.enabled = active;
        }

        bool enableBehaviours =
            active &&
            !matchFrozen &&
            !isDead;

        SetBehaviourEnabled(
            "FirstPersonController",
            enableBehaviours
        );

        SetBehaviourEnabled(
            "HitscanShooter",
            enableBehaviours
        );

        SetBehaviourEnabled(
            "BotAI",
            enableBehaviours
        );

        SetBehaviourEnabled(
            "AIBotAnimation",
            enableBehaviours
        );
    }

    private void SetBehaviourEnabled(
        string typeName,
        bool enabledState
    )
    {
        foreach (
            MonoBehaviour behaviour
            in GetComponentsInChildren<MonoBehaviour>(
                true
            )
        )
        {
            if (
                behaviour != null &&
                behaviour.GetType().Name ==
                typeName
            )
            {
                behaviour.enabled =
                    enabledState;
            }
        }

        foreach (
            MonoBehaviour behaviour
            in GetComponentsInParent<MonoBehaviour>(
                true
            )
        )
        {
            if (
                behaviour != null &&
                behaviour.GetType().Name ==
                typeName
            )
            {
                behaviour.enabled =
                    enabledState;
            }
        }
    }
}