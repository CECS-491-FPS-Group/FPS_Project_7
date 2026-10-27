# Anti-cheat continuation handoff

## Branch, scope and Friday goal

- Branch: `feature/anti-cheat-complete`.
- Implementation base: `63532e17b425058c9bdd078da9f4d75dab27b6e6` (`63532e1`, LAN discovery test).
- Unity: **6000.3.8f1**; FishNet is vendored in this repository.
- Friday goal: substantially complete both **Dynamic Anti-Cheat** (server gameplay
  detection) and **Static Anti-Cheat** (defined client/build file integrity).
- Current delivery: horizontal movement observer plus the small
  Windows Mono build-file integrity system, both defaulting to ReportOnly, described in
  [StaticIntegrity.md](StaticIntegrity.md). Clean static runtime and ReportOnly
  tamper/recovery passed per the user; server disconnects succeeded, but the new
  disconnected-client menu/message recovery remains pending runtime validation.

See [MovementObserver.md](MovementObserver.md) for measurement details, formulas,
full acceptance steps, and the Mac command for repeating deterministic checks.

## Files needed to reproduce this chunk

The implementation commit includes only the following changes and documentation:

| Path | Purpose |
| --- | --- |
| `Assets/Scripts/SpeedHackDetector.cs` | Refactored server observer; startup, ownership, respawn hooks, evidence logs and host development context menus |
| `Assets/Scripts/HorizontalMovementObservation.cs` | New pure XZ measurement, allowance integration, suspicion and rebase logic |
| `Assets/Scripts/HorizontalMovementObservation.cs.meta` | Required new Unity script GUID `e3cc7eec217b49bb91d902933db9b1c4` |
| `Assets/Scripts/ApprovedMovementLimits.cs` | New shared base movement values and boost multipliers |
| `Assets/Scripts/ApprovedMovementLimits.cs.meta` | Required new Unity script GUID `7cff5dbe69b74b5580fd46b57c908e87` |
| `Assets/Scripts/ApprovedMovementBoost.cs` | Applies shared values to owner controller; development-only excessive-speed/teleport controls |
| `Assets/Scripts/MatchBoosts.cs` | Minimal server Speed-state accessor and development-only server test grants |
| `Assets/Health.cs` | Two observer notifications around existing server respawn placement and validated acknowledgment |
| `Assets/Prefabs/Player.prefab` | Observer added to active player root through a prefab variant override |
| `Tests/MovementObserverChecks.cs` | Standalone deterministic checks; outside Assets, not a Unity runtime test assembly |
| `Docs/MovementObserver.md` | Detailed observer reference and runtime acceptance matrix |
| `Docs/AntiCheatHandoff.md` | This Windows continuation guide |

Existing `.meta` files for modified scripts/prefab are already tracked and unchanged.
In particular, `SpeedHackDetector.cs.meta` retains GUID
`4d39abcfeddf4c9d8166dbcb6da9956e`, used by the new prefab attachment. Do not regenerate
script GUIDs. Docs and Tests are outside Assets and do not require Unity metadata.

Two pre-existing untracked files are deliberately **not** part of the commit:

```text
Assets/FishNet/CodeGenerating/cecil-0.11.4/Mono.Cecil.sln.meta
Assets/FishNet/Runtime/Transporting/Transports/Tugboat/LiteNetLib/LiteNetLib.csproj.meta
```

They remain on the Mac working tree and are not transferred by this branch push.
No other unrelated modifications were present when preparing this handoff; preserve
any unrelated Windows working-tree changes before switching or pulling.

## Architecture and behavior

MainMenu's `GameplayPlayerSpawner` already points to `Assets/Prefabs/Player.prefab`.
The active prefab inherits `Assets/PlayerCapsule Variant.prefab`, which inherits
Starter Assets' first-person capsule prefab. Its added SpeedHackDetector observes
the server's replicated XZ position in LateUpdate. No manual component addition is needed.

Preserve the existing owner-driven Starter Assets CharacterController movement,
client-authoritative NetworkTransform, input/ownership gating, server combat/damage,
health/currency/scoreboard/match authority, and token-based respawn handshake.
The installed FishNet source was inspected; its client-authoritative owner ignores
observer transform updates, so a server position assignment is not an owner correction.
Do not redesign movement or edit FishNet vendor code to implement this observer.

`GetServerHorizontalSpeed()` is the single authoritative allowance selector. It uses
this server's compiled shared constants and `MatchBoosts.ServerHasSpeedBoost(Owner)`,
which checks server effect state/deadlines. Owner controller settings, client ping,
LocalKind snapshots and client-reported limits are not trusted inputs.

