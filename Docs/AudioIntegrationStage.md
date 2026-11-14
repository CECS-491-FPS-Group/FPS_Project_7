# Selected audio/settings integration

2026-11-13: the user reports **all AC05 runtime checklist steps passed**. This is
user-reported Windows runtime evidence (including audio/settings, combat/F10,
listener lifecycle, recovery and clean startup integrity), not tests executed by
the agent. It does not complete broader latency/macOS/release or every terrain case.
AC05 is preserved; the following setup/checklist records that checkpoint. Current
combined integration work requires a new numbered build/manifest.

## Scope and source

On `integration/anti-cheat-merged-test`, based on terrain checkpoint **17d2c02**.
Origin was fetched and `origin/Audio_Management` still matched audited pinned
**11c62b658f40dcfa83f86f4154d8f9258dbf6507** (DylanMueller1). No newer changes
were included. This is a selective adaptation, not a branch merge.

Included from that source: GameAudioSettings and its music/SFX preference keys;
LobbyAudioSettingsUI's volume-row approach, adapted to the actual current menus;
ShotAudioBroadcaster's positional falloff, adapted into existing PlayerAudio; and
only Health's procedural hurt/death clip synthesis. Source metas are preserved for
the two imported scripts. No source scene/player/weapon replacement was copied.

Music, Glock/shotgun and first footstep clips were already present in Resources from
earlier integration. Read-only Git blob comparisons confirm all four are identical
to their counterparts at the pinned source, so no duplicate audio imports were needed.
Existing Resource names/GUIDs remain. The new GameplayAudioClips helper synthesizes
the source's short hurt/death effects once per process, without importing its Health.

Excluded: the branch's alternate shooter/weapon holster/projectile systems, its
client-triggered shot-audio ServerRpc, local Health/respawn logic, persistent
LobbyMusicManager, microphone/VoiceChat/Gain prototype, and other gameplay stages.
Current server combat/ammo/reload/cadence/health/currency/boost/spawn/round checks,
terrain, request limits, integrity and disconnect recovery remain authoritative.
No spawn redesign: AC04's limited centre-area spawn variety is recorded separately.

## Playback and trust boundaries

- **Shots:** the existing server-accepted `PresentAcceptedShot` ObserversRpc remains
  the only shot-audio trigger. Ownership, request budget, finite aim, life/config,
  reload/ammo/cadence and server hit/damage processing still precede it. Rejected
  requests do not reach it. No new audio ServerRpc or client-selected clip/path exists.
- This checkout had **no predicted owner shot audio**. It still waits for the accepted
  result, playing once on each observing client, including the owner/host. Attributes
  remain BufferLast=false/RunLocally=false; no second server-local playback is added.
  Shooter-confirmed recoil/hits and soldier animation remain on their existing paths.
- The existing RPC now includes a boolean sound kind derived from the **accepted
  server profile's pellet count**. It is captured when firing, so a later switch or
  SyncVar delivery order cannot change that shot's clip. Multi-pellet profiles use
  the shotgun clip; other profiles use the existing shot clip. This changes the wire
  signature: **rebuild all participants together; AC04 clients are incompatible**.
- Owner shots use 2D local playback. Remote shots emit at the player's head height,
  full nearby volume inside 6m, quadratic fade to silence at 90m. Reused sources avoid
  per-shot GameObjects; Doppler is disabled. Volume remains intentionally conservative.
  Sound does **not** raycast walls/implement acoustic occlusion; solid structures still
  obstruct combat via the existing server physics, independently of audio.
- **Hurt/death:** a local C# presentation event exposes existing HP SyncVar changes.
  A graphical host uses its server-side callback; a remote instance uses the client
  callback. The host's later client callback is filtered, preventing double playback.
  Only decreasing positive HP produces a cue. A killing change uses death instead of
  hurt; initial snapshots, unchanged HP and respawn healing are silent. Shield-blocked
  hits do not change HP and therefore produce no damage cue. No new HP RPC/state exists.
  Network coalescing may combine HP changes; this is presentation, not per-hit auditing.
- **Footsteps:** existing displacement-derived presentation remains, now respecting
  SFX volume and recognizing static box surfaces as well as terrain/decks. It uses a
  bounded nonallocating ground probe, stops while dead/uninitialized/airborne and drops
  large respawn/teleport deltas. Replicated footsteps are approximate; they are not
  a detector or authoritative movement/grounding evidence. No movement controller changed.
- Menu/lobby music, UI clicks, shots, steps and hurt/death respect their local category.
  No reload/landing/pickup/ambient/voice sounds or new gameplay mechanics were added.

## Listener and settings lifecycle

