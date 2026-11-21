# Occluded aim review signal (AC09)

Based on tested AC08 `384a6c6`, on integration/anti-cheat-merged-test. **AC08 checklist
passed per user aggregate report**; no per-case logs, timings or optional-test detail
were supplied. This does not establish AC09 acceptance or broader macOS/latency coverage.

## What the server actually receives

| Source / method | Observation and trust boundary |
| --- | --- |
| FirstPersonController.CameraRotation; Player NetworkTransform | Client rotates root yaw and local camera-target pitch. Root position/rotation arrive client-authoritatively; remote server transform can be smoothed. Existing root replication does not supply full camera aim. No authority/prediction/NetworkTransform changes here. |
| CombatPresentation.PrepareAim; HitscanShooter.LateUpdate/RequestShot | Camera centre-ray includes local look/recoil. Previously full direction arrived only at requested shots. Server chooses eye origin and validates direction, weapon state and geometry; isolated shot directions cannot prove continuous tracking. |
| LocalReplayCapture / ReplayArchive | Local root position/rotation recording, no camera pitch/recoil/continuous aim, no trusted server input. It is **not** used by this signal. |
| PlayerCameraSetup.ServerShotOrigin; PlayerPosture.Apply/AimOrigin | Approved stance immediately updates server collider/eye, with invariant root/feet. Actual shots originate at this eye; there is no separate authoritative muzzle offset. Crouch/stand changes reset observation. ADS is local FOV, not an aim/damage allowance. |
| HitscanShooter.ReportAim/ProcessServerAim; OccludedAimObserver.Receive | New owner-only 5Hz unreliable direction + sequence, on the existing NetworkBehaviour. Approximately 80 payload bytes/s/player (12-byte vector + 4-byte sequence), about 1.28KB/s at 16 players **before FishNet/transport headers**; server-only, never rebroadcast. Sender must be the actual owner connection reference, authenticated/active/spawned. Finite near-unit direction, approved pitch, advancing sequence; server bucket 6/s burst4 before processing. Burst coalesces into one latest sample. No target IDs or origins accepted. Invalid telemetry is silently dropped, not a cheating penalty. |
| NetworkTransform.OnDataReceived; OccludedAimObserver.ReceivedPosition | Copy remote target goal/tick/receipt time before smoothing; no pooled object retained. Tick is novelty only. Target evaluation still uses server collision pose, requiring a new received position within .65s and <=.9m disagreement; padded coverage includes both smoothed and received bounds. Host target positions are local, not remote telemetry. No client grounded/velocity or effect claim is trusted. |
| PlayerAudio; accepted-shot hook; HealthPresentationChanged(asServer) | Footsteps are local presentation based on movement, not proof of hearing. All candidates within 40m +2m margin are suppressed as potential nearby cues. Accepted server shots within 90m +2m give2.5s cue grace. Hurt cues use40m +2m; already covered by the more conservative nearby suppression. Nothing establishes that a listener heard a sound, their volume, terrain attenuation or headphones. |

This small feed is needed to observe changes **between shots**. It does not authorize
hits/damage, weapon values, targets, visibility or movement. A modified client can
omit it, send innocuous aim, falsify root poses, or manufacture correlations. Server
geometry establishes cover relative to its current replicated poses, not what was
rendered on a client's earlier frame, intent, wallhack use or actual camera direction.
No lag rewind, enemy replication filtering, aim correction or trusted attestation.
Cover is evaluated at sampled server snapshots; the report does not prove the target
remained hidden, or followed a particular path, between those snapshots.

## Conservative signal and bounded work

`AimOcclusionGeometry.Evaluate` returns Visible, Covered or Uncertain. Clear interior
body-point rays indicate exposure; to claim Covered, **all eight corners of padded
target bounds must be blocked by the same static BoxCollider**. A convex box's shadow
then covers the bounds. Other actors, Rigidbody/Animator obstacles, non-box/mixed
occluders, overlaps at the origin, saturated buffers, disagreement between eye and
weapon origins or exposed bound edges are Uncertain. An uncovered empty corner does
not establish visible body; it simply prevents confident cover. Triggers/cosmetic
layer2 are ignored. Mesh terrain and many future doors are deliberately inconclusive.
Box pose/size/centre changes reset evidence; open/moving doors cannot silently retain
old coverage. The currently selected building has one solid box, including decorative
windows; actual collision openings remain visible/uncertain, never presumed solid.

`OccludedAimObserver.LateUpdate` runs at most5Hz/player. Registry capped16 (UI default4,
supported1..16); one target context/player, no target history growth. Living eligible
same-scene opponents inside approved weapon range and a10-degree cone are filtered
before physics. Similar candidate bearings (<2deg separation) are ambiguous. Nearby
cue/stale pairs skip physics. Shooter movement >.05m/evaluation suppresses evidence:
this initial rule is for a **stationary observer tracking a moving hidden opponent**.
This avoids attributing bearing changes from walking to hidden aim tracking.

`OccludedAimTracking.Observe` requires2s visibility/uncertainty/cue grace, then a6s
window with>=20 fresh samples, <=.65s sample gaps, <=3deg alignment, >=12deg travel in
both aim and target bearing, and matching angular changes for>=80% elapsed time.
Matching changes each exceed.1deg, agree within cosine.9 and amplitude ratio.5..1.5.
Near-vertical bearings (>80deg) are uncertain. Completed windows do not overlap.
Held doorway aim/static targets/jitter cannot pass travel/correlation requirements.
Short follow-through after disappearing is tolerated. These are conservative demo
heuristics, **not a calibrated statistical cheating classifier**; correlations can
have legitimate explanations and never justify automatic punishment here.

