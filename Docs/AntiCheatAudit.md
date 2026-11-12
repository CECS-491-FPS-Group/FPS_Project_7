# Trust-boundary audit and first weekend batch — 2026-11-12

Checkpoint update: the user supplied AC03-Clean F10 rejection logs for invalid aim,
switch budget, scoreboard and balance query limits; normal movement/shooting worked
and no disconnect occurred. Record these as user-observed targeted results, not
startup Verified or a complete boost/pickup/recovery/isolation pass. AC03 artifact
verification and the corrected server installation/missing Test descriptor are
recorded in AntiCheatHandoff.md and WindowsAntiCheatBatch.md. All 112 pure checks
were rerun before the source checkpoint; interactive remaining coverage stays pending.

## Checkout and evidence

Inspected `feature/anti-cheat-complete` at `e4d0e157bea4095350b08f4025530608c1d674f9`.
Starting working tree was clean. `fe0f922..HEAD` changes only the five handoff/status
documents; gameplay source still matches the recovery checkpoint before this batch.
AGENTS.md, all five requested documents, current project scripts, active player
prefab inheritance and installed FishNet source were inspected. Unity remains
6000.3.8f1. MainMenu's spawner references Player.prefab, which inherits the capsule
variant; its NetworkTransform remains client-authoritative, interval 1, smoothing 2/2.
No vendor/prefab/scene/authority edits are included in this batch.

Status labels below mean **protected** (implemented), **needs runtime coverage**
(implemented but not established by the reported tests), **gap fixed here**, or
**deferred/limitation**. User-reported AC02 tests are historical evidence, not newly
performed integration tests. File references describe the resulting source.

## Combat

| Entry point / evidence | Finding and status |
| --- | --- |
| `Assets/Scripts/Shooting.cs:105` RequestWeapon | **Protected:** FishNet ownership plus explicit sender==Owner, active/authenticated, server/spawned, Health.CanAct, bounded index, different equipped ID, server 0.25s switching deadline. Previous magazine is saved, current reload cancelled, chosen magazine restored. Shot cooldown is deliberately not reset by switching. **Needs runtime coverage:** rapid switching during cooldown/reload, latency and respawn magazine state. |
| `Shooting.cs:156` RequestReload and `:125` ReconcileServerWeapon | **Protected:** sender/ownership checks; living/unlocked health, not already reloading, ammo below magazine, finite positive server reload duration. Completion occurs only at the server deadline in Update/request reconciliation; no client completion RPC. Death/match end cancel reload; accepted respawn refills server magazines. Duplicate reloads cannot accelerate it. |
| `Shooting.cs:213` RequestShot | **Protected:** active authenticated owner, server/spawned, finite origin, living server Health, finite near-unit direction, pitch/yaw consistency, valid server weapon values, not reloading, positive ammo and server cooldown. Only direction is client supplied; no damage, hit ID, origin, range, cadence, ammo or boost amount is accepted from the client. Invalid directions/cooldown requests return before ammo consumption/physics. |
| `Shooting.cs:74` OnStartServer, `ApplyProfile`, selected SyncVar/Profile | **Protected:** server selects from its configured profiles; damage/rate/reload/range/magazine originate from the server copy. Client edits affect local presentation/input, not server limits. The current un-serialized profile array uses Sidearm/Rifle/Shotgun defaults. Future weapon profiles do not require a hardcoded anti-cheat weapon-ID allowlist. **Deferred:** malformed developer configuration (empty/null profile arrays, nonfinite aim/spread settings) needs a separate asset/configuration validation pass; it is not evidence of a malicious client. |
| `Shooting.cs:282` FireServerRay | **Protected:** server camera-mount origin; server range and spread/pellet count (clamped 1–16); RaycastAll chooses nearest collider after excluding self and dead bodies; triggers ignored, world geometry blocks damage. Victim must be spawned, tagged Player, active owner, different owner. Health.TakeDamage owns damage/death. **Needs runtime coverage:** wall/door/collider edges, shotgun and rapid yaw/latency. |
| `Assets/Health.cs:69` TakeDamage; MatchBoosts.Has / Invulnerable / DamageMultiplier | **Protected:** damage only through server methods, positive integer damage, spawned/live/not match-ended; server Shield dictionary/deadline blocks damage, server Damage multiplies by 2. Health's first positive-to-zero transition pays one kill reward and records death once. Effects clear on death/match end. **Needs runtime coverage:** Shield/Damage replacement/expiry and boosted death. |
| `Health.cs:26` CanAct, `GameplayPlayerSpawner.cs` ReturnToLobby, `RoundTimer.cs` BeginPhase/Update | **Protected:** dead/respawn-pending and final match lock reject actions. **Existing gameplay rule:** CanAct does not require GAME phase; pregame/postgame combat remains possible until EndMatch locks it after postgame. No new phase restriction was invented. Confirm desired phase design separately. |
| RequestShot's former 60-per-fixed-second counter; unbudgeted reload/switch | **Gap fixed here:** all three actions now consume one connection-scoped processing budget before reconciliation or raycasts. Rate/burst floor 60; increased from fastest valid server weapon rate (`max(60, 2*rate+8)`). This is processing admission, separate from the unchanged per-shot cadence. Rejected calls do not imply cheating or call enforcement. |

