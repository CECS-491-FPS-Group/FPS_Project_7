# Optional server enforcement

Both systems default to **ReportOnly**. Leave them there until their report-only
runtime tests pass. The user subsequently supplied clean startup verification and
ReportOnly tamper/recovery passes, plus successful static/dynamic server disconnects.
Disconnected-client UI was initially stale. The user now reports the instructed
recovery tests passed, specifically confirming static/dynamic MainMenu explanations
and startup rejection of a still-modified client. These are user-tested manual
results, separate from automated checks; see [DisconnectRecovery.md](DisconnectRecovery.md).
This chunk adds policy to existing evidence, not new detectors.

## Configuration

In Editor Play Mode, before pressing Host, select the persistent NetworkManager
object. Its automatically installed **FileAntiCheat** component exposes:

| Server field | Default | Meaning |
| --- | --- | --- |
| Static Mode | ReportOnly | Disconnect only for eligible static results |
| Dynamic Mode | ReportOnly | Disconnect only for sustained-speed evidence above threshold |
| Dynamic Suspicion Threshold | 8 | Effective value clamped to 5–10; nonfinite values use 8 |

These are ordinary server-local Inspector fields, with no client setter RPC or
broadcast. Runtime Inspector changes take effect on the next qualifying evidence.
They do not persist after exiting Play Mode. No prefab changes are required.

For standalone servers, set environment variables **before launching** the server.
They also work for an Editor launched from that environment:

```powershell
$env:FPS_STATIC_AC_MODE = 'ReportOnly'
$env:FPS_DYNAMIC_AC_MODE = 'ReportOnly'
$env:FPS_DYNAMIC_AC_THRESHOLD = '8'
```

Only exact `ReportOnly` and `Disconnect` values are accepted; invalid environment
modes fall back to ReportOnly and log configuration warnings. Unset values use the
Inspector fields. Environment overrides apply at server start; inspect the
`[AC Policy] server static=... dynamic=... dynamicThreshold=...` log. An already
running Unity process does not inherit changes made in a new shell. A client setting
its own fields/environment cannot alter the remote server's policy.

## Eligibility and disconnect safety

Static comparison validates supported build identity, exact slots, status/digest
encoding, sender/session, sequence and cooldown before an actionable verdict.
`HashMismatch` and `MissingRequiredFile` are eligible. `UnreadableFile`, unsupported
builds, malformed reports, client/server configuration errors, missing reports and
late reports are log-only. If any required file is unreadable, the report is
conservatively UnreadableFile even when another file is missing or mismatched.
If startup missed its 60s deadline, all later reports/rechecks for that connection
remain labeled LateReport and cannot trigger static disconnect.

Dynamic suspicion still reports at 4, caps at 10, and increases/decreases by actual
elapsed seconds of completed, non-overlapping approximately 2s windows. The default
enforcement threshold 8 is reachable in four excessive windows (about 8s measured
movement after grace). Thresholds are constrained above reporting and within the
existing cap. Enforcement evaluates fresh sustained-speed results before warning
throttling; throttled logs neither count as extra evidence nor delay enforcement.
An isolated displacement, repeated displacements alone, invalid position, or a
server frame gap does not qualify. Boost limits/tails and lifecycle resets are
unchanged. A qualifying sustained window is required even if prior suspicion exists.

The existing FileAntiCheat connection session owns a shared once-only disconnect
guard. `[AC Policy]` logs category, owner ID, action, and static per-file details or
dynamic object/rule/distance/allowance/elapsed/suspicion/threshold before calling
FishNet `NetworkConnection.Disconnect(false)`. The installed API marks the connection
disconnecting and flushes pending data before transport closure; it does not wait
for client acknowledgment. No ServerManager.StopConnection call is used. Existing
disconnect callbacks still clean up roster, scores, currency, owned objects and
integrity sessions. The instructed recovery checklist is now user-reported passed;
this does not establish exhaustive latency/stress or unreported boost coverage.

The actual local host connection is **never disconnected** by either policy.
Qualifying violations log `action=HostReportOnly` once per connection, while normal
evidence reporting continues. A player-hosted server assumes a trusted host; this
does not protect players against a malicious server operator.