- Walk/sprint remain 4/6 m/s; Speed multiplies both by 1.5, permitting sprint at 9.
- Shield, Damage and Jump retain the 6 m/s horizontal ceiling. Existing immunity,
  2x damage and 1.5x jump-height behavior remain; base jump height is 1.2.
- Boosts never disable observation. Speed removal/expiry/replacement starts a
  bounded one-second old-limit tail for delivery/deceleration, driven by server state.
  Continued client movement/requests cannot renew the tail. Windows integrate limits.
- Spawn and server ownership changes acquire a baseline for 1.5 seconds. Death,
  invalid owner, match end and teardown discard measurements/suspicion. Normal
  pregame/postgame movement remains observed until Health locks gameplay.
- Server-issued respawn placement starts 1.5 seconds of bounded grace. A valid
  acknowledgment rebases after server placement without extending that deadline.
  Existing RPC validation and revival behavior are unchanged.

Initial thresholds: sample at least .25s; sustained windows approximately 2s; 10%
speed margin; .35s peak-speed replication slack plus .5m per sustained window;
12m additional displacement slack. Suspicion rises/decays by completed-window elapsed
seconds, reports at 4 and caps at 10. Server frame gaps over 1s reacquire an
inconclusive baseline. Warnings are capped at one per player per 5s, with suppressed
counts and bounded state. All decisions use actual monotonic server elapsed time.

Known limitations: reporting occurs after transforms are accepted/smoothed. Modest
speed advantages and loops between samples can be missed; delivery backlogs can
look suspicious. Invalid movement can still affect shots and pickup proximity.
There is no vertical/fly/aim detector, correction or ban. Optional sustained-speed
disconnect policy now exists; see AntiCheatEnforcement.md. Existing respawn has
no acknowledgment timeout: a non-acknowledging player stays dead. Late stale transform
delivery after the grace deadline needs testing. Future approved teleports/recovery
need explicit bounded server hooks; the older TerrainSpawnPlacer is not attached.

## Development controls

Only `UNITY_EDITOR || DEVELOPMENT_BUILD` includes these controls:

- On the owning client, **F7** toggles 3x local movement (18 m/s normal sprint,
  27 boosted). This simulates a modified client; detection remains on the server.
- **F8** moves the owner +30m in world X and sends its transform. Choose generated
  terrain with a safe destination. This grants no exemption.
- On the host Inspector's runtime player SpeedHackDetector component, use
  `Development/Grant Speed/Shield/Jump/Damage (8 seconds)`. Grants use server effect
  state and normal broadcasts, work only for a living player during GAME, and expose
  no client grant RPC. F7 resets on death/control lock/disable.
- Test-control logs use `[Movement AC TEST]` / `[Boost TEST]`; detection uses
  `[Movement AC]`. Leave F7 off during legitimate-movement tests.

## Evidence and validation status

### Actually executed during implementation

- Compiled the project's Assembly-CSharp sources against installed Unity references
  with Editor, development-player and release-player symbols using bundled Roslyn:
  all passed, with existing unrelated warnings.
- Ran **26 deterministic C# checks** against the actual measurement class using
  bundled Mono: all passed. These exercise measurement logic and lifecycle-reset
  primitives; they do not instantiate FishNet/MatchBoosts or execute health RPCs.
- Checked prefab override references/unique IDs and unchanged inheritance,
  NetworkTransform, input and combat serialized blocks.
- Checked compiled metadata: test controls present in Editor/development and absent
  with release symbols.
- `git diff --check` passed; no FishNet vendor or static-integrity changes.

These are compiler/measurement/serialization checks, not Unity integration passes.
During handoff preparation the branch, diff, tracked/new metadata and staging scope
were re-inspected. The earlier compilation/runtime-measurement checks were not rerun
merely to prepare documentation and commit.

### User-supplied runtime results

Windows two-player movement smoke test was performed and supplied by the user:
startup owner 0/object 1 and owner 1/object 2, base/boosted limits 6/9;
remote owner 1 sustained-horizontal-speed reports at roughly 34-35m over 2s
against roughly 16m allowed; F8 horizontal-displacement reports around 30m.
The user reports the basic test seemed to work. This is supplied runtime evidence,
not an independently observed full acceptance pass. The inspected build is
`Desktop/DevelopmentBuild`, Windows Mono. Do not mark boost transitions, respawns,
match cycles, isolation, late join, or latency as passed from this smoke test.

### Still pending

For the movement change, this Windows Editor log showed successful Unity import,
compilation and FishNet IL postprocessing; the user supplied the Windows two-player
smoke results above. Full power-up/respawn compatibility, three-player isolation,
match cycles, late join, latency, macOS builds/runtime, and release input exclusion
in use remain pending. The new static-integrity changes need fresh Unity import,
FishNet processing, player builds, and runtime tests; see StaticIntegrity.md.

