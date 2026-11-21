# Received-goal movement observations — 2026-11-21

Base integration/anti-cheat-merged-test **ad26fe8**. Since previous hardening 28a3482,
only the user's committed Road/Water material changes were present; initial tree clean.
No branch integration/reset/vendor/scene/prefab/material change in this batch.

The user reports the latest **AC07 runtime batch passed**, recorded as aggregate
user-reported evidence. Its required F7 disconnect/MainMenu/manual-rejoin regression
is included in that aggregate report, superseding the earlier AC06 skipped status.
No per-step logs, timings, measurements or optional-fixture breakdown were supplied.
No new AC08 or simulated-network runtime acceptance is inferred.

## Actual movement model / evidence boundary

| Source inspected | Current behavior and implication |
| --- | --- |
| ApprovedMovementLimits; ApprovedMovementBoost.Update; owner Starter Assets FirstPersonController.Move | Walk 4, sprint 6 m/s; server Speed selects 1.5x (walk 6/sprint 9). Crouch walk 1/1.5, sprint retains priority 6/9. Acceleration uses SpeedChangeRate 10 and controller velocity locally. Server never treats those client velocity/input fields as proof. |
| FirstPersonController.JumpAndGravity; player prefab | Jump height 1.2, approved Jump 1.8; gravity -15. Derived initial upward velocity sqrt(-2*g*h) = 6 / sqrt(54) (~7.35) m/s, theoretical apex time .4/~.49s before discrete/collision effects. Gravity is applied in the same update; JumpTimeout .1, FallTimeout .15. Its positive terminal-velocity comparison does not implement a dependable downward speed cap; this batch deliberately does not classify fast falling or edit vendor/controller code. |
| FirstPersonController.GroundedCheck/Update | Local CheckSphere at rootY - GroundedOffset (offset -.14), radius .5, GroundLayers mask bits 9 (layers 0 and 3), triggers ignored. Jump uses the previous frame's Grounded before checking it again. None of these client flags are an authoritative contact/trajectory history. |
| CharacterController prefab; PlayerPosture.Apply; PlayerCameraSetup | Height 2, radius .5, centerY .93, slope limit 45 degrees, step .25, skin .02. Crouch clamps height to diameter 1, centerY .43; feet remain rootY-.07, root scale/position unchanged. Visual compression and eye changes do not justify a root-Y jump. Server-approved stance changes collision/shot origin immediately. Snapshot server gravity/slope/step/feet geometry at spawn; no client limit fields. |
| RoundImplementation worldSettings -> New World Settings/New Structure Settings; TerrainGenerator/RoadMeshBuilder/StructureSpawner | Generated terrain, static procedural road/bridge MeshColliders and Building_01 collider prefab. No Rigidbody/Animator/MonoBehaviour on that selected building prefab. WaterSurface creates a rendered plane without collider/swimming/buoyancy. No platform-carry/jump-pad rule found in active controller/terrain flow. Moving/kinematic bodies, players, parented/tilted/scaled roots or unready collision data are ambiguous for new air observations. Future moving transforms without Rigidbody need explicit compatibility review. |
| GameplayPlayerSpawner.TryGetSpawnPosition/TryGetRespawnPosition; Health.Update/AcknowledgeRespawn | Server chooses separated, dry, slope/clearance-checked placement. Spawn/ownership grace 1.5s; death/pending respawn/match lock suppress observation. Only issued respawn token authorizes placement; ACK rebase cannot extend its fixed grace. New evidence clears with these same lifecycle resets, phase transitions, despawn/server stop and ownership changes. Client Teleport flags never authorize grace. No other active runtime teleport/carry rule found. |
| Player NetworkTransform prefab and installed NetworkTransform.cs | Client-authoritative root, send interval 1 tick, interpolation/extrapolation 2 ticks, automatic distance teleport disabled; scale/parent synchronization disabled. TimeManager default 30Hz, no serialized MainMenu tick override found. Host-server remote transforms can be smoothed; server-only receive processing uses instant rates. Existing punitive horizontal observer samples the resulting transform in LateUpdate and is preserved unchanged. |
| NetworkTransform.ServerUpdateTransform/DataReceived/OnDataReceived (lines ~2315-2368); SpeedHackDetector.OnReceivedGoal/ObserveReceivedGoals | New rules copy decoded local Position/Tick before smoothing, only for active authenticated remote owners on the server. Root local coordinates equal world only in the supported unparented layout. Callback data is pooled: copy, never retain/mutate. Advancing packet tick marks novelty only; all budgets use monotonic SERVER receipt time. Reliable settled/rotation-only identical positions are not fresh position evidence. Local host has no remote receive stream; new rules explicitly skip it. Old host observation/exemption remains. |

