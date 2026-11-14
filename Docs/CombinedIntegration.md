# Combined team integration — 2026-11-13

Base: 2130850, integration/anti-cheat-merged-test. No subsequent source changes were
present on resume; only pre-existing Road.mat/Water.mat edits, preserved separately.
Recovery feature/anti-cheat-complete remains 9570966; previous builds/manifests stay.
The user reports **all AC05 audio runtime checklist steps passed**. That is user-run
Windows evidence, not agent-executed tests or full terrain/latency/macOS acceptance.

## Refreshed audit

Fetched origin again. All previously pinned team tips still match the 2026-11-12
audit. No unexpected new team branch/commit was fetched. The additional remote
integration/anti-cheat-merged-test points at our already included 2130850 (the user
published that checkpoint); this task does not push. Content comparison matters:
selective ports do not make their source commits ancestors.

| Source tip | Content / overlap / dependency | Approach |
| --- | --- | --- |
| Soroush 3a52ecd | 5 nonancestral commits; water/road/bridge/structure subset already adapted at 17d2c02 | Already present; no new tip. Keep multiplayer scene and lifecycle fixes. |
| Audio_Management 11c62b6 | 32 nonancestral commits include old combat/main; settings/falloff/SFX already adapted at 2130850 | Already present and AC05 user-tested. Do not import shooter/microphone/Health alternatives. |
| crawl-and-crouch 1d93e5f | 5 nonancestral commits; controller is byte-identical to main. Ctrl crouch, speed 1, root Y-scale .4, sprint FOV 60; imported rebinding samples/menu prototype | Adapt owner controls and rebinding to project-owned input assets; server-approved posture, no vendor replacement or global time pause. No working crawl/slide implementation exists in the controller. |
| Divyanshu_Mehta b9147af | 15 nonancestral commits; 4 original buffs overlap current MatchBoosts. Unique armor/invisibility, bot-kill rewards, bot ragdoll; alternative Health/MatchManager incompatible | Port effects into existing authorities. User explicitly chose PvP reward adaptation and cosmetic player ragdolls; see decisions below. |
| theater-mode/recording 8ee49ee | 7 nonancestral commits; bounded local recording already adapted. Source UI has dummy match buttons, Watch has no playback handler | Adapt real local file selection/presentation; no new recorder NetworkBehaviour or competing scene. Movement-only playback scope, not a complete match simulation. |
| feature/lobby-connection a642bdd | 2 nonancestral commits; LobbyRow prefab and RefreshIcon are byte-identical here; other source changes drop later LAN/recovery/security wiring | Already-present functional/visual work. Exclude old whole-scene/font/connection replacements. |
| main 2a00d78 / fullmerge-test 122ebf7 | 4 / 22 nonancestral commits; combined older local Health/AI/round systems, crouch already same as crouch source | Not integration bases. Select only justified features above; no blanket merge. |
| rejoin-hostmigration-test 780d39c | 6 commits fork before recovery/anti-cheat; caller-chosen GUID identifies cached HP/ammo/position/credits/score. Duplicate-live check is not proof of token ownership; competing recovery controllers | Defer: trusted identity/resume policy, post-kick handling and server-owned restoration need a networking/security batch. No completed host election/migration implementation established. |
| main/Divyanshu bot variants | Local MonoBehaviour/NavMeshAgent fire/TakeDamage, different match difficulty/scoring; procedural terrain has no integrated runtime NavMesh or authoritative bot registration | Defer bots: server-owned AI actors, pathing/navmesh, targeting, damage/reward attribution and replication are required. Independent player features proceed. |
| Shooting-physics a5b4a0e | Included within Audio source ancestry; current server profiles already cover sidearm/rifle/shotgun, accepted recoil/impacts/animation | Alternative projectile/weapon authority excluded; asset presence alone is not an active missing feature. |
| full/combat integration 63532e1/c6b7be2; round-control 222e9d8 | Ancestors already included | No repeat merges. |
| early lobby/main-menu/anticheat; kat/james/orgin-pacoooooo/firebase | Earlier prototypes, already reused assets/menu sliders, gitignore/demo, optional backend SDK | Exclude obsolete/duplicate systems; no intended missing game mechanic established from these tips. Backend/voice are separate scope. |

Exact remaining source identities: crouch 1d93e5f88bf80614af31eb49c2071ab2ca549694;
effects b9147af5a56f42a8af0642ed0f7dc68000a19f21;
theater 8ee49ee05331b90b6661b23915d35ab837f20d12;
rejoin 780d39c04ae7e576e704bbaed8451f20ba6d9d56.
No assessment relies on commit timestamps or branch names alone.

