# AC09 acceptance procedure (historical)

**AC09 live procedure worked per user report**, recorded2026-11-23 without invented
per-case logs/metrics. Preserve AC09 comparison pair. Do not repeat these historical
build/install steps over existing artifacts. Current acceptance for the subsequent
cover/pickup/recording fixes is **AC10**, after checking unused destinations:
[ReleaseCandidateRuntime.md](ReleaseCandidateRuntime.md). Actual new automated results:
[CapstoneDecisionValidation.md](CapstoneDecisionValidation.md). Earlier pending labels
below describe the prior checkpoint, not today's evidence.

Use current integration/anti-cheat-merged-test, including source checkpoint 1c7f5b7.
AC08 passed per user report; **AC09 has no reported live multiplayer pass**. Automated
and scripted checks are separate. At inspection Desktop/Builds contained AC07/AC08
and manifests through AC08: **AC09 is still the next unused number**, so this run uses
AC09 with the incident implementation. Recheck destinations; if any AC09 pair now
exists, preserve it and use the next unused number consistently instead.

## Prepare once

1. Close standalones/Play Mode. Import with Unity6000.3.8f1 and allow compile/FishNet
   processing. Both FileAntiCheat modes default ReportOnly; retain host exemption.
   No manual UI/prefab wiring. This review stage adds no RPC/layout change relative
   to 152fc8e, but runtime assembly/hash/UI changed: **rebuild all participants** for
   one matching clean cohort. Do not mix AC08/old aim binaries with the new server.
2. Windows x86_64, Mono, Development Build, existing MainMenu/LobbyScene_v1/
   RoundImplementation scenes. Build to fresh **Desktop/Builds/AC09-Clean**, normal
   `Project FPS 7.exe`. Keep AC08 untouched.
3. From the **finished clean executable**, Tools/Anti-Cheat/Generate Windows Mono
   Integrity Manifest. Save outside build as **Desktop/Builds/Manifests/approved-AC09.json**.
   This also writes `Project FPS 7_Data/integrity-client.json`. Do not rebuild after
   generation. Do not baseline Test or regenerate any earlier trusted pair.
4. Verify the three selected clean hashes and client buildId. Preserve installed
   approved.json as a distinct backup. Install the **existing new clean archive** at
   `<project>/IntegrityServer/approved.json` for Editor hosting. Check the absolute
   `FPS_INTEGRITY_MANIFEST` override if set: that wins over default. Standalone server
   default is executable-folder/IntegrityServer/approved.json. Desktop archive is not
   installation. Restart hosting to load it.
