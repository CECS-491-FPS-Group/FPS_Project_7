# AC07: request/configuration and integrity lifecycle runtime gate

2026-11-21 update: the user reports this latest AC07 batch **passed as a whole**.
Record user-reported aggregate runtime evidence, including its required F7 recovery/
rejoin step; no detailed logs/measurements or optional-fixture breakdown supplied.
The original pending instructions below are historical. New movement/latency work
remains pending in MovementExpansionRuntime.md (AC08), not covered by this report.

Pending user tests. AC06 combined gameplay smoke passed per user; **AC06 anti-cheat
disconnect/rejoin was NOT performed**. This gate carries that test forward. Windows
preferred; no new detector/threshold, crouch animation or power-up balance change.
No manual UI wiring. Rebuild Editor/standalone participants together; runtime changed
despite unchanged network component order, message fields and 17-prefab registration.

## Finished build -> trusted manifest -> copies

1. Save/stop all running sessions; compile/FishNet process this branch. Both
   FileAntiCheat modes default **ReportOnly**. Check Inspector and server environment
   FPS_STATIC_AC_MODE/FPS_DYNAMIC_AC_MODE overrides. Leave both ReportOnly initially.
2. AC07 build folders/archive were unused when inspected; recheck before building.
   Preserve AC06. Build Windows **Mono Development**, existing MainMenu-first scenes,
   into Desktop/Builds/AC07-Clean/Project FPS 7.exe; require successful completed build.
3. Tools -> Anti-Cheat -> Generate Windows Mono Integrity Manifest: select finished
   Clean exe, save Desktop/Builds/Manifests/approved-AC07.json outside build. This
   also writes Clean/Project FPS 7_Data/integrity-client.json. Do not rebuild afterward
   or generate trusted hashes from Test. Checks remain Assembly-CSharp.dll,
   globalgamemanagers and resources.assets under the build's Data directory.
4. Preserve installed AC06 server copy, then install existing new AC07 archive as
   **<project>/IntegrityServer/approved.json** (Editor host), or the trusted absolute
   FPS_INTEGRITY_MANIFEST path. Standalone server default is executable directory/
   IntegrityServer/approved.json. Desktop archive placement alone is not installation.
   Restart hosting; require approved AC07 identity, ReportOnly/ReportOnly and no
   ServerConfigurationError. Deployment files/backups remain local, outside Assets.
5. Only **after descriptor generation**, copy finished AC07-Clean to fresh AC07-Test
   plus a second clean remote copy/machine. Inspect destinations; never overwrite a
   prior pair. Verify three SHA-256 entries and descriptor identity against archive.
   Run Editor host + two remotes, require **each** remote startup `Verified` and local
   Editor `EditorSkipped (not verified)`. No inference from gameplay/F10 logs.

## One normal/probe/recovery batch