One latest-goal slot coalesces bursts; no unbounded queue. Changes <=1mm (the current
prefab position sensitivity) are treated as unchanged, not hover evidence. New collision sampling is
at most 4Hz/player. Multiple moves within one server frame can be lost; remote ticks
are not trusted elapsed time, jump time, velocity or entitlement. Goal positions are
still client claims, not attestation or actual paths. No sparse-segment noclip test.

## New, permanently report-only rules

Implemented in ReceivedMovementObservation, MovementSupportQuery and the existing
SpeedHackDetector (no added NetworkBehaviour or RPC). **They never call punitive
policy or modify HorizontalMovementObservation.Suspicion**, even in Dynamic Disconnect.

| Rule | Evidence / bounds |
| --- | --- |
| repeated-unexplained-displacement | Reuse the existing displacement margin/slack/burst constants on raw horizontal changes, plus a derived upward counterpart. Three separate events in 15s, >=1s apart; fixed three-time ring. Rebase on every event; one jump, duplicate goal or smoothing frames cannot count repeatedly. Long packet gaps increase distance allowance by server elapsed time; downward displacement is not called a teleport because long falls are legal. |
| excessive-unsupported-ascent | Clear endpoint collision-proximity volume, fresh changed positions with <=1s gaps, >=6 samples over >=1.5s. Upward ceiling = max(approved jump launch, approved sprint*tan(server slope limit)). Compare net rise against ceiling*time + ceiling*.35s + server step + existing .5m slack. Peak allowance retained for one completed window across boost transitions, then refreshed, not indefinitely. This catches excessive rise, not every slow fly trajectory. |
| unsupported-level-motion | Fresh changed XYZ goals, clear support volume throughout, height span <=max(.05m, 2*skin). At least 8 samples over max(4s, 4*upwardCeiling/abs(gravity)). Report includes actual horizontal distance and height span. This is a moving hover candidate; **motionless/silent hovering is not reliably detectable**. Rotation-only or identical goals clear the air interval; they never mature a hover verdict. |

Broad support proximity overlaps the radius+step footprint down below feet; own
colliders, triggers and Ignore Raycast/corpse layer are excluded. Any nearby static
geometry suppresses air inference (not proof of groundedness); Rigidbody/player
support, saturated 32-hit array or missing ready terrain returns Unknown. This favors
missed candidates over falsely accusing stairs/edges/bridges/roofs. It is not a swept
path test or a reliable wall-climbing/noclip detector. Sampling can miss intermediate
contacts; these rules remain observations pending runtime/network characterization.

Server frame gaps >1s reset new evidence; missing network goals break air continuity.
Invalid numeric/geometry inputs never reach physics as evidence. Each rule logs at
most once/player/5s with suppressed counts. Logs include observed/allowed values,
elapsed/fresh count, owner/object, raw source, receipt age/tick, support/quality,
approved effect/limits/gravity, crouch, armor, phase and lifecycle state. A server
Inspector Development/Log movement sample quality entry gives a neutral snapshot.
No heartbeat or periodic client scan was added.

