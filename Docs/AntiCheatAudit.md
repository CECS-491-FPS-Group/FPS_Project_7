# Trust-boundary audit and first weekend batch — 2026-11-12

## Current closeout: active wiring and Release — 2026-11-23

Resumed **b2b7343**, clean tree on integration/anti-cheat-merged-test. No intervening
changes or unrelated edits. **AC09 passed per user Windows report**; no invented
per-case logs/measurements. AC10 automated source checks passed; **AC10 interactive
acceptance is still pending**. AC10 folders/archive are absent locally, not silently
assumed installed. No missing in-scope implementation or unwired planned protection
was found. New changes explain decisions and test wiring/Release, without changing
gameplay, detector thresholds, networking, policies or adding a detector.

Source checkpoints: **e1e7d9d** comments, **c536a5d** active wiring/export checks.
Actual Windows Mono non-Development **AC11-Release-Clean** exported, matching manifest
f64c7d6b877842c68a3b65ba10f74c10 generated from that finished build. Exported DLL passes
control/reachability/default/aim independence checks. Player launched/rendered MainMenu
and Theater entry, logged menu listener, closed normally; **no Release host/join/gameplay
or startupVerified test was performed**. AC09/installed baseline untouched. Future
results must identify candidate/buildId: neither AC09 acceptance nor AC11 launch
establishes AC10 acceptance. macOS, broad latency/stress remain unverified.
Decision traces, tests and cost methodology: [CapstoneDecisionValidation.md](CapstoneDecisionValidation.md).
New acceptance: [ReleaseCandidateRuntime.md](ReleaseCandidateRuntime.md), next unused AC10.

