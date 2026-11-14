using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Client-local physics only. No Health, NetworkObject, pickup logic or damage authority.
public sealed class CosmeticCorpse : MonoBehaviour
{
    private static readonly List<CosmeticCorpse> Alive = new();
    private Collider[] _colliders;
    private double _expires;
    public const int MaximumCount = 8;
    public const double Lifetime = 8;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Alive.Clear();

    public static CosmeticCorpse Create(Transform visual)
    {
#if UNITY_SERVER
        return null;
#else
        var prefab = Resources.Load<GameObject>("PlayerRagdoll");
        if (prefab == null || visual == null) return null;
        var root = new GameObject("Cosmetic corpse"); root.SetActive(false);
        SceneManager.MoveGameObjectToScene(root, visual.gameObject.scene);
        var body = root.AddComponent<CosmeticCorpse>();
        GameObject model = Instantiate(prefab, root.transform);
        foreach (Animator animator in model.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
        model.transform.SetPositionAndRotation(visual.position, visual.rotation);
        model.transform.localScale = visual.lossyScale;
        var pose = new Dictionary<string, Transform>();
        foreach (Transform part in visual.GetComponentsInChildren<Transform>(true))
            if (!pose.ContainsKey(part.name)) pose.Add(part.name, part);
        foreach (Transform part in model.GetComponentsInChildren<Transform>(true))
        {
            part.gameObject.layer = 2; // Built-in Ignore Raycast; all gameplay probes exclude this layer.
            if (part != model.transform && pose.TryGetValue(part.name, out Transform source))
                part.SetPositionAndRotation(source.position, source.rotation);
        }
        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true)) renderer.enabled = true;
        Rigidbody[] bones = model.GetComponentsInChildren<Rigidbody>(true);
        foreach (Rigidbody bone in bones) bone.isKinematic = true;
        body._colliders = model.GetComponentsInChildren<Collider>(true);
        foreach (Collider collider in body._colliders) collider.enabled = true;
        body._expires = Time.realtimeSinceStartupAsDouble + Lifetime;
        root.SetActive(true);
        foreach (Health player in FindObjectsByType<Health>(FindObjectsSortMode.None)) body.IgnorePlayer(player.gameObject);
        foreach (Rigidbody bone in bones)
        { bone.isKinematic = false; bone.useGravity = true; bone.linearVelocity = bone.angularVelocity = Vector3.zero; }
        Alive.RemoveAll(c => c == null); Alive.Add(body);
        while (Alive.Count > MaximumCount) Retire(Alive[0]);
        return body;
#endif
    }
    private void IgnorePlayer(GameObject player)
    {
        if (_colliders == null) return;
        foreach (Collider actor in player.GetComponentsInChildren<Collider>(true))
            foreach (Collider corpse in _colliders)
                if (actor != null && corpse != null) Physics.IgnoreCollision(actor, corpse, true);
    }
    public static void IgnoreForPlayer(GameObject player)
    {
        foreach (var body in Alive) if (body != null) body.IgnorePlayer(player);
    }
    public static void Retire(CosmeticCorpse body)
    {
        if (body == null) return;
        Alive.Remove(body); body.gameObject.SetActive(false);
        if (Application.isPlaying) Destroy(body.gameObject); else DestroyImmediate(body.gameObject);
    }
    private void Update() { if (Time.realtimeSinceStartupAsDouble >= _expires) Retire(this); }
    private void OnDestroy() => Alive.Remove(this);
}