## Original Windows movement smoke procedure (basic results recorded above)

From this repository in PowerShell, first inspect `git status`. Preserve any local
work; do not reset or discard it. With a clean/safely preserved working tree:

```powershell
git fetch origin
git switch feature/anti-cheat-complete
git pull --ff-only origin feature/anti-cheat-complete
```

If the local branch does not yet exist, `git switch` normally creates it tracking
the matching origin branch. Do not merge/force-reset a diverged local branch.
Open this project using Unity **6000.3.8f1**, then:

1. Allow import and FishNet postprocessing to finish. Resolve/report compilation
   errors before testing; do not change working networking to work around them.
2. Confirm the active `Assets/Prefabs/Player.prefab` root has one enabled observer
   override; keep its inherited components/settings. Build a Windows standalone
   **Development Build** from the same checkout, outside Assets. Old clients must
   be rebuilt because the network prefab gained a NetworkBehaviour.
3. Open `Assets/Scenes/MainMenu.unity` in the Editor, enter Play Mode and host through
   the existing UI. Launch one standalone client, join through the existing lobby
   flow, ready both players and start the match normally.
4. On the Editor host Console, enable warnings, disable Collapse and filter
   `[Movement AC]`. Expect two startup entries with distinct owner/object IDs and
   base/boosted ceilings 6/9. Wait at least two seconds after spawning.
5. Focus the standalone window. Sprint/strafe/jump normally for 15s: expect no
   movement warning. Press F7 and sprint unobstructed for 8-12s: expect a server
   sustained-speed report attributed to this remote player, with no kick/correction.
   Toggle F7 off; move normally and let suspicion decay.
6. Wait at least 6s after the last warning, then press F8 once from a safe location:
   expect one server displacement report. Verify subsequent ordinary movement stays
   quiet and both players can still shoot, take damage and respawn normally.
7. Record actual results, owner IDs, relevant logs, Unity/build/backend versions and
   failures here or in a follow-up test record. A two-player smoke pass is not a
   substitute for the remaining acceptance matrix.

Continue with the full [MovementObserver.md runtime matrix](MovementObserver.md):
host plus at least two remote clients; normal/boosted sprint; Speed expiry while
sprinting; Speed replacement by each other effect; Jump while moving; Shield and
Damage combat behavior; death/respawn while boosted; excessive speed beyond 9;
post-anomaly recovery; match return/restart; late join where supported; network
delay/bursts; host-vs-remote behavior; and non-development builds. Do not tune from
one clean local run or treat these pending tests as passed.

## Static Anti-Cheat continuation

### Latest runtime evidence and disconnect recovery chunk

The user subsequently supplied clean remote startup Verified (owner 1), local Editor
EditorSkipped (owner 0), and a passing ReportOnly after-load tamper/recovery test.
Static lobby enforcement logged HashMismatch -> Disconnect -> remote Id 1 stopped;
dynamic gameplay enforcement logged suspicion 8.07, threshold 8 -> Disconnect ->
remote Id 2 stopped. Host continued and later returned to lobby without that player.
These establish successful server disconnects. The disconnected standalone remained
in stale lobby UI or a frozen gameplay view; usable client recovery was **not passed**.
Immediate host roster behavior and a second clean client were not established.

The current chunk sends a targeted fixed-code server notice before the existing
FishNet graceful disconnect, reusing its ~100ms/minimum-two-tick delivery interval.
Disconnecting immediately makes IsActive false; existing gameplay request guards and
strengthened lobby guards reject inactive senders. ClientDisconnectRecovery installs
once, retains/dismisses the reason, drains current FishNet scene work, disposes the old
client-only manager, clears session state and loads MainMenu locally. Runtime TMP/UI
notice requires no manual wiring. Explicit menu return is an intentional leave.
Existing server roster/despawn/match paths and host exemption are preserved.

See [DisconnectRecovery.md](DisconnectRecovery.md) for inspected FishNet APIs, changed
paths, exact messages, rebuild/manifest instructions and the pending manual matrix.
Host and all clients require rebuilding for the new Notice broadcast; generate a
matching manifest only from the clean final rebuild. No installed or Desktop manifest
or external test file was changed in this implementation chunk.

Executed: compiler checks and 18 notice-state / 31 integrity / 33 movement checks.
Pending: Unity import/FishNet processing, fresh builds, targeted delivery, local scene
recovery/UI/cursor, reconnect/races, ordinary leave, host/second-client survival and
prompt roster/owned-player cleanup. Do not infer these passes from server-stop logs.

