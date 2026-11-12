# Windows runtime batch: request admission and legitimate gameplay

## AC03 user-observed results and setup correction

The user ran Editor host + one Windows Development standalone from AC03-Clean,
without tampering. Normal movement/shooting worked; host Console showed invalid aim,
switch request-budget, scoreboard-query and balance-query rejection logs. No disconnect
occurred, as intended. This is a targeted F10/basic gameplay user-observed pass only.
Startup Verified was **not supplied**; full boosts, pickups, recovery and second-clean-
client isolation remain pending, along with the broader rows below.

Inspection verified all three selected AC03-Clean and AC03-Test files against
`Manifests/approved-AC03.json`, build `bac7c31cb0a74bb3bd60e50b1daad852`. The installed
server copy was still AC02; it was preserved and replaced with the existing verified
AC03 archive, without baseline regeneration. AC03-Test lacked its generated client
descriptor; the existing matching Clean descriptor was copied into that missing path.
No selected binaries, existing descriptors or clean artifacts were overwritten.
Restart hosting and explicitly check `[File AC] approved build=...` plus remote
startup Verified before further integrity tests. AC03 is now occupied; the earlier
unused-number inspection below is historical, so re-run the selection script for
any future build. See AntiCheatHandoff.md for current checkpoint/integration status.

Prepared 2026-11-12. Current inspection found only AC01/AC02 build pairs and archives;
**AC03 is currently unused**, checked rather than assumed. No build, trusted manifest
or external test copy was changed by this implementation session. Both AC02 copies
still match the installed trusted AC02 baseline. Preserve those pairs.

## Prepare a matching clean/test pair

Runtime source changed, so rebuild the Editor host and standalone clients from the
same checkout. There are no new RPC/broadcast signatures or NetworkBehaviour/prefab
ordering changes; the rebuild supplies the actual server fixes and local F10 controls.

1. Stop old standalone sessions and Play Mode. Let Unity 6000.3.8f1 finish import,
   compile and FishNet postprocessing. Keep both server modes ReportOnly. Check the
   `[AC Policy]` startup log and environment overrides, not just a saved Inspector.
2. Recheck the next unused number in PowerShell (these commands only inspect paths):

   ```powershell
   $acRoot = Join-Path $env:USERPROFILE 'Desktop/Builds'
   $acNames = @(Get-ChildItem -LiteralPath $acRoot -Name) + @(Get-ChildItem -LiteralPath (Join-Path $acRoot 'Manifests') -Name)
   $acNumber = 1
   while ($acNames -match ('AC' + $acNumber.ToString('00') + '(?![0-9])')) { $acNumber++ }
   $acTag = 'AC' + $acNumber.ToString('00')
   $acClean = Join-Path $acRoot ($acTag + '-Clean')
   $acTest = Join-Path $acRoot ($acTag + '-Test')
   $acManifest = Join-Path $acRoot ('Manifests/approved-' + $acTag + '.json')
   $acClean
   $acTest
   $acManifest
   ```

   Expected today: AC03-Clean / AC03-Test / Manifests/approved-AC03.json. If any of
   those paths now exists, choose another unused number; never overwrite an old pair.
3. In Build Profiles choose Windows Mono, **Development Build**, existing MainMenu-first
   scene list. Build `Project FPS 7.exe` into the printed Clean folder. Require a
   successful build and normal launch before approval. Preserve build/version/backend
   and build output logs in your test record.
4. Run **Tools > Anti-Cheat > Generate Windows Mono Integrity Manifest** on that exact
   completed clean executable. Save the trusted archive to the printed manifest path.
   The tool writes the separate client descriptor automatically. Do not rebuild the
   selected build after hashing; independently rebuilt binaries need their own manifest.
5. Copy the finished clean build, including its descriptor, to a new Test folder:

   ```powershell
   if (Test-Path -LiteralPath $acTest) { throw 'Test copy already exists; preserve it and choose a new number.' }
   Copy-Item -LiteralPath $acClean -Destination $acTest -Recurse
   ```

6. From the **project root**, preserve the installed server baseline before selecting
   the new archive. Do not overwrite any earlier saved deployment backup:

   ```powershell
   $acInstalled = Join-Path (Get-Location) 'IntegrityServer/approved.json'
   New-Item -ItemType Directory -Path (Split-Path $acInstalled) -Force | Out-Null
   if (Test-Path -LiteralPath $acInstalled) {
       $acPrevious = $acInstalled + '.before-' + $acTag + '-' + [guid]::NewGuid().ToString('N') + '.json'
       Copy-Item -LiteralPath $acInstalled -Destination $acPrevious
   }
   Copy-Item -LiteralPath $acManifest -Destination $acInstalled -Force
   ```

   If `FPS_INTEGRITY_MANIFEST` is set for the server process, its configured trusted
   location takes precedence. Use that deployment location or launch with the override
   removed. Server selection happens at start; restart hosting and verify
   `[File AC] approved build=... platform=Windows backend=Mono path=...` matches the
   Test descriptor. No rebuild is needed after installing the manifest. Keep the
   approved archive, installed copy and clean build outside any tamper operations.