| Protection / actual active wiring | Decision and action | Strongest evidence / implementation versus evidence gap |
| --- | --- | --- |
| Static integrity: MainMenu NetworkManager -> GameplayPlayerSpawner.Awake installs FileAntiCheat; authenticated Report broadcast -> Receive/EvaluateReport -> BuildIntegrity/IntegrityReportLifecycle | Fixed three slots versus developer-installed baseline, exact coverage/build/session/rate/deadline. Only timely validated mismatch/missing file optionally disconnects; unavailable/config/late/unsupported/malformed distinct and nonpunitive. |65 pure plus native lifecycle, historical Windows tamper/recovery and AC09 user report. Active installer and exported Release paths verified. No missing implementation. New AC10 startupVerified requires live evidence; offline hashes/launch are not verification. Client hashes falsifiable, three files only. |
| Horizontal movement: spawned Player.prefab variant has enabled SpeedHackDetector + client-authoritative NetworkTransform; LateUpdate -> HorizontalMovementObservation -> FileAntiCheat.EvaluateDynamic | Server ApprovedMovementLimits + MatchBoosts.ServerHasSpeedBoost; integrated elapsed allowance, bounded transition, Health-issued respawn grace. Sustained horizontal evidence optionally disconnects at unchanged threshold8. No correction. |33 horizontal pure; actual resolved authority/observer wiring and server spawn checked; native terrain82; AC08/09 user reports. No missing implementation. AC10 clean boosts/respawn and F7 isolation/rejoin pending. Replicated roots/smoothing remain untrusted observations. |
| Secondary movement: same observer subscribes OnDataReceived, copies raw pooled goals/ticks, ObserveReceivedGoals -> ReceivedMovementObservation/MovementSupportQuery | Bounded fresh receipt evidence for repeated displacement/ascent/unsupported moving level motion; report-only in every mode, never horizontal suspicion. Stale/ambiguous support suppresses. |58 pure received plus native support/terrain; AC08/09 user reports. Deliberately no generic wall-traversal detector, slow/silent flight guarantee or contact/path authority. Source instrumentation is wired, not a hidden prototype. |
| Combat/world cover: selected variant's single HitscanShooter under approved PlayerCameraSetup camera; owned RequestShot/Weapon/Reload -> ProcessServer... -> CombatRayGeometry -> Assets/Health.TakeDamage | Auth/session/budget/alive/config/finite/aim/cadence/ammo/reload/stance/origin gates before request side effects. Eye/body overlap + bidirectional path and supported root. Invalid origin rejects without ammo/damage/reward/effects; valid wall shot spends ammo/presents, nearest solid stops damage. No rejection kick. |Existing22 server/fixture +40 decision assertions, native PhysX and AC09 earlier-build user acceptance. Active camera/prefab/registration verified. No code omission. New remote near-cover regression pending; deliberately no lag rewind/position attestation. Existing pre/postgame CanAct policy retained. |
| Pickups/effects/rewards: active RoundImplementation RoundTimer.Start adds MatchBoosts/MatchScoreboard; MatchBoosts.Update polls owned Health -> TryCollectServer -> ApplyServerBoost; Health death -> RecordDeath -> RewardServerStreak | No collection RPC. Living same-scene owner, unexpired ID/range/clear body path, atomic grant/consume once. Server effect values/deadlines/replacement, Armor separate, death/round reset, original Shield/Speed/Jump/Damage pool at5/10/... consecutive PvP kills. No rejection kick. |Actual RoundTimer installer now exercised, contention/duplicate/death/expiry/stale-round/Armor/Invisibility server cases within40;36 pure effect checks. New reachable/obstructed/contested pickup live evidence pending, not missing code. Client root still falsifiable; balance deferred. |
| Requests/config: GameplayPlayerSpawner.TryConsumeCombatRequest, NetworkLobbyUI ready/ping RPCs in LobbyScene_v1, PlayerCameraSetup.RequestStance, MatchScoreboard.OnRequest, TrackPlayerCurrency.OnBalanceRequest | FishNet sender/ownership plus active/auth/state/finite/ID/range checks, reference-scoped per-connection budgets before expensive scans/physics. Invalid weapon snapshot is developer error. Ordinary rejection never cheating penalty. |78 pure, native/server config and lifecycle; historical F10/AC07/09 user evidence. No missing implementation or new unguarded developer handler. Transport/deserialization/DDoS is deliberately outside gameplay budgets. |
| Disconnect recovery: FileAntiCheat.ApplyPolicy targeted Notice then installed FishNet Disconnect(false); roster OnClientConnectionState/spawner RemoteState; ClientDisconnectRecovery.OnClientState -> ReturnLocally | Once per connection; pending connection inactive immediately. FishNet despawns owner; lobby removes roster/ready/ping/budgets. Remote locally disposes old manager, loads MainMenu, restores cursor/menu and dismissible reason/fallback. Explicit host Return alone may stop server. |18 pure notice,60 unconnected lifecycle, historical AC02 and AC09 user reports; actual manager installation and Release notice/dismiss paths retained. No missing implementation. AC10 A kick/rejoin while B continues and Release multiplayer recovery still need live evidence. Reason delivery best effort. |
| Occluded aim: HitscanShooter.OnStartServer adds OccludedAimObserver, owned5Hz ReportAim -> ProcessServerAim -> observer; server geometry/approved stance/current target eligibility -> OccludedAimTracking | Fresh client-reported aim correlated with moving confidently covered target after grace/cue filters. Always report/review flag, no damage/correction/kick in any mode. Invisible/dead/cosmetic corpses excluded, uncertainty suppresses. |24 pure aim,121 native including clean brief-follow/cue case, actual spawned installer and fixture; AC09 user acceptance. No missing implementation. Aim/poses concealable/forged, no client visibility/hearing/intent proof or inferred human positive. |
| Incident review: MainMenu spawner installs AimIncidentRecorder; RoundTimer.BeginPhase starts/ends match; observer Record -> AimIncidentCapture -> AimIncidentArchive worker -> MainMenu LocalReplayMenu -> AimIncidentReviewMenu | Stable match/participant/incident IDs; reuse of numeric connection ID does not inherit flag. Bounded lead/follow-up/grouping/gaps/partials,2 clips/participant/32 match, bounded worker/disk. Offline local Reviewed note independent of gameplay/punishment. |29 pure incident +29 save/select/play/note +19 load; leave/round/reusedID/attribution/partials/noisy actor. Release MainMenu Theater entry rendered and exported review paths retained. AC10 interactive usability/live profiling pending. Host-local editable files, no full-match/map-accurate replay/durability guarantee. |

