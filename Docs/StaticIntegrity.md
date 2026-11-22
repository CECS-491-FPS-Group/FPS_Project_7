# Build-file integrity (schema 1, ReportOnly by default)

Incident closeout: AC08 Clean/Test/installed server archive still match preserved
a70a4853fb014589aaa7e703be232ef2, read-only. No AC09 live pass supplied; AC09 build/
archive still absent at inspection. Rebuild all peers for new runtime assembly/UI,
then finished Clean -> archive/client metadata -> separate trusted server install
-> Test copy. See AimIncidentRuntime.md. Incident JSON/notes live under persistent
user data, outside checked game files; recording/review does not invalidate hashes.
No integrity/admission/enforcement change or trusted-attestation claim.

AC09 update: AC08 checklist passed per user aggregate report; no per-case logs or
measurements supplied. AC08 Clean/Test three hashes/descriptors and installed default
server copy match archive **a70a4853fb014589aaa7e703be232ef2**, read-only, unchanged.
New owner aim RPC/runtime code requires all peers rebuilt as next unusedAC09 (recheck).
Follow OccludedAimRuntime.md: completed Clean -> generate archive/client descriptor
-> preserve/install trusted server copy -> copy Test after metadata -> restart host
-> explicit remote startupVerified. Do not regenerate AC08 baseline. No integrity
protocol/policy/default/admission changes; client reports still falsifiable and host
trusted. AC09 runtime acceptance, broader macOS/latency coverage remain pending.

2026-11-21: AC07 batch reported passed by user as a whole, without detailed logs/
measurements. Read-only AC07 Clean/Test hashes/descriptors and installed default server
manifest match archive 84e29324cc274267be86f92042703d49; untouched. New movement runtime
source requires AC08 (recheck unused number), all peers rebuilt and matching manifest.
Follow MovementExpansionRuntime.md: finished Clean -> generate archive/client metadata
-> install separate trusted server copy -> Test copy. No new static rule/enforcement/
admission change; client reports remain falsifiable, host trusted. AC08 pending.

2026-11-13 hardening: AC06 Clean/Test three hashes/identities and installed server copy
match archived 5d276f6feab646a3bbf210b8472fb738 (read-only; untouched). AC06 gameplay
smoke passed per user; disconnect/rejoin skipped. New runtime source requires AC07,
recheck unused number. See AntiCheatHardeningRuntime.md for manifest/copy order,
duplicate/error fixtures and pending runtime. Committed policy remains ReportOnly.

IntegrityReportLifecycle closes startup Pending into an explicit result at receipt or
the fixed 60s deadline. Missing is logged once; malformed/unsupported/client-unavailable/
server-config/read errors are terminal unverified, not success or automatic punishment.
Late matching hashes now log `LateReport (matching hashes; not verified)`; sticky late
state/enforcement ineligibility unchanged. Receipt is not admission or proof of honesty.
Release rechecks reject explicitly once; development rechecks retain sequence/15s limits.
Client hashing now has a 60s watchdog and sends unavailable once on failure/timeout;
cancellation cannot guarantee interrupting OS I/O, so one retired worker is retained
until completion, with no replacement/retry loop. A later session blocked by it has a
bounded unavailable result too. No admission restriction added. Hash reports can be
falsified; this is not trusted remote attestation. Older status/build notes follow.

2026-11-13 current combined stage: AC05 audio checklist (including clean startup)
passed per user. Read-only AC05 Clean/Test selected hashes/client identities and
installed default server manifest match archive 70cb725d6d834c51832442c0e4599570;
no baseline/artifact changed. Controls/armor/streak network changes now require all
peers rebuilt together, next unused AC06 (recheck). Follow CombinedIntegrationRuntime.md
for Clean -> archived manifest/client metadata -> separate server installation -> Test
copy order. New combined startup Verified remains pending. Earlier build-number and
installed-manifest notes below are historical checkpoints. Hash-report trust limits
and ReportOnly defaults are unchanged.

AC04 follow-up: user-described screenshot confirms remote owner 1 startup Verified.
Both AC04 copies' checked files/client identities match their archive; installed
default server manifest is byte-identical (read-only verification). No owner 2 startup
result or broader test completion is inferred. Current audio source changes require
a new AC05 clean build/manifest; see AudioIntegrationStage.md. Preserve AC04 and do
not regenerate its baseline. Earlier checkpoint/build-number notes below are historical.

