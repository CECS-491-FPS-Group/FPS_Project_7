# Gameplay branch inspection — 2026-11-12

## Scope and checkpoint

Source checkpoint: **`91a420f`**, `Bound gameplay requests and checkpoint AC03 validation`,
on `feature/anti-cheat-complete`, based on `e4d0e15`. Seventeen reviewed batch files
(source, one Unity script meta, tests and documentation) were staged explicitly and
the staged diff/check were inspected before commit. All 112 deterministic checks
passed again. No generated build, backup or ignored IntegrityServer deployment file
was committed. No detectors/gameplay refactors were added in this inspection task.

`git fetch origin` completed after checkpointing. The sandbox first blocked writing
FETCH_HEAD; the approved retry succeeded. No remote changes were reported by fetch.
No branches were merged, checked out, created or pushed. Remote-tracking refs below
are the fetched tips, not inferred from names/dates. No develop branch exists among
the fetched/local refs.

## AC03 evidence and deployment correction

The user ran Editor host + one Windows Development standalone from **AC03-Clean**,
without tampering. Normal movement/shooting worked. Host logs showed:

```text
Rejected shot shooter=1: invalid aim direction
Rejected switch shooter=1: request budget exceeded
kind=scoreboard rejected; no snapshot built
kind=balance rejected; no balance scan/reply
```

No disconnect occurred, matching nonpunitive request rejection. These are user-observed
F10/basic gameplay results. No startup Verified evidence was supplied. Complete
boost/pickup/recovery, second-clean-client isolation, broader reload/weapon/geometry,
latency and macOS coverage remain pending.

Archive `Desktop/Builds/Manifests/approved-AC03.json` is schema 1 Windows/Mono, identity
`bac7c31cb0a74bb3bd60e50b1daad852`. Its three required hashes match AC03-Clean and
AC03-Test. Clean's generated descriptor matched the archive. The installed default
`<project>/IntegrityServer/approved.json` was still AC02, and no inspected process/user/
machine manifest override was present. The old copy was preserved under an ignored
`approved.json.before-AC03-...json` backup; the verified existing archive was installed
and compared byte-for-byte. The running server must restart to select it.

Test lacked `Project FPS 7_Data/integrity-client.json`. Only that missing file was
created from Clean's existing matching descriptor; no existing Test artifact was
overwritten. A final read-only inventory/hash comparison found **314 files in each
copy, no missing/extra/different files**. No baseline was regenerated, no checked game
file changed, and older numbered pairs remain preserved. Explicitly record remote
startup Verified after restarting hosting; neither F10 logs nor offline file matches
prove that the earlier session used an approved server baseline.

## Ancestry and branch findings

Counts describe commits reachable from a branch but not the source checkpoint.
`git cherry HEAD <ref>` found no patch-equivalent commits for the inspected divergent
primary branches. However, earlier integration work manually adapted several of their
features: semantic overlap is not proof of identical commits or equivalent behavior.

| Branch / fetched tip | Relevant unique work | Already included elsewhere? | Compatibility/likely conflicts | Recommendation |
| --- | --- | --- | --- | --- |
| Local/remote `feature/full-integration-test` `63532e1`; `feature/combat-integration` `c6b7be2`; `round-control` `222e9d8` | No new commits beyond this checkpoint | All tips are ancestors of current branch | Current source adds authority, observer, integrity and recovery on top | **Exclude repeat merges**; useful historical bases only |
| Local `main` `2ca177b`; `origin/main` `2a00d78` | Remote has AI patrol/search/fire, respawn UI and sprint/crouch/FOV work (3 unique non-merge commits) | Main's remote tip is contained in fullmerge-test, Audio_Management, crawl-and-crouch and theater-mode/recording. Local main is an older ancestor here | Remote Health is a MonoBehaviour with local damage/respawn. BotAI is MonoBehaviour/NavMeshAgent and calls old TakeDamage(int), without server gating. Controller loses ResetMovementState and uses Camera.main/FindWithTag | **Do not use as combined base**. Clarify whether bots are required; port them under server authority in a separate batch |
| `origin/fullmerge-test` `122ebf7` | Older combined rounds/results/HUD, boosts, AI animation and audio scene | Contains remote main, Divyanshu through `da3bd98`, Audio through `97dfd43`; not the latest tips of either feature branch | Health is local MonoBehaviour; alternate MatchManager vs current RoundTimer/Health lifecycle. Build settings include Playground and no RoundImplementation. No current AC03 protections/recovery | **Exclude as base/blanket merge**. An older partial integration, not a ready complete build |
| `origin/Soroush` `3a52ecd` | Water-aware layout, bridge spans/sample prefabs, road meshes, structure spawning, scene setup/validation; 5 unique commits | Current branch has earlier deterministic terrain/core layout, not these water/bridge/structure additions | TerrainGenerator/WorldLayout/settings/MapTestScene overlap. New OnLayoutBuilt/SeaLevel consumers and structure settings must reach the active RoundImplementation scene, not just MapTestScene; collider readiness/spawn/seed determinism must remain valid | **First inclusion candidate** on a separate integration branch, with selective conflict resolution and asset wiring |
| `origin/Audio_Management` `11c62b6` | Volume settings/sliders, positional shot falloff, hurt/death/footstep SFX; local microphone capture prototype | Contains main and Shooting-physics tip. Fullmerge has only earlier audio. Current IntegrationAudio/CombatPresentation already supply basic menu/click/shot/impact presentation | Branch moves HitscanShooter to WeaponScripts as a MonoBehaviour; its audio RPC relays client-claimed shots without server cadence validation. Health/menus/prefabs overlap. VoiceChat captures audio locally; no network voice implementation | **Port selected presentation/settings**, driven by existing accepted-shot/damage events. Exclude legacy shooter replacement and microphone/voice work unless explicitly desired |
| `origin/Divyanshu_Mehta` `b9147af` | Newer killstreak rewards, live scoreboard changes, armor, invisibility and ragdoll; 5 commits beyond fullmerge | Four basic boosts, scoreboard, blood/HUD already have different server-authoritative equivalents here; armor/invisibility/killstreak/ragdoll are genuinely additional | PlayerBoosts/BoostPickup/ArmorVestPickup use local effect/trigger state; Health differs, MatchManager tracks different score/round semantics. Extra effects require server damage/visibility/pickup rules, not a second boost authority | **Clarify required new mechanics**, then selectively port. Exclude duplicate local boost/health/round systems |
| `origin/crawl-and-crouch` `1d93e5f` | Input/rebinding/menu assets beyond main; prototype crouch/FOV behavior | Its FirstPersonController blob is **identical** to origin/main (`1fddae4...`). Crouch is already in main/audio/fullmerge. No separate working crawl behavior established from this controller | Global Camera.main/FindWithTag and root scaling can affect wrong player/collision; preserve owner input and respawn ResetMovementState. Rebinding/action references need inspection | **Selective owner-scoped input/crouch port**, if wanted; do not claim crawl completion from branch name |
| `origin/rejoin-hostmigration-test` `780d39c` | Six commits: reconnect identity/prompt, 60s cached position/Health/ammo/boost/currency/score state, return UI | Forks at `63532e1`; not included in anti-cheat/recovery branch | Touches Health/Shooting/spawner/lobby/session/menus and adds competing persistent return controllers. Its Health lacks observer respawn hooks. Caller-chosen GUID claims permit identity changes; lifetime/rate/authentication and kick-vs-network-loss policies need review. No completed host migration established | **Defer whole merge**; separate security/lifecycle integration after foundation. Preserve existing deliberate kick cleanup; clarify rejoin after kick and trusted identity model |
| `origin/theater-mode/recording` `8ee49ee` | Theater scene/menu and recording data/UI, 3 commits beyond main | Current LocalReplayCapture explicitly adapts local recording already; no playback UI integrated | Rewrites menu/terrain and adds a player NetworkBehaviour; recorded client transforms are diagnostics, not anti-cheat truth | **Optional playback/UI port only**; preserve current local bounded capture and network prefab ordering |
| `origin/Shooting-physics-and-potential-decal` `a5b4a0e` | Weapon holster, alternate hitscan/projectile paths, recoil/hit-marker assets | Tip is an ancestor of Audio_Management. Current code already implements 3 server profiles and accepted-shot presentation | MonoBehaviour shooter/local ammo/hits conflict with current authoritative HitscanShooter and its GUID; duplicate types/assets possible | **Exclude separate merge**. Clarify whether new weapon models/projectile mechanics are needed; reuse cosmetic assets selectively |
| `origin/feature/lobby-connection` `a642bdd` | Two commits beyond included local `010645d`: lobby row/list/menu polish and a button fix | Local LobbyRow prefab is identical at current HEAD; current browser/LAN flow already provides functionality | MainMenu YAML still differs and contains later active wiring here; ProjectSettings/font changes also accompany feature commits | **Inspect/port visual delta only**, not whole scene/settings replacement |
| `origin/feature/main-menu` `83fadef`; `origin/feature/lobby` `e5f0947`; `origin/feature/anticheat` `74dadc8` | Early menu/lobby and cube/dummy-file AC prototypes | Main-menu sample: 5 of 8 changed relevant assets are byte-identical here; active menu/lobby/AC behavior has later implementations | Older scenes and dummy-file/self-baseline AC would regress current behavior | **Exclude obsolete implementations**; commits not ancestral does not mean features are missing |
| `origin/firebase` `39c3fd0` | PlayFab SDK/editor packages and test scene (2 unique commits) | Not included; existing project has separate Firebase assets | Large new dependency/auth/account surface, not current gameplay integration | **Defer**, clarify account/backend requirements; do not add SDKs just to combine branches |
| `origin/kat` `88e38da`; `origin/james` `23739a2`; `origin/orgin/pacoooooo` `5685773` | Imported weapon/environment/controller assets; gitignore-only change; bean networking demo | Kat: 69 of 93 changed relevant assets already byte-identical here. James has no gameplay files. Bean demo differs from active player architecture | Remaining sample scenes/controllers/NetworkObjects are not demonstrated active gameplay improvements | **Exclude blanket merges**; inspect specifically requested cosmetic assets only |

## Recommendation: B, separate integration branch from the checkpoint

Keep `feature/anti-cheat-complete` as the recoverable AC03 source baseline. Proposed
new branch name: `integration/weekend-gameplay`, **based on `91a420f`** (or its later
documentation-only audit descendant). No branch is created by this audit.

Proposed bounded merge/port order, with compile + focused runtime gate between steps:

1. `origin/Soroush@3a52ecd`: bring in map/structure/settings assets and necessary code,
   retaining current generation/network spawn integration. Wire the active game scene.
2. Selected `origin/Audio_Management@11c62b6` presentation/volume/settings changes on
   current CombatPresentation/IntegrationAudio and validated shot/damage events.
   Do not replace Health, Shooting or lobby networking with prototype versions.
3. Selected input/crouch changes from `origin/crawl-and-crouch@1d93e5f` (which already
   contains main's crouch controller); retain client authority, owner scope, server
   approved speed constants and ResetMovementState. Confirm whether actual crawl is
   a required new mechanic before implementing it.
4. Only agreed unique `origin/Divyanshu_Mehta@b9147af` additions (ragdoll/armor/
   invisibility/killstreak). Keep one server Health/MatchBoosts/score/reward authority;
   these need explicit game rules before porting, so do not automatically include all.
5. Rejoin/AI/projectile/theater-playback work is optional subsequent scope, not a
   prerequisite for this weekend's stable multiplayer demonstration. Rejoin needs
   its own identity, session-budget, approved placement and post-kick policy review.

This is an **integration order**, not permission for wholesale automatic merges of
all those branches. Their commits overlap old systems; port/cherry-pick selected
changes with explicit conflict resolution, never silently choose entire old files.
There is no reason to first merge main, fullmerge, combat/full-integration, round-control
or the old anti-cheat/menu branches into the current baseline.

## Integration runtime gates and Unity compatibility

- Preserve `.meta` GUIDs and prefab variant inheritance. Review active MainMenu ->
  NetworkManager -> Player prefab and RoundImplementation references, build scene
  order, collider layers, input asset links, World/Layout/Structure ScriptableObjects
  and bridge prefab references. Test-editor MapTestScene is not proof of active-scene wiring.
- Rebuild every peer after source changes. Any new NetworkBehaviour changes prefab
  ordering; rebuild peers together and inspect FishNet prefab collection, never mix
  earlier AC03 clients with changed integration prefabs. Keep vendor code unchanged.
- After map integration: two/three peers, same seed/settings, collider-ready terrain,
  structures/bridge/water placement, server spawning, ground layers, world bounds,
  death/respawn on safe surfaces and no spurious horizontal observations.
- After cosmetics/input: owner-only controls, camera/UI/audio isolation, remote shot
  sounds only on accepted shots, no double firing, occlusion/ammo/reload/cadence still
  server-owned; crouch transitions/clearance/jump/slope/Speed replacement tested cleanly.
- After effects: server-authorized pickup once, Shield/Damage/armor rules, visibility
  and killstreak attribution, expiry/death/reset, scores/currency and late observers.
- Every gate retains ReportOnly defaults and host exemption. Re-run F10 application
  rejection smoke and historical static/dynamic disconnect/menu/rejoin-by-hand tests.
  Complete boosts/pickups/recovery/second-clean-client isolation currently remain pending.
- Generate a **new unused numbered clean build and manifest only from the finished
  known-good integrated build**, preserve AC01–AC03 pairs, deploy the trusted copy
  separately, create Test only after client metadata generation, then explicitly
  establish startup Verified. AC03 hashes cannot validate an integrated rebuild.

No integration runtime or branch readiness/build pass was performed by this audit.
Pending gameplay choices should be confirmed before the integration implementation
task; no new detectors should be expanded until that feature set is settled.
