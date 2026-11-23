# Capstone server decision validation

Resumed integration/anti-cheat-merged-test at534ab8b with a clean tree. AC09 live
procedure **worked per user report**; this is aggregate user-reported acceptance,
not new agent-run live measurements. AC09 Clean/Test/archived and installed manifest
hashes match5fcabd3f39f84fee92c0a717aeb869bd read-only. No artifacts regenerated.

## Shot receipt to final decision

Active MainMenu NetworkManager -> GameplayPlayerSpawner.playerPrefab ->
Assets/Prefabs/Player.prefab, inherited FirstPersonController and MainCamera.
FishNet owned RequestShot supplies direction and authenticated connection, not an
origin, damage, weapon values or hit. ProcessServerShot verifies exact owner
reference, active/auth/spawn/config/budget/alive/CanAct, finite unit aim, approved
pitch/yaw, reload/ammo/cadence. ServerShotOrigin applies approved crouch immediately;
standing resolved eye offset(0,1.375,0), body centre(0,.93,0), crouching halves eye
height and lowers body to.43. ADS/recoil change local direction/FOV, weapon switching
changes approved profile, none may choose origin. Root pose remains client-authoritative.

Confirmed gap: a thin solid roof between body and eye leaves the eye point itself
clear. Previous point-only guard passed it. New world-only body/eye overlap and
bidirectional segment checks reject it before ammo, damage, rewards or accepted-shot
presentation. Reverse ray also covers one-sided procedural mesh faces without changing
Physics.queriesHitBackfaces. Saturation is uncertain/rejected, not a cheating verdict.
Cheap invalid-request checks now precede geometry. Never confuse this with a legal
shot hitting cover: that still spends ammo, dispatches accepted audio/effects, hits
the nearest server collider and cannot damage someone behind it.

CapstoneServerChecks uses actual server-spawned prefabs/Health/MatchScoreboard with
simulated authenticated owners:35 assertions currently pass. Tests cover the old roof
endpoint ambiguity, new rejection, nonfinite/zero/wrong-facing/other-session/dead/
match-locked/reloading/empty/cooldown requests without ammo/HP/kill/streak/presentation
side effects, valid switch, respawn magazine reconciliation, immediate crouch/stand
wall shots, legal clear kill and exactly-once death. A development-only counter marks
the actual accepted-presentation dispatch; wire delivery remains an interactive test.
Server-defined weapon-specific cadence/pellets/ammo/range/effects remain unchanged.

## Pickup receipt to final decision

There is **no collection request RPC**. FishNet replicates owner position; MatchBoosts
Update polls authenticated server connections/owned objects. TryCollectServer is the
same atomic server-only decision used by polling and fixtures: active GAME, owned
spawned living Health in the same scene, existing unexpired ID, finite distance<=2.5m,
clear world segment from body to pickup, approved effect applied to that exact eligible
Health, then removal. Main-thread serialization makes a contested pickup one-winner;
duplicate polling sees an absent ID. Clients cannot replay an old-round ID request.

Confirmed gap: distance alone collected pickups through a solid wall. New world
obstruction gate rejects while retaining pickup/effect state. Open-space collection,
two contenders, duplicate polling, wrong owner/ID, out-of-range, expiry, death before/
after collection, Armor/Invisibility and postgame/stale-ID cleanup are exercised in
the real server decision path. Approved grant/replacement/death state stays server-owned.

Existing documented phase rule is retained: combat follows Health.CanAct, including
pregame/postgame until final EndMatch; pickup/streak-boost entitlement requires GAME.
This batch does not invent a warmup/postgame redesign. If the intended game requires
GAME-only kills/currency, that is an explicit gameplay-rule decision: current rules
permit them before final lock. See the release-candidate assessment for scope.

Terrain, review, profiling and final acceptance evidence are added in the closeout.
