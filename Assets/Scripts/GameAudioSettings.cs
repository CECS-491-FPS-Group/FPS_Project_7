using System;
using UnityEngine;

// Adapted from Audio_Management@11c62b6. Local preferences, never network state.
public static class GameAudioSettings
{
    private const string MusicKey = "audio.music";
    private const string SfxKey = "audio.sfx";

    public static event Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSubscriptions() => Changed = null;

    public static float Normalize(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 1f : Mathf.Clamp01(value);
    public static void Save() => PlayerPrefs.Save();

    public static float MusicVolume
    {
        get => Normalize(PlayerPrefs.GetFloat(MusicKey, 1f));
        set
        {
            value = Normalize(value);
            if (MusicVolume == value) return;
            PlayerPrefs.SetFloat(MusicKey, value);
            Changed?.Invoke();
        }
    }

    public static float SfxVolume
    {
        get => Normalize(PlayerPrefs.GetFloat(SfxKey, 1f));
        set
        {
            value = Normalize(value);
            if (SfxVolume == value) return;
            PlayerPrefs.SetFloat(SfxKey, value);
            Changed?.Invoke();
        }
    }
}
