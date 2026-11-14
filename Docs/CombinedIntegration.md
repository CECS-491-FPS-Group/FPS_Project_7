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

## Remaining stages in this authorized batch

2. Authoritative armor/invisibility/PvP streak rewards and bounded cosmetic ragdolls.
3. Local replay/theater presentation and final combined validation/runtime instructions.

Preserve logical commits between stages. Do not expand detectors, integrate bots/
rejoin or loosen enforcement. Both modes remain ReportOnly with host exemption.
