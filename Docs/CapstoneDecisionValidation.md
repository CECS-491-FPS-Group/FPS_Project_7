# Capstone server decision validation

Resumed integration/anti-cheat-merged-test at534ab8b with a clean tree. AC09 live
procedure **worked per user report**; this is aggregate user-reported acceptance,
not new agent-run live measurements. AC09 Clean/Test/archived and installed manifest
hashes match5fcabd3f39f84fee92c0a717aeb869bd read-only. No artifacts regenerated.

Local source commits: ea9c839 (server cover/collection),2cca814 (root geometry),
d706105 (fair capture, native routes and load/profiler validation). Core source entry
points: Assets/Scripts/Shooting.cs / HitscanShooter.RequestShot, ProcessServerShot,
FireServerRay; PlayerCameraSetup.cs / ServerShotOrigin, ServerBodyOrigin;
MatchBoosts.cs / Update, TryCollectServer, ApplyServerBoost; SpeedHackDetector.cs /
OnReceivedGoal, ObserveReceivedGoals, LateUpdate. All use the active registered
Player.prefab and current scene configuration, not historical feature-branch code.

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

An additional native server case demonstrates a finite rolled root extending both
body and eye past a wall. Current controllers rotate yaw only and never scale/parent
their network root. Shot and collection decisions reject unsupported root geometry
(upright dot>=.999, unit scale within.001, no parent). This rejects a request, does not
correct movement, increase suspicion or disconnect. A future supported lean/parented
surface mechanic must supply an explicit approved geometry rule.

CapstoneServerChecks uses actual server-spawned prefabs/Health/MatchScoreboard with
simulated authenticated owners:37 assertions currently pass. Tests cover the old roof
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

## Movement receipt and actual terrain

Installed FishNet NetworkTransform.ServerUpdateTransform -> DataReceived ->
OnDataReceived(previous,next) supplies decoded Position/Tick before smoothing.
SpeedHackDetector.OnReceivedGoal copies those pooled values, accepts advancing ticks
from the active remote connection, and coalesces into one latest slot. It evaluates
changed raw goals at most4Hz; server receipt time, not client tick, measures elapsed
time. The original horizontal observer still samples the smoothed transform in
LateUpdate and retains its existing punitive suspicion path. The two evidence types
remain separate. Positions are client claims; freshness is not contact/path truth.

Server-owned ApprovedMovementLimits/Boost state supplies walk4/sprint6, Speed6/9,
normal Jump1.2/approved1.8, gravity-15. Current capsule height2, radius.5, centerY.93,
step.25, slope45 degrees; crouch height1/centerY.43 keeps feet unchanged. Sprint while
crouched remains legal. ADS is presentation, not a speed entitlement. Health death,
issued respawn placement/ACK, phase changes, scene/despawn and ownership reset evidence
with bounded existing tolerance. Cosmetic corpses have no Health/NetworkObject/live
observer, ignore-raycast colliders and no live-controller collision; dead Health is
not an aim candidate. Armor/Invisibility stay server state, not new movement limits;
invisible targets are excluded from physical-cover evidence. No new water/swim or
moving-platform mechanic is active. See MovementExpansion.md for source details.

Native TerrainIntegrationChecks now drives the resolved capsule with actual
CharacterController.Move over generated RoundImplementation terrain (seed2385,
signature8d371621/poi=8/plots=20/bridges=3). Seven3-second synthetic routes: road,
two bridges, two POI slopes/roof/fall routes, building exterior and dry water edge.
Sprint/approved Speed then replacement/expiry, approved moving Jump, crouch/stand
are applied;84 synthetic fresh observation samples produced zero secondary reports.
The suite's existing Full/ServerOnly collider/layout and spawn obstruction/wait/
recovery checks also pass. This is native collision plus scripted state, not owner
input, packet timing, real boost collection or live respawn acceptance.

**No repeated-wall-traversal detector:** a2m endpoint segment through a wall and a
legal4m detour around its end can produce the same one-second endpoint pair at6m/s.
Coalesced goals lack intervening path/contact evidence; repetition does not remove
that ambiguity. Mesh/box queries cannot establish which path was taken. Existing raw
freshness/support instrumentation is retained; no sparse-line noclip claim, new rule,
threshold change or movement correction was introduced.

## Incident robustness and clean comparison

Existing real server bridge -> bounded worker archive -> Theater selection/scrub ->
Reviewed-note reload tests still pass with Dynamic=Disconnect. They cover grouped
overlapping alerts, distinct actors, disconnect/round-end partials, reused connection
IDs, match/life attribution, gaps, malformed data, storage failure and bounded
retention. These are scripted injections/simulated authenticated owners, not live
human evidence. Review remains offline/local with no policy or punishment route.

New native clean comparison uses a target moving from exposed to covered at<=3m/s,
brief following before holding the angle, crouched exposure and an injected plausible
cue. Same geometry/temporal model: visible and covered samples exist, zero alerts,
zero flags/files. Cue injection establishes suppression behavior, not what anyone
heard. New pure regression ensures reusing a clean lead ring never attributes the
previous target's samples to the next target.

Confirmed fairness problem: one noisy participant could consume all32 match clips
by changing encounters. New **two clips per participant per match**, within the old32
match cap, reserves capacity for all16 supported participants. Existing overlapping
alerts can still extend their clip to old duration/frame caps. Recording exhaustion
logs neutrally/rate-limited; observer reports and enforcement are unchanged. Pure
tests demonstrate40 attempted encounters cannot crowd out another participant and
all16 can retain two each. Rejoins receive new participant IDs, still bounded by
global32; this is not account identity or a guarantee across unlimited reconnects.

## Measured cost (automated hosted Unity fixtures)

