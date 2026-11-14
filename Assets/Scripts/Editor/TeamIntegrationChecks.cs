using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

// Focused native checks between team ports; then existing unconnected audio lifecycle suite.
public static class TeamIntegrationChecks
{
    private static int checks;
    [MenuItem("Tools/Integration/Validate Team Features")]
    public static void Run()
    {
        GameObject actor = null, ceiling = null;
        InputActionAsset first = null, second = null;
        try
        {
            checks = 0;
            var template = Resources.Load<InputActionAsset>("GameplayInput");
            Require(template != null && template.FindAction("Crouch") != null, "project-owned input asset includes crouch");
            first = UnityEngine.Object.Instantiate(template); second = UnityEngine.Object.Instantiate(template);
            var jump = first.FindAction("Jump"); jump.ApplyBindingOverride(0, "<Keyboard>/j");
            second.LoadBindingOverridesFromJson(first.SaveBindingOverridesAsJson());
            Require(second.FindAction("Jump").bindings[0].effectivePath == "<Keyboard>/j", "binding overrides round trip");
            Require(template.FindAction("Jump").bindings[0].overridePath == null, "local rebind cannot mutate shared template");
            second.RemoveAllBindingOverrides();
            Require(second.FindAction("Jump").bindings[0].effectivePath == template.FindAction("Jump").bindings[0].path, "reset restores original bindings");
            foreach (string name in new[] { "Shoot", "Aim", "Reload", "Weapon1", "Weapon2", "Weapon3", "Scoreboard", "Pause" })
                Require(template.FindAction(name) != null, "action exists: " + name);
            actor = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab"));
            actor.transform.position = new Vector3(9000, 0, 9000);
            var posture = actor.GetComponent<PlayerPosture>() ?? actor.AddComponent<PlayerPosture>();
            var capsule = actor.GetComponent<CharacterController>();
            posture.Initialize();
            float height = capsule.height;
            Vector3 feet = capsule.center - Vector3.up * height * .5f;
            Vector3 scale = actor.transform.localScale;
            Vector3 aim = posture.AimOrigin - actor.transform.position;
            posture.Apply(true);
            Require(capsule.height < height && capsule.height >= capsule.radius * 2, "crouch respects physical capsule minimum");
            Require(Vector3.Distance(feet, capsule.center - Vector3.up * capsule.height * .5f) < .001f, "crouch preserves feet");
            Require(actor.transform.localScale == scale, "crouch never scales network root");
            Require(posture.AimOrigin.y < actor.transform.position.y + aim.y, "approved posture lowers server shot origin");
            ceiling = new GameObject("Crouch clearance fixture", typeof(BoxCollider));
            ceiling.GetComponent<BoxCollider>().size = new Vector3(4, .2f, 4);
            ceiling.transform.position = actor.transform.position + Vector3.up * (capsule.center.y + height * .5f);
            Physics.SyncTransforms();
            Require(!posture.CanStand(), "standing blocked by low ceiling");
            UnityEngine.Object.DestroyImmediate(ceiling); ceiling = null; Physics.SyncTransforms();
            Require(posture.CanStand(), "standing possible after obstruction removed");
            posture.Apply(false);
            Require(Mathf.Abs(capsule.height - height) < .001f && Vector3.Distance(posture.AimOrigin, actor.transform.position + aim) < .001f,
                "standing restores collider and server eye origin");
            CheckEffects(actor);
            CheckReplays();
            Debug.Log($"[Team CHECK] PASS checks={checks}; controls/posture geometry, network runtime pending.");
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            if (Application.isBatchMode) EditorApplication.Exit(1);
            return;
        }
        finally
        {
            if (ceiling != null) UnityEngine.Object.DestroyImmediate(ceiling);
            if (actor != null) UnityEngine.Object.DestroyImmediate(actor);
            if (first != null) UnityEngine.Object.DestroyImmediate(first);
            if (second != null) UnityEngine.Object.DestroyImmediate(second);
        }
        AudioIntegrationChecks.Run();
    }
    private static void Require(bool value, string label)
    {
        if (!value) throw new InvalidOperationException("[Team CHECK] FAIL: " + label);
        checks++;
    }
    private static void CheckEffects(GameObject actor)
    {
        var prefab = Resources.Load<GameObject>("PlayerRagdoll");
        Require(prefab != null && prefab.GetComponentsInChildren<Rigidbody>(true).Length == 11, "source ragdoll bone bodies imported");
        Require(prefab.GetComponentsInChildren<MonoBehaviour>(true).Length == 0 &&
            prefab.GetComponentsInChildren<FishNet.Object.NetworkObject>(true).Length == 0, "corpse asset has no gameplay/network scripts");
        Require(prefab.GetComponentsInChildren<CharacterJoint>(true).Length == 10, "source joints retained");
        CosmeticCorpse corpse = CosmeticCorpse.Create(actor.GetComponent<PlayerCameraSetup>().soldierVisual);
        Require(corpse != null, "cosmetic corpse created from live visual pose");
        CosmeticCorpse.IgnoreForPlayer(actor);
        foreach (Collider bone in corpse.GetComponentsInChildren<Collider>())
        {
            Require(bone.gameObject.layer == 2 && (Physics.DefaultRaycastLayers & (1 << bone.gameObject.layer)) == 0,
                "corpse excluded from combat/spawn/clearance probes");
            foreach (Collider player in actor.GetComponentsInChildren<Collider>())
                Require(Physics.GetIgnoreCollision(bone, player), "corpse cannot physically block player collider");
        }
        CosmeticCorpse.Retire(corpse);
        Require(corpse == null, "corpse retires immediately outside Play Mode");
        var bodies = new System.Collections.Generic.List<CosmeticCorpse>();
        for (int i = 0; i <= CosmeticCorpse.MaximumCount; i++)
            bodies.Add(CosmeticCorpse.Create(actor.GetComponent<PlayerCameraSetup>().soldierVisual));
        Require(UnityEngine.Object.FindObjectsByType<CosmeticCorpse>(FindObjectsSortMode.None).Length == CosmeticCorpse.MaximumCount,
            "creating excess corpses retires oldest bodies");
        var last = bodies[bodies.Count - 1];
        typeof(CosmeticCorpse).GetField("_expires", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(last, -1d);
        last.SendMessage("Update");
        Require(last == null, "expired corpse lifetime retires body");
        foreach (var body in bodies) CosmeticCorpse.Retire(body);
        Require((int)MatchBoosts.Kind.Armor == 5 && (int)MatchBoosts.Kind.Invisibility == 6, "original four effect IDs preserved");
        Require(CombatEffectRules.OriginalReward(5) == 0, "streak cannot select armor");
    }
    private static void Invalid(Action action, string label)
    {
        try { action(); } catch (System.IO.InvalidDataException) { checks++; return; }
        throw new InvalidOperationException("[Team CHECK] FAIL invalid data accepted: " + label);
    }
    private static void CheckReplays()
    {
        var data = new LocalReplayCapture.Recording { scene = "RoundImplementation", owner = 1 };
        data.frames.Add(new LocalReplayCapture.Frame { time = 0, position = Vector3.zero, rotation = Quaternion.identity });
        data.frames.Add(new LocalReplayCapture.Frame { time = 2, position = new Vector3(10, 0, 5), rotation = Quaternion.identity });
        var restored = ReplayArchive.Parse(JsonUtility.ToJson(data));
        Require(restored.frames.Count == 2 && restored.owner == 1, "current local capture round trip");
        Require(Vector3.Distance(ReplayArchive.PositionAt(restored, 1), new Vector3(5, 0, 2.5f)) < .001f, "movement playback interpolation");
        Require(ReplayArchive.PositionAt(restored, 99) == restored.frames[1].position, "playback ends at last sample");
        var legacy = ReplayArchive.Parse("{\"mapName\":\"Test\",\"frames\":[{\"time\":0,\"posX\":4,\"rotW\":1}]}");
        Require(legacy.frames[0].position.x == 4, "source theater capture format supported");
        Invalid(() => ReplayArchive.Load("../outside.json"), "parent traversal");
        Invalid(() => ReplayArchive.Load("file.txt"), "non-replay extension");
        data.version = 99; Invalid(() => ReplayArchive.Validate(data), "future schema"); data.version = 1;
        var bad = data.frames[1]; bad.time = -1; data.frames[1] = bad;
        Invalid(() => ReplayArchive.Validate(data), "time reversal");
        bad.time = 2; bad.position.x = float.NaN; data.frames[1] = bad;
        Invalid(() => ReplayArchive.Validate(data), "nonfinite position");
        bad.position = Vector3.zero; bad.rotation = new Quaternion(); data.frames[1] = bad;
        Invalid(() => ReplayArchive.Validate(data), "zero quaternion");
        data.frames.Clear(); Invalid(() => ReplayArchive.Validate(data), "empty capture");
        for (int i = 0; i <= ReplayArchive.MaximumFrames; i++) data.frames.Add(new LocalReplayCapture.Frame());
        Invalid(() => ReplayArchive.Validate(data), "oversized frame list");
    }
}