5. **After client metadata generation**, copy all Clean to fresh AC09-Test and a
   second clean copy/machine. Inspect destinations first; never overwrite a pair.
   Verify hashes/buildIds for both copies. Exact verification/copy PowerShell remains
   in [OccludedAimRuntime.md](OccludedAimRuntime.md#build-manifest-installation-copies-in-this-order),
   using the chosen unused number. Checked files still Assembly-CSharp.dll,
   globalgamemanagers, resources.assets; incident data is outside that set.

## One host, two remotes

Editor host operates Console/Inspector. Remote **A** shoots and performs the eventual
F7 regression; remote **B** is target/clean second client. Require each remote
`[File AC] owner=<actual id> kind=startup result=Verified`. EditorSkipped is not verified.
Keep Static/Dynamic=ReportOnly initially. Console should also show
`[Aim INCIDENT] match=<GUID> started` on GAME entry. Record build/owner IDs, source,
HP/logs and actual outcomes; do not invent durations or infer Verified from other logs.

The game phase defaults30s. For a single manageable test match, the Editor host may
set RoundTimer.gameLength=180 **in Play Mode during PREGAME, before GAME starts**.
This is a temporary test duration, not a committed balance/threshold change. Otherwise
split the cases across normal rounds. Restore/exit Play Mode afterward.

| Case | Action on the map we actually have | Expected evidence |
| --- | --- | --- |
| Open-ground valid hit | A selects Sidearm; B stands on open ground, no Shield/Armor/Damage effects. A fires one settled centre-body shot within range. | Accepted-shot/hit log with A's actual owner ID; approved damage40 lowers B HP100->60. No flag merely for shooting. |
| Solid-cover blocked shot | B moves fully behind a building's solid exterior wall. A fires toward that position. Repeat after crouch/stand, still behind wall. | Accepted shot hits building collider, B HP unchanged; ordinary blocked shots consume ammo and are nonpunitive. No need for a window/door; current visual openings may be solid BoxCollider. |
| Clean exterior-corner hold | A holds a fixed crosshair on a building's **exterior corner**, while B moves behind the adjacent wall, then peeks into view. Prefer >42m separation where practical; closer is additionally suppressed for potential audio cues. Run15s, then briefly follow B <2s after disappearing and stop. | No `source=client-aim` occluded-aim alert or live incident from clean holding. No window/doorway required. An exposed ray may damage B; covered rays must not. |
| Live data context | On Editor select A's spawned Player root, OccludedAimObserver menu: Development/Log aim observation state (server only). | `aimSeen=True`, recent aimAge in active play. Candidate may be none due conservative filters. This does not itself prove detection or integrity. |
| Scripted incident | On that component choose **Development/Run occluded aim fixture (server only)**. Wait12s while A/B keep playing normally. Stop early only to test partial cleanup, using Development/Stop occluded aim fixture. | Labeled `[Aim AC FIXTURE] action=ReportOnly ... source=scripted-fixture target=synthetic`; queued incident at alert, then final snapshot. `[Aim INCIDENT] saved=<match>_<incident>.json ... final=True` confirms disk completion. Source match/participants are synthetic and distinct from the context A/host. No live actor movement/HP/connection changes. |
| Report-only independence | Temporarily change host FileAntiCheat Dynamic Mode=Disconnect and rerun fixture. B continues clean movement/combat throughout. | Same review flag/saved file, no aim policy action or disconnect. Each fixture run has fresh match/participant/incident IDs even though synthetic connection labels repeat -1/-2. |
| Existing F7 regression | With host Dynamic=Disconnect, A presses plain F7, moves continuously on open ground until unchanged sustained-horizontal policy qualifies. | Existing DynamicMovement action=Disconnect; A gets usable MainMenu/explanation. B and host continue. Old actor's disable cleanup resets probe; dismiss, manually rejoin without pressing F7 again; startup Verified, no stale notice/incident identity. If aborting before kick, Alt+F7 resets. Restore Dynamic=ReportOnly. |
| Normal lifecycle | Normal shooting/reload/switch, Speed/Jump expiry/replacement, crouch/steps, death/respawn, return-to-lobby and another match, intentional leave. Optionally save an ordinary movement replay. | Existing gameplay/allowances intact; fresh match ID on next GAME, no stale flags. No unexpected aim alert. Actual actor leave/round end during live capture must produce a labeled usable partial if a live flag occurred; do not manufacture a human positive. |

## Review on the host

1. Confirm the final fixture file saved; finish the gameplay/rejoin checks first.
   Both remotes intentionally leave, then host stops the session and returns locally
   to MainMenu. Review is offline; it must not interrupt the clean-client isolation test.
2. **Theater / Replays -> Server aim incidents**. Cycle Match/Flagged player selectors
   to the fixture match, choose its `[FIXTURE]` incident. Match the saved GUID/log and
   source. It should show PARTIAL with fixture-completed/stopped reason (a12s fixture
   ends before the normal12s post-alert capture window finishes).
3. Scrub before the alert, at the alert and through follow-up. Cyan/orange are stored
   shooter/target sample dots; yellow is stored reported aim from server eye. Toggle
   XZ/XY. Read cover/decision, cue/freshness/ages and measurements. This is an evidence
   schematic, **not actual map geometry, a continuous inferred path or shooter's POV**.
   In missing intervals it must say GAP and hide current actors/ray, not interpolate.
4. Enter `Scripted fixture reviewed; not human-play evidence.` -> **Reviewed + Save note**.
   Back/reopen and verify both note and Reviewed survive. It must not change policy,
   suspicion, scores or rejoin eligibility. Open any ordinary movement replay too.
5. Confirm no test-only temp archives/builds/manifests/notes are staged for git. The
   normal host archive is `Application.persistentDataPath/AimIncidents`; server-only,
   not downloaded by clients. Refresh if the last asynchronous snapshot was still
   pending when the menu opened. Report recording errors separately from cheating.

## Short capstone demo script

“Two clean remote clients verify against a separate server-approved manifest.
An open-ground shot deals server-approved damage; solid cover blocks it. Holding
an exterior corner is a clean case. This scripted hidden-target trajectory creates
a report-only review incident, even with Dynamic=Disconnect. Separately, F7 demonstrates
the existing speed disconnect/rejoin while the other client continues. We now leave
the session to review the saved fixture in Theater: lead-in, reported aim, target
samples, decisions and the partial ending; missing intervals would be labeled gaps.
I save a local Reviewed note, which never drives enforcement. This is explainable
sampled evidence, not proof of intent or trusted attestation, not full-match replay.”

Pending until actually observed: this entire live batch, real supported-size load,
broader latency/macOS and standalone Release runtime. Automated fixture passes do
not substitute for human clean-case acceptance. No new detector or ML work required.
