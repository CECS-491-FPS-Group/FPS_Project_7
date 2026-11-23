# Anti-cheat maintenance and multiplayer merge handoff

Closeout source: e1e7d9d / c536a5d, integration/anti-cheat-merged-test. No missing
in-scope implementation found; pending evidence is not an instruction to add a
detector. Start future multiplayer work from the **accepted closeout HEAD**, including
its documentation commit, rather than an older feature branch. Acceptance lives only
in [ReleaseCandidateRuntime.md](ReleaseCandidateRuntime.md); coverage/actions/limits
live in [AntiCheatAudit.md](AntiCheatAudit.md). Do not transfer a pass between binaries.

## What a teammate should preserve

- MainMenu's GameplayPlayerSpawner.Awake installs FileAntiCheat, AimIncidentRecorder
  and ClientDisconnectRecovery; their absence as serialized components is intentional.
  PlayerCameraSetup.Awake installs local controls/posture/effect presentation;
  HitscanShooter.OnStartServer installs the server aim observer. RoundTimer.Start
  installs MatchBoosts/MatchScoreboard. Do not add duplicate competing installers.
- The selected registered variant is Assets/Prefabs/Player.prefab, with inherited
  camera/controller and enabled SpeedHackDetector. NetworkTransform remains client
  authoritative, no parent/scale sync. Preserve GUIDs, inheritance, NetworkBehaviour
  ordering, ownership and FishNet registration; rebuild a matching cohort after any
  actual layout/RPC change. The new closeout changes add no network surface.
- A server-owned limit/effect is authoritative; a replicated position or reported
  aim/hash is an observation/claim. Server scene geometry is current geometry, not
  historical visibility or proof of a client's actual path, screen or hearing.
- Keep admission checks before ammo, damage, rewards or accepted-shot effects.
  Reject invalid requests in every policy mode, without treating ordinary rejection
  as cheating. Preserve valid wall-hit ammo/presentation and weapon-specific behavior.
- Request budgets use server elapsed seconds and connection-reference identity.
  Cleanup on Stopped/server/scene boundaries matters: FishNet numeric IDs are reused.
  Never replace these dictionaries with ClientId-keyed trust/suspicion state.
- Movement respawn grace starts once when Health issues a placement token. Its ACK
  confirms only that pending token, rebases and cannot extend the fixed deadline.
  Client teleport flags are not authorization. New uncertain movement evidence stays
  separate from horizontal punitive suspicion; no generic sparse-segment noclip rule.
- Incidents use server monotonic seconds since GAME and match/participant GUIDs;
  life/object changes split encounters. Frames/alerts copied at the queue boundary
  must stay immutable. Main thread owns gameplay/Unity objects; one worker owns JSON
  serialization/pruning/atomic IO. Never access transforms from that worker or block
  gameplay waiting for disk. Queue/disk failures are neutral, not player verdicts.
- Aim correlation, incident flags and Reviewed notes **never** enter FileAntiCheat,
  damage or punishment, even Dynamic=Disconnect. Release IL checks guard that boundary.
- A targeted kick must use local ClientDisconnectRecovery on the remote, not global
  ReturnToLobby. Notice codes are best effort; fallback recovery still runs. Only an
  explicit host Return may stop its server. Preserve Bind/Unbind and reason clearing
  on dismissal/new attempt, plus disposal of old manager before MainMenu creates one.

## If you add or change a feature

| Change | Source of approved values / coupled path | Smallest useful checks, then runtime |
| --- | --- | --- |
| Weapon | Assets/Scripts/Shooting.cs WeaponProfile defaults and frozen server snapshot in OnStartServer; ConfigurationError/ServerGameplayValidation. RequestWeapon/Reload/Shot -> ProcessServer... -> CombatRayGeometry -> Assets/Health.cs. Child camera mount is the only approved ray origin; ADS is presentation. |Pure ServerGameplayValidationChecks via RunAntiCheatChecks; Tools/Anti-Cheat/Validate Active Wiring; Validate Server Obstruction (actual ammo/HP/origin/weapon/fixture paths). Live cadence/reload/switch, legal wall hit, open hit, crouch/stand near cover. Invalid configuration is a developer error. |
| Movement ability / stance | ApprovedMovementLimits.cs, MatchBoosts.ServerKind/ServerHasSpeedBoost and ApprovedMovementBoost. PlayerPosture/PlayerCameraSetup provide approved collider/eye; SpeedHackDetector reads server-approved limits and fixed grace. |MovementObserver/ReceivedMovement pure cases; native Team checks and terrain checks. Add legal transition/expiry/death/respawn/gap cases before enabling a new allowance; live boost/crouch/terrain/recovery. A parented/moving platform needs explicit support semantics, not relaxed thresholds. |
| Pickup / effect / reward | MatchBoosts.Update/TryCollectServer/ApplyServerBoost: server polling, same scene/living owner,2.5m finite range, expiry, clear body path and atomic consume. Health owns Armor/damage/death. MatchScoreboard.RecordDeath owns confirmed kills/reset and5/10/... streak rewards. CombatEffectRules.OriginalReward is Shield/Speed/Jump/Damage only. |CombatEffectRules pure; Validate Server Obstruction includes real contention/duplicate/death/stale-round/Armor/Invisibility. Live reachable/obstructed collection, two contenders, expiry/replacement/death. New effect does not automatically join the original reward pool. |
| Map collider / spawn | Active RoundImplementation TerrainGenerator.worldSettings.structureSettings, Building_01, procedural road/bridge MeshColliders; spawner dry/clear placement. CombatRayGeometry hard obstruction is distinct from AimOcclusionGeometry conservative same-static-box coverage. |Tools/Map/Validate Integrated Terrain; Team native geometry; active wiring and server obstruction. Live wall/corner/opening/crouch/bridge/slope/spawn. Visual windows need real collider openings to allow shots. Moving/mixed/mesh cover can be uncertain for aim while still blocking damage. |
| Network request / roster | Existing owned RPC or authenticated broadcast; GameplayPlayerSpawner.TryConsumeCombatRequest and per-connection ready/ping/scoreboard/balance/stance gates before expensive work. NetworkLobbyUI removes roster/ready state; FishNet despawns disconnected objects. |Pure request budgets/config, existing server/lifecycle checks. Live F10 and normal gameplay, A kick/rejoin while B stays functional. No request for client-chosen damage, reward, effect, file path or expected hashes. |
| Build layout / integrity | BuildIntegrity.RequiredPaths/protocol slots, FileAntiCheat.Receive/EvaluateReport, IntegrityReportLifecycle. Editor BuildIntegrityManifestTool hashes finished output only; server baseline is separately installed. |Pure BuildIntegrity checks; actual exported Release surface check; matching clean startup/tamper/recovery. Never regenerate trusted hashes from Test; adding slots changes protocol and needs all peers rebuilt. macOS layout is unverified; schema1 Windows Mono is the demonstrated path. |