Both defaults ReportOnly, host exempt. No new RPC/layout/vendor edits/threshold widening.
AC09 comparison pair untouched. Invalid requests reject independently of punishment.
No serialized FileAntiCheat policy override exists in the active scenes/prefabs;
the MainMenu spawner installs it with compiled defaults. Static supports one approved
buildId, not a multi-version catalog. Release measures once per authenticated connection,
not continuously; F9 is Development-only. Missing/unverifiable reports leave gameplay
admitted by deliberate policy: there is no unverified-client admission gate.
See [AntiCheatMaintenance.md](AntiCheatMaintenance.md) for source/edit/test dependencies,
Release inventory and trust/thread rules. One acceptance sheet remains
[ReleaseCandidateRuntime.md](ReleaseCandidateRuntime.md), including Release host setup.

Rechecked closeout results:341 deterministic,121 native,60 lifecycle,82 terrain,
22 original server/fixture,40 decision/installer,29 incident,19 load,15 active-asset
wiring checks. Full Unity import/compile/FishNet and147-source Editor/Development/
Release compiler profiles passed. Actual non-Development export and exported-DLL
release/aim metadata checks passed. A malformed GUID in a newly added test .meta
and untitled-scene restoration in the new Editor harness were corrected before
successful export; no existing GUID or gameplay decision changed.

### Ranked remaining work / decision

1. **Windows must-pass and short live profiler capture**: high gameplay impact, small
   effort, no new detector risk; two remotes, cover/pickup/lifecycle/review/enforcement
   isolation. No remaining automated blocker found.
2. **Adverse-network/terrain/stance clean characterization**: moderate effort; retain
   clean-alert routes and diagnose freshness/support, never loosen thresholds.
3. **Stronger contact/path/visibility authority**, if later required: substantial
   design. Sparse endpoints cannot justify repeated traversal. Enemy filtering,
   authority rewrite, full-match recording and ML remain deferred.

Scoped capstone implementation can be called complete **after new interactive
acceptance**. New remote cover/collection tests are a release gate: failure could
prevent legal shots/pickups or permit through-cover actions. Existing pre/postgame
combat/reward policy needs an explicit gameplay decision only if GAME-only rewards
are intended; preserve documented behavior. Broad latency/macOS/stress and Release
multiplayer/recovery/settings interaction remain pending. Export and MainMenu launch
are now demonstrated only for AC11-Release-Clean, not a connected-gameplay pass.

## Historical capstone coverage: incident review closeout

Source 1c7f5b7/81fddd9, based on152fc8e. AC08 user-reported pass; **no AC09 live
multiplayer pass supplied**.337 deterministic/118 native/60 unconnected lifecycle/
22 server-fixture/29 incident integration assertions passed. Source findings,
precise limits/cost and actual fixture scope: [AimIncidentReview.md](AimIncidentReview.md).
Pending combined live gate: [AimIncidentRuntime.md](AimIncidentRuntime.md).

