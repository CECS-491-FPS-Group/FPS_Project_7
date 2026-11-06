using FishNet.Object;
using UnityEngine;

// Lets other players hear this player's gunshots. The shooter's client reports which weapon
// fired, the server relays it, and every other client plays that weapon's sound in 3D at the
// shooter's position, so it gets quieter with distance and is silent beyond maxDistance.
public class ShotAudioBroadcaster : NetworkBehaviour
{
    [Header("3D falloff")]
    public float minDistance = 6f;
    public float maxDistance = 90f;
    [Range(0f, 1f)] public float remoteVolume = 1f;
    public float soundHeight = 1.5f;

    private WeaponHolster holster;

    private void Awake()
    {
        holster = GetComponent<WeaponHolster>();
    }

    // Called by the local player's weapon each time it fires.
    public void NotifyShot(GameObject weapon)
    {
        if (!IsSpawned || !IsOwner || holster == null)
            return;

        int weaponIndex = holster.IndexOfWeapon(weapon);
        if (weaponIndex >= 0)
        {
            ServerShot((byte)weaponIndex);
        }
    }

    [ServerRpc]
    private void ServerShot(byte weaponIndex)
    {
        if (holster == null || weaponIndex >= holster.weapons.Length)
            return;

        ObserversShot(weaponIndex);
    }

    // The owner already heard their own shot locally.
    [ObserversRpc(ExcludeOwner = true)]
    private void ObserversShot(byte weaponIndex)
    {
        if (holster == null || weaponIndex >= holster.weapons.Length || holster.weapons[weaponIndex] == null)
            return;

        HitscanShooter shooter = holster.weapons[weaponIndex].GetComponent<HitscanShooter>();
        if (shooter == null || shooter.gunshotClip == null)
            return;

        float volume = shooter.gunshotVolume * remoteVolume * GameAudioSettings.SfxVolume;
        PlayPositional(shooter.gunshotClip, transform.position + Vector3.up * soundHeight, volume);
    }

    private void PlayPositional(AudioClip clip, Vector3 position, float volume)
    {
        GameObject soundObject = new GameObject("Remote Gunshot");
        soundObject.transform.position = position;

        AudioSource source = soundObject.AddComponent<AudioSource>();
        source.clip = clip;
        source.volume = volume;
        source.spatialBlend = 1f;
        source.dopplerLevel = 0f;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.rolloffMode = AudioRolloffMode.Custom;
        source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, BuildRolloffCurve());
        source.Play();

        Destroy(soundObject, clip.length + 0.1f);
    }

    // Full volume inside minDistance, then a smooth falloff to silence at maxDistance.
    private AnimationCurve BuildRolloffCurve()
    {
        float nearFraction = Mathf.Clamp01(minDistance / Mathf.Max(maxDistance, 0.01f));
        AnimationCurve curve = new AnimationCurve();
        const int steps = 10;

        for (int i = 0; i <= steps; i++)
        {
            float t = Mathf.Lerp(nearFraction, 1f, (float)i / steps);
            float falloff = 1f - (t - nearFraction) / Mathf.Max(1f - nearFraction, 0.0001f);
            curve.AddKey(t, falloff * falloff);
        }

        curve.AddKey(0f, 1f);
        return curve;
    }
}
