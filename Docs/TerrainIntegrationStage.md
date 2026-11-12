# Terrain/structures integration stage (Windows runtime gate)

## Source and scope

Branch: `integration/anti-cheat-merged-test`, created from audited checkpoint
`9570966f11d59e8d5ddf19f58cd820d1e3089477`. `91a420f` remains its completed
anti-cheat source ancestor. `feature/anti-cheat-complete` stays at `9570966` as the
recovery baseline. Origin was fetched; Soroush's tip still matched pinned commit
**`3a52ecd03259820237fa62387c63a3a7679e13b4`**. No later tip changes were included.

This is a selective port, not an ancestry merge. Attribution: Soroush Mirzaee,
principally `ccd42ca` (water-aware layout, bridges, road mesh, structure spawning),
with sample bridge assets from `19ea29e`/`6fadd84`, as present at that pinned tip.

Included: World/Layout/Structure settings, water-aware POIs/building pads/road spans,
RoadMeshBuilder, WaterSurface, StructureSpawner, road/water/bridge materials, sample
bridge meshes/prefabs, water-aware Editor validation, and wiring on the existing Map
Generator in **RoundImplementation**. The existing collidable Building_01 prefab is
reused. Existing WorldBoundary is wired on the same root. MapPreview passes sea level.

Excluded: Soroush's sample scene replacement, single-player scene setup/player
installation and bridge-asset creation tools; alternate health/shooter/lobby/round
systems; all audio/crouch/bots/Divyanshu/reconnect/host-migration/theater work. Neither
MainMenu, player prefabs/variants, weapon assets, FishNet/vendor code nor prefab
registration/network component order changes. The prefab collection remains 17.

## Multiplayer integration decisions

- Each peer generates locally from the same saved seed/settings/assets, as before.
  No new seed protocol is introduced. Saved seed **2385**, grid 5, chunk width 196m,
  world width 980m, sea level 6m. Clients still report terrain readiness to the server;
  server-selected spawning waits for its colliders and that connection's readiness.
  Differing builds/settings can diverge: do not mix AC03 with this stage.
- One bounded `[Terrain] ready ...` summary per completed generation exposes a
  diagnostic layout signature and counts. Expected for seed 2385:
  `layout=8d371621/poi=8/plots=20/bridges=3 buildings=20 bridgeVertices=512 seaLevel=6`.
  The signature hashes road points and counts, not every asset/physics outcome. It
  helps compare peers; it is not trusted client evidence or a new anti-cheat detector.
- Active bridges use Soroush's **procedural deck fallback** with MeshCollider, following
  the graded polyline including approaches. Sample straight bridge pieces are
  preserved but not selected in Structure Settings: their level endpoint assembly
  does not necessarily follow curved/graded roads. No double decks are generated.
  Land roads are a visual strip 0.08m above the carved terrain; terrain supplies their
  collision. Buildings retain their original solid colliders and use terrain layer 0,
  already included by the current player's ground mask. Invisible boundary walls use
  existing layer 1 (TransparentFX), excluded from the player's ground mask 9 but still
  colliding with the player. This avoids treating a boundary wall as grounded floor;
  no layer definitions/collision matrix or player prefab is changed.
- Water is visual only: no collider, swimming, buoyancy, force, speed change, damage
  or teleport. A player can wade on submerged terrain. No movement allowance or
  threshold change is justified. Bridges/structures introduce ordinary static collision.
- Server spawn/respawn placement now samples the highest dry generated surface,
  including decks/roofs, rejects slopes beyond the controller's slope limit and
  obstructed capsules, and retains player separation. If no candidate is safe, existing
  bounded retry behavior waits; it does not place a player inside geometry. Roof
  placement is possible. Pickup placement also avoids submerged/covered terrain;
  existing server eligibility, duplicate collection, boosts and payouts are unchanged.
- World clearing now removes roads/structures/water/bounds together. Destroyed chunks
  ignore late worker callbacks; generated native meshes are released. This handles
  regeneration and match-scene teardown without changing global scene/round flow.

All combat/health/currency/boost/respawn authorities, owner controls and client-owned
NetworkTransform remain. Static/dynamic policy defaults remain **ReportOnly**, host
enforcement exemption remains, and observer thresholds/tolerances are unchanged.
Hash reports remain falsifiable by malicious clients; player-hosted servers trust
their host. Static coverage remains three selected files, not all scene/asset data.

## Checks actually run (2026-11-12)

- All **112** existing pure checks pass (18 notice, 31 integrity, 33 movement, 30
  gameplay admission). `powershell -NoProfile -ExecutionPolicy Bypass -File Tests/RunAntiCheatChecks.ps1`.