Combat limitations: roots are still client-authoritative replicated/smoothed positions.
The server mount is not proof of legitimate movement. There is no rewind/lag
compensation, client click-history verification, aim-bot detector or visibility-based
network secrecy. Raycasts starting inside world colliders and camera/cover placement
need geometry tests; changing collision semantics without those tests could break
legitimate shooting. The existing aim envelope is a sanity check, not trusted aim.
`maxYawDifference` can reject rapid legitimate aim arriving before root rotation;
observe latency results rather than loosening it speculatively.

## Movement and lifecycle

| Entry point / evidence | Finding and status |
| --- | --- |
| `SpeedHackDetector.cs` GetServerHorizontalSpeed / LateUpdate; `MatchBoosts.cs` ServerHasSpeedBoost | **Protected:** server constants 6 m/s base sprint and 9 with server-approved Speed state; no client speed/ping/snapshot-based allowance. Shield/Damage/Jump keep 6 horizontal. |
| `HorizontalMovementObservation.cs` Observe | **Protected:** monotonic elapsed time; disjoint ~2s windows, suspicion reporting at 4/cap 10, clean elapsed decay; bounded replication slack and 1s prior-Speed tail on server state decrease; boost changes cannot reset suspicion or renew tail by continued fast movement. Server frame gaps >1s are inconclusive. Sustained-only enforcement at effective 5–10 (default 8) precedes warning throttling. |
| `SpeedHackDetector.cs` OnStartServer / OnOwnershipServer / AuthorizeServerRespawn / CompleteServerRespawn; `Health.cs:101` Update / `:143` AcknowledgeRespawn | **Protected:** spawn/ownership baseline grace; dead/invalid owner/match teardown discard state. Server chooses a finite safe spawn through the spawner, issues a nonzero token, places first; owner ACK must match pending token and ownership/dead/match state. ACK rebases without extending the fixed grace deadline. Duplicate/stale ACKs do nothing. No ACK means existing dead state persists (no timeout/recovery added). |
| `PlayerCameraSetup.cs` ApplyRespawnPosition; `GameplayPlayerSpawner.cs` TrySpawnPlayer / TryGetRespawnPosition / ReturnToLobby | **Protected:** collider-ready generated terrain and server-selected placement, client readiness token/seed/presence before spawning; one player per connection. Scene/match teardown retains existing behavior. Only respawn is currently an approved runtime teleport; legacy TerrainSpawnPlacer is not attached. |
| Starter Assets FirstPersonController JumpAndGravity / GroundedCheck; ApprovedMovementBoost.Update | **Needs runtime coverage:** owner-side Jump height 1.2/1.8, slopes, steps, falls, Speed expiration/replacement, boosted respawn and server delivery bursts. Server horizontal observer does not trust the owner's Grounded flag or vertical controller velocity. |
| Full XYZ is replicated, but observer samples XZ only | **Deferred/limitation:** repeated displacement can be reported, but it intentionally rebases without charging jumps to sustained suspicion. Single or repeated displacement alone never disconnects. Vertical/fly observation is technically possible, but legitimate terrain elevation, steps, long falls, Jump boosts, smoothing/backlogs and respawn must be characterized first. No verified server vertical envelope or authoritative grounded trajectory exists. A vertical-speed cap or repeated-teleport punishment would be speculative now. |