The inspected AC04 Editor log places listener warnings during menu transitions and
before owning PlayerCameraSetup.OnStartClient in gameplay (e.g. warnings immediately
before owner 0's successful owner callback). Current player prefab camera/listener
references are valid, and the owner is enabled while remotes are disabled. This
supports a **transition/loading gap**, not evidence of an ongoing ownership failure
after the owner callback. The old menu-only fallback was destroyed when gameplay loaded.

IntegrationAudio now creates **one local persistent audio root/listener before scene
load**, separate from any NetworkManager. It follows the registered owning camera in
LateUpdate after look/recoil, and remains alive through loading, death/respawn, teardown
and MainMenu recovery. Menu camera is a fallback; with no camera it retains its last
pose. Existing serialized scene/player listener components are disabled locally,
never enabled on remote players. Only the actual owner registers its camera; stopping
or destroying a different player cannot clear that registration. UNITY_SERVER builds
skip the local audio bootstrap/player audio. Existing prefab inheritance/GUIDs remain.

The same root owns menu music and clicks, replacing the former separate persistent
click object. There is no second LobbyMusicManager. Scene/player/settings subscriptions
are removed on teardown, and static registration resets for a new Play session.

**No manual UI wiring:** MainMenu -> existing **Settings** opens the original settings
canvas with Music and Sound Effects rows added at runtime. Existing menu buttons and
placeholder settings controls are retained. Lobby has a compact **Audio** button at
the top right that opens/closes the same two rows. Controls belong to their scene;
there is no new gameplay pause/input flow. Test layout/clickability at your resolution.
Legacy source UI/Skin sprite paths failed under Unity 6; plain colored Image controls
replace those missing-resource dependencies.

Values clamp to 0–1 (non-finite values fall back to 1), stored in PlayerPrefs keys
`audio.music` / `audio.sfx`. They apply live, save on menu dismissal/scene exit, pause
or application quit, and survive restart. They are never synchronized over the network.
Running processes keep independent local settings; identical builds under one OS user
share the usual persisted company/product PlayerPrefs store, so the last process to
save supplies the next launch's defaults. Separate build folders are not separate
preference profiles. This stage does not add a profile/account system.

Both anti-cheat modes still default **ReportOnly**, with host enforcement exemption.
Client hash claims remain falsifiable; player-hosted servers assume a trusted host.

## Validation actually run (2026-11-12)

- **112 existing pure checks passed** (notice 18, integrity 31, movement 33, request 30).
- Current-source **Editor/Development/Release compiler checks passed**, with existing
  LobbyCodeUI/obsolete vendor API/unused event warnings.
- Full Unity6000.3.8f1 import/compilation and **FishNetILPP** completed successfully
  for runtime/Editor assemblies; registration remains **17 prefabs**. No new
  NetworkBehaviour, serialized network layout or registration/scene/prefab change.
- **48 automated unconnected Play Mode checks passed with graphics enabled**:
  required clips, finite volumes, actual UI callbacks, music/click category gains,
  player live mute, owner-camera handoff, unowned player camera/listener disabled,
  host/remote/dead/respawn cue classification, falloff endpoints, one listener/root
  across MainMenu -> lobby -> gameplay -> lobby -> MainMenu, and no stale settings/
  player event subscriptions. Checks restore the original audio preferences afterward.
  These exercise local Unity lifecycle and presentation rules, not real RPC delivery,
  a multiplayer death/respawn or audible human acceptance.
- First UI attempt caught missing source builtin sprite paths and an inactive-parent
  test lookup; both corrected. A subsequent -nographics Play run stopped at URP
  RenderTexture.Create on the null graphics device. Graphics-enabled batch passed
  without suppressing errors or disabling the render pipeline. Existing Cinemachine
  package diagnostics remain. Logs/compiled outputs are local ignored artifacts.
- Added script metas/GUID uniqueness and unchanged protected scene/prefab/vendor/
  terrain/detector assets were checked. Existing Road.mat/Water.mat changes were present
  on entry, preserved byte-for-byte, and are excluded from this audio commit.

Repeat: `powershell -NoProfile -ExecutionPolicy Bypass -File Tests/RunAntiCheatChecks.ps1`.
With the Editor closed, run Unity **without -nographics**:

```text
Unity.exe -batchmode -projectPath <checkout> -executeMethod AudioIntegrationChecks.Run -logFile <log>
```

Or **Tools > Audio > Validate Audio Lifecycle** with Play Mode stopped. It saves only
after your normal dirty-scene prompt, opens MainMenu, enters/exits Play Mode, and tests
unconnected scenes. It does not host, join or mutate a trusted baseline. Editor code
is excluded from players. Automated assertions are separate from the manual rows below.

## AC04 evidence and AC05 build setup

AC04 user evidence is in TerrainIntegrationStage.md: Editor host + two remotes,
reported normal movement/shooting/power-up collection/F10, reported screenshot of
owner 1 startup Verified, observer startup for 0/1/2 at 6/9 and ReportOnly policies.
Do not infer owner 2 Verified or every terrain/boost/respawn/recovery case.

Read-only inspection reconfirmed both AC04 copies' selected files and client build
identities match archived **68eef2a27f65489d96b28e4bf08a48ff**. Installed default
`<project>/IntegrityServer/approved.json` is byte-identical to the AC04 archive.
No inspected process/user/machine manifest or policy override was set. No build,
manifest, metadata, backup or deployment was changed. **AC05 is next unused**, verified
from Desktop/Builds and Manifests; recheck before creating it.

1. Stop old standalone sessions and Play Mode. Open this audio checkpoint in Unity
   6000.3.8f1, finish import/FishNet processing. Keep both modes ReportOnly and F7 off.
2. Ensure `Desktop/Builds/AC05-Clean`, `AC05-Test`, any AC05 backup directory and
   `Manifests/approved-AC05.json` do not already exist. If occupied, inspect/preserve
   them and choose the next unused number. Preserve AC02/03/04 pairs.
3. Build Windows **Mono Development**, existing MainMenu-first scene list, into
   `AC05-Clean/Project FPS 7.exe`. Rebuild every peer because the presentation RPC
   signature changed. Require successful build and launch; do not mix AC04 clients.
4. **Tools > Anti-Cheat > Generate Windows Mono Integrity Manifest**: select that
   finished clean executable and save `Desktop/Builds/Manifests/approved-AC05.json`.
   The tool also creates Clean's `Project FPS 7_Data/integrity-client.json`. Do not
   rebuild afterward, hash Test or assume independently rebuilt binaries match.
5. Stop hosting. Preserve the existing installed approved.json under a fresh backup
   name outside the builds; copy the existing new archive to
   **`<project>/IntegrityServer/approved.json`**. If `FPS_INTEGRITY_MANIFEST` is set
   before starting Unity/server, select that trusted deployment instead. The Desktop
   archive alone is not a server installation. Restart hosting to load it; installation
   requires no rebuild. Keep the approved archive and server copy separate from Test.
6. **After** metadata generation, copy the complete AC05-Clean folder into a new
   AC05-Test folder (including integrity-client.json). Leave Clean/archive pristine.
   Use Test for the remote session. No tampering is needed for this audio batch.

## One focused Windows runtime batch (pending)

Editor host + AC05-Test remote, preferably a second clean remote. Record owner IDs,
build ID, relevant logs and what was heard/observed. Both policies ReportOnly.

| Check | Action / expected outcome |
| --- | --- |
| Clean join | Host/join normally. Server approved identity matches AC05; require remote `kind=startup result=Verified`. Editor host remains `EditorSkipped (not verified)`. No unsupported/configuration/mismatch result. |
| Local controls | In MainMenu Settings, move Music and Sound Effects independently; 0 mutes the selected category, restoring it resumes audible output. Close/reopen Settings; enter lobby and use Audio. Changes on one process do not change the other running processes. Quit/relaunch one client and confirm persistence (same OS-user copies share the saved store as explained above). Check rows/buttons remain visible/clickable and existing menu navigation works. |
| Shots/position | Alive in match, fire each weapon from host and each remote. Owner hears one cue per accepted trigger (including shotgun, not one per pellet); other peers hear it once at that shooter. Move left/right, turn the listener, then separate beyond 90m: direction/attenuation should change and distant shots become silent. Try switching just after firing: the accepted shot keeps its sound type. No predicted owner double-shot; latency may delay accepted audio. |
| Hurt/death/steps | Take nonlethal real damage, then a killing hit: one hurt cue for the nonlethal change, one death cue instead of hurt for the killing change, on host and remote. No cue for initial spawn/respawn healing. Shield-blocked hits are silent for damage. Footsteps work on terrain, bridges and building roofs, stop airborne/dead, and do not burst on respawn placement. Check SFX mute affects these and UI clicks. |
| Combat/F10 | Normal ammo/cadence/reload/switch/damage remain functional. Alive on remote, press F10 three times >=2s apart: existing invalid aim/switch budget/scoreboard/balance rejects, **no remote shot audio from rejected probes**, no disconnect. Wait >=2s, then normal combat works; second clean client is unaffected. |
| Lifecycle/listener | Kill/respawn, let match return to lobby, start a second match. Music is menu/lobby only, with no doubled loops. Console/Player.log has bounded `[Audio] scene=... listener=local ...` and owner-camera registration, with no zero/multiple-listener warning. In Editor inspect exactly one active AudioListener on Local Audio; remote player listeners remain disabled. Death must not silence the local listener. |
| Recovery | Remote intentional Return/leave -> usable MainMenu; settings/click/music still work. Also stop hosting while a remote is in gameplay: local recovery/fallback notice works and audio returns with it. Manually rejoin a restarted host, require fresh startup Verified and no duplicate audio root, stale message or listener. Host/other players remain functional when one client leaves. This does not re-prove every punitive kick/latency race. |

No audible Windows standalone/multiplayer audio pass is claimed yet. The broader
terrain/boost/respawn matrix, latency, release runtime and macOS remain incomplete.
Windows is preferred; Touch Bar F-key alternatives remain optional future work.

After this gate, recommended next integration stage is the audited **owner-scoped
input/crouch port**, only when authorized and after reviewing remaining terrain/boost
regressions. Clarify whether actual crawl is wanted. Spawn variety is a separate
bounded follow-up. Do not automatically merge the entire crouch branch or begin it here.