Run all pure checks:
`powershell -NoProfile -ExecutionPolicy Bypass -File Tests/RunAntiCheatChecks.ps1`.
Unity menus above are Editor checks, distinct from live acceptance. Scene/prefab
wiring check: `AntiCheatCloseoutChecks.Run` (15 checks). Real manager/player/round
installer assertions are in CapstoneServerChecks, invoked by CombatServerChecks.Run.
Source paths under Assets/Scripts unless specified; no vendor edits required.

## Release surface: visibility and reachability

All probe/fixture code below is guarded by UNITY_EDITOR || DEVELOPMENT_BUILD.
Release removes built-in abuse shortcuts, not legitimate user rebindings to those keys.
Actual exported AC11 Mono DLL, not just a synthetic profile, was inspected. Tests
also confirm the corresponding Development methods remain for AC10 acceptance.

| Development entry | Release result |
| --- | --- |
| F7 excess speed; F8 one displacement; Alt+F7 reset; Alt+F8 cycle repeated/ascent/level/off | Input calls, probe methods and state absent. Normal owner movement remains. |
| F9 local integrity recheck; Ctrl+F9 replay last report | Input/test state absent; Release server passes allowRechecks=false, so a modified client cannot regain the missing UI via a recheck report. Startup reporting/validation remains. |
| F10 lobby ready/ping churn and match invalid-shot/switch/scoreboard/balance probes | Trigger/probe methods absent. Normal guarded RPC/broadcast handlers remain and reject invalid requests from a modified client. No separate grant/fixture network handler exists. |
| Escape-menu repeated/ascent/level/reset buttons | Development text, delegates/calls and target methods absent. Resume, Controls/Rebind, Save movement replay and intentional Return remain. |
| Server Inspector/context fixtures: aim positive/stop/quality, movement quality, weapon-config fixtures; Speed/Jump/Shield/Damage/Armor/Invisibility grants | Launchers/coroutines/state/grants absent, not merely hidden. No unrestricted audio/effect broadcast or grant RPC added. |
| FPS_INTEGRITY_TEST_STARTUP, FPS_MOVEMENT_TEST_NETWORK | Environment-test readers absent. Release ignores these fixture variables. |
| F6 movement-save shortcut | Shortcut absent; normal menu Save remains. |
| Hidden-target diagnostics / overlay | No player-facing live hidden-target overlay. Aim/incident diagnostics stay in server logs/local files; Development quality context actions absent. Normal server logs and offline evidence detail remain intentionally. |

Repeat against a real output with Tests/CheckReleaseSurface.ps1 and
Tests/CheckAimAssemblies.ps1, supplying -DevelopmentAssembly and -ReleaseAssembly.
The first also checks compiled ReportOnly defaults and Release remote-recheck refusal;
the second checks aim/incident code has no policy/disconnect/damage path. This is
metadata/control-flow evidence, not a claim that every Release UI button was clicked.

## Who can see review screens

Theater / Replays is available to **any graphical instance in MainMenu after leaving
its session**, in Release too. Ordinary local movement capture/playback stays intact.
Server aim incidents are recorded only on the host/server. The reviewer reads that
machine's Application.persistentDataPath/AimIncidents; there is no download/list/read
RPC to fetch another server's hidden positions. A remote on another machine normally
has no host incidents. Instances sharing a Windows account share the same local folder
and can see its archives; manually copying files grants local review access. This is
trusted local storage, not an account permission system or encrypted evidence store.
The offline schematic is not the shooter's camera/map reconstruction. Review/notes
are local, revision-scoped and never feed enforcement.

## Merge gate

No known implementation blocker remains. Accept AC10 on the final closeout source
using the one Windows sheet, recording the generated buildId and actual observations.
Then create the future multiplayer branch from the accepted closeout commit named
in the final handoff, preserving the recovery branch and all build/manifest pairs.
After integration, rerun active wiring, server obstruction, native/lifecycle, release
surface and the small two-remote gate; do not assume a scene/prefab/network change
inherits the old acceptance. Leave dev tools until the separate clean-game pass.

Deliberately deferred: generic wall traversal/contact authority, slow/silent flight
guarantees, trusted remote attestation, lag rewind, enemy replication filtering,
full-match recording, ML, bans/host migration/bots, crouch cosmetics and balance.
Broad adverse-network/macOS/maximum-size live profiling remain missing evidence,
not implemented claims. Player-host trust and client-reported root/aim/hash limits
remain explicit even after acceptance.
