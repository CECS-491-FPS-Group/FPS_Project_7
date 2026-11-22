# Server aim incident review

Source checkpoints: `81fddd9` (bounded capture model), `1c7f5b7` (server/archive/Theater
integration). Resumed integration/anti-cheat-merged-test at 152fc8e with a clean tree;
170fafa/152fc8e were present, with no intervening work to discard. AC08 passed **per
user report**. AC09 had automated/scripted checks only; **no AC09 live multiplayer
pass has been supplied**. This batch does not claim one.

## Capture and identity

`GameplayPlayerSpawner.Awake` installs one non-network AimIncidentRecorder on the
existing persistent manager. `RoundTimer.BeginPhase` starts a new match at GAME and
finalizes at phase exit; scene/server stop also finalize. It uses server monotonic
seconds from GAME start, not client timestamps. Each match gets a GUID. Each actual
connection reference gets a fresh participant GUID in that match; connection numbers
are debug labels only. Disconnect removes the reference. Respawn retains participant
identity but changes the encounter's life/object reference. Rejoin with a reused
connection number never inherits an old flag. Incident and encounter GUIDs survive
snapshot revisions and JSON save/load. There is no account identity or ban system.

`OccludedAimObserver.LateUpdate` retains the existing decision path and records at
<=5 Hz, with an extra capture allowed exactly when an alert falls between recorder
ticks. No new aim, geometry, suspicion or enforcement threshold. It retains existing
server transform positions, approved eye origin, owner-reported aim, receipt ages,
freshness/cue/stance/effect states, cover classification/name and decision reasons.
The alert stores duration, matched seconds, sample count, angular travel/error and
query count. Recording adds **no physics query and no network message**.

The last target can be sampled during follow-up after the shooter looks away, labeled
with the observer's current non-evidence decision. It does not become new suspicion.
Actor death/despawn, new target/life, disconnect, round/scene/server end close usable
partials. Missing elapsed updates set gap markers; unavailable aim/actors stay missing.
No position or direction is invented to finish a clip. A source/server recording
failure logs a bounded neutral warning and never changes gameplay or policy.

| Bound | Behavior |
| --- | --- |
| Lead-in | Up to 10 seconds /51 samples, per current shooter-target context. Shorter if the context itself is new. |
| Follow-up/grouping |12 seconds after latest alert; overlapping alerts for same pair/life/source share one incident. |
| Clip |30 seconds,160 frames,4 stored alerts maximum. Continuous alerts beyond the cap retain the existing match flag, without growing the clip. A quiet interval >22 seconds before a subsequent alert permits a new, nonoverlapping encounter clip. |
| Match memory |16 active contexts/participants,32 incidents/flagged participant identities at most per match, even with connection churn. |
| Worker |One background writer,16 pending immutable snapshots (coalesced by incident ID); no unbounded retry or worker creation. Queue saturation is explicitly a storage failure, not a player verdict. |
| Disk |Version1 JSON, <=256 KiB/incident, latest 32 files,7-day retention; <=8 MiB incident payload plus bounded review sidecars and transient atomic-write files. Retention/expiry pruned on saves; expired files omitted from catalog. |
| Notes |Separate <=4 KiB sidecar,280-character local note. Tied to evidence revision; a later snapshot makes an earlier review stale. Notes are deleted with expired evidence; marking Reviewed does not renew retention. |

The first alert queues an immediately usable incomplete snapshot; follow-up/lifecycle
end queues the final revision. Worker serialization/IO is best-effort. Abrupt process
termination, full disk, denied permissions or a stalled OS write cannot guarantee
durability. A stalled writer does not stall the main thread or spawn replacements;
the queue stays bounded. Final partials are tested for normal leave/round-end paths.
Only owned leaf names are loaded; size/schema/finite-value/identity checks precede
playback. Linked folders/files and arbitrary path selection are rejected.

## Host-local Theater

Storage: `Application.persistentDataPath/AimIncidents`. With current project settings
on Windows: `%USERPROFILE%/AppData/LocalLow/DefaultCompany/Project FPS 7/AimIncidents`.
Files are `<matchGuid>_<incidentGuid>.json`; notes append `.review.json`. They are
outside the build's checked files. No client RPC fetches hidden positions or notes.
Processes with physical access to the same OS account's folder can read local files;
there is no claim of protection from a malicious host or local file editor.

Leave the session, return to MainMenu -> **Theater / Replays -> Server aim incidents**.
Cycle the Match and Flagged player selectors; select an incident (six rows/page).
Play/Pause and scrub the shared-style timeline. Toggle **Top XZ / Side XY**. Cyan is
shooter, orange target, yellow the reported aim ray from the recorded server eye.
Dots are actual actor root samples, not hitboxes, with uniform plot scale; no map
geometry, host camera or client POV is reconstructed. Ray extent is a5–100m display
length, not a range/hit test. Scrubbing is step playback: the UI labels the selected
snapshot time, reports missing aim/poses, and hides current markers/ray in gaps.
Recorded cover classification is shown, never recomputed against today's map.