## Other gameplay entry points

| Entry point / evidence | Finding and status |
| --- | --- |
| `Assets/Scripts/MatchBoosts.cs` Update / SpawnPickup | **Protected:** no collect RPC; server creates/chooses/expires pickups only during GAME. Only active/authenticated living gameplay players are eligible; 3D radius 2.5m; list entry is removed before granting effect, so a second player/request cannot consume it again. **Gap fixed here:** the former `distanceSquared > 6.25` rejection failed open for NaN. A positive finite distance predicate now rejects NaN, Infinity and overflow while preserving the same 3D radius. |
| Owner-replicated pickup position | **Deferred/limitation:** position is still owner-authoritative; teleporting to a pickup can acquire it until detection/enforcement. Pickup line-of-sight is not checked; collection through thin walls within radius needs a design decision. The numerical fix does not prove movement legitimacy. |
| `RoundHandler/TrackPlayerCurrency.cs:117` AddCurrency / AddCurrencyToRegisteredPlayers; `Health.cs:69`; `RoundTimer.cs:106` | **Protected:** server-only positive reward API, registered active/authenticated connection, integer overflow rejection. Kill payout is tied to first death transition; passive income occurs during server GAME only, with no burst catch-up. Balance request contains no amount or player ID. No current purchase/spend entry point was found in active project scripts; do not invent shop validation. |
| `TrackPlayerCurrency.cs:93` OnBalanceRequest, `MatchScoreboard.cs:125` OnRequest | **Gap fixed here:** authenticated queries formerly repeated player scans, snapshots/sorting/serialization/replies without admission limits. Each now has an independent per-connection 2/s, burst-4 budget before that work. Server-originated rewards/snapshots are not throttled by client query budgets. |
| `NetworkLobbyUI.cs` ReportPingServerRpc / CmdToggleReady / StartGameClicked | **Protected:** sender-attributed roster membership, active/authenticated checks, clamped reported ping, transition/ready revalidation. **Concrete remaining gap:** ready/ping updates have no frequency budget; repeated changes can cause UI/broadcast churn even though they cannot create another player's entry or change gameplay limits. Lower priority than this batch's combat/query work. |
| GameplayPlayerSpawner.OnTerrainReady; Health.AcknowledgeRespawn | **Protected:** authenticated active connection and bounded pending state, accepted once per seed/token/lifecycle. Duplicate payloads reject before spawning/placement; no unbounded request evidence storage. |

Installed FishNet source was checked: RpcProcessor.CreateRemoteClientIsOwnerCheck
generates ownership checks for RequireOwnership RPCs; NetworkBehaviour.ReadServerRpc
invokes the registered reader with the actual sending connection. Broadcast handlers
default to requiring authentication. The gameplay methods retain explicit sender,
active-state and ownership/membership checks instead of relying solely on codegen.
Graceful Disconnect(false) marks the sender inactive immediately; project combat,
respawn, ready, ping, terrain and query handlers reject it. Vendor networking remains
unchanged. Budgets protect application work, not all transport/decoder CPU or traffic.

## Static integrity and recovery

