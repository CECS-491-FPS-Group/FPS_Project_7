# AC08 Windows movement / network characterization (pending)

AC07 was reported passed by the user in aggregate, including its required recovery/
rejoin regression. No detailed logs/timings or optional-fixture breakdown supplied.
Everything below is a **new pending** gate. Agent checks and limitations:
[MovementExpansion.md](MovementExpansion.md). No manual UI wiring; rebuild all peers.
NetworkBehaviour order/RPC fields stay unchanged, but all runtime scripts/manifest
identities must match. Preserve AC07 and all available previous pairs.

## Build / manifest / server / copies

1. Stop clients and Play Mode, import/compile/FishNet process this branch. Both
   committed policies remain ReportOnly; inspect Inspector and FPS_STATIC_AC_MODE/
   FPS_DYNAMIC_AC_MODE overrides. Clear prior integrity/network test environment
   settings. Do not change thresholds, gravity, colliders or boost durations to pass.
2. AC08 folders and approved-AC08.json were unused at inspection; recheck both
   Desktop/Builds and Desktop/Builds/Manifests. If occupied choose the next wholly
   unused number. Build Windows **Mono Development**, MainMenu-first existing scenes,
   into Desktop/Builds/AC08-Clean/Project FPS 7.exe. Require a completed successful build.
3. Tools -> Anti-Cheat -> Generate Windows Mono Integrity Manifest. Select finished
   Clean exe, save trusted Desktop/Builds/Manifests/approved-AC08.json outside build.
   The tool writes Clean/Project FPS 7_Data/integrity-client.json. Do not rebuild after
   generation or baseline from Test. Checked files remain Managed/Assembly-CSharp.dll,
   globalgamemanagers and resources.assets; no whole-installation scanning.
4. Preserve installed AC07 server manifest, copy the new approved archive to
   **<project>/IntegrityServer/approved.json** for Editor hosting, or the absolute
   trusted FPS_INTEGRITY_MANIFEST override. Standalone server default is executable
   folder/IntegrityServer/approved.json. Restart hosting to select it. Desktop archive
   placement alone is not installation. Keep deployment files/backups uncommitted.
5. **After client descriptor generation**, copy all Clean to fresh AC08-Test and a
   second clean copy/machine. Inspect existing destinations, do not overwrite prior
   pairs. Compare all three selected hashes and descriptor build ID with the archive.
   Join Editor host + two remotes; require each remote startup **Verified**. Editor
   host must say EditorSkipped (not verified), server approved identity must be AC08.

## Baseline and demonstrations (session 1)

Keep Static/Dynamic ReportOnly initially. Record only actual outcomes/logs, with
build/owner IDs, effect, stance, route and network profile. Prefer development menu
buttons to function chords when testing on a Touch Bar/custom keyboard.

| Case | What to do / expected result |
| --- | --- |
| Clean lifecycle/terrain | Lobby ready/start, both remote inputs/cameras/listeners independent. Walk/sprint over roads/bridges, slopes/steps, building edges/roofs and near water. Jump normally, fall from elevated static ground, crouch under obstacles and transition on inclines. No new rule report for these legitimate paths. |
| Approved effects | Test Speed/Jump, expiry/replacement by Shield/Damage/Invisibility, Armor preserving timed effect. Jump just before Jump expiry/replacement, move while Speed expires, die while boosted, respawn and change rounds/scenes. Existing token handshake and 1.5s grace; no placement counted as repeated abuse. Server Inspector Development grants are 8s, normal timed pickups/rewards 20s (unchanged). |
| One vs repeated displacement | In open terrain, move normally first to establish samples. Plain F8 once: old horizontal-displacement report may occur; no new repeated event report from smoothing. After reset/history aging, Alt+F8 selects repeated probe (or Escape -> Development: repeated teleports -> Resume). It alternates 40m every 2s; after three qualifying independent events expect `rule=repeated-unexplained-displacement action=ReportOnly`. |
| Excessive ascent | Select via menu or cycle Alt+F8 while previous probe is active. Resume; local forced upward movement should produce `excessive-unsupported-ascent` only with enough changed goals/clear support. No new-rule kick or suspicion addition. |
| Unsupported level motion | Select menu option; Resume. Client jumps to start+11.2m and follows a tiny horizontal orbit at constant Y. Expect `unsupported-level-motion` after >=4s sufficient clear, changed-goal data. This is a moving-hover candidate; do not call it detection of silent stationary hovering. Walls/roofs/unready terrain intentionally suppress it. |
| Reset / normal recovery | Alt+F7 or menu Development: reset / return probe disables all movement probes and old F7 excess speed, resets local controller and returns the active probe to its captured start. Auto-reset also occurs at 12s. Opening menu while active stops it. Death cancels without moving over a server respawn. Walk/jump/crouch/shoot normally afterward; allow 15s event history to age. Return teleport can itself add one report-only event. |
| Sample quality | On Editor server select remote actor SpeedHackDetector -> Development/Log movement sample quality. Stationary/rotation-only/silent goals must not become hover reports. Note unknown support/sparse arrivals rather than assuming failed probes prove clean movement. Logs identify raw source/age/tick, observed/allowed, sample count, effect, geometry, phase and suppressed/coalesced counts. |
| Isolation / report-only | During each probe, another clean client keeps moving/shooting/collecting/respawning normally. After baseline, optionally set host Dynamic=Disconnect temporarily, run new probes again: **their reports must remain ReportOnly** and must never call the old kick threshold. Existing genuine excessive horizontal speed can still independently qualify. Restore mode afterward. |