2026-11-12 recheck: the existing 31 deterministic integrity checks pass. AC02 clean
and test selected files still match the installed trusted AC02 manifest. No baseline
was regenerated or modified. Directory/archive inspection found AC03 unused; the
runtime-source changes require a new matching clean build/manifest for demonstration.
Use WindowsAntiCheatBatch.md, rechecking unused numbers before building. Static
protocol/enforcement policy is unchanged; AntiCheatAudit.md explicitly records the
never-report, unavailable/configuration and malicious-client limitations.

This checks three files from one developer-approved **finished Windows Mono build**.
It defaults to reporting without disconnecting, gating authentication/lobby/gameplay,
locking movement, or scanning periodically. Optional server-controlled enforcement
is described in [AntiCheatEnforcement.md](AntiCheatEnforcement.md); leave both modes
ReportOnly until runtime tests pass. A malicious client
can falsify these reports or send known-good hashes. This is file-integrity detection,
not trusted remote attestation or runtime-injection detection.

## Selected files and supported layout

Paths are relative to `Project FPS 7_Data` (the executable basename determines the
folder in the generator; runtime uses `Application.dataPath`):

| File | Reason |
| --- | --- |
| `Managed/Assembly-CSharp.dll` | Shipped game scripts, including gameplay and integrity code |
| `globalgamemanagers` | Unity build configuration and serialized manager data |
| `resources.assets` | Shipped Resources data; useful for a small data-integrity demonstration |

All three exist in the inspected `Desktop/DevelopmentBuild`
output. Cached Unity build input identifies Mono. This is deliberately partial
coverage: other assemblies, scene assets, resource sidecar data, native libraries,
and the executable are not covered. `weapon_stats.json` is unused and not checked.
Logs, saves, settings, replays, and manifests are excluded.

`BuildIntegrity.DataRootForBuild` separates Windows `.exe`/`_Data` from macOS
`.app/Contents/Resources/Data`. The runtime records Windows/macOS and Mono/IL2CPP
identities separately; schema 1 accepts Mono only. macOS layout and runtime handling
are **unverified**, and the supplied generation menu intentionally targets Windows
Mono only. IL2CPP requires a future explicit code-file policy; it is not supported.

## Wire and lifetime

`GameplayPlayerSpawner.Awake` installs `FileAntiCheat` once on its existing persistent
NetworkManager. No scene/prefab changes or new NetworkBehaviours are needed. Rebuild
**all test clients** for the new broadcast protocol; old clients cannot submit it.

The client starts one asynchronous worker after authentication. A fixed report holds
schema, platform, backend, a 128-bit build ID, sequence, mode, and exactly three
indexed SHA-256 digests with read/missing/unreadable status. It has no strings,
arrays, paths, or declared lengths: there is no variable payload to allocate from a
client-provided size. Required slots must be 0/1/2 exactly; duplicates, invalid statuses,
and nonzero digests marked missing/unreadable fail validation. Hashes use fixed
256-bit fields; manifest hashes must be exactly 64 lowercase hex characters.
Reports are attributed to FishNet's authenticated sending connection, never a
client-supplied owner. There is no RPC for selecting files or reporting another owner.

Server state is keyed by connection, discarded on disconnect/server stop/destruction,
and persists across lobby/gameplay/respawns. Startup deadline: 60 monotonic seconds
after server authentication. A missing startup report is logged once; a later report
is explicitly `LateReport result=...`, including subsequent rechecks in that session.
Late sessions are excluded from static enforcement. Reports have increasing sequences and a 15s
server cooldown. Invalid-frequency logs occur at most once per connection. In Editor
or development builds only, F9 requests a local recheck, limited to one worker and
15s intervals. Release servers reject rechecks, and release clients contain no F9
control. Rechecks have `kind=recheck`; they do not replace the startup deadline or
turn a missing startup into an on-time result. Hashing uses a worker, bounded buffers,
and cancellation on disconnect; no Unity gameplay objects are read by the worker.

All disk reads use hardcoded relative paths beneath the build data directory.
Nonallowlisted paths and symbolic links/junctions in the path are rejected. Manifests
are capped at 8 KiB, with exact ordered coverage. There is no runtime file creation,
baseline generation, or network submission of approved hashes.