- Current runtime source compiles with installed Unity references for Editor,
  Development and Release symbols. Existing LobbyCodeUI/obsolete vendor API/unused
  event warnings remain; these compiler checks alone do not run Unity/FishNet.
- Full project **Unity 6000.3.8f1 batch import/compilation and IL postprocessing** ran
  successfully after the user saved/closed the Editor. Logs identify FishNetILPP for
  Assembly-CSharp; prefab generation found the same 17 registrations. No codegen error.
  Existing Cinemachine package asmref/partial-class diagnostics remain.
- Full-project `TerrainIntegrationChecks.Run`: **65 native Unity mesh/PhysX checks
  passed**. Active-scene references/building colliders, generated roads/decks/water,
  dry POI surfaces, deck centreline collision, Full/ServerOnly surface/layout parity,
  changed-seed variation, clear/rebuild/in-flight teardown, and the real server
  placement method (safe candidate, obstruction avoidance, fully blocked rejection,
  recovery) were exercised. This is Edit Mode, not network gameplay.
- Pinned water-aware `MapValidation.Run` passes: 250 land-road samples, max error
  **0.167m**, centreline mask 1.000, **0** carved-under-bridge samples, **1,849**
  untouched-terrain samples with **0** mismatches, deterministic repeat and changed seed.
  The first run of the older validator failed because it expected underwater bridge
  points to be carved/masked as land roads; porting Soroush's validator corrected the
  obsolete expectation and added explicit dry-placement/under-bridge checks. No
  gameplay threshold was loosened. An earlier isolated fixture attempt could not
  resolve the Editor entry point; the successful full-project runs supersede it.
- Serialized audit passes: 80 GUID references resolve, added asset GUIDs are unique,
  required metas exist, all 40 scene IDs are unique. Protected player/network/vendor/
  detector assets remain unchanged and whitespace checks pass. Logs/output are local
  ignored artifacts.

Repeat native checks with Play Mode stopped using **Tools > Map > Validate Integrated
Terrain** (prompts to save dirty scenes), and **Tools > Map > Validate Layout**. Or,
with the Editor closed, run Unity `-batchmode -nographics -projectPath <checkout>
-executeMethod TerrainIntegrationChecks.Run -logFile <log>`; the entry point exits
0/1 itself. Run MapValidation.Run separately in the same way.

**Pending:** Windows standalone build, rendering, actual RPC/network generation
timing, interactive host/client collision/combat/boost/respawn and recovery. No stage
multiplayer pass is claimed. Broader latency, macOS, release runtime and unreported
power-up regressions remain pending. Windows remains the preferred test environment.

## AC04 build -> manifest -> server -> test copy

Desktop/Builds and its Manifests archive were inspected: AC01/02/03 are occupied;
**AC04 was unused**. Preserve all older pairs and deployments. Recheck before use:

```powershell
$acRoot = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Builds'
$acTag = 'AC04'
$acClean = Join-Path $acRoot ($acTag + '-Clean')
$acTest = Join-Path $acRoot ($acTag + '-Test')
$acManifest = Join-Path $acRoot ('Manifests/approved-' + $acTag + '.json')
if ((Get-ChildItem -LiteralPath $acRoot -Name) -match ($acTag + '(?![0-9])') -or
    (Get-ChildItem -LiteralPath (Join-Path $acRoot 'Manifests') -Name) -match ($acTag + '(?![0-9])')) {
    throw 'AC04 is occupied; inspect it and use the next unused number instead.'
}
```

1. Open this integration checkout in Unity 6000.3.8f1. Require no compile/FishNet errors.
   Keep Static and Dynamic **ReportOnly** (FileAntiCheat on the running NetworkManager,
   before Host); check `FPS_STATIC_AC_MODE`/`FPS_DYNAMIC_AC_MODE` server-process overrides
   and the `[AC Policy]` log. Keep F7 off and avoid F8/F9 for this clean map batch.
2. **Rebuild every standalone participant**, Windows Mono **Development Build**, using
   the existing MainMenu-first build scene list and active RoundImplementation. Build
   `Project FPS 7.exe` into `$acClean`; require a successful build and launch. No new
   NetworkBehaviour/RPC layout was added, but every peer needs the new map source/assets.
3. From the finished, unmodified Clean executable run **Tools > Anti-Cheat > Generate
   Windows Mono Integrity Manifest**. Save the archive as `$acManifest`. The tool also
   writes `Project FPS 7_Data/integrity-client.json` into Clean. Do not rebuild after
   hashing or use Test as a baseline. Independent rebuilds need their own hashes.