## Confirmed gameplay choices

User approved adapting the source reward to each **5 consecutive PvP kills** (5/10/15),
reset on death/match boundary. Source MatchManager uses Random.Range(1,5), so the
original reward pool is **Shield/InfiniteHealth, Speed, Jump, Damage only**; Armor and
Invisibility must not be added to that pool. User also approved cosmetic player death
ragdolls, adapted from the bot-only presentation. Neither decision is pending approval.

## Stage 1: controls/posture (implemented)

Source: Thanh Nguyen's 1d93e5f / controller shared with 2a00d78. Preserve the owner-only
Starter Assets controller and input gating; vendor code/assets are unchanged. A new
project-owned GameplayInput resource retains existing movement/look bindings and adds
Ctrl crouch, Escape menu, aim/reload/weapon/scoreboard actions. Input instances and
PlayerPrefs binding overrides are local; MainMenu Settings -> Controls/Rebind and
owner Escape menu expose keyboard/mouse rebinding, reset, cancellation and timeout.
Gameplay menu is local only (server clock continues); no automatic reconnect.

Server-approved crouch lives on existing PlayerCameraSetup (new SyncVar/RPC, no new
NetworkBehaviour). Ownership/authentication/alive checks and an 8/s burst-8 budget
precede clearance work. Stand waits under a ceiling; no punitive verdict. Repeated
requests are bounded. Death/respawn reset posture. Controller/remote hit capsules,
eye target and body visuals shrink without scaling the NetworkTransform root. Source
.4 height is clamped to the physical capsule diameter (current 2m/.5m gives 1m height),
feet stay fixed. Sprint retains source priority over crouch speed (1m/s); Speed uses
existing multiplier. Observer ceiling remains 6/9, no detector/threshold adjustment.
Sprint FOV 60 is owner-only and aim zoom takes precedence. Server rays use a fixed
prefab-derived eye offset scaled by approved stance, never a client-supplied origin.

Existing replay saving is exposed on the menu. F6's temporary test shortcut is now
development-only, while normal menu save remains available in release. Stance and
all existing gameplay requests reject inactive/pending-disconnect connections.

Stage checks: current-source compiler check passed; Unity import/FishNet processing,
19 native input/posture checks and the existing 48 unconnected audio lifecycle checks
passed. Native checks cover binding isolation/round-trip/reset, physical capsule
minimum, fixed feet/root scale, lowered server origin, ceiling rejection and restoration.
Interactive host/remote crouch, latency, actual rebind device events and low-cover
shooting remain pending for the one combined Windows batch.

## Stage 2: authoritative effects and cosmetic player ragdolls (implemented)

Source b9147af. Armor grants 100 up to server max 100, absorbs incoming damage before
HP, survives timed-buff replacement, resets on respawn/match end. Armor collection does
not replace the timed boost. Invisibility lasts the existing 20s/replaces other timed
effects; hides remote body rendering only, not collision, damage, sound or network
position data. Server pickups now cycle the six types. No client grant/collection RPC.
Native ServerMatchActive/ownership/alive/scene validation selects accepted values.

Server confirmed deaths reset victim streak and increment an eligible killer only
during GAME. At 5/10/15/... the same authoritative application path grants one random
original four boost, replacing the previous timed effect. Streak resets at GAME exit,
death, disconnect and new match. No reward for self-kill, unregistered/inactive killer
or duplicate HP-zero transition. Existing Health's positive-to-zero death path is the
only caller; no kill-report RPC. Source bot scoring/difficulty and MatchManager excluded.
Armor is synchronized on existing Health; streak joins existing scoreboard snapshot.
HUD shows armor/streak. Shield/Damage and movement allowances retain server ownership.

The source's 11-body/10-joint ragdoll asset is adapted into a new cosmetic resource.
Removed its local Health, root motor collider and extra animation object; no gameplay
scripts or NetworkObject remain. Copy live visual pose into a separate corpse, disable
animation, enable local bone physics. Do not move/replace the live network actor. Bone
colliders use built-in Ignore Raycast (layer 2), excluded from shots, spawn/standing/
footstep probes. Explicit collider-ignore pairs prevent blocking existing/new/revived
players without changing global collision layers. Corpse has no pickup/health ownership.
At most eight corpses, eight-second lifetime, immediate retirement on respawn/despawn,
scene-owned cleanup on transitions. Local physical poses may differ between peers;
they are presentation only. Host has the same query exclusions; dedicated server
builds skip corpse creation. No collision/authority/detector thresholds are weakened.

Stage checks: 148 pure checks (112 existing + 36 armor/streak rules); 61 native input/
posture/corpse checks and 48 existing unconnected audio checks passed with Unity/
FishNet processing. Actual PvP reward delivery, pickup races, invisibility visibility,
death/respawn corpse physics and connection/scene timing remain user runtime tests.

## Stage 3: local replay presentation (implemented)

Source theater-mode/recording **8ee49ee05331b90b6661b23915d35ab837f20d12** supplied
the recording format and theater intent. Dummy matches/unwired Watch are replaced
with a real local-file list, bounded top-down XZ path, interpolated marker, timeline
and play/pause. MainMenu -> Theater / Replays opens only outside a running session.
Escape -> Save movement replay uses the existing owner-local recorder; teardown also
saves new samples. Playback creates no gameplay scene, physics actor or NetworkManager.
**Movement-only presentation, not full match/video/shot/other-player reconstruction.**

Version 1 captures and prior current/source flat-position formats are supported.
Files stay under Application.persistentDataPath/Replays; leaf names only, no traversal/
reparse links, 16 MiB input limit, 36,000 frames, finite bounded samples. Listing
inspects at most 128 files and displays the newest 12 within that inspected subset.
Path rendering is capped near 2,048 segments. Invalid/unavailable files show an error.
Captures are editable/falsifiable local presentation, never anti-cheat evidence.

Compatibility completion: modal backdrops prevent click-through; menus remain usable
while dead for save/leave without enabling combat. Shot emitters follow crouch head
height. Damage multiplier is captured once per accepted trigger: a reward earned by
one pellet affects subsequent shots, not that same shot's remaining pellets.

## Final available validation (agent executed)

- Five deterministic suites: **148 assertions** (18 notice, 31 integrity, 33 movement,
  30 request validation, 36 armor/streak), via Tests/RunAntiCheatChecks.ps1.
- Full Unity 6000.3.8f1 graphics-enabled import/compilation/FishNet processing;
  registration stays **17 prefabs**. TeamIntegrationChecks: **73 native** assertions.
  AudioIntegrationChecks: **52 unconnected Play Mode lifecycle** assertions, including
  theater opening/listener continuity. Interactive multiplayer remains pending.
- Current-source Editor, Development and Release compiler checks: **133 game source
  files**. Release metadata has no development grant/probe methods or F6-F10 inputs;
  normal menu replay saving remains available.
- New asset/meta GUID uniqueness and corpse external GUID resolution passed; 46
  unique prefab object IDs, 11 bodies/10 joints. Existing player prefab, scenes,
  registration asset, vendor code and AGENTS.md unchanged in this batch.
- Road.mat/Water.mat edits match entry backups byte-for-byte, remain uncommitted.
  Recovery branch remains 9570966. No push/main merge/build regeneration.
- Read-only AC05 Clean/Test three hashes and client identities match archive
  **70cb725d6d834c51832442c0e4599570**; default server manifest is byte-identical.
  No artifacts changed. AC06 has not been built.

Ignored evidence: Logs/CombinedIntegrationFinal.log and Combined-{Editor,Development,
Release}-compile.log. Existing LobbyCodeUI OnValidate, unused-event/vendor obsolete
API and package diagnostics remain. Initial licensing token warning did not prevent
compilation, Play Mode or orderly shutdown. No standalone build, interactive audio/
multiplayer, actual reward delivery, latency or macOS pass is claimed.

Controls checkpoint **4b8e275**; effects/corpses **3f22c28**; local replay and final
compatibility follow in their logical commit. Pending combined Windows test batch:
[CombinedIntegrationRuntime.md](CombinedIntegrationRuntime.md).

## Remaining risks and ranked next batches

1. Run AC06 Windows batch; resolve concrete integration faults before more anti-cheat.
2. Finish combat/boost/lifecycle/delivery-burst validation on the combined game.
   Crouch is approved geometry; positions remain client-authoritative. Invisibility
   hides rendering only, not replicated positions (no fog of war). Pickup proximity
   inherits movement trust. Neither is trusted remote attestation.
3. Then revisit the trust-boundary audit for a justified bounded validation improvement.
   Vertical/fly/repeated-teleport observation needs map/latency evidence first.
4. Central spawn variety remains a separate follow-up. Bots require authoritative
   actors/NavMesh; rejoin requires identity/resume/kick policy. Do not import their
   competing local health/round systems to bypass these dependencies.

Both modes remain ReportOnly with host exemption; no thresholds loosened/new detector.
Windows is preferred. Broad latency/macOS/release runtime and unreported terrain/
power-up regressions remain open. Mac Touch Bar alternate development controls are
optional future work. Malicious clients can falsify hash reports; player-hosted
servers assume a trusted host. No automatic reconnect or bans.