| Area | What the server prevents/rejects | What can disconnect | Review flag | Strongest evidence / claim still needing live evidence |
| --- | --- | --- | --- | --- |
| Static integrity | Client cannot choose server expected hashes; bounded exact coverage, deadlines and explicit error results | Optional validated mismatch/missing required file only; default ReportOnly, host exempt | None in this focused incident stage | Historical Windows tamper/recovery user results,65 pure checks; AC08 preserved matching pair rechecked. New clean cohortVerified pending. Client hashes falsifiable, not attestation. |
| Movement | Approved allowances/lifecycle validation; client movement itself not corrected | Existing sustained-horizontal suspicion threshold8, optionalDynamic=Disconnect; host exempt. New displacement/ascent/level-motion observations report-only | None here | AC08 user checklist pass; original F7 recovery evidence,33+58 pure movement cases. Current remote F7/rejoin/clean-B regression and real load pending. Slow/silent flight/contact truth remain gaps. |
| Combat obstruction/weapon state | Server cadence/ammo/reload/weapon values/alive/facing/direction and nearest solid collision; enclosed origin rejects before ammo/HP | No ordinary rejection kick | None here | Native and actual server-prefab HP/stance checks, AC08 gameplay report. Actual map open-ground hit/solid-wall block/exposed exterior corner still needs current live evidence. No lag rewind, roots falsifiable. |
| Requests | Owned/authenticated active sender, IDs/finite/range/state, per-connection ready/ping/combat/scoreboard/balance/stance budgets before expensive work | No ordinary duplicate/malformed/rejected request kick | None here | Existing automated budgets and historical F10 user logs; later AC08 aggregate pass. Transport/DDoS and broader bursts remain outside this gate. |
| Effects/rewards | Server-owned boosts/armor/shield/damage/pickup consumption; timed expiry/replacement and once-per-kill milestone rewards | No effect/reward rejection kick | None here |36 pure effect cases and combined user AC06/AC08 reports. Current legitimate boost/respawn/lifecycle regression pending; balance deferred. |
| Occluded aim | No new gameplay prevention or aim correction; bounded validated aim feed and conservative server classification | **Never**, evenDynamic=Disconnect; separate from punitive suspicion | Existing alert flags shooter in match; GUID identity, bounded lead/follow, grouped partial-capable incident |24 pure aim cases plus25 capture cases, native geometry, actual scripted save/select/play/review with Dynamic=Disconnect. No human tracking/corner-clean live pass, no proof of what client saw/heard or intent. |
| Developer review | Bounded local schema/files/notes; step playback and explicit gaps/source/partial state | **Never**; no policy/network API | Reviewed/note is local workflow only, not another detector or score |29 automatic pipeline assertions including reused connection ID, gap UI, retention/disk failure and note persistence. Visual usability, actual human live capture and supported-size real load still pending. Ordinary Theater remains movement-only, not full-match replay. |

Main changed boundary: OccludedAimObserver supplies existing decisions/poses to
AimIncidentRecorder; RoundTimer starts/ends the match, connection-reference identity
is removed on leave. The observer's detection/enforcement thresholds and permissions
are unchanged. Lead-in recording does no additional physics or network replication.
File serialization/IO runs on one bounded worker. Review reads local files only after
leaving the session. Release metadata checks incident classes have no RPC/NetworkBehaviour
or FileAntiCheat/Disconnect/TakeDamage call. No general anti-cheat framework.

Highest-value remaining work: (1) combined Windows live acceptance; (2) retain any
clean-alert route as regression and profile real 16-player load/latency; (3) consider
better movement contact/pose history only as a separate design. Broad authority,
enemy replication filtering, ML/full-match replay, bots/migration, cosmetic crouch
and balance stay deferred. Local host/server and client hash/aim reports remain trust
limitations. The table above is the current coverage claim; earlier audits follow.

## Previous AC09 implementation audit (historical)

AC08 `384a6c6` checklist passed **per user aggregate report**, no supplied per-case
logs/measurements or optional breakdown. Initial tree clean, no branch integration.
New AC09 interactive acceptance remains pending. Current source is authoritative.
Details: [OccludedAimObservation.md](OccludedAimObservation.md),
[CombatObstruction.md](CombatObstruction.md), [OccludedAimRuntime.md](OccludedAimRuntime.md).