The original horizontal limits, suspicion/decay, 1s Speed tail, threshold 8 (5..10),
ReportOnly defaults and host exemption are unchanged. Only eligible sustained
horizontal-speed and validated on-time static mismatches/missing-required-file cases
can disconnect through the existing optional policies. Invalid gameplay requests
are still prevented independently of punitive modes. New motion is observed, not
corrected/stopped. Client-authoritative positions, silent hover/slow fly, within-frame
motion, contact history, latency and malicious-host/hash-report limits remain.

## Development controls / recovery

Inspected bindings: F6 replay, F7 speed, F8 single teleport, F9 integrity/Ctrl+F9
duplicate, F10 request probes; Alt+F7/Alt+F8 are unused by default input actions.
No binding IDs/preferences changed. These chords and menu buttons are **absent from
release**. Use menus if custom bindings or Touch Bar access make chords inconvenient.

| Control | Exact behavior / expected server result |
| --- | --- |
| Plain F7 / F8 | Existing speed toggle (3x) / single +30m X jump retained. Single displacement does not qualify for a new repeated report. |
| Alt+F8 | Cycle Off -> Repeated teleport -> Ascent -> Level motion -> Off; selecting another mode resets the prior mode first. |
| Escape menu Development buttons | Directly select each of the three probes; Resume starts its timer. Menu opening while a probe is running stops/returns it. No local menu time pause for server. |
| Repeated teleport | Alternate captured start and +40m X every 2s. After three independent qualifying events expect repeated-unexplained-displacement, action=ReportOnly. Choose an open area; packets coalesced/lost or inadequate data can delay/prevent the report. |
| Ascent | Force local Y upward at 3x local jump launch rate; expect excessive-unsupported-ascent only after clear supported sample conditions. XZ authority/rules unchanged. |
| Level motion | Hold Y at start+11.2m and move XZ in a .25m orbit so there really are changing position packets; expect unsupported-level-motion after sufficient clear-air data. This is not a silent stationary-hover demonstration. |
| Alt+F7 / menu reset | Disable speed/new probes, reset local controller motion, return an active probe to its captured start. No server exception/grace RPC. Return itself can add a report-only event. Each probe auto-resets/returns after 12s. Death/ownership loss/despawn cancels without returning over a server placement/respawn. |

After each probe walk/jump/crouch normally; allow the 15s event history to age before
a separate repeated-event trial. The actual received point may be unready/near a
wall/roof: use the quality snapshot and an open location, not relaxed thresholds.

## Available checks / acceptance status

Agent executed: **288 deterministic assertions** (230 previous + 58 received-motion
cases), full Unity6000.3.8f1 import/compile/FishNet processing (**17 prefabs**), **102
native checks** (including 13 support/stop fixtures), **60 unconnected Play Mode**
lifecycle checks; current-source Editor/Development/Release compilation (137 files).
Development methods present, release F6-F10/probe methods/fields/network-test setting
excluded. New GUID/meta references checked. No vendor, prefab ordering or registration
change. Existing unrelated compiler/package warnings remain.

Pure tests cover legal 1.2/1.8 jumps, elevated fall, slopes/steps, boost expiry,
stationary/stale/duplicate/bursty arrivals, reset/recovery, separate players, event
expiry and all new rules' policy ineligibility. Native fixtures cover floor/bridge,
incline/step, crouch feet anchor, self/trigger/corpse exclusions, dynamic/saturated/
missing support, listener removal and repeated stop. They do not establish live
FishNet delivery or interactive probe/latency acceptance. Local evidence:
Logs/MovementExpansionFinal.log and Movement-*-compile.log (ignored).

No new standalone build, interactive multiplayer or adverse-network simulation was
run by the agent. [MovementExpansionRuntime.md](MovementExpansionRuntime.md) is the
pending AC08 gate. AC07 three hashes/client IDs on Clean/Test and installed default
server manifest match archived **84e29324cc274267be86f92042703d49**, checked read-only.

Next highest-value work: record AC08 clean/adverse-network evidence and false-positive
boundaries, then review slow/silent flight and collision/contact observability before
any new enforcement. Trusted authoritative trajectories would require separately
authorized networking/authority work. Defer animation, balance, bots and migration.