4. Stop hosting. From the project root preserve the installed trusted copy, then
   install the existing newly generated clean archive (no hash regeneration):

   ```powershell
   $acInstalled = Join-Path (Get-Location) 'IntegrityServer/approved.json'
   New-Item -ItemType Directory -Path (Split-Path $acInstalled) -Force | Out-Null
   if (Test-Path -LiteralPath $acInstalled) {
       $acPrevious = $acInstalled + '.before-' + $acTag + '-' + [guid]::NewGuid().ToString('N') + '.json'
       Copy-Item -LiteralPath $acInstalled -Destination $acPrevious
   }
   Copy-Item -LiteralPath $acManifest -Destination $acInstalled
   ```

   Default Editor server path is **`<project>/IntegrityServer/approved.json`**.
   `FPS_INTEGRITY_MANIFEST`, if set before server process launch, overrides it: inspect
   and select the intended trusted deployment there instead. Desktop archive placement
   alone does not install it. Restart hosting after selection; verify the approved build
   identity/path log. Installing the manifest does not require rebuilding. This stage
   did not change the existing AC03 deployment or create an AC04 manifest.
5. **Only after metadata generation**, copy Clean to a new Test folder:

   ```powershell
   if (Test-Path -LiteralPath $acTest) { throw 'Preserve the existing Test copy.' }
   if (!(Test-Path -LiteralPath (Join-Path $acClean 'Project FPS 7_Data/integrity-client.json'))) {
       throw 'Generate the clean manifest/client metadata first.'
   }
   Copy-Item -LiteralPath $acClean -Destination $acTest -Recurse
   ```

   Use AC04-Test as the remote client, keep AC04-Clean/archive/installed baseline
   separate, and never commit builds, backups or deployment copies.

## One focused runtime batch (all rows pending)

Use Editor host + AC04-Test remote; a second clean remote is useful for isolation.
Record each peer's terrain summary, owner IDs, approved build identity, relevant logs,
and actual outcomes. Repeat matches as needed without changing source/settings.

| Step | Action and expected evidence |
| --- | --- |
| 1. Clean entry | MainMenu -> host/join lobby -> ready -> match. Explicit remote `kind=startup result=Verified`; Editor owner is `EditorSkipped (not verified)`. Hash mismatch/unsupported/config errors stop the clean gate, even in ReportOnly. F10 logs do not establish Verified. |
| 2. Map parity | Compare `[Terrain] ready` seed/signature/counts above on both peers (standalone Player.log), then meet at the same bridge/building/road. Both see the same structures and agree on where collision blocks movement. No missing/pink surfaces, floating bridge approaches, duplicated structures or peer-specific gaps. |
| 3. Spawn/respawn | Initial players appear separated on dry supported surfaces with usable controls. Kill one player normally, wait for server respawn/ACK, repeat near structures. No buried/underwater/falling spawn or displacement accusation. A blocked spawn may wait, but must not place inside solids. |
| 4. Traversal | Walk/sprint across each bridge and its approaches, roads, slopes, around building corners and beside/shallow water. Check floor/side collision on both peers. Water itself is not a solid floor or speed effect; avoid assuming swimming exists. No movement warning during ordinary traversal. |
| 5. Combat geometry | With both players alive, solid building/bridge geometry obstructs shots according to existing collision/range rules; an exposed target takes server-authorized damage. Try sidearm/rifle/shotgun, reload and switch. No damage through a solid wall, duplicate death/reward or remote weapon/control regression. |
| 6. Legitimate boosts | In the **host Editor**, select the remote player's SpeedHackDetector component menu: Development/Grant Speed (8 seconds), sprint over roads/decks/slopes through expiry; grant Jump and jump/fall, then replace Speed with Jump while moving. These use server-approved state. No horizontal accusation; no F7/F8. Check normal pickups/Shield/Damage if testing them, but record those separately rather than inferring coverage. |
| 7. F10 | Alive in gameplay, press remote F10 three times, >=2s apart. Expect invalid aim, switch budget exceeded, scoreboard `no snapshot built`, balance `no balance scan/reply` logs (bounded). No disconnect. Wait >=2s, then normal switch/reload/fire/HUD still work; a second clean client remains usable if present. |
| 8. Scene lifecycle | Let the match return to lobby, then start another match. Fresh terrain summary/counts are stable, no leftover/duplicated colliders or worker exceptions, fresh players and ready/start flow work. |
| 9. Local recovery | Ordinary remote leave returns to usable MainMenu with cursor/input and correct remaining roster; manually join again without stale notices. For missing-reason recovery, stop hosting while the remote is in gameplay: it should recover locally and show its existing fallback notice; restart hosting and manually rejoin. Historical targeted AC02 kick messages remain user-tested; this clean ReportOnly batch does not re-demonstrate punitive kicks. |

Stop at this stage and report failures with logs/location. Do not increase movement
thresholds or disable checks to hide terrain issues. After reviewing this gate, select
the next authorized integration stage; no other feature branch is automatically next.