## One runtime session batch (use more rounds as needed)

Use Editor host + Test standalone; add a second clean standalone if available for
isolation. Keep F7 off, avoid F8, and leave both modes ReportOnly throughout this batch.
No F9 file tamper is needed to demonstrate these server request fixes. Record owner
IDs, selected weapon, ammo before/after, logs and observed failures; nothing below
is a pass until actually run.

1. **Clean entry:** normal lobby join, ready and start. Expect remote startup Verified
   and EditorSkipped for Editor host. Host a normal match; allow spawn/load/Health
   initialization to finish. Existing HUD, movement and combat should work.
2. **Legitimate weapons:** select 1/2/3. Shoot sidearm, automatic rifle and shotgun;
   check one round consumed per accepted shot (including a multi-pellet shotgun shot).
   Empty a magazine and confirm no further shot damage. Reload: repeated R cannot
   shorten the server deadline; firing during reload cannot consume ammo/damage.
   Switch during reload, switch rapidly and back, and verify saved magazines plus
   existing shot cooldown prevent free refill/cadence bypass. Expect ordinary
   `[Weapon] accepted shot/reload started/reload complete` and no request-budget rejects
   during normal use. Some cooldown rejects can reflect already-existing server
   cadence/delivery behavior; report their frequency rather than changing thresholds.
3. **Three F10 probes:** focus the living owning Test client in GAME. Do not hold fire
   or R while probing. Wait >=2s between presses; each keypress cycles a phase and
   writes a client `[Request TEST]` description. This is development-only:

   | Press | What it actually sends | Expected server result / what this proves |
   | --- | --- | --- |
   | First | One shot request with NaN direction | `[Combat] Rejected shot ... invalid aim direction`; ammo/weapon/HP unchanged. Exercises the existing server finite-direction validation over the real RPC. |
   | Second | 128 invalid-index switch requests (`-1`), then a zero-direction shot | At current weapon rates, the connection burst is 60; expect `[Combat] Rejected switch ... request budget exceeded` (1s log limit). No switch/ammo/HP change or disconnect. Exercises admission of a request type that previously had no budget. The same gate is called by shots/reloads; automated tests establish bucket sharing, but this one fixture does not prove every possible invalid RPC combination. |
   | Third | 16 scoreboard and 16 balance query broadcasts | Expect bounded `[Request Limit] owner=... kind=scoreboard` and `kind=balance` logs (each max once/5s). At a single arrival instant only four requests per kind can reach snapshot/scan work; a prolonged delivery burst may legitimately refill some tokens. No forged balance, score, reward or disconnect. |

   F10 loops back to phase one; phase count resets on new player spawn. If a probe
   happens while dead/loading/match-locked, local controls or server health validation
   can suppress it; repeat in a living normal round. It does not bypass any ownership,
   configuration, gameplay or static-integrity checks.
4. **Recovery/isolation:** wait >=2s, then normally switch/reload/fire. Query-driven
   displays must remain usable; server balance/score updates still publish normally.
   A second client must be unaffected by the test client's budget exhaustion. Let
   the match return/restart and manually leave/rejoin once; verify fresh connection
   state and no old budget/message leaks. Do not infer full latency/scene-race coverage.
5. **Required legitimate effects:** on host use the existing runtime SpeedHackDetector
   component grant menus (8s). Test Speed sprint through expiry and replace Speed with
   Jump while moving; no movement warning is expected. Test Shield blocking real
   shots, Damage doubling server damage, then their expiry/replacement. Avoid extra
   pickups changing the intended effect. Both modes remain ReportOnly.
6. **Death/respawn and pickups:** kill a target while reloading/boosted (Shield must
   expire or be replaced first). Verify dead-player shots rejected, one death reward,
   expected server placement/ACK, normal controls/magazines on revival, and no
   displacement warning. Have two players approach one normal pickup; server must
   consume once and grant once. The finite-position pickup fix has deterministic
   NaN/Infinity tests; this runtime row checks normal-radius/duplicate behavior only.
   Do not inject NaN into a live CharacterController to claim a test pass.

Expected outcome: valid gameplay unaffected, invalid work rejected with bounded logs,
no new punitive evidence/disconnects. This batch does not demonstrate immunity to
arbitrary flooding, malicious hash-report spoofing, wall/aim exploits, vertical cheats,
or all boost/latency transitions. Record those limitations with the actual results.

## Next focused batch after reviewing these results

Prioritize the unreported clean boost/respawn and weapon/geometry rows that fail or
remain uncertain, then ready/ping frequency limits + server profile validation and
static unavailable/error cases. Only after those should vertical/fly or repeated-
teleport observation be considered, initially ReportOnly. See AntiCheatAudit.md for
ranking and trust-boundary references. Keep AC02/AC03 archives pristine; restore any
future file-tamper test from its own matching backup, never from a different build.