Only fresh owner aim and fresh changed target data count; repeated evaluations,
old sequences, silence, stopped transforms, large server frame gaps, invalid data
or unsupported geometry cannot fill elapsed evidence. Latest-slot coalescing can
discard motion. Under loss/jitter this rule can miss suspicious behavior by design.

Spawn/respawn token revision, dead/pending placement/match state, approved stance,
large position changes, owner connection reference, target identity/life, round and
scene changes reset/suppress state. Eligible means active GAME with generated ready
terrain. Server-approved Invisibility on either actor excludes the pair, separately
from physical occlusion. Despawn/server stop/disable removes the registry entry and
both subscriptions; repeated Begin/End are idempotent. Reused numeric IDs inherit no
connection/target evidence. Cosmetic ragdolls never supply targets or cover.

Alerts are server-only `[Aim AC] action=ReportOnly rule=correlated-occluded-aim
source=client-aim`, at most once/10s/player. They include owner/object, target
owner/object, duration, samples, matched seconds, angular travel/error, distance,
cover name, aim/target age, crouch/effects, cue and query context. **No call to
FileAntiCheat policy, damage, disconnect or punitive horizontal suspicion exists.**
Dynamic=Disconnect does not change this. Existing static/sustained-speed optional
enforcement, ReportOnly defaults and trusted local-host exemption are preserved.

## Demonstration and evidence

On a server-spawned Player root in the Editor Inspector, OccludedAimObserver context
menu offers **Development/Log aim observation state (server only)** and **Development/
Run occluded aim fixture (server only)**. No new key binding or UI wiring. The
fixture creates an isolated local PhysicsScene: synthetic moving capsule bounds50m
away, one solid box, scripted matching aim at.2s steps for12s. It exercises the same
geometry and temporal model, **bypassing live admission/selection/freshness/audio/
lifecycle gates by explicit synthetic inputs**. An alert is labeled `[Aim AC FIXTURE]`
`source=scripted-fixture target=synthetic`, not evidence about a human or connection.
Stop via Development/Stop occluded aim fixture, or wait12s; stop/despawn unloads it.
It never moves live players, changes HP or contacts policy. All fixture/quality
controls, coroutine/state and context menus are absent from Release.

Actual automated checks: seven pure suites **312 assertions** (24 new aim cases);
Unity native **118** (16 new cover/correlation cases), **60** existing unconnected
Play Mode lifecycle checks; separate server-only **22** assertions (HP/ammo/stance,
aim sender identity, invalid/duplicate/burst rejection, repeated cleanup). Owners in
the latter are simulated; no remote transport claim. The actual development fixture
coroutine also ran with Dynamic=Disconnect: one scripted alert, duration6.00s,
31 samples,5.60 matched seconds, Covered,15 queries; actor HP100/position/activity
unchanged and isolated scene unloaded. This is agent-run scripted Play Mode evidence,
**not human play or remote-client evidence**. Editor/Development/Release
141-source compile, FishNet processing17 prefabs, release fixture/punitive-call
inspection and new asset metadata checks pass. Existing unrelated package/vendor/
LobbyCodeUI warnings remain. Logs are ignored machine-local validation artifacts.

Native synthetic benchmark on this machine:61 classifications/12 virtual seconds,
915 queries in2.487ms; maximum16 actors x5Hz x10 virtual seconds represented by800
classifications/12,000 queries in16.759ms total, including SyncTransforms. One full
same-origin Covered evaluation uses15 queries (2 overlaps,5 body rays,8 corner rays),
maximum1200 queries/s at16 actors; default4 gives300/s under this worst-case fixture.
Separate origins can require23 queries/evaluation. This is a small static fixture,
not realistic map population, network load, broad-phase complexity or frame profiling.

One initial elevation fixture incorrectly expected exposure while its sightline
still crossed the tall wall; corrected the fixture, not detector tolerances. No clean
fixture produced an alert. Native regression includes real clear rays, mixed cover,
partial/crouch/elevated exposure and origin-inside blind spot. Pure regressions include
held angles, brief lost visibility, corner appearances, cues, stale/missing/duplicate/
delayed updates, independent players, angular wrap and lifecycle resets.

**Pending:** AC09 actual owner telemetry/selection and live-map human clean behavior,
remote obstruction HP, supported-size live performance, high-latency characterization,
macOS, Release runtime and all interactive checks in [OccludedAimRuntime.md](OccludedAimRuntime.md).
AC08 user pass is not evidence for these new checks. Review any clean alert before
expanding scope; retain its trajectory/geometry as a regression. Never tune thresholds
merely to make the demonstration pass.

Source map: [Shooting.cs](../Assets/Scripts/Shooting.cs),
[OccludedAimObserver.cs](../Assets/Scripts/OccludedAimObserver.cs),
[AimOcclusionGeometry.cs](../Assets/Scripts/AimOcclusionGeometry.cs),
[OccludedAimTracking.cs](../Assets/Scripts/OccludedAimTracking.cs),
[PlayerCameraSetup.cs](../Assets/Scripts/PlayerCameraSetup.cs),
[PlayerPosture.cs](../Assets/Scripts/PlayerPosture.cs),
[Health.cs](../Assets/Health.cs), [PlayerAudio.cs](../Assets/Scripts/PlayerAudio.cs).

Reproducible checks: `powershell -ExecutionPolicy Bypass -File
Tests/RunAntiCheatChecks.ps1`; Unity Tools/Integration/Validate Team Features, then
Tools/Anti-Cheat/Validate Server Obstruction (includes actual scripted coroutine).
`Tests/CheckAimAssemblies.ps1 -DevelopmentAssembly <compiled-dev-DLL>
-ReleaseAssembly <compiled-release-DLL>` checks controls/feed/punitive-call metadata.
This batch used current-source compiler-profile DLLs for Development/Release metadata,
not an exported Release player; actual Release build/runtime remains pending.