Server outcomes use `[File AC] owner=... kind=startup|recheck result=...`:
`Verified`, `HashMismatch`, `MissingRequiredFile`, `UnreadableFile` (with per-file
details), `UnsupportedBuild`, `MissingReport`, `LateReport`, `InvalidReport`,
`ServerConfigurationError`, or `ClientConfigurationError`. Configuration errors are
not cheating verdicts. The actual local Editor host is `EditorSkipped (not verified)`;
remote Editor-skip claims are unsupported. Only validated, on-time HashMismatch or
MissingRequiredFile results can disconnect when the server explicitly selects that
mode. Unreadable/configuration/unsupported/malformed/late/missing reports remain
log-only. The local host is never disconnected by this policy.

## Exact Windows demonstration

1. Stop Play Mode and standalone clients. Let Unity 6000.3.8f1 import/compile and run
   FishNet postprocessing. In Build Profiles use Windows **Mono**, **Development
   Build**, with the existing scene list starting at MainMenu. Build to a fresh
   folder, e.g. `Desktop\IntegrityDemo\Project FPS 7.exe`. Do not reuse the old smoke
   executable: it predates this protocol. Check the build succeeds and launches.
2. Create `IntegrityServer` in the project root. Select **Tools → Anti-Cheat →
   Generate Windows Mono Integrity Manifest**, choose the completed executable, and
   save the trusted copy as `<project>\IntegrityServer\approved.json`. The tool hashes
   the completed files off-thread, generates a fresh GUID build identity plus schema,
   platform, backend, UTC timestamp, relative paths and SHA-256 hashes, and writes a
   separate client descriptor to `<build>\Project FPS 7_Data\integrity-client.json`.
   Keep the trusted output outside the client build; the tool enforces this separation.
3. Start the Editor host normally from MainMenu. Default server configuration is
   `<parent of Application.dataPath>\IntegrityServer\approved.json`, which is the
   project-root location in the Editor and the executable's folder in Windows players.
   Alternatively set `FPS_INTEGRITY_MANIFEST` to an absolute trusted JSON path **before
   starting Unity/server**. The server loads one approved manifest at server start,
   never from client messages. Look for `approved build=... Windows/Mono`; resolve
   `ServerConfigurationError` before testing. To select a different approved build,
   replace this trusted file (or change the environment variable), then restart the
   server. Do not rebuild. Do not save the approved copy under Assets/StreamingAssets.
4. Launch the exact generated standalone build, join through the normal UI, and wait
   for `owner=1 kind=startup result=Verified` (owner ID may differ). The Editor host is
   skipped explicitly. Check lobby, movement, shooting, damage, and respawning. Record
   their actual outcomes separately; a hash pass does not establish gameplay success.
5. With the standalone **still running and loaded**, back up its selected file and
   descriptor outside the build. Example in PowerShell (adjust demo folder):

   ```powershell
   $demoData = "$env:USERPROFILE\Desktop\IntegrityDemo\Project FPS 7_Data"
   $demoBackup = "$env:USERPROFILE\Desktop\IntegrityDemoBackup"
   New-Item -ItemType Directory -Path $demoBackup -Force
   Copy-Item -LiteralPath "$demoData\globalgamemanagers" -Destination "$demoBackup\globalgamemanagers"
   Copy-Item -LiteralPath "$demoData\integrity-client.json" -Destination "$demoBackup\integrity-client.json"
   $demoStream = [IO.File]::Open("$demoData\globalgamemanagers", [IO.FileMode]::Append, [IO.FileAccess]::Write)
   try { $demoStream.WriteByte(65) } finally { $demoStream.Dispose() }
   ```

   Wait at least 15s after the prior report, focus the standalone, press **F9**, and
   expect `kind=recheck result=HashMismatch ... globalgamemanagers=mismatch` on the
   server. This is a **recheck**, not a startup test. If Windows denies the write
   because a file is locked, no tamper occurred: record that and use a file which can
   be changed after load. Never count failure to launch as detection.