LobbyScene_v1 NetworkLobbyUI.maximumPlayers is **4**; supported configuration maximum
is16. Observation cadence5Hz, each workload covers30 **virtual** seconds,150 samples
per actor, runs synchronously in the Unity Editor process. Times below are total
Stopwatch milliseconds, **not per-frame, live play or thirty real seconds**. GC deltas
are GC.GetTotalMemory(false) minus a pre-run collected baseline, not exact allocations;
no gen0 collection occurred during these loops. No live profiler capture was run.

Final source run, Logs/RC-server-final.log; core OccludedAimTracking + capture,
snapshot validation and worker submission, excluding physics/transport/candidate
filtering. Synthetic encounter grace ends at14s, with qualifying capture later in
the30s run; earlier poses form lead-in. Last sample29.8s, round-end30s. A dedicated
assertion verifies every active clip is actually finalized/submitted by the timed
End call. An initial version failed that assertion because clips had already closed;
fixture timing was corrected, without changing detection or capture thresholds.
Actual async serialization/pruning/atomic disk IO uses isolated Temp:

| Players / qualifying captures | Calls | Observe+capture total ms | Finalize+submit ms | Heap delta bytes | Worker writes / total ms / max ms | Queue high / coalesced |
| --- | ---: | ---: | ---: | ---: | --- | --- |
|4 /0 |600 |1.241 |.004 |32,768 |0 /0 /0 |0 /0 |
|4 /1 |600 |1.060 |.017 |81,920 |2 /14.273 /11.563 |1 /0 |
|4 /4 |600 |.973 |.064 |229,376 |5 /22.749 /9.723 |4 /3 |
|16 /0 |2,400 |3.361 |.002 |131,072 |0 /0 /0 |0 /0 |
|16 /1 |2,400 |3.455 |.025 |180,224 |2 /13.713 /10.370 |1 /0 |
|16 /16 |2,400 |4.312 |.243 |917,504 |17 /86.324 /12.647 |16 /15 |

Exactly4/16 participant identities are used, with ring target selection and independent
synthetic per-candidate trajectories (not a coherent physical multiplayer simulation).
Every qualifying participant's final file survived load/validation. Intermediate
revisions coalesce on one worker; disk serialization is not on the main thread.
These numbers cannot establish live main-thread spike/GC behavior under transport,
terrain rendering or an OS-stalled disk. Finalize timing includes submission, not
waiting for durability. The older14.426ms bridge figure was total for2,416 calls.

Final actual16-prefab clean recorder bridge:14.652ms total for2,416 calls, heap delta
131,072bytes afterGC. Reflection/GetComponent/posture included; spawning/transport/
physics/IO/GC-measurement excluded. It remains fixture timing.

Before optimization,16-actor clean target-churn fixture (target changes every sample,
same5Hz/30virtual seconds/2,416 calls) took10.771ms with79,544,320-byte heap delta.
Lazy clip allocation/GUIDs and a reused clean lead ring reduced final rerun to3.288ms
and131,072bytes, zero incidents. Matching workload/measurement method, not a live
allocation benchmark. Clip frames/alerts allocate only after an actual alert.

Actual-map PhysX query fixture places synthetic candidates around a generated solid
building, same5Hz/30virtual seconds. Seed2385:4 actors604 evaluations/9,060 queries;
16 actors2,416 evaluations/36,240 queries (15 per confidently covered evaluation).
Final measured totals12.314ms/42.155ms respectively, from Logs/RC-terrain-final.log.
These exclude transport/target filtering, include native
geometry calls, and are not additive live frame guarantees. Incident capture adds
zero physics/network queries. Main/worker Profiler markers now support the short live
capture in ReleaseCandidateRuntime.md: AC.Movement.Observe, AC.Aim.Observe,
AC.Incident.Capture/Finalize/Queue/Write, worker AntiCheat/IncidentWriter.

## Actual validation and release-candidate decision

- Eight pure suites **341 checks** (18 notice,65 integrity,33 horizontal,78 request/
  config,36 effects,58 received movement,24 aim,29 incident).
- Unity6000.3.8f1 full project import/compilation/FishNet processing passed;
  **121 native**, **60 unconnected Play Mode lifecycle**, **82 terrain** checks.
- Server/fixture harness retains its **22 checks**, plus **37 new server decision**,
  **29 incident integration**, **19 load** assertions. Simulated owners and fixture
  clocks are explicitly distinct from live transport/human play.
- Current147-source Editor/Development/Release compiler profiles passed. Existing
  metadata checks confirm development probes/fixture state absent Release, normal
  telemetry/reviewer retained, aim/incident code has no policy/disconnect/damage/RPC
  path. GUID/.meta references, unchanged materials and preserved AC09 hashes checked.
- Actual Release player export/runtime, live profiling and new remote near-cover/
  pickup/lifecycle acceptance **pending**. Existing unrelated warnings retained.

Recommended status: **scoped capstone implementation is release-candidate complete
after the new interactive acceptance passes**. No known remaining automated blocker.
Newly fixed cover/pickup decisions require remote regression before calling them
accepted. Falsified client root/aim/hash claims, trusted host, no lag rewind, sparse
movement contact truth, local editable review files and limited3-file coverage remain
explicit architectural limits, not proof of prevention of all cheats. No aim/review
flag or review note can punish. Both defaults ReportOnly and host exemption retained.

Ranked follow-up: (1) Windows must-pass acceptance and short profiler capture;
(2) adverse-network/stance/boost/terrain clean routes and any clean-alert regression;
(3) separately designed movement contact history if stronger traversal coverage is
required. macOS, broad stress and actual Release export remain unverified. Settings,
balance, cosmetic crouch, host migration, bots and broad authority changes deferred.
