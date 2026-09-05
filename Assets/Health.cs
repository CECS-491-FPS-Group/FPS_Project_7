using System.Collections;
using UnityEngine;

public class Health : MonoBehaviour
{
    public int maxHp = 100;
    public int currentHp;

    [Header("Respawn")]
    public bool respawnOnDeath;
    public float respawnDelay = 3f;
    public Transform respawnPoint;

    private Vector3 startingPosition;
    private Quaternion startingRotation;
    private bool isDead;

    private void Awake()
    {
        currentHp = maxHp;
        startingPosition = transform.position;
        startingRotation = transform.rotation;
    }

    public void TakeDamage(int damage)
    {
        if (isDead)
            return;

        currentHp = Mathf.Max(0, currentHp - damage);
        Debug.Log(gameObject.name + " took " + damage + " damage. HP left: " + currentHp);

        if (currentHp <= 0)
            Die();
    }

    private void Die()
    {
        isDead = true;
        Debug.Log(gameObject.name + " died.");

        if (respawnOnDeath)
            StartCoroutine(RespawnRoutine());
        else
            Destroy(gameObject);
    }

    private IEnumerator RespawnRoutine()
    {
        RespawnUI respawnUI = FindFirstObjectByType<RespawnUI>();
        SetPlayerActive(false);

        float timeRemaining = respawnDelay;
        while (timeRemaining > 0f)
        {
            if (respawnUI != null)
                respawnUI.ShowCountdown(Mathf.CeilToInt(timeRemaining));

            timeRemaining -= Time.deltaTime;
            yield return null;
        }

        CharacterController controller = GetComponent<CharacterController>();
        if (controller != null)
            controller.enabled = false;

        transform.SetPositionAndRotation(
            respawnPoint != null ? respawnPoint.position : startingPosition,
            respawnPoint != null ? respawnPoint.rotation : startingRotation);

        if (controller != null)
            controller.enabled = true;

        currentHp = maxHp;
        isDead = false;
        SetPlayerActive(true);

        if (respawnUI != null)
            respawnUI.Hide();

        Debug.Log(gameObject.name + " respawned.");
    }

    private void SetPlayerActive(bool active)
    {
        foreach (Renderer modelRenderer in GetComponentsInChildren<Renderer>(true))
            modelRenderer.enabled = active;

        CharacterController controller = GetComponent<CharacterController>();
        if (controller != null)
            controller.enabled = active;

        SetBehaviourEnabled("FirstPersonController", active);
        SetBehaviourEnabled("HitscanShooter", active);
    }

    private void SetBehaviourEnabled(string typeName, bool enabled)
    {
        foreach (MonoBehaviour behaviour in GetComponents<MonoBehaviour>())
        {
            if (behaviour != null && behaviour.GetType().Name == typeName)
                behaviour.enabled = enabled;
        }
    }
}