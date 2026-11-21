# AC09 Windows capstone runtime batch (pending)

AC08 checklist passed **per user aggregate report**; no individual logs/measurements
supplied. Preserve its tested build/manifest pair. AC09 folders/archive were unused
at inspection; recheck all three destinations before building. All actions below
are pending interactive acceptance, separate from agent-run automated fixtures.
Windows preferred; no manual UI wiring. Rebuild **every participant**, including
Editor host import: existing HitscanShooter has a new owner aim RPC. Never mix AC08
and AC09 peers even though NetworkBehaviour order and prefab registration are unchanged.

## Build, manifest, installation, copies (in this order)

1. Stop clients/Play Mode. Import this branch with Unity6000.3.8f1; allow compilation
   and FishNet processing. Both FileAntiCheat modes must be ReportOnly initially.
   Select Windows x86_64 **Mono**, Development Build, normal MainMenu/LobbyScene_v1/
   RoundImplementation scene list. Build finished clean output to
   `Desktop/Builds/AC09-Clean`, executable `Project FPS 7.exe`. Do not overwrite AC08.
2. Tools > Anti-Cheat > **Generate Windows Mono Integrity Manifest**. Select the
   completed AC09-Clean executable. Save trusted archive **outside the build** to
   `Desktop/Builds/Manifests/approved-AC09.json`. This also writes
   `AC09-Clean/Project FPS 7_Data/integrity-client.json`. Do not rebuild afterward.
   Checked files remain Managed/Assembly-CSharp.dll, globalgamemanagers and
   resources.assets: shipped game code/Unity config/resources, not the whole install.
3. Verify all three clean hashes and descriptor buildId against the saved archive.
   Preserve the currently installed AC08 manifest, then install the **existing new
   clean archive** as `<project>/IntegrityServer/approved.json` for Editor hosting.
   If FPS_INTEGRITY_MANIFEST is set, that absolute trusted override wins: install
   there intentionally or remove the override for the host process before launch.
   A standalone server defaults to executable-folder/IntegrityServer/approved.json.
   Restart hosting to load it. Desktop archive placement alone is not installation.
4. **Only after metadata generation**, copy entire AC09-Clean to fresh AC09-Test and
   another clean client copy/machine. Inspect destinations first; never overwrite a
   prior pair. Compare selected hashes/buildId on each copy. Do not tamper this batch.
5. Editor host + **two remote Windows clients A and B**. Start from MainMenu, join,
   ready, match. Require host configured buildId=AC09 archive, both remotes
   `[File AC] ... kind=startup result=Verified`; EditorSkipped is not verified.
   A console rejection/probe log alone does not establish clean integrity.

Optional PowerShell verification/installation from repository root, **after step2**:

```powershell
$builds = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Builds'
$archive = Join-Path $builds 'Manifests/approved-AC09.json'
$data = Join-Path $builds 'AC09-Clean/Project FPS 7_Data'
$manifest = Get-Content -LiteralPath $archive -Raw | ConvertFrom-Json
foreach ($entry in $manifest.files) {
    $actual = (Get-FileHash -LiteralPath (Join-Path $data $entry.path) -Algorithm SHA256).Hash
    if ($actual.ToLowerInvariant() -ne $entry.sha256) { throw "Clean mismatch: $($entry.path)" }
}
$client = Get-Content -LiteralPath (Join-Path $data 'integrity-client.json') -Raw | ConvertFrom-Json
if ($client.buildId -ne $manifest.buildId) { throw 'Client metadata does not match' }
if ($env:FPS_INTEGRITY_MANIFEST) { throw 'Inspect host manifest override before installation' }
$installed = Join-Path $PWD 'IntegrityServer/approved.json'
if (Test-Path -LiteralPath $installed) {
    Copy-Item -LiteralPath $installed -Destination ($installed + '.before-AC09-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.bak')
}
New-Item -ItemType Directory -Path (Split-Path $installed) -Force | Out-Null
Copy-Item -LiteralPath $archive -Destination $installed
$test = Join-Path $builds 'AC09-Test'
if (Test-Path -LiteralPath $test) { throw 'Inspect existing AC09-Test; do not overwrite' }
Copy-Item -LiteralPath (Join-Path $builds 'AC09-Clean') -Destination $test -Recurse
```

Keep generated builds, archives, backups, installed server manifests and Logs out of
git. Never baseline a modified Test copy. Server config failures/unsupported/read/
late/missing/malformed reports are unverified categories, not automatic cheating kicks.
Client hash reports remain falsifiable; player-host assumes a trusted host.

## One focused session