| Step | Expected server/client outcome |
| --- | --- |
| Lobby normal ready/ping | Ordinary ready/cancel, ping bars and start eligibility work. Another clean client remains independent. |
| Lobby F10 once on remote Test | Development probe sends 32 toggles + 32 pings via existing RPCs. At most burst 4/type plus elapsed 2/s refill are admitted. `[Request Limit] owner=... kind=ready rejected; no lobby mutation.` and equivalent `kind=ping`. No disconnect; final readiness may change, inspect/reset it before Start. Wait >5s, retry ordinary ready; other client's readiness/pings remain functional. |
| Match / Shift / crouch | Shift sprints without the old 90 -> 60 camera zoom; RMB Aim zooms. Sprint limits remain 6/9. Controls/Rebind preferences persist. An intentional Aim -> Shift override still zooms; change just Aim via rebind if undesired. No automatic preference migration/reset; global Reset bindings discards all overrides, use only deliberately. Crouch/stand under ceilings and shoot around low cover; remote capsule/origin match stance, no behind-wall damage or false movement accusation. |
| Weapons / F10 | Use all three weapons, empty/reload, switch during reload/cooldown, death/respawn. Existing game F10 phases remain: NaN shot -> invalid aim rejected; after >2s, invalid-switch burst -> budget rejection; after >2s, scoreboard/balance flood -> no expensive work/reply beyond budgets. No damage/ammo spend/SFX for rejected shots, no penalty. Other clean client keeps shooting/moving. |
| Configuration fixture | On Editor host select a spawned player's Camera HitscanShooter -> component menu Development/Validate weapon configuration fixtures. Expect `[Weapon Config TEST] live=valid null=rejected NaN=rejected ... no live config changed, no player verdict.` This demonstrates the shared validator only, not a live misconfigured spawn. Real invalid server definitions produce one `[Weapon Config] ... combat disabled. Developer configuration error, not a cheating verdict.`; fix definitions and start a fresh spawn/session. |
| Effects/lifecycle regression | Armor capped at 100, absorbs before HP, Shield first. Armor retains Speed; Invisibility replaces timed boost and expires/restores rendering. Confirm legitimate Speed/Jump, death/respawn/round reset and streak milestone 5 (optionally 10) grants once from original four only. Other client isolated. Durations unchanged (normal timed 20s, dev fixtures 8s); no balance acceptance inferred. |
| Duplicate integrity | After startup +15s, Ctrl+F9 on Test replays its last measured report. One `InvalidReport frequency/sequence`, no new success/enforcement. After another 15s plain F9 sends a fresh measured recheck -> Verified. The second clean client remains Verified/functional. |
| Integrity error matrix | Optionally run the fixed development startup fixtures below one session/process at a time. No category here disconnects, even with current optional Disconnect policy; restore a normal process before movement enforcement. |
| Required carried-forward F7 recovery | Start a fresh clean session with both remotes startup Verified. On **Editor host only**, temporarily Static=ReportOnly, Dynamic=Disconnect, threshold unchanged at 8. On remote Test press F7 once, hold forward/sprint until sustained suspicion reaches threshold (roughly 8s measured evidence after grace, not a single displacement). Expect one `[AC Policy] ... category=DynamicMovement action=Disconnect ... threshold=8`, remote stop, owned-player/score/roster cleanup. Host and second clean client continue the same match. Kicked client reaches usable MainMenu with existing movement explanation, cursor/buttons/listener working. |
| Fresh manual rejoin | Do not rebuild/re-hash/restart the server to hide state. Manually join the same server again, waiting for normal lobby return if current join flow requires it. Require fresh startup Verified, no stale notice or old F7 speed flag, normal ready/input/combat/boost state. Other client unaffected. Test intentional leave too. Restore host Dynamic=ReportOnly afterward (Play Mode changes should not be committed). |

## Fixed startup fixtures (Development standalone only)

Close **only Test**, keep host/other clean client running. Launch one new Test process
from PowerShell (choose one exact value; unset for normal startup):

```powershell
$env:FPS_INTEGRITY_TEST_STARTUP = 'Malformed'
& "$env:USERPROFILE/Desktop/Builds/AC07-Test/Project FPS 7.exe"
Remove-Item Env:FPS_INTEGRITY_TEST_STARTUP
```

The child inherits the setting; remove it from the parent immediately so subsequent
normal launches do not inherit a fixture. No files/manifests are changed. Never set
this on Clean or the host. Values are implemented only in Editor/Development code;
Editor host itself ignores the startup override. Restart the Test process to switch.

| Value | Expected result / exact meaning |
| --- | --- |
| Malformed | Measured report's first slot becomes duplicate slot 2 -> InvalidReport. Not Verified; report receipt is terminal unverified. |
| Unsupported | Measured build identity is altered -> UnsupportedBuild, not cheating. |
| Unreadable | First digest replaced by unreadable status -> UnreadableFile, no enforcement. This proves status handling, **not an actual OS read failure**. |
| Suppress | No startup task/report. After 60s one MissingReport (not verified); connection stays playable, no pending/retry/log loop. Do not press F9; reconnect normally afterward. |
| Delay | Real measured startup held until 65s. MissingReport at 60s, then `LateReport (matching hashes; not verified)`; later F9 stays late/ineligible. Reconnect with fixture unset for normal verification. |

Native tests cover server manifest/configuration, unsupported, Editor skip and digest
classification; the unconnected Play fixture checks blocked-worker watchdog behavior.
Actual locked-file/server deployment error transport tests and broader latency/macOS
remain pending unless explicitly run/reported. Hashes can be falsified by malicious
clients; no admission restriction was added and a player-hosted server trusts its host.