The scene-specific lobby status UI did not recover established clients. The subsequent
disconnect-recovery chunk adds a fixed one-byte targeted server notice before
Disconnect(false), using FishNet's built-in ~100ms/two-tick graceful interval. It
requires no acknowledgment. The client independently recovers on connection stop
and shows a retained/dismissible reason or a generic fallback on its local MainMenu.
See DisconnectRecovery.md for the user-reported targeted runtime passes and remaining
coverage. Both committed modes still default to ReportOnly (`FileAntiCheat.cs:22–23`).

## Manifest inspection and installation status

The following is historical initial setup; the current AC02 setup is below.
At implementation inspection, `Desktop/approved.json` was a valid schema-1
Windows/Mono manifest matching all three selected files of `Desktop/DevelopmentBuild`.
Its client descriptor was identical. The known-good Desktop manifest was preserved.
The default **project-root `IntegrityServer/approved.json` was absent**, and no
`FPS_INTEGRITY_MANIFEST` process/user/machine override was found in the inspected
environment. No server-approved load or runtime static Verified result is claimed.
Desktop placement alone does not configure the server.

The default trusted location is `IntegrityServer/approved.json` beside `Assets` for
the Editor, or beside the executable for a Windows server. Alternatively the server
uses the path in `FPS_INTEGRITY_MANIFEST`. It loads once at server start. Keep it
separate from client tamper copies and outside Assets. Do not commit deployments.

Current AC02 setup: untouched `Desktop/Builds/AC02-Clean`, separate `AC02-Test`,
archived `Desktop/Builds/Manifests/approved-AC02.json`, and matching trusted
`<project>/IntegrityServer/approved.json`. The archive and installed copy are
byte-identical, build ID `96d48de44b8e41b2aeae868704157912`; no override was present
in the inspected process environment. The closed test client already matched its
original backup and all three approved hashes at end-of-day inspection. No baseline
was regenerated and no clean build or manifest was modified. See AntiCheatHandoff.md
for the workflow; Windows is currently preferred. Earlier example paths below are
illustrative; use the matching AC02 pair or a new separately approved clean rebuild.

## Windows test sequence

1. **Rebuild.** Keep the old DevelopmentBuild and Desktop manifest as an untouched
   known-good pair. Import/compile this code in Unity 6000.3.8f1 and let FishNet finish.
   Build a new Windows Mono **Development Build** into `Desktop/IntegrityEnforcement`
   with the existing MainMenu-first scene list. Rebuild all test clients for current
   code, although this policy chunk adds no new network messages/NetworkBehaviours.
   A changed Assembly-CSharp.dll needs a new matching manifest.
2. **Generate and install.** Use Tools → Anti-Cheat → Generate Windows Mono Integrity
   Manifest on the **new completed, clean executable**. Save as
   `Desktop/approved-enforcement.json`, keeping the original `Desktop/approved.json`.
   The tool also writes the matching client descriptor. From project-root PowerShell:

   ```powershell
   New-Item -ItemType Directory -Path .\IntegrityServer -Force
   Copy-Item -LiteralPath "$env:USERPROFILE\Desktop\approved-enforcement.json" -Destination .\IntegrityServer\approved.json
   ```

   There was no installed file at inspection; if one now exists, preserve it under
   a separate name first. With an override configured, install at that path instead
   or remove the override before launching Unity. Start/restart the host and confirm
   `[File AC] approved build=...` matches the new client descriptor. No rebuild after
   installation. Never generate from a modified test build.
3. **Clean ReportOnly.** In Play Mode before Host, set Static Mode=ReportOnly,
   Dynamic Mode=ReportOnly. Host normally, join using the new standalone. Expect
   `kind=startup result=Verified` for the remote connection within 60s; local Editor
   host is EditorSkipped, not Verified. Exercise normal lobby/gameplay. Record real
   output; generation/hash comparisons alone are not runtime verification.