Use A as shooter/observer, B as target, Editor host as clean third player and log
operator. Use Sidearm, no Shield/Armor/Invisibility for HP cases; wait out effects
or respawn. Fire single settled shots at distances inside weapon range. Identify
actual collider edges in Editor Scene view: current building windows/doors may be
decorative solid BoxCollider, so use a **real collision gap/between structures**.

| Case / exact action | Expected host evidence / client outcome |
| --- | --- |
| Legal opening | A shoots B's exposed capsule through a real collision gap. `[Weapon] ... accepted shot` and `[Combat] Accepted shot shooter=A hit=Capsule` (actual collider name may vary). Server target HP falls by approved sidearm damage40, absent armor/effects. Host third player continues normally. |
| Solid cover / around corner | B completely behind one solid building wall. A fires toward B's known position. Accepted shot hits building collider, B HP unchanged. Ordinary blocked shot consumes ammo and is **not** a cheating penalty. Repeat immediately after approved crouch/stand; obstruction stays authoritative. An actually enclosed eye, if encountered, logs `Rejected shot ... server origin inside solid cover or overlap query uncertain`, no ammo/HP change. Do not move an actor into geometry to force it. |
| Partial exposure / corner appearance | B edges out or crouches/stands at a corner; A shoots only a genuinely exposed body point. Clear ray may damage; wall-covered ray must not. No physical-cover aim alert merely because B is partly exposed or visible through a gap. Server uses current pose, no lag rewind. |
| Clean doorway hold | A holds crosshair still at an opening for15s. B walks across/behind adjacent wall and appears briefly. Prefer >42m separation where geometry allows, to exercise the non-cue case; nearer cases also clean but conservatively suppressed for potential footsteps. Expect **no** `source=client-aim` alert. Then A follows B for<2s after disappearance and stops; again no alert. Do not infer tracking when crosshair is static. |
| Telemetry context | On Editor server select A's spawned Player root -> OccludedAimObserver context menu -> **Development/Log aim observation state (server only)**. Expect `aimSeen=True`, recent `aimAge` during normal active play. Target/cover may be none from conservative filters; this is not verification or a cheating verdict. All hidden-target diagnostics stay in host Console. |
| Scripted positive fixture | On the same server component -> **Development/Run occluded aim fixture (server only)**. Wait12s. Expect start, then roughly8s virtual evidence `action=ReportOnly rule=correlated-occluded-aim source=scripted-fixture ... target=synthetic duration>=6 samples>=20 visibility=Covered`, then finished/cleaned. This injects known synthetic trajectories into shared geometry/temporal logic, **not live network telemetry**, human play or a claim that A cheated. Stop early with Development/Stop occluded aim fixture; rerun for full positive. A, B and host keep moving/shooting; fixture never changes them. |
| Always report-only | After initial clean cases, set **host only** FileAntiCheat Dynamic Mode=Disconnect temporarily; Static stays ReportOnly. Rerun fixture. It must still log ReportOnly, with no AC Policy Disconnect, HP/correction or connection change from this rule. Live new aim rule has no punitive path either. |
| Short existing F7 regression | With host Dynamic=Disconnect, A presses plain F7 in open terrain and moves continuously until existing sustained-horizontal-speed evidence reaches its unchanged policy threshold. Expect `[AC Policy] ... DynamicMovement action=Disconnect`, A's MainMenu explanation, B/host continue. The old actor's disable cleanup resets the probe; dismiss and manually rejoin without pressing F7 again. A gets fresh startup Verified, no stale notice/session evidence. If aborting before a kick, Alt+F7 resets probes. Local host remains exempt. Restore Dynamic=ReportOnly. |
| Lifecycle / clean isolation | After rejoin, normal combat/reload/switch, approved Speed/Jump expiration/replacement, crouch/steps, death while boosted, respawn, match->lobby->match, intentional leave. No stale aim evidence across life/scene/connection IDs; B remains functional throughout. Invisible opponents are excluded from physical-cover signal. |

Optional live human correlation trial needs A stationary and B >42m away, moving
behind the **same full box**, no recent shot cue, fresh data and >=2s grace+6s matching
aim. It may be difficult to aim at an unseen moving player without external cues;
**do not interpret no alert as detector failure or invent a human-play positive**.
Use the labeled fixture for a reproducible capstone positive. It deliberately does
not exercise owner admission/candidate selection/cue/lifecycle gates; server harness
and pending interactive checks cover those separately. No wallhack/aimbot supplied.

Record actual owner/build IDs, HP, logs and whether results are human or scripted.
If a clean case alerts, retain the exact route/stance/geometry/timing and investigate
before widening use. No threshold loosening or new enforcement. Broad latency/jitter,
supported-size live performance, macOS and Release runtime remain separate pending
work; AC08 aggregate pass provides no measurements for these new aim paths.
