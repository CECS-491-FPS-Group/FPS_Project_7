# Combined integration: one Windows runtime batch (AC06)

**All new rows below are pending user testing.** AC05 audio checklist passed per the
user; AC04 terrain evidence remains partial. Agent-run evidence: CombinedIntegration.md.
Use Editor host plus **two** Windows Development clients. Rebuild **every participant**:
posture RPC/SyncVar, Health armor SyncVar, scoreboard streak payload and new effect IDs
make old clients incompatible even though NetworkBehaviour ordering and 17 registered
prefabs are unchanged. No manual UI/Inspector wiring. Keep both enforcement modes
**ReportOnly** and host exemption. Do not change thresholds to make tests pass.

## Build, trust installation and copies (order matters)

1. Close clients/stop Play Mode, inspect git status and preserve unrelated material
   work. Allow Unity import/compilation/FishNet processing on this integration branch.
   Inspect FileAntiCheat on MainMenu's NetworkManager: both modes ReportOnly. Also
   check/remove FPS_STATIC_AC_MODE and FPS_DYNAMIC_AC_MODE environment overrides.
2. AC06 folders and approved-AC06.json were absent at inspection. Recheck
   Desktop/Builds and Desktop/Builds/Manifests; if occupied, use the next wholly unused
   number throughout. Preserve AC02-AC05 and matching manifests. Build Profiles:
   Windows, **Mono**, **Development Build**, existing enabled scenes starting at
   MainMenu. Finish a successful build into
   Desktop/Builds/AC06-Clean/Project FPS 7.exe. Do not reuse an older executable.
3. **Tools -> Anti-Cheat -> Generate Windows Mono Integrity Manifest**, select that
   completed Clean exe, save trusted output outside the build to
   Desktop/Builds/Manifests/approved-AC06.json. The tool also writes
   AC06-Clean/Project FPS 7_Data/integrity-client.json. Do not rebuild afterward.
   Checked files: Managed/Assembly-CSharp.dll (game code), globalgamemanagers
   (build/manager data), resources.assets (shipped Resources, including controls/corpse).
4. Back up the installed trusted server file, then copy the archived AC06 manifest to
   **<project>/IntegrityServer/approved.json** for the Editor host (or the trusted
   absolute FPS_INTEGRITY_MANIFEST override path). Standalone server default is its
   executable folder/IntegrityServer/approved.json. Desktop archive placement alone
   does not install it. Keep this outside Assets; never commit local deployment files.
   Restart hosting after installation. Require `approved build=... Windows/Mono`,
   both effective policies ReportOnly and no ServerConfigurationError. Never generate
   trusted hashes from AC06-Test or a modified build.
5. **After client metadata generation**, copy all of AC06-Clean to fresh AC06-Test
   (and another copy/machine for the second remote). Inspect existing destinations;
   do not overwrite older artifacts. Compare all three files' SHA-256 values with
   the archived manifest and confirm client descriptor identity on each copy.
   Launch MainMenu/join normally. Require each remote
   `kind=startup result=Verified`; Editor host `EditorSkipped (not verified)`.
   F10/gameplay logs do not establish integrity verification.

No tampering is needed for this feature gate. Later integrity regressions must use
the documented after-load Test-only procedure with external original backups; restore
and reverify before normal gameplay. Do not regenerate any preserved baseline.

## Consolidated acceptance checklist

Record build/owner IDs, actual outcome and relevant logs per row. A successful path
does not complete every boost, lifecycle, latency or second-client isolation case.

