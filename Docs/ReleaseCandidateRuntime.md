# Windows capstone acceptance (AC10)

AC09 live procedure **worked per user report**; no new per-case measurements/logs
were supplied. This is the regression gate for the subsequent server-cover/pickup
and recording-cost fixes. Automated evidence is in CapstoneDecisionValidation.md.
Use an Editor host and **two remote Development clients**, A and B. Windows is the
preferred environment. No manual UI/prefab wiring. Keep development controls and
fixtures for this run. This is separate from the later clean-game/settings pass.

## Build -> trusted manifest -> installation -> client copy

At inspection AC07/08/09 Clean/Test and archives existed; AC10 did not. Recheck
AC10-Clean, AC10-Test and approved-AC10.json before building. If occupied, preserve
all three and choose the next unused number consistently. Never overwrite AC09 or
generate a trusted baseline from a modified Test. Current AC09 identity:
5fcabd3f39f84fee92c0a717aeb869bd; Clean/Test/installed/archive rechecked read-only.

1. Stop standalones/Play Mode. Unity6000.3.8f1, current integration branch. Import,
   compile and allow FishNet processing. Windows x86_64 **Mono Development Build**,
   existing MainMenu -> LobbyScene_v1 -> RoundImplementation scene list. Build to
   `Desktop/Builds/AC10-Clean/Project FPS 7.exe`. This pass adds no RPC or network
   component layout; gameplay assembly/hash changed: **rebuild every participant**
   as one cohort. Never assume independently rebuilt outputs share hashes.
2. From the **finished clean executable**, Tools -> Anti-Cheat -> **Generate Windows
   Mono Integrity Manifest**. Archive outside the build as
   `Desktop/Builds/Manifests/approved-AC10.json`. Generator also writes
   `Project FPS 7_Data/integrity-client.json`; do not rebuild afterward. Selected
   files are Managed/Assembly-CSharp.dll (gameplay code), globalgamemanagers (Unity
   scene/config data), resources.assets (shipped resources), not mutable user data.
3. Verify Clean's three hashes and client buildId. Preserve installed AC09 manifest;
   install the existing new archive at `<project>/IntegrityServer/approved.json`
   for Editor hosting. `FPS_INTEGRITY_MANIFEST` is an absolute trusted override and
   takes priority: inspect it before installation/starting Unity. Standalone server
   default is executable-folder/IntegrityServer/approved.json. Restart hosting to
   load the approved baseline. Desktop placement alone does not configure the server.
4. **After generation of client metadata**, copy entire Clean to a fresh AC10-Test.
   Compare both copies against the archive. A runs Test, B runs Clean (or another
   verified clean copy/machine). Leave AC09 and all archived pairs unchanged.

PowerShell from repo root **after generation** (inspect destinations first):

```powershell
$buildRoot = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Builds'
$clean = Join-Path $buildRoot 'AC10-Clean'
$test = Join-Path $buildRoot 'AC10-Test'
$archive = Join-Path $buildRoot 'Manifests/approved-AC10.json'
$approved = Get-Content -LiteralPath $archive -Raw | ConvertFrom-Json
function Confirm-ClientCopy($folder) {
    $data = Join-Path $folder 'Project FPS 7_Data'
    foreach ($entry in $approved.files) {
        $actual = (Get-FileHash -LiteralPath (Join-Path $data $entry.path) -Algorithm SHA256).Hash
        if ($actual.ToLowerInvariant() -ne $entry.sha256) { throw "Mismatch: $folder / $($entry.path)" }
    }
    $descriptor = Get-Content -LiteralPath (Join-Path $data 'integrity-client.json') -Raw | ConvertFrom-Json
    if ($descriptor.buildId -ne $approved.buildId) { throw 'Metadata mismatch' }
}
Confirm-ClientCopy $clean
if ($env:FPS_INTEGRITY_MANIFEST) { throw 'Inspect host manifest override before installing' }
$installed = Join-Path $PWD 'IntegrityServer/approved.json'
if (Test-Path -LiteralPath $installed) {
    Copy-Item -LiteralPath $installed -Destination ($installed + '.before-AC10-' + (Get-Date -Format 'yyyyMMdd-HHmmssfff') + '.bak')
}
New-Item -ItemType Directory -Path (Split-Path $installed) -Force | Out-Null
Copy-Item -LiteralPath $archive -Destination $installed
if ((Get-FileHash $installed).Hash -ne (Get-FileHash $archive).Hash) { throw 'Install mismatch' }
if (Test-Path -LiteralPath $test) { throw 'Existing Test copy: inspect, do not overwrite' }
Copy-Item -LiteralPath $clean -Destination $test -Recurse
Confirm-ClientCopy $test
```