| Area / entry points | Prevented, rejected, reported, disconnect | Strongest evidence | Important limitation |
| --- | --- | --- | --- |
| Static — FileAntiCheat.Receive; BuildIntegrity; IntegrityReportLifecycle | Trusted server baseline, exact bounded coverage/session/deadline, explicit terminal unavailable/error results. Only validated mismatch/missing required file optionally disconnect; default ReportOnly. | Historical Windows tamper/startup rejection/recovery/MainMenu user tests;65 pure checks and lifecycle fixtures; AC08 aggregate pass; archived/installed/Clean/Test hashes rechecked read-only. | Client can forge hashes; limited3-file coverage, trusted host/deployment, no attestation or unverified-client admission restriction. |
| Movement — SpeedHackDetector; HorizontalMovementObservation; ReceivedMovementObservation | Approved boosts/placement/respawn tolerances. Sustained horizontal suspicion optionally disconnects; single/repeated displacement/ascent/level-motion always report-only. No correction. | AC08 user aggregate checklist pass; historical Windows F7 disconnect/recovery;33+58 pure movement and native support checks. | Client-authoritative positions, sparse paths/contact uncertainty, silent/slow flight, coalescing; no dependable generic noclip. |
| Combat — HitscanShooter.ProcessServerShot/RequestWeapon/RequestReload; CombatRayGeometry; Health | Server weapon snapshot, finite/unit/facing/state/cadence/ammo/reload checks, approved effects. Nearest solid blocks HP; new enclosed-origin rejects before ammo. Ordinary rejection nonpunitive. |78 pure validation checks; new real-prefab server-only HP/ammo/stance harness (22 assertions including aim admission/fixture), native blind-spot reproduction; AC08 normal combat user pass. | Current replicated geometry, no lag rewind; forged root can change legal origin. Decorative windows may be solid; actual AC09 remote collision/stance acceptance pending. |
| Requests — GameplayPlayerSpawner.TryConsumeCombatRequest; ready/ping/scoreboard/currency; PlayerCameraSetup.RequestStance | Active/auth/owner/state/ID/finite gates; session-reference budgets before expensive work, duplicate stance avoids clearance. Pending disconnect immediately inactive. No rejection auto-kick. | AC03 supplied F10 rejection logs, AC07/08 aggregate user passes; pure budgets/validation and lifecycle checks. | Transport/deserialization before gameplay gate; DDoS and broader burst/load characterization outside this protection. |
| Effects/rewards — MatchBoosts.ApplyServerBoost/ServerKind; Health; MatchScoreboard | Server pickup consumption/eligibility, armor/damage/shield, expiration/replacement/death/round reset. Five-consecutive-PvP milestones once per confirmed kill, original boost pool. |36 pure effect checks/native fixtures; AC06 combined and later AC07/08 user aggregate passes. | Shared20s duration source-confirmed; balance and detailed power-up/latency permutations not all independently established. |
| Occluded aim — ProcessServerAim; OccludedAimObserver; AimOcclusionGeometry; OccludedAimTracking | Bounded owner 5Hz aim, fresh target receipts, stance/geometry, conservative same-box coverage/grace/elapsed angular correlation. Alwaysreport-only; no damage/correction/punitive suspicion. |24 pure clean/positive/cue/gap/lifecycle cases,16 new native checks, server admission/session/listener checks; actual synthetic coroutine positive/cleanup in Dynamic=Disconnect with no actor state change.800 synthetic evaluations/12,000 queries/16.759ms. | Falsifiable/omittable aim/pose; no proof of client visibility/intent. Nearby cues, moving observer, mesh/mixed/partial cover and stale data suppress. Live AC09 acceptance pending; no aim-based bans. |

### Changed trust boundaries and justified fixes

- **Already protected:** accepted-shot server damage, weapon/effect entitlement,
  nearest world hit and approved crouch origin preserved; no duplicate health/weapon/
  round system or threshold adjustment.
- **Concrete missing validation fixed (170fafa):** ray inside enclosing world collider
  missed it. Native reproduction and server HP/ammo regression justify bounded origin
  overlap rejection, before damage/ammo; uncertainty is not a cheating verdict.
- **New bounded observation:** root yaw/full direction at shots did not support
  continuous tracking. Owner telemetry adds between-shot samples, not camera truth.
  Local replay lacks aim and is not used. Server owns geometry/stance/effects/cue
  timestamps/receipt clock; clients still report root poses and directions.
- **Protected but not AC09 runtime-tested:** live collision/stance edges, remote
  telemetry freshness/candidate selection/cue suppression and lifecycle isolation.
  Scripted positives explicitly bypass these gates; they are not human/transport tests.
- **Architectural/deferred:** lag compensation, enemy replication filtering, movement
  authority, kernel/process tools, signatures and ML. Release IL checks aim classes
  cannot call FileAntiCheat, Disconnect or TakeDamage. Host trust/hash limitations stay.

### Ranked remaining gaps