| Entry point / evidence | Finding and status |
| --- | --- |
| FileAntiCheat.ServerState; BuildIntegrity.Validate / ReadManifest | **Protected:** trusted local developer-installed manifest, schema/platform/backend/build ID and exact ordered required paths/hashes; 8KiB cap. Client expected hashes never replace the server copy. Errors loading baseline log ServerConfigurationError, not a cheating verdict. Tool hashes finished Windows Mono files, writes separate client/server descriptors, does not rebuild. macOS handling is unverified; IL2CPP unsupported by schema 1. |
| FileAntiCheat.Receive; BuildIntegrity.Compare | **Protected:** actual authenticated active sending connection/session; fixed-size primitive payload with exactly three numbered slots; duplicate/missing slot coverage, invalid status/hash encoding, unsupported schema/build/backend/platform rejected/classified. Sequence must increase, with >=15s cooldown. Duplicate/replayed/flood reports do not retrigger checks or enforcement; invalid-frequency log once per connection. Only validated, on-time HashMismatch/MissingRequiredFile can disconnect when server chooses it. Unreadable vetoes mixed-failure enforcement conservatively. |
| FileAntiCheat.Update / Authenticated / RemoteState / ClientState | **Protected:** 60s startup deadline, one missing log; subsequent late reports stay late through rechecks; state bound to connection and removed on stop. Never-reporting clients receive one MissingReport log and **remain connected even in Disconnect mode by current policy**. This is unavailable verification, not a pass. Malformed/config-error reports can satisfy receipt bookkeeping but remain explicitly unverified; no automatic retry/enforcement is claimed. |
| ClientDisconnectRecovery / DisconnectNoticeState | **Protected and historically user-tested:** fixed server-only targeted reason, best-effort graceful close, local scene recovery, clear-on-dismiss/new-attempt, local-host exemption. Static/dynamic mode defaults remain ReportOnly. Exact-message tests repaired to the user's intentional text. No new network messages or enforcement categories added here. |

**Architectural limitation:** a compromised client can report known-good hashes/build
identity, suppress reports or falsify missing/unreadable statuses. This is demonstrable
file-integrity detection, not trusted remote attestation. Missing-report enforcement
was deliberately not expanded here; its availability/compatibility policy needs its
own reviewed batch. A player-hosted server trusts the host. AC02 hashes matched both
preserved copies and the installed trusted manifest during this session; nothing in
that build/manifest pair was modified.

## Ranked batches

| Priority | Work | Impact / effort / false-positive risk / demonstration |
| --- | --- | --- |
| 1 — implemented | Shared combat request admission, expensive query admission, finite pickup eligibility; stale notice assertions | High application-work/numeric safety value; small changes; low risk (generous refill, no punishment); F10 invalid-input/flood probes and deterministic numerical checks |
| 2 — next runtime batch | Normal weapons, reload/switch/respawn, Shield/Damage, Speed transitions, pickup race and request recovery | High value to establish working authoritative protection; low implementation effort; no thresholds changed; clear HUD/server-log/gameplay evidence |
| 3 — follow-up code/validation | Ready/ping churn limits, server profile configuration validation, static unavailable/error matrix | Medium/high value; small/medium effort; low risk if configuration failures remain distinct; repeatable request/config fixtures; no new detectors needed |
| 4 — geometry/latency validation | Origin/cover edges, fast aim, queued delivery, disconnect/scene races, multi-client isolation | High practical value; medium effort; uncertain until measured; server ray/evidence and legitimate-control tests |
| 5 — optional new observation | Vertical/fly or repeated-teleport reporting | Medium demonstration value; medium/high effort and false-positive risk; first characterize terrain/falls/boosts and start ReportOnly if later authorized |

## Checks and demonstrations

Executed: repaired 18 notice checks, existing 31 integrity + 33 movement checks, and
30 new pure gameplay-validation checks (112 total). Editor, development/release-symbol
runtime and Editor-tool compilation against installed Unity references passed with
existing unrelated warnings. Development F10 probes call only existing RPCs/broadcasts;
they cannot grant effects, HP, ammo or exemptions. No static/dynamic modes are consulted
by request rejection. Test helpers do not instantiate FishNet, execute physics, or
simulate Health RPC lifecycle: full wiring/gameplay/transport tests remain pending.

Reproduce pure suites with
`powershell -NoProfile -ExecutionPolicy Bypass -File Tests/RunAntiCheatChecks.ps1`.
The default Windows script policy initially blocked the runner before tests; this
process-only override passed without changing persistent execution policy.

See [WindowsAntiCheatBatch.md](WindowsAntiCheatBatch.md) for the next practical runtime
batch, the checked-unused AC03 build/manifest workflow, exact F10 meaning, expected
logs and remaining runtime tests. Earlier AC02 manual results remain user-tested,
not newly agent-observed passes.
