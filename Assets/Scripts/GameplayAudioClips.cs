using UnityEngine;

// Only the hurt/death synthesis from DylanMueller1's Health at Audio_Management@11c62b6.
// No local damage, death or respawn logic is imported.
public static class GameplayAudioClips
{
    private static AudioClip _hurt, _death;
    public static AudioClip Hurt => _hurt != null ? _hurt : _hurt = Create(false);
    public static AudioClip Death => _death != null ? _death : _death = Create(true);

    private static AudioClip Create(bool death)
    {
        const int rate = 44100;
        float duration = death ? .8f : .25f;
        float[] samples = new float[Mathf.CeilToInt(rate * duration)];
        var random = new System.Random(death ? 13 : 7);
        float lowPass = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            float t = (float)i / rate;
            float noise = (float)(random.NextDouble() * 2 - 1);
            lowPass += (death ? .15f : .25f) * (noise - lowPass);
            if (death)
            {
                float fall = Mathf.Sin(2 * Mathf.PI * (330 * t - 220 * t * t)) * Mathf.Exp(-4 * t);
                float impact = Mathf.Sin(2 * Mathf.PI * (90 * t - 60 * t * t)) * Mathf.Exp(-9 * t);
                float crunch = lowPass * Mathf.Exp(-14 * t);
                samples[i] = Mathf.Clamp01(t / .003f) * Mathf.Clamp01((duration - t) / .1f) *
                    (.45f * fall + .7f * impact + .8f * crunch);
            }
            else
            {
                float thud = Mathf.Sin(2 * Mathf.PI * (150 * t - 280 * t * t)) * Mathf.Exp(-16 * t);
                float crunch = lowPass * Mathf.Exp(-30 * t);
                samples[i] = Mathf.Clamp01(t / .002f) * (.7f * thud + .8f * crunch);
            }
        }
        AudioClip clip = AudioClip.Create(death ? "Death" : "Damage Taken", samples.Length, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    public static void Release()
    {
        if (_hurt != null) Object.Destroy(_hurt);
        if (_death != null) Object.Destroy(_death);
        _hurt = _death = null;
    }
}