| Rank / next bounded work | Impact / effort / false-positive risk / demonstration |
| --- | --- |
|1. AC09 remote collision and clean aim acceptance; retain any clean-alert fixture; live profiling | High acceptance value, small effort, no new punitive risk; supplied Windows batch immediately demonstrable. |
|2. Aim/movement delay/loss and terrain/stance edge characterization | High confidence value, medium testing effort; uncertainty must suppress evidence. Installed simulator outgoing-only, no independent jitter. No thresholds loosened. |
|3. Slow/silent flight and contact truth | Meaningful missing coverage, medium/high architecture effort and false-positive risk without server data. Instrument before claims. |
|4. Combat lag/pose history and forged movement/origin | High gameplay impact, substantial networking/design effort. Current guards cannot prove actual travel or lagged visibility. Separate design batch. |
|5. Aim cue/cover blind spots and falsified telemetry | Medium review value, high false-positive risk if relaxed. Expand only from observed cases; malicious client can omit feed. |
|6. Stronger integrity/admission/replication trust | High theoretical impact, substantial scope; explicitly limited capstone claims, no broad architecture introduced. |

Finish AC09 runtime before more detectors. Broad latency/stress/Release runtime,
macOS, cosmetic crouch, balance, bots/migration remain deferred. Older notes follow.

## Movement evidence expansion — 2026-11-21

Current base ad26fe8 (only Road/Water material commit since 28a3482). AC07 batch
reported passed by user in aggregate, required F7 recovery/rejoin included; no logs/
measurements invented and optional cases not itemized. Prior pending AC07 notes below
are historical. This is movement coverage, not another branch integration.

| Changed evidence boundary | Protection / limitation |
| --- | --- |
| SpeedHackDetector.OnReceivedGoal; installed NetworkTransform.ServerUpdateTransform/DataReceived/OnDataReceived | New remote samples before smoothing, authenticated active owner/server guard. Copy pooled data; one latest slot, monotonic server receipt time. Tick only deduplicates novelty, never grants elapsed time/velocity. Original LateUpdate smoothed-transform horizontal enforcement untouched. |
| ReceivedMovementObservation.Observe | Separate report-only repeated displacement, excessive clear-air ascent and unsupported level-motion candidate. Ring three/15s/1s separation; current-frame coalescing and rebase prevent replay/smoothing overcounts. Explicit sparse/stationary/invalid/support-quality states; no client grounded/velocity entitlement. |
| MovementSupportQuery.Evaluate; PlayerPosture | Server broad endpoint geometry, finite parameters, ready terrain, fixed feet reference; own/trigger/corpse excluded. Nearby geometry suppresses, dynamic/saturated/unsupported roots ambiguous. Contact between sparse samples is not established. No path-based noclip or trustworthy motionless-hover claim. |
| SpeedHackDetector.ResetReceived/ObserveReceivedGoals/OnStopServer | Existing lifecycle resets plus phase/server-gap reset, clean listener removal; fixed-size per-actor state and <=4Hz bounded nonalloc queries. Three independently throttled rule logs. No call from new evidence to FileAntiCheat.EvaluateDynamic; old suspicion/enforcement untouched. |
| ApprovedMovementBoost; LocalPlayerControls; FileAntiCheat.DevelopmentConfigureNetwork | Bounded owner-only local development probes, reset/death/12s auto-return; no exception RPC. Outgoing-only latency profiles via installed simulator, no promised jitter/real retransmission model. Controls/fields/setting absent release. |

Actual model, parameters, missing-data reasoning and all limitations:
[MovementExpansion.md](MovementExpansion.md). Agent checks pass: 288 deterministic,
102 native, 60 unconnected Play Mode, full Unity/FishNet17 and three compiler profiles/
release exclusions. No interactive/simulation pass claimed. New AC08 acceptance:
[MovementExpansionRuntime.md](MovementExpansionRuntime.md).

New rules only observe. Existing server combat/request/state checks prevent invalid
gameplay requests; optional validated static/sustained-horizontal policies can remove
remote connections, with host exemption/default ReportOnly unchanged. Root motion is
still client-authoritative; slow flight, silent hovering, goal/coalescing ambiguity,
missing contact/ground truth, cover/lag compensation and forged hashes remain gaps.
Characterize AC08 network/terrain evidence before considering any new enforcement or
authority work. No threshold loosening, signatures/kernel tools or aim punishment.

## Merged-game reconciliation / bounded hardening — 2026-11-13

