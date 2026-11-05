using UnityEngine;

public class Health : MonoBehaviour
{
    public int maxHp = 100;
    public int currentHp;
    public AudioClip damageClip;
    [Range(0f, 1f)] public float damageVolume = 0.8f;

    private static AudioClip generatedDamageClip;

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
    }

    public void TakeDamage(int damage)
    {
        currentHp -= damage;

        // Played at a point so the sound survives the object being destroyed on death.
        AudioSource.PlayClipAtPoint(damageClip, transform.position, damageVolume * GameAudioSettings.SfxVolume);

        Debug.Log(gameObject.name + " took " + damage + " damage. HP left: " + currentHp);

        if (currentHp <= 0)
        {
            Die();
        }
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
        Debug.Log(gameObject.name + " died.");
        Destroy(gameObject);
    }
}