The existing movement Theater does not retain seed/geometry/lifecycle/aim history
needed for faithful map reconstruction. Its ordinary capture, files and playback
remain unchanged. The incident view therefore deliberately says **schematic**, not
full-match replay. PARTIAL/INCOMPLETE and source are explicit. `scripted-fixture` rows
are distinct from `client-aim` observations. Fixture actor IDs are synthetic; running
the fixture never flags the live context host/player.

Enter a short note and click **Reviewed + Save note**. Back/reopen verifies persistence.
Review state has no route to suspicion, damage, disconnect or bans. The reviewer is
offline and cannot start the network. Dedicated-server operators can locally copy
their archive to a graphical developer instance for review; no network download or
automatic sharing was added. No manual scene/prefab/UI wiring is required.

## Actual automated evidence

- Eight deterministic suites: **337 assertions**, including 25 incident cases for
  clean behavior, lead-in, grouping, immutable revisions, quiet encounters, cadence,
  deadlines, partials, IDs/life/round reset, capacity, malformed data and gaps.
- Unity6000.3.8f1 import/compile/FishNet processing17 prefabs; **118 native** geometry
  checks and **60 existing unconnected Play Mode** lifecycle checks passed.
- Existing server fixture **22 assertions**, plus **29 incident integration assertions**:
  actual synthetic alert -> worker save -> Theater button -> match/player selection ->
  playback -> Reviewed/note reload; reused numeric connection IDs within a match,
  round/disconnect partials, no clean flag, gap UI, byte/count/expiry limits, path
  rejection and contained disk failure. Actual server prefabs use simulated owners;
  injected bridge samples are isolated under project Temp, not human evidence.
- The scripted fixture ran with Dynamic=Disconnect, emitted ReportOnly, left actor
  HP/position/activity unchanged and cleaned its isolated scene. Its synthetic clock
  bypasses live candidate/transport/cue gates, as before. It exercises the same
  geometry/temporal logic, archive and reviewer; it is not a live positive.
- Current147-source Editor/Development/Release compiler profiles and release IL
  checks passed. Aim/incident classes have no policy/disconnect/damage calls, new
  RPC or NetworkBehaviour. Fixture controls/coroutine/state are absent Release;
  local review and existing `scripted-fixture` data labels remain deliberately.
  Actual standalone Release export/runtime and all new live acceptance are pending.
- GUID/meta references and unchanged AC08 Clean/Test/installed manifest hashes
  checked read-only. Existing unrelated package/vendor/LobbyCodeUI warnings remain.

Costs measured on this machine, **fixtures, not live play**:16 contexts x5 Hz over30
virtual seconds,2,416 observations and48 snapshots took3.595 ms for pure capture;
largest clip 151 frames,71,388 serialized bytes, one serialization0.634 ms. Approximate
managed heap delta536,576 bytes. Clean 16-server-prefab capture bridge took14.426 ms
total for2,416 calls, including reflection/GetComponent/approved-posture sampling;
heap delta524,288 bytes. Spawn, live transport, physics queries, disk and GC-measurement
time excluded from bridge timing. Heap deltas use GC.GetTotalMemory, not an exact
allocation profile; these totals are not per-frame or supported-size live load claims.

Reproduce: `powershell -NoProfile -ExecutionPolicy Bypass -File Tests/RunAntiCheatChecks.ps1`;
Unity Tools/Integration/Validate Team
Features; Tools/Anti-Cheat/Validate Server Obstruction (now includes the incident
pipeline); `Tests/CheckAimAssemblies.ps1` against compiled Development/Release DLLs.
The archive fixture temporarily selects a project Temp folder and restores it;
generated validation output is ignored. A negative path test initially caught the
wrong exception type; corrected the test, without weakening path rejection.

## Limits and acceptance

An aim correlation is a **review flag, not proof of wallhack/intent**. Aim and root
poses are still client-reported, can be falsified/omitted, and do not establish what
was seen/heard between sampled server snapshots. Conservative geometry/cue rules,
client hash-report limitations, trusted host assumption, ReportOnly defaults and
host exemption remain. No new detector, aim punishment, admission policy, full-match
replay, enemy replication filtering, movement-authority rewrite or ML training.

**Pending combined Windows acceptance:** [AimIncidentRuntime.md](AimIncidentRuntime.md).
AC08 user pass and scripted AC09 passes do not complete it. Live capture/candidate
freshness, exterior-corner clean behavior, actual map HP obstruction, UI usability,
second-client isolation/recovery and real load remain to be observed. macOS, broad
latency/stress and Release runtime remain pending. Windows is preferred.