Inspected integration/anti-cheat-merged-test at **06595a6** with only pre-existing
Road.mat/Water.mat edits. No reset/merge/vendor change. AC06 combined smoke (rebinding,
effects, killstreaks, ragdolls, movement replay, combat and match lifecycle) passed
**per user report**. The AC06 anti-cheat disconnect/rejoin test was explicitly skipped.
This supersedes earlier pending combined-smoke notes, not wider unreported coverage.

| Changed boundary / source reference | Reconciled finding |
| --- | --- |
| Shooting.cs:250 LateUpdate; GameplayInput; LocalInputBindings.Create | **Protected/presentation:** ADS only changes local FOV, never damage/cadence/range/spread/speed limits. Default Shift is Sprint, RMB Aim. Separate sprint FOV 60 caused apparent ADS; removed without changing binding IDs or saved overrides. Native binding-resolution checks preserve intentional Shift Aim overrides. Runtime fix pending. |
| PlayerCameraSetup.cs:148 RequestStance, :36 ServerShotOrigin; PlayerPosture.Apply/CanStand | **Protected + ordering gap fixed:** owned active/authenticated spawned living actor; 8/s burst-8 before clearance; ceiling rejects standing. Duplicate same-stance requests now avoid clearance. Approved stance applies immediately and the server origin refreshes from that state, closing RPC-before-Update eye/collider lag. No client origin accepted; ADS cannot move the server origin. Death/respawn still reset geometry. |
| Shooting.cs:60 ConfigurationError, :107 OnStartServer; ServerGameplayValidation.WeaponConfigurationError | **Concrete gap fixed:** null/empty/oversized catalogs, null entries, nonfinite/out-of-range weapon and aim values formerly reached indexing/clamps/rays. Validate once, snapshot server definitions and aim envelope, disable that shooter's combat on invalid configuration with one developer error. Shot/switch/reload reject it independently of punitive modes. No cheating verdict/disconnect. Profiles/IDs stay extensible; see bounds below. |
| Shooting.cs:159 RequestWeapon, :210 RequestReload, :271 RequestShot, :336 FireServerRay | **Already protected, reused:** owner/active/auth/spawn/alive checks, shared processing budget, saved magazines/cooldown/reload deadlines, finite aim envelope, server rays/world obstruction and first-death damage authority. Now use captured server configuration, including range and boosted damage arithmetic. Shot multiplier remains captured per trigger. Combat in pre/postgame follows existing Health.CanAct rule; no new phase restriction invented. |
| NetworkLobbyUI.cs:292 AdmitLobbyRequest, :172 ReportPingServerRpc, :281 CmdToggleReady | **Concrete gap fixed:** independent 2/s burst-4 ready/ping budgets before scans/mutation/SyncVar-driven UI work. Server initialized/spawned, transition, active/authenticated and registered connection required. Existing sender-attributed roster, ping clamp and start/ready revalidation retained. Log at most once/type/connection/5s; no penalty. |
| ConnectionReferenceComparer; installed FishNet NetworkConnection.cs:217-241 | **Concrete lifecycle gap fixed:** FishNet equality/hash uses mutable ClientId. Relevant session/player/readiness/combat/query/score/currency/boost tables now use object identity; stop callbacks remove state and server-stop clears tables. New native test proves another object with the same ID cannot inherit a session and ID mutation cannot strand removal. Same-object pooling still requires existing Stopped cleanup, retained. |
| MatchBoosts.cs:67 ApplyServerBoost, :44 OnRemoteState, :49 OnServerState, :60 ServerHasSpeedBoost | **Already authoritative + cleanup fixed:** server selects pickup/grant, living same-scene actor, active authenticated connection, finite duration and approved kind. Armor adds 100 capped by Health; doesn't replace timed effects. Invisibility replaces timed effect, expires via server dictionary/deadline, only hides remote rendering. Effects clear on death/non-GAME/scene and now explicitly on disconnect/server stop before ID reuse. No grant/collection RPC. Existing Speed allowances/tail unchanged. |
| Health.cs:76 TakeDamage, :186 AddServerArmor, :102 EndMatch, :155 AcknowledgeRespawn | **Protected, reused:** Shield first, armor absorbs before HP, integer-safe armor arithmetic; armor reset start/respawn/end. Only first positive-to-zero HP transition calls RecordDeath. No client armor/HP/kill/reward amount. |
| MatchScoreboard.cs:99 RecordDeath, :120 ResetStreaks; MatchBoosts.cs:85 RewardServerStreak; RoundTimer.BeginPhase | **Protected, reused:** one confirmed death increments eligible PvP killer, each 5/10/15... grants original enum 1..4 only (Shield/Speed/Jump/Damage). Death and GAME exit reset, disconnect removes score/streak, scene resets. Server stop now explicitly clears scores too. No client kill/streak RPC or duplicate detector added around this path. |
| FileAntiCheat.cs:254 Receive/:288 EvaluateReport; IntegrityReportLifecycle.TryAccept/Expire/Complete | **Lifecycle/logging gap fixed:** bounded explicit Pending -> terminal result. Never-report: one MissingReport at 60s. Unsupported/config/read/malformed results close pending without verification. Missed startup receipt remains sticky LateReport; matching late hashes say `LateReport (matching hashes; not verified)` rather than a success label. Duplicate/sequence/frequency rejection logs once, including release recheck rejection formerly silent. Fixed payload/coverage/build checks retained. |
| FileAntiCheat.cs:137 Update/:211 SendUnavailable/:217 BeginCheck | **Concrete gap fixed:** unexpected worker failure formerly only logged locally, leaving server awaiting a report. Failure/60s watchdog now sends unavailable once, cancels best effort, suppresses stale/timed-out results, retains at most one worker until OS I/O retires. A subsequent session blocked by that worker gets one bounded unavailable result, no automatic worker/retry loop. Rechecks remain development-only/manual/15s. |