Controls are development-only: plain F7 toggles 3x speed, plain F8 does +30m X;
**Alt+F8** cycles Off -> Repeated -> Ascent -> Level -> Off; **Alt+F7** resets.
Direct menu selections avoid cycling. New rules skip the local host's non-received
stream; demonstrate on remote clients. No client request grants server exceptions.

## Adverse-network sessions (small matrix; NOT yet run)

Installed FishNet `TransportManager.LatencySimulator.AddOutgoing/IterateOutgoing`
delays **outgoing application packets only on the configured instance** by L ms
plus scheduling/tick effects. Configure both endpoints for both directions:
host outgoing L + remote outgoing L gives nominal **L one-way each / 2L added RTT**.
Only host configured means remote->server is not delayed by this feature; nominal
added remote RTT is L. Host local client/server traffic also traverses both outgoing
directions when Simulate Host is enabled (default). These are source-derived meanings,
not measured RTT promises.

This simulator has **no independent jitter-range setting**. Leave Out Of Order=0;
reordering is not a jitter model. Packet Loss .02 means 2% probability: unreliable
packets are discarded, reliable packets instead get an extra 30% of L delay. This
does not model real reliable retransmission/congestion. LiteNetLib has internal
DEBUG-only simulation fields, but no supported active-game configuration path was
established for them; do not assume those fields/settings have been exercised.

Use a fresh session/process for each row; stop sessions before changing/flushing
simulation queues. On Editor host, set TransportManager -> Latency Simulator fields
in Play Mode **before Host**. For standalone remotes launch with the listed
development-only environment profile. Keep both policies ReportOnly during these rows.

| Profile on both endpoints | Host fields | Repeat this manageable subset |
| --- | --- | --- |
| Baseline | Enabled=false; client env unset | Full session 1 above. |
| 100ms | Enabled=true, Latency=100, Packet Loss=0, Out Of Order=0; client `100ms` | Sprint/crouch slopes/steps, Jump expiry, Speed replacement, boosted death/respawn, one level-motion probe then reset. Expected no legitimate accusations, candidate only when sufficient samples. |
| 150ms-loss2 | Enabled=true, Latency=150, Packet Loss=.02, Out Of Order=0; client `150ms-loss2` | Same short route and transitions, one ascent/repeated probe, recovery/isolation. Nominal added RTT 300ms plus loss/scheduling effects; insufficient data should become inconclusive, not hovering. |

Launch each fresh remote from PowerShell, e.g.:

```powershell
$env:FPS_MOVEMENT_TEST_NETWORK = '100ms' # or 150ms-loss2
& "$env:USERPROFILE/Desktop/Builds/AC08-Test/Project FPS 7.exe"
Remove-Item Env:FPS_MOVEMENT_TEST_NETWORK
```

The child inherits the value; removing it affects subsequent launches, not that
running child. The setting is applied once at authentication/server start and logs
`[Network TEST]`; it does not simulate pre-authentication traffic on the client.
It is ignored in release builds. Do not use it on a running child expecting a live
change. Editor inherits its environment at process launch; Inspector setup is easier.
For each row keep the second remote present, and record which endpoints are enabled.
No deterministic jitter/loss measurements are claimed by the build or tests.

## Existing enforcement regression / completion

Return to a fresh baseline/no-simulation session. Require remote startup Verified.
Temporarily set **host** Static=ReportOnly, Dynamic=Disconnect, threshold unchanged 8.
On remote Test press **plain F7**, move/sprint until existing sustained evidence
qualifies. Expected once-only DynamicMovement disconnect, remote usable MainMenu
with existing movement reason, prompt player/score cleanup. Host + other clean client
continue the same match. Manually join again (normal lobby return if required),
require new startup Verified and no stale notice/probe state, normal movement/combat.
Test intentional leave and normal match-to-lobby too. Restore host ReportOnly.

Archive only results actually observed. New rules remain report-only regardless of
this acceptance; no automatic threshold relaxation or elevation into enforcement.
