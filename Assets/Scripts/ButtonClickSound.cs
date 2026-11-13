using UnityEngine;
using UnityEngine.UI;

public class ButtonClickSound : MonoBehaviour
{
    private static AudioClip clickClip;
    private static float clickVolume = 1f;
    private Button[] buttons;

    public void Configure(AudioClip customClip, float volume)
    {
        if (customClip != null)
        {
            clickClip = customClip;
        }

        clickVolume = volume;
    }

    private void Awake()
    {
        EnsureClip();

        buttons = GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
        {
            button.onClick.AddListener(PlayClick);
        }
    }

    private void OnDestroy()
    {
        if (buttons == null)
        {
            return;
        }

        foreach (Button button in buttons)
        {
            if (button != null)
            {
                button.onClick.RemoveListener(PlayClick);
            }
        }
    }

    private static void EnsureClip()
    {
        if (clickClip != null)
        {
            return;
        }

        const int sampleRate = 44100;
        const float duration = 0.08f;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        clickClip = AudioClip.Create("UI Button Click", sampleCount, 1, sampleRate, false);

        float[] samples = new float[sampleCount];
        for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
        {
            float time = sampleIndex / (float)sampleRate;
            float envelope = 1f - sampleIndex / (float)sampleCount;
            samples[sampleIndex] = (Mathf.Sin(time * 2f * Mathf.PI * 1100f) * 0.7f
                + Mathf.Sin(time * 2f * Mathf.PI * 1800f) * 0.3f) * envelope * 0.45f;
        }

        clickClip.SetData(samples, 0);
    }

    private static void PlayClick()
    {
        if (clickClip != null)
        {
            IntegrationAudio.PlayClick(clickClip, clickVolume);
        }
    }
}