### Earlier bounded chunk: optional enforcement

The user reports successful Desktop manifest generation, **not runtime static
verification**. Offline inspection found the Desktop manifest valid for all three
current DevelopmentBuild files, with an identical client descriptor. Default
`<project>/IntegrityServer/approved.json` was missing; no manifest environment override
was found in the inspected process/user/machine environment. No approved server load
is established. The known-good Desktop copy was left untouched. Do not commit machine
deployment paths or manifests. A fresh build from the enforcement code needs a new
separately named manifest; preserve the old build/manifest pair.

`FileAntiCheat` now exposes independent server Static/Dynamic Mode fields, both
ReportOnly by default, plus a dynamic threshold defaulting to 8 (effective 5–10,
report threshold 4, cap 10). Optional server-start environment overrides and exact
Windows test steps are in [AntiCheatEnforcement.md](AntiCheatEnforcement.md).
No client policy RPC exists. Static enforcement permits only validated, on-time
HashMismatch/MissingRequiredFile; unreadable and all other failure categories stay
log-only. Late startup status persists through rechecks. Dynamic enforcement uses
only fresh sustained-speed windows at threshold; displacement/invalid positions
never qualify, and measurement allowances/lifecycle behavior are unchanged.

The connection session guards disconnect once across both systems, logs before
FishNet Disconnect(false), and exempts the local host with HostReportOnly. Existing
roster/match/disconnect handlers are preserved. A player-hosted server trusts its host.
No new network messages or prefab changes were added in the enforcement chunk;
rebuild all clients to exercise current code and generate a matching clean manifest.

Executed this chunk: 31 integrity and 33 movement deterministic checks; compiler
checks for Editor/development/release symbols and Editor tool. Runtime static
verification, enforcement, host safety, disconnect cleanup and boost/respawn/match
regression are pending. Leave enforcement off until ReportOnly runtime tests pass.

### Initial static implementation context

The user authorized static implementation after reviewing the limited movement smoke
results. `FileAntiCheat` now runs via authenticated connection broadcasts on the
existing persistent NetworkManager, installed by GameplayPlayerSpawner. Its old
dummy-file creation, self-baselining, and disconnect code are removed. No prefab
inheritance/GUID, movement, combat, or FishNet vendor changes were made.

New files: `Assets/Scripts/BuildIntegrity.cs` (+meta),
`Assets/Scripts/Editor/BuildIntegrityManifestTool.cs` (+folder/script metadata),
`Tests/BuildIntegrityChecks.cs`, and `Docs/StaticIntegrity.md`.
`IntegrityServer/` is ignored as local deployment configuration.

See [StaticIntegrity.md](StaticIntegrity.md) for exact Windows build, generation,
trusted server installation, clean startup and after-load F9 tamper/missing-file
recheck steps. Rebuild all test clients for the added broadcast protocol; this
chunk adds no NetworkBehaviour or prefab modifications. Keep the trusted server
copy separate from the client tamper directory. Only one approved Windows/Mono
build is needed for the demonstration; macOS layout is structured but unverified,
and IL2CPP is unsupported by this first schema.

Executed: 19 real C# policy/hash checks, Editor/development/release compiler checks,
Editor menu compilation, and F9 release metadata exclusion checks. Static Unity
import, FishNet broadcast generation, builds and multiplayer demonstrations remain
pending. The older cached release response file omitted existing scripts; release
compiler validation used the current development source/reference list without
DEVELOPMENT_BUILD. No static runtime pass is claimed.

The original static design context (the later explicit enforcement authorization
above supersedes its no-disconnect restriction) was:

Implement versioned **platform/backend-specific file-integrity manifests generated
from final known-good builds**. Identify build version, target platform, scripting
backend and manifest schema; select an explicit small allowlist of meaningful shipped
code/data artifacts and resolve paths for Windows and macOS. The server owns trusted
expected hashes/manifests; do not derive its baseline from a joining client or
auto-create missing files as the old FileAntiCheat prototype does.

Use bounded, sender-attributed client reports and request frequency/size limits.
Distinguish pass, mismatch, missing/unreadable file, unsupported build and timeout.
Start report-only with demonstrable logs; add no automatic enforcement. **A compromised
client can falsify these reports or submit known-good hashes: this is not trusted
remote attestation or arbitrary runtime-injection detection.** Nonces can correlate
requests but do not make a compromised client honest.

Preserve working movement/combat/respawn architecture throughout continuation.
No FishNet vendor edits, movement redesign, automatic disconnects/corrections/bans,
kernel drivers or invasive OS monitoring are authorized by this chunk.
