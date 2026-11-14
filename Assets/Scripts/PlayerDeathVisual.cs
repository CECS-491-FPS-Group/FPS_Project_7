using UnityEngine;

// Adapts Divyanshu's bot ragdoll to player presentation, without cloning the live actor.
public sealed class PlayerDeathVisual : MonoBehaviour
{
    private CosmeticCorpse _corpse;
    public void Show()
    {
        if (_corpse != null || !GetComponent<Health>().IsClientInitialized) return;
        _corpse = CosmeticCorpse.Create(GetComponent<PlayerCameraSetup>().soldierVisual);
    }
    public void Clear() { CosmeticCorpse.Retire(_corpse); _corpse = null; }
    private void OnDestroy() => Clear();
}