4. **Safe tamper/recheck and recovery.** Keep the client loaded. Use the exact backup,
   append, client-manifest-tamper, rename/missing, and restoration commands in
   [StaticIntegrity.md](StaticIntegrity.md), with `$demoData` changed to
   `$env:USERPROFILE\Desktop\IntegrityEnforcement\Project FPS 7_Data` and a fresh
   external backup directory. After each change wait at least 15s since the previous
   report, focus standalone and press F9. Expect HashMismatch for appended data even
   if its client-local expected hash is changed; MissingRequiredFile for the renamed
   selected file. Restore the exact backup and descriptor, F9 after 15s, expect
   Verified; reconnect restored client for startup Verified. These F9 results are
   explicitly `kind=recheck`. If writing/renaming fails due to a lock, no successful
   tamper occurred. Failure to launch is not a detection pass.
5. **Static Disconnect, only after step 3–4 pass.** Start with restored files and a
   clean on-time startup Verified. On the **server** set Static Mode=Disconnect and
   Dynamic Mode=ReportOnly (or restart with the corresponding environment settings).
   Append the backed-up data file after load, wait 15s and F9. Expect exactly one
   `[AC Policy] ... category=StaticIntegrity action=Disconnect` with file evidence,
   followed by only that remote client leaving. Host remains listening; remaining
   players/roster still work. Restore the exact file/descriptor before relaunching;
   clean reconnect must verify. Test missing-file enforcement with the same after-load
   rename procedure in a fresh verified session. Restore immediately afterward.
   Revert Static Mode to ReportOnly.
6. **Dynamic Disconnect, only after report-only movement/boost tests pass.** Keep
   static ReportOnly and clean client files. Set server Dynamic Mode=Disconnect,
   threshold=8. Start a normal match, wait at least 2s after spawn, focus remote client,
   press F7 and sprint unobstructed for 12–15s without pickups or death. Expect earlier
   sustained reports then one DynamicMovement Disconnect when suspicion reaches 8
   from completed windows. Check evidence owner/object IDs, elapsed time and threshold.
   Other connections/server must survive. A separate F8-only session must report a
   displacement without disconnect. On the host-owned player, sustained F7 abuse must
   instead produce HostReportOnly and leave the server running. Revert Dynamic Mode
   to ReportOnly after this isolated test.
7. **Clean boost/respawn regression.** F7 off; both modes ReportOnly first. Using the
   existing host player component development menus, grant Speed for 8s, sprint
   through expiry, then repeat Speed replaced by Shield, Damage and Jump. Test moving
   Jump, normal Shield immunity and Damage combat. Have another player kill the target
   during an eligible boost and verify effects clear, respawn placement/acknowledgment,
   HP/ammo/input and continued movement. Expect no movement warning; static files
   should remain Verified. Repeat clean rows with only Dynamic Disconnect enabled
   after the ReportOnly rows pass, then return both modes to ReportOnly. Test match
   return/restart and roster cleanup separately; these are not already passed.

## Available checks versus pending runtime work

Executed: 31 deterministic integrity checks; 33 deterministic movement checks,
including enforcement eligibility, late/unreadable exclusions, threshold reachability,
non-overlapping evidence timing, cap/decay and lifecycle reset. Editor, development
player, release-symbol player and Editor-tool compiler checks passed against local
Unity references with existing warnings. No vendor/scene/prefab changes.

User-tested separately: clean startup, ReportOnly tamper/recovery, static lobby kick
with MainMenu reason, startup rejection of the still-modified file, dynamic kick
above threshold 8 with MainMenu reason, and the instructed recovery checklist.
No agent-observed runtime pass is claimed. Current source-message test assertions
need updating after the user's intentional copy edits; the end-of-day notice-test
rerun fails on its old exact fallback expectation (see AntiCheatHandoff.md).

Pending: unreported boost/boosted-respawn regressions, broader latency/burst/race
stress, release-player runtime exclusion, unreported static failure categories, and
macOS validation. Neither generation nor offline hash matching alone is a runtime pass.
Mac Touch Bar F-key controls were inconvenient; alternate development controls are
optional future work. Defaults remain ReportOnly despite isolated enforcement tests.
