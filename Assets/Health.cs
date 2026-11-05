using System.Collections;
using UnityEngine;

public class Health : MonoBehaviour
{
    public int maxHp = 100;
    public int currentHp;
    public AudioClip damageClip;
    [Range(0f, 1f)] public float damageVolume = 0.8f;

    private static AudioClip generatedDamageClip;

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

        if (damageClip == null)
        {
            if (generatedDamageClip == null)
            {
                generatedDamageClip = CreateDamageClip();
            }

            damageClip = generatedDamageClip;
        }

        startingPosition = transform.position;
        startingRotation = transform.rotation;
    }

    public void TakeDamage(int damage)
    {
        if (isDead)
            return;

        // Played at a point so the sound survives the object being destroyed on death.
        AudioSource.PlayClipAtPoint(damageClip, transform.position, damageVolume * GameAudioSettings.SfxVolume);

        currentHp = Mathf.Max(0, currentHp - damage);
        Debug.Log(gameObject.name + " took " + damage + " damage. HP left: " + currentHp);

        if (currentHp <= 0)
            Die();
    }

    private static AudioClip CreateDamageClip()
    {
        const int sampleRate = 44100;
        const float duration = 0.25f;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];
        System.Random random = new System.Random(7);
        float lowPass = 0f;

        for (int i = 0; i < sampleCount; i++)
        {
            float time = (float)i / sampleRate;
            float noise = (float)(random.NextDouble() * 2.0 - 1.0);
            lowPass += 0.25f * (noise - lowPass);

            float thud = Mathf.Sin(2f * Mathf.PI * (150f * time - 280f * time * time)) * Mathf.Exp(-16f * time);
            float crunch = lowPass * Mathf.Exp(-30f * time);
            float attack = Mathf.Clamp01(time / 0.002f);

            samples[i] = attack * (0.7f * thud + 0.8f * crunch);
        }

        AudioClip clip = AudioClip.Create("Damage Taken", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
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