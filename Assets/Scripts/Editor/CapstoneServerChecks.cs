using System;
using System.Collections.Generic;
using System.Reflection;
using FishNet.Connection;
using FishNet.Managing;
using UnityEngine;
using UnityEngine.SceneManagement;

// Real prefab + actual server decision paths. Synthetic authenticated owners, not packet tests.
internal static class CapstoneServerChecks
{
    private static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static object Read(object obj, string field) => obj.GetType().GetField(field, Flags).GetValue(obj);
    private static void Set(object obj, string field, object value) => obj.GetType().GetField(field, Flags).SetValue(obj, value);
    private static object Call(object obj, string method, params object[] args) => obj.GetType().GetMethod(method, Flags).Invoke(obj, args);
    private static void Sync(object obj, string field, object value) => Read(obj, field).GetType().GetProperty("Value").SetValue(Read(obj, field), value);
    private static int assertions;
    private static void Check(bool ok, string reason) { if (!ok) throw new Exception("[Capstone SERVER] " + reason); assertions++; }
    internal static void Run(NetworkManager manager)
    {
        assertions = 0; var scene = SceneManager.CreateScene("RoundImplementation");
        var owned = new List<GameObject>(); var world = new GameObject("Server collection/configuration fixture");
        SceneManager.MoveGameObjectToScene(world, scene);
        var timer = world.AddComponent<RoundTimer>(); timer.enabled = false; timer.timerIsRunning = true; timer.currentState = RoundTimer.gameStates.GAME;
        var boosts = world.AddComponent<MatchBoosts>(); boosts.enabled = false; Set(boosts, "_manager", manager);
        var scores = world.AddComponent<MatchScoreboard>(); scores.enabled = false; Set(scores, "_manager", manager); Set(scores, "_listening", true);
        NetworkConnection Owner(int id)
        {
            var c = new NetworkConnection(manager, id, 0, true); Call(c, "ConnectionAuthenticated"); Call(c, "SetLoadedStartScenes", true); return c;
        }
        GameObject Spawn(NetworkConnection c, Vector3 point)
        {
            var go = UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab"), point, Quaternion.identity);
            SceneManager.MoveGameObjectToScene(go, scene); manager.ServerManager.Spawn(go, c, scene); owned.Add(go); return go;
        }
        var owner = Owner(6010); var other = Owner(6011);
        var player = Spawn(owner, new Vector3(22000, 300, 22000)); var victim = Spawn(other, player.transform.position + Vector3.forward * 8);
        Call(scores, "Register", owner); Call(scores, "Register", other);
        var shooter = player.GetComponentInChildren<HitscanShooter>(true); var health = player.GetComponent<Health>(); var target = victim.GetComponent<Health>();
        var posture = player.GetComponent<PlayerCameraSetup>();
        var wall = new GameObject("Cover regression", typeof(BoxCollider)); SceneManager.MoveGameObjectToScene(wall, scene);
        try
        {
            Vector3 eye = posture.ServerShotOrigin, body = posture.ServerBodyOrigin;
            Debug.Log($"[Capstone ORIGIN] resolved standing offset={eye - player.transform.position} body={body - player.transform.position}; " +
                $"source=active Player prefab and inherited camera; no muzzle/client origin.");
            victim.transform.position = eye + Vector3.forward * 8 - Vector3.up * victim.GetComponent<CharacterController>().center.y;
            Sync(target, "_currentHp", 20); // Any leaked valid ray would kill and award a streak.
            wall.GetComponent<BoxCollider>().size = new Vector3(3, .02f, 3);
            wall.transform.position = Vector3.Lerp(body, eye, .5f); Physics.SyncTransforms();
            var query = new CombatRayGeometry();
            Check(query.OriginClear(eye, player.transform), "old eye-point test alone admits endpoint across thin roof");
            Check(!query.WorldSegmentClear(body, eye, player.transform), "body-to-eye must not cross thin solid roof");
            int Accepted() => (int)Read(shooter, "_developmentAcceptedShots");
            void Reject(Vector3 direction, NetworkConnection sender, string why)
            {
                int ammo = shooter.CurrentAmmo, hp = target.CurrentHealth, count = Accepted();
                Call(shooter, "ProcessServerShot", direction, sender);
                var rows = (Dictionary<NetworkConnection, MatchScoreboard.Row>)Read(scores, "_scores");
                Check(shooter.CurrentAmmo == ammo && target.CurrentHealth == hp && Accepted() == count && rows[owner].Kills == 0 && rows[owner].Streak == 0, why);
            }
            Reject(Vector3.forward, owner, "roof-displaced origin cannot consume ammo, HP, kill/streak or accepted presentation");
            wall.transform.position += Vector3.right * 30; Physics.SyncTransforms();
            Vector3 originalTarget = victim.transform.position;
            player.transform.rotation = Quaternion.Euler(0, 0, 90);
            Vector3 rolledEye = posture.ServerShotOrigin;
            wall.GetComponent<BoxCollider>().size = new Vector3(.05f, 3, 3);
            wall.transform.position = player.transform.position + new Vector3(-.5f, .5f, 0);
            victim.transform.position = rolledEye + Vector3.forward * 8 - Vector3.up * victim.GetComponent<CharacterController>().center.y;
            Physics.SyncTransforms();
            Check(query.OriginClear(rolledEye, player.transform) && query.WorldSegmentClear(posture.ServerBodyOrigin, rolledEye, player.transform),
                "finite rolled root can extend both body/eye beyond wall while endpoint/segment alone look clear");
            Reject(Vector3.forward, owner, "unsupported rolled root cannot authorize a displaced shot origin");
            player.transform.rotation = Quaternion.identity; victim.transform.position = originalTarget;
            wall.transform.position += Vector3.right * 30; Physics.SyncTransforms();
            Reject(new Vector3(float.NaN, 0, 1), owner, "nonfinite direction no side effects");
            Reject(Vector3.zero, owner, "zero direction no side effects");
            Reject(Vector3.back, owner, "wrong-facing direction no side effects");
            Reject(Vector3.forward, Owner(owner.ClientId), "reused numeric owner ID not same session");
            Sync(health, "_dead", true); Reject(Vector3.forward, owner, "dead shooter no side effects"); Sync(health, "_dead", false);
            Call(shooter, "ReconcileServerWeapon"); Check(shooter.CurrentAmmo == 30, "living respawn reconciliation restores approved magazine");
            Sync(health, "_matchEnded", true); Reject(Vector3.forward, owner, "match-locked shooter no side effects"); Sync(health, "_matchEnded", false);
            Sync(shooter, "_reloading", true); Sync(shooter, "_reloadDeadline", double.MaxValue);
            Reject(Vector3.forward, owner, "reload rejects without accepted shot"); Sync(shooter, "_reloading", false);
            Sync(shooter, "_ammo", 0); Reject(Vector3.forward, owner, "empty magazine rejects without accepted shot"); Sync(shooter, "_ammo", 30);
            Set(shooter, "_nextServerShotTime", double.MaxValue); Reject(Vector3.forward, owner, "cadence rejects without ammo or damage"); Set(shooter, "_nextServerShotTime", 0d);
            Call(shooter, "ProcessServerWeapon", -1, owner); Check(shooter.CurrentAmmo == 30 && shooter.WeaponName == "Sidearm", "invalid switch cannot change magazine/config");
            Call(shooter, "ProcessServerWeapon", 1, owner); Check(shooter.WeaponName == "Rifle", "valid weapon-specific switch retained");
            Check(Vector3.Distance(posture.ServerShotOrigin, eye) < .001f, "switch/ADS presentation cannot move approved origin");
            Set(shooter, "_nextSwitchTime", 0d); Call(shooter, "ProcessServerWeapon", 0, owner);
            // Accepted wall hit: ammo and presentation happen; no HP/kills.
            wall.GetComponent<BoxCollider>().size = new Vector3(3, 5, .2f); wall.transform.position = eye + Vector3.forward * 3; Physics.SyncTransforms();
            int before = Accepted(); Call(shooter, "ProcessServerShot", Vector3.forward, owner);
            Check(shooter.CurrentAmmo == 29 && target.CurrentHealth == 20 && Accepted() == before + 1, "valid wall hit consumes one round and dispatches accepted presentation without HP");
            Set(shooter, "_nextServerShotTime", 0d); Sync(posture, "_crouching", true); Call(shooter, "ProcessServerShot", Vector3.forward, owner);
            Check(shooter.CurrentAmmo == 28 && target.CurrentHealth == 20 && posture.ServerShotOrigin.y < eye.y, "immediate approved crouch against cover remains a valid blocked shot");
            Set(shooter, "_nextServerShotTime", 0d); Sync(posture, "_crouching", false); Call(shooter, "ProcessServerShot", Vector3.forward, owner);
            Check(shooter.CurrentAmmo == 27 && target.CurrentHealth == 20, "immediate stand against cover remains a valid blocked shot");
            wall.transform.position += Vector3.right * 30; Physics.SyncTransforms(); Set(shooter, "_nextServerShotTime", 0d);
            Call(shooter, "ProcessServerShot", Vector3.forward, owner);
            var score = ((Dictionary<NetworkConnection, MatchScoreboard.Row>)Read(scores, "_scores"))[owner];
            Check(target.IsDead && score.Kills == 1 && score.Streak == 1, "legal clear shot kills/rewards once through actual Health/score path");
            target.TakeDamage(40, owner);
            Check(((Dictionary<NetworkConnection, MatchScoreboard.Row>)Read(scores, "_scores"))[owner].Kills == 1, "repeated dead-target damage cannot duplicate kill/streak");
            Sync(target, "_dead", false); Sync(target, "_currentHp", 100); Sync(target, "_armor", 0);
            CollectionCases(boosts, timer, player, victim, owner, other, wall);
            Debug.Log($"[Capstone SERVER] PASS assertions={assertions}; real server shot/collection/state, simulated owners; no transport/live acceptance claim.");
        }
        finally
        {
            foreach (var go in owned) if (go != null && go.GetComponent<Health>().IsSpawned) manager.ServerManager.Despawn(go);
            UnityEngine.Object.Destroy(world); UnityEngine.Object.Destroy(wall); SceneManager.UnloadSceneAsync(scene);
        }
    }
    private static void CollectionCases(MatchBoosts boosts, RoundTimer timer, GameObject a, GameObject b, NetworkConnection first, NetworkConnection second, GameObject wall)
    {
        var pickups = (List<MatchBoosts.Pickup>)Read(boosts, "_pickups");
        var health = a.GetComponent<Health>(); var other = b.GetComponent<Health>();
        double now = boosts.GetType().GetProperty("Now", Flags).GetValue(boosts) is double time ? time : 0;
        var body = a.transform.TransformPoint(a.GetComponent<CharacterController>().center);
        b.transform.position = a.transform.position + Vector3.right * .4f;
        int id = 0;
        int Add(MatchBoosts.Kind kind = MatchBoosts.Kind.Speed) { pickups.Add(new MatchBoosts.Pickup { Id = ++id, Position = body + Vector3.forward, Type = kind, Expires = now + 30 }); return id; }
        bool Collect(NetworkConnection c, Health h, int pickup) => (bool)Call(boosts, "TryCollectServer", c, h, pickup);
        int one = Add(); wall.GetComponent<BoxCollider>().size = new Vector3(3, 3, .1f); wall.transform.position = body + Vector3.forward * .5f; Physics.SyncTransforms();
        Check((body + Vector3.forward - a.transform.position).sqrMagnitude < 2.5f * 2.5f, "distance-only old eligibility could reach through this thin wall");
        Check(!Collect(first, health, one) && pickups.Count == 1 && boosts.ServerKind(first) == MatchBoosts.Kind.None, "world cover prevents through-wall collection");
        wall.transform.position += Vector3.right * 30; Physics.SyncTransforms();
        Check(Collect(first, health, one), "alive owned player in range and clear space collects");
        Check(!Collect(second, other, one) && !Collect(first, health, one) && pickups.Count == 0, "two players and duplicate polling cannot consume pickup twice");
        boosts.Clear(first); one = Add(); Sync(health, "_dead", true);
        Check(!Collect(first, health, one) && pickups.Count == 1, "death before collection cannot grant/consume"); Sync(health, "_dead", false);
        Check(!Collect(second, health, one) && !Collect(first, health, -1), "wrong sender/player and invalid ID cannot collect");
        Vector3 original = health.transform.position; health.transform.position += Vector3.right * 20;
        Check(!Collect(first, health, one), "out-of-range server pose cannot collect"); health.transform.position = original;
        Check(Collect(first, health, one), "eligibility recovers without widening radius"); health.TakeDamage(1000, second);
        Check(boosts.ServerKind(first) == MatchBoosts.Kind.None, "death after collection clears approved effect");
        Sync(health, "_dead", false); Sync(health, "_currentHp", 100);
        one = Add(MatchBoosts.Kind.Armor); Check(Collect(first, health, one) && health.CurrentArmor == 100, "Armor uses server Health");
        one = Add(MatchBoosts.Kind.Invisibility); Check(Collect(first, health, one) && boosts.ServerKind(first) == MatchBoosts.Kind.Invisibility, "Invisibility uses approved server state");
        one = Add(); var expired = pickups[0]; expired.Expires = now - 1; pickups[0] = expired;
        Check(!Collect(first, health, one), "expired pickup rejected even before update pruning");
        timer.currentState = RoundTimer.gameStates.POSTGAME; Call(boosts, "Update");
        Check(pickups.Count == 0 && boosts.ServerKind(first) == MatchBoosts.Kind.None && !Collect(first, health, one), "postgame clears pickups/effects and stale collection");
        timer.currentState = RoundTimer.gameStates.GAME;
        Check(!Collect(first, health, one), "previous-round pickup ID remains absent");
    }
}