6. While leaving the changed data file on disk, edit only the client descriptor to
   contain its new hash, retaining its build ID and paths:

   ```powershell
   $demoManifest = Get-Content -Raw -LiteralPath "$demoData\integrity-client.json" | ConvertFrom-Json
   $demoManifest.files[1].sha256 = (Get-FileHash -LiteralPath "$demoData\globalgamemanagers" -Algorithm SHA256).Hash.ToLowerInvariant()
   $demoManifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath "$demoData\integrity-client.json"
   ```

   Wait 15s and F9 again: expect **HashMismatch again**. The server still uses its
   separate approved copy. Changing the client build ID instead yields UnsupportedBuild;
   deleting/corrupting its descriptor yields ClientConfigurationError, never Verified.
7. Restore the data file and descriptor from the backup. For a missing-file recheck,
   rename the data file *after load*:

   ```powershell
   Copy-Item -LiteralPath "$demoBackup\globalgamemanagers" -Destination "$demoData\globalgamemanagers" -Force
   Copy-Item -LiteralPath "$demoBackup\integrity-client.json" -Destination "$demoData\integrity-client.json" -Force
   Rename-Item -LiteralPath "$demoData\globalgamemanagers" -NewName 'globalgamemanagers.held'
   ```

   Wait 15s, F9: expect `MissingRequiredFile ... globalgamemanagers=missing`. Restore
   with `Rename-Item -LiteralPath "$demoData\globalgamemanagers.held" -NewName
   'globalgamemanagers'`. Wait 15s and F9: expect Verified. Quit/relaunch the fully
   restored client and reconnect for a fresh **startup** Verified result. Never
   regenerate the approved manifest from tampered files.
8. Optional deadline test: connect an old/nonreporting client only if protocol-compatible,
   or use a debugger to prevent sending. Expect one MissingReport after 60s; gameplay
   stays usable. Test unsupported build by changing only the client build ID. Test
   server configuration by moving the trusted copy aside and restarting the host;
   expect configuration errors, not cheating. Restore configuration afterward.

Every independently rebuilt build needs its own generated manifest; equal source
does not imply equal binary hashes. Generate once from the finished known-good build,
copy that exact build to test machines, and deploy its trusted manifest separately.
The manifest itself is not hashed, so generation/installation needs no rebuild.
Do not use the trusted server deployment directory as a client tamper-test directory.

## Validation status

Historical initial setup: the user successfully generated the Desktop approved.json from DevelopmentBuild.
Offline inspection found all three hashes match and the client descriptor is identical.
The default server manifest installation was absent; no override was found in the
inspected environment. The Desktop copy was preserved, not regenerated or installed.
Subsequently the user supplied clean remote startup Verified, ReportOnly tamper/
recovery passes, and a successful static server disconnect. The disconnected client
initially remained in stale UI. The user has since reported the instructed recovery
tests passed, with screenshots reported to confirm the static MainMenu explanation
and startup rejection when reconnecting with the still-modified file. These are
manual user-tested results, not automated checks; see [DisconnectRecovery.md](DisconnectRecovery.md). Follow
[AntiCheatEnforcement.md](AntiCheatEnforcement.md) for the current rebuild/install and
ReportOnly-first enforcement test sequence.

Executed in this implementation session: 19 standalone deterministic checks of real
policy/hash code (clean, changed/missing/locked files, duplicate/invalid slots,
coverage, paths, malformed identity/hash/schema/platform/backend, bounded manifest,
and digest roundtrip); compiler checks against installed Unity references for Editor,
development player, release player, and Editor menu; release metadata inspection for
F9 exclusion. These are not Unity integration tests.

Current workflow: keep `Desktop/Builds/AC02-Clean` untouched; tamper only its separate
`AC02-Test` copy. The matching archive is `Desktop/Builds/Manifests/approved-AC02.json`;
the trusted server copy is `<project>/IntegrityServer/approved.json`, identical at
inspection (build ID `96d48de44b8e41b2aeae868704157912`). Both client descriptors
identify that build. With AC02-Test closed, all three checked SHA-256 hashes matched
the installed trusted manifest; globalgamemanagers also matched its preserved backup
byte-for-byte. No outstanding tamper required a restore write. No clean files,
manifests or baseline were modified. Windows is preferred for continued tests.

User-tested passes now include clean startup, ReportOnly tamper/recovery, static
after-load mismatch enforcement and explanation, and modified-file startup rejection.
The recovery checklist is reported passed in aggregate. Remaining categories are not
implied passed: separately unreported missing/unreadable/unsupported/malformed/late/
configuration scenarios, broader latency/race stress, release runtime checks, and all
macOS validation. No compromised-client attestation guarantee is claimed.