Do not commit builds/manifests/backups/local deployment. No tampering is necessary
for this run; static tamper enforcement has historical user evidence. Hash reports
can be forged by a modified client: integrity detection, not remote attestation.

## Must pass first: one host, A and B

Host operates Console/Inspector. Start Static=ReportOnly, Dynamic=ReportOnly on the
persistent NetworkManager's FileAntiCheat (or documented FPS_STATIC_AC_MODE /
FPS_DYNAMIC_AC_MODE server overrides). Committed defaults stay ReportOnly; host
exemption remains. Later change only host Dynamic Mode for explicit tests, then
restore it. Use normal rounds or temporarily set RoundTimer.gameLength=180 **in
Play Mode during PREGAME before GAME starts**; do not save a scene/settings change.

| Order | Actions | Expected host evidence and client behavior |
| --- | --- | --- |
|1. Clean admission | Host MainMenu -> lobby, A/B join and ready -> match. | Each remote `[File AC] owner=<id> kind=startup result=Verified`, approved buildId matches archive; host EditorSkipped means not verified. New `[Aim INCIDENT] match=<GUID> started` at GAME. Both clients usable. |
|2. Combat near cover | A selects Sidearm; B has no Armor/Shield/Damage/Invisibility. In open ground A fires one settled centre-body shot. B then fully hides behind a solid exterior wall; A shoots while standing, then crouching/standing beside cover. B peeks around an exterior corner for a valid exposed hit. | Open hit lowers HP by approved40 absent effects. `[Weapon] ... accepted shot` / `[Combat] Accepted shot shooter=<A> hit=<collider>`; blocked shot hits wall, spends one round and plays normal effects, B HP unchanged. Exposed hit damages. No rejection merely for aiming at solid cover; no damage through it. Decorative windows may have solid colliders, so use actual collision gaps/open ground. |
|3. Clean corner and lifecycle | A holds a fixed exterior corner crosshair for15s; B moves behind adjacent wall then peeks. A briefly follows B for<2s after visibility loss, then holds still. A collects Speed, moves over road/bridge/slope, lets it expire or replaces with Jump, performs moving jump, dies while boosted, respawns and moves/shoots normally. | No client-aim alert/incident from clean hold/brief follow; ordinary pickup once, approved effect/expiry, no new movement accusation during legal travel/respawn. B remains functional. Prefer >42m for clean aim geometry where practical; near cues conservatively suppress too. Do not claim exact duration from visual impression. |
|4. Review-only positive | Host sets Dynamic=Disconnect, selects A's spawned Player root / OccludedAimObserver context menu **Development/Run occluded aim fixture (server only)**. A/B continue normal gameplay for12s. | `[Aim AC FIXTURE] ... action=ReportOnly rule=correlated-occluded-aim source=scripted-fixture target=synthetic ...`; queued then `[Aim INCIDENT] saved=<match>_<incident>.json ... final=True`. Synthetic match/actors are distinct from A/B and explicitly labeled. Nobody disconnects, loses HP or receives correction from this signal. **Development/Stop occluded aim fixture** stops it safely; an early stop may give a shorter partial. |
|5. Old enforcement/rejoin | Dynamic still Disconnect: A presses plain F7, runs continuously on open ground until sustained speed reaches the unchanged policy threshold. B keeps moving/shooting. | `[AC Policy] ... DynamicMovement ... action=Disconnect`; A returns to usable MainMenu with movement explanation (project's intended edited wording), host/B continue same match. Dismiss, A manually rejoins, fresh startup Verified, no stale notice. Old actor disable resets probe; if aborting before kick use Alt+F7. Restore host Dynamic=ReportOnly. |
|6. Review and round | Finish round/return to lobby, all leave intentionally. Host returns MainMenu -> Theater / Replays -> **Server aim incidents**. Select the fixture's saved match/player/incident, play/scrub before alert and follow-up, toggle XZ/XY. Save short note via **Reviewed + Save note**, close/reopen. | Flag/source/IDs match host saved file; reported aim ray, server actor samples, recorded cover/decision and partial/gap labels readable. Missing intervals hide markers/ray, never invent a path. Fixture commonly ends partial before normal follow-up completes. Note survives reopen; no kick/suspicion/score/rejoin effect. Ordinary movement replay still opens. |

Record actual owner IDs, build identity, relevant logs/HP and outcomes. An isolated
fixture positive is not a human tracking finding, nor proof of a hacked client.
If clean corner behavior alerts, preserve incident and exact route as a regression;
do not widen thresholds. Missing/stale data and geometry ambiguity must suppress.

## Extended diagnostics after must-pass

- A/B approach one pickup together in open space: one effect/one removal. Dead or
  out-of-range player cannot collect. Try a pickup visible through/adjacent to solid
  cover if the map gives a reproducible position: must move around obstruction to
  collect. Then normal collection recovers. No client collection RPC exists; server
  polls current pose. Armor/Invisibility replacement/expiry/death/round cleanup,
  five-consecutive-PvP milestones and normal reload/switch/F10 too. Do not fabricate
  a pickup placement or relax its radius solely to test it.
- Editor **Tools/Anti-Cheat/Validate Server Obstruction**, with Play Mode stopped,
  exercises roof-between-body/eye and rolled-root rejection, open/partial/blocked
  shots, real pickup races and full incident pipeline in an isolated session. It
  leaves real actors untouched and isolates archives in Temp. These are automated
  fixtures, not client transport or human evidence.
- Other retained controls: F8 one displacement; **Alt+F8 cycles Off -> Repeated ->
  Ascent -> Level -> Off**, Alt+F7 resets movement probes; F9 integrity
  recheck, F10 bounded request probes. See MovementExpansionRuntime.md for exact
  placement/effects. New secondary movement and all aim rules stay report-only.
- If a real live incident occurs, test leaving/round end during follow-up, load
  labeled partial, then rejoin with reused numeric ID: new participant, no old flag.
  Do not manufacture live-positive evidence. Scripted pipeline already tests those
  lifecycles. Both targets/aim ages/source/gaps must remain understandable in review.
- Broader latency/loss, actual Release export/runtime, macOS and real16-player load
  remain separate pending coverage; use the installed outgoing-only FishNet simulator
  as documented in MovementExpansionRuntime.md. No unspecified jitter/RTT claims.

## One short live Profiler capture (still pending)

Editor host + A/B, CPU Timeline and GC Alloc recording, Deep Profile **off**. Capture
60 real seconds:20 clean play,20 with one fixture,20 with simultaneous fixtures on
two different server Player components. Ensure GAME lasts long enough. Note actual
players, frame rate, terrain seed, duration and fixture overlap; this measures a
hosted3-player run, not maximum16. Save the capture outside git.

Inspect main thread `AC.Movement.Observe`, `AC.Aim.Observe`, `AC.Incident.Capture`,
`AC.Incident.Finalize`, `AC.Incident.Queue`; worker **AntiCheat/IncidentWriter** /
`AC.Incident.Write`. Report per-frame total/p95/max milliseconds and recurring GC
bytes, especially finalization. `Development/Log aim observation state (server only)`
gives query/sample context; geometry tests already count queries. Do not infer exact
live query counts from CPU time. Check any main-thread spike against surrounding
render/network/GC work before attributing it to disk. Worker lag/disk failure must
stay neutral and not freeze gameplay. This capture is not yet performed by the agent.

## Short demo

Two matching clients verify against a separate host baseline. Server-approved combat
damages in open space and stops at cover; valid wall shots still spend ammo. Clean
corner holding creates no incident. A labeled fixture produces a review flag and
saved lead-in/follow-up even with Dynamic Disconnect; the clean second client keeps
playing. F7 independently demonstrates existing speed kick, MainMenu and manual
rejoin. Offline Theater shows reported aim, server samples, decisions and gaps; a
Reviewed note is local and never punishment. State the limits: sampled client claims,
trusted host, no attestation/intent proof, no full-match replay or reliable noclip.