Definition safety bounds (developer errors, not player limits): 1..64 profiles; damage
1..int.MaxValue/2 (current Damage is 2x), magazine 1..1,000,000, pellets 1..16;
finite rate 0.1..1000/s, reload 0.1..3600s, range 0.1..100000m (prior 0.1 minima
retained as validation rather than silent clamps), spread 0..90 degrees,
zoom 1..1000; finite yaw 0..180, pitch 0..89.9f. These include current three weapons
and future 100Hz fixture; not a hardcoded weapon-ID allowlist. Raise implementation
bounds deliberately with work/cost validation for future weapons. Server snapshots
are selected at network spawn; correct definitions and start a fresh spawn/session,
not a normal Health respawn or hot edit of an active shooter.

Integrity receipt is not admission: an on-time unavailable/malformed report is a
terminal unverified result; a later valid development recheck may recover it. Missing
the startup receipt deadline stays late. Only on-time validated HashMismatch or
MissingRequiredFile can invoke existing optional static enforcement. Unreadable vetoes
mixed failures; other categories stay log-only. No new unverified-client admission
restriction was added. A malicious client can suppress/falsify reports/known-good
hashes; this remains file-integrity detection, not trusted remote attestation.

Actual checks: **230 deterministic assertions**, full Unity/FishNet processing with
**17 prefabs**, **89 native** and **60 unconnected Play Mode** assertions; 135-source
Editor/Development/Release compilation; Development probe presence and release
metadata exclusion (F6-F10/grants/probes/startup-test setting/state). Native checks
exercise real classifier/connection identity and binding resolution, not transport.
Play Mode blocked-task fixture exercises real watchdog bookkeeping, not an OS I/O hang.
Early fixture property/event-routing/import errors were corrected; final run passes.
Existing unrelated package/compiler/licensing warnings remain. No new standalone/
interactive network pass claimed. See AntiCheatHardeningRuntime.md and GameplayBacklog.md.

Next movement batch: characterize legitimate crouch/sprint/Speed/Jump, long falls,
slopes/water/steps, approved respawn and delivery bursts first. Client-authoritative
root position, absent server-grounded/vertical envelope, no rewind and ray-origin/
cover geometry remain architectural limits. Crouched sprint is intentionally legal,
so a 1m/s crouch enforcement ceiling would be wrong. Invisibility still sends positions;
no fog of war. No new behavioral detector, balance change or threshold relaxation here.

The remaining sections are historical pre-integration audit references; method line
numbers and the old ranking/build advice there are superseded by this reconciliation.

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