| Test | Expected result / evidence |
| --- | --- |
| Controls and persistence | MainMenu Settings -> Controls / Rebind. Change keyboard binding, close/reopen/restart, reset, cancel and time out a rebind. Only owner responds. Same-user copies normally share PlayerPrefs; use separate accounts/machines to test independent saved preferences. |
| Lobby -> match, two remotes | Ready/start roster correct, one active listener per instance, matching terrain/structures/collision, safe spawns, no remote input/camera takeover. Clustered central spawn variety remains known follow-up. |
| Local Escape menu | Other players/server clock continue. Menu/rebinding block local movement/shooting/click-through. Resume restores cursor/input; save/leave remain usable while dead. No duplicate audio managers/NetworkManagers. |
| Crouch/aim | Default Ctrl shrinks capsule/remote visual and eye; feet/root scale stay fixed. Crouch walk 1 m/s, sprint source priority and FOV 60, scope zoom priority. Low ceiling blocks stand until clear. Test obstruction/hits in both stances with all three weapons. Bounded `[Stance] ... server approved` / `stand blocked by clearance`. |
| Legitimate movement | Walk/sprint/crouch/jump over roads/bridges/slopes/near water; Speed/Jump activation, expiry, replacement, respawn and scene transition. No sustained-speed accusations for legitimate movement. Ceilings stay 6/9. Include Speed -> Invisibility, Speed -> Armor and original boosts. |
| Armor/damage | Gray pickup grants up to 100; armor absorbs before HP, excess reaches HP; Shield prevents damage first. Armor does not replace timed Speed/Jump/Damage, repeated armor caps at 100. Death/respawn/match end clears it. HUD and `[Boost] Server granted Armor` / health logs agree. |
| Invisibility | Purple pickup replaces timed effect for normal 20s. Both other peers see body disappear/return on expiry/death; collision, damage and sound remain. No immunity/roster disappearance. Test replacement and respawn restoration. |
| Pickup race/isolation | Contest one pickup: exactly one accepted collection/grant. Uninvolved second client retains its health/effects/input state. |
| PvP streak | During GAME obtain real consecutive kills: none at 1-4, exactly one `[Streak] owner=... streak=5 reward=...`, then one at 10, only Shield/Speed/Jump/Damage. HUD/scoreboard agree. Death resets; next five rewards once. GAME exit/new match/disconnect reset. Multi-pellet kill counts once; earned Damage affects subsequent triggers. No fake-kill/grant RPC. |
| Cosmetic corpses | Death creates separate falling body; respawn still works. Walk/shoot/collect through corpse location, no blocked spawn/standing or absorbed hit. Body retires on revive/despawn, within 8s otherwise, max 8 per instance; scene/recovery cleanup. Physical poses may differ locally. |
| Combat/audio/F10 | Normal ammo, reload, switch, damage, rewards and positional shot audio in both stances. F10 on owner Development client gives invalid-aim, switch-budget, scoreboard/balance rejection logs, no disconnect/rejected-shot remote SFX or damage. Normal gameplay resumes, other clean client unaffected. |
| Local theater | Escape -> Save movement replay, confirm `[Replay] Saved local movement capture: ...`. Leave to MainMenu -> Theater / Replays, select/play/pause/scrub; XZ path/marker follow capture. Back/Escape and listener work. Corrupt/unsupported JSON shows an error. Proves local movement presentation only, not full match replay. |
| Lifecycle/recovery | Death/respawn restores posture, armor/effects, input/camera/listener. Match -> lobby -> new match clears streaks/corpses. Intentional leave preserves host/other client. Separately repeat existing targeted kick recovery if desired: MainMenu message/cursor, manual rejoin, no stale state. Keep feature gate ReportOnly; record temporary enforcement changes separately. |

For repeatable legitimate boost transitions on the **Editor host**, select the target
spawned player's SpeedHackDetector -> Development/Grant menu (Speed, Shield, Jump,
Damage, Armor, Invisibility). Timed fixtures last **8s**, normal pickups/rewards
**20s**; Armor grants 100 points. Server-only, no grant RPC, excluded from release.
Use real kills for streak-delivery acceptance; pure tests do not prove network delivery.
F6 saves only in Editor/Development; normal menu save also exists in release.

Record failures with exact transitions before further anti-cheat work. Broad latency,
macOS, release runtime, vertical observation, authoritative bots and secure rejoin/
host migration remain separate pending work. Windows is the preferred environment;
Touch Bar alternative development controls are optional follow-up.
