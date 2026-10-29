using System;
using UnityEngine;

// Persisted volume settings shared by the lobby music and UI sound effects.
public static class GameAudioSettings
{
    private const string MusicKey = "audio.music";
    private const string SfxKey = "audio.sfx";

    public static event Action Changed;

    public static float MusicVolume
    {
        get => PlayerPrefs.GetFloat(MusicKey, 1f);
        set
        {
            PlayerPrefs.SetFloat(MusicKey, Mathf.Clamp01(value));
            Changed?.Invoke();
        }
    }

    public static float SfxVolume
    {
        get => PlayerPrefs.GetFloat(SfxKey, 1f);
        set
        {
            PlayerPrefs.SetFloat(SfxKey, Mathf.Clamp01(value));
            Changed?.Invoke();
        }
    }
}
