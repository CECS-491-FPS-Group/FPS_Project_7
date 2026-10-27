# Movement observer (ReportOnly by default)

Implemented on `feature/anti-cheat-complete`, starting from `63532e1`.
This observes server-side replicated XZ movement. It never corrects positions,
detects vertical movement, or performs file-integrity checks. Optional server
Disconnect mode for sustained evidence now exists; defaults remain ReportOnly.
See [AntiCheatEnforcement.md](AntiCheatEnforcement.md) for configuration and tests.

## Wiring and authority

`Assets/Scenes/MainMenu.unity` already assigns `Assets/Prefabs/Player.prefab` to
`GameplayPlayerSpawner`. That prefab now adds `SpeedHackDetector` to its root as a
variant override. Its inheritance, NetworkObject, NetworkTransform, CharacterController,
Starter Assets controller, input gating and shooting configuration are preserved.
No Inspector setup is required. Rebuild **all** test clients together: a NetworkBehaviour
was added, so mixing old and new network prefabs is unsupported.

`ApprovedMovementLimits` defines the existing walk 4, sprint 6, jump height 1.2,
Speed multiplier 1.5, and Jump-height multiplier 1.5. `ApprovedMovementBoost` uses
these values instead of caching mutable controller fields. Future gameplay tuning
must update this shared definition; changing the Inspector's controller fields alone
will be overwritten by the approved boost component.

The only authoritative allowance selector is `SpeedHackDetector.GetServerHorizontalSpeed()`.
It uses this server's compiled constants and `MatchBoosts.ServerHasSpeedBoost(Owner)`.
That accessor reads the server effect dictionary and deadline. It never reads
`LocalKind`, client speed fields, client ping, or reported limits.

| Server effect | Horizontal limit | Other gameplay behavior |
| --- | ---: | --- |
| None | 6 m/s | Normal movement |
| Shield | 6 m/s | Existing server damage immunity |
| Speed | 9 m/s | Walk 6, sprint 9 |
| Jump | 6 m/s | Jump height 1.8; vertical motion is ignored by this observer |
| Damage | 6 m/s | Existing server 2x damage |

No effect disables sampling. All players can legitimately sprint, so the observer
uses the sprint ceiling even when a client appears to be walking.

## Measurement and lifecycle

- `LateUpdate` runs observation only on spawned server objects with an active,
  authenticated owner. Remote-client components cannot produce detection evidence.
- Uses monotonic `Time.realtimeSinceStartupAsDouble`, not a configured interval,
  owner time, packet timestamps, or `Time.deltaTime`.
- Integrates allowance each server frame, then samples XZ displacement at least
  every .25 seconds. The samples accumulate horizontal path length in approximately
  two-second non-overlapping windows.
- A window's distance budget is `integratedApprovedSpeed * 1.10 + 0.5m + peakLimit * .35s`.
  The extra distance tolerates bounded replication bursts; it is not renewed on
  every sample. Incoming packet APIs/vendor code are unchanged.
- An excessive completed window adds its elapsed seconds to suspicion (cap 10).
  A clean window subtracts its elapsed seconds (floor 0). Sustained reports begin
  at suspicion 4, usually two excessive windows. One bad window alone is insufficient.
- A sampled displacement above `sampleBudget * 1.10 + 12m + samplePeakLimit * .35s`
  reports immediately and rebases. It does not charge that teleport into later
  sustained windows, and it does not clear previously accumulated suspicion.
  Displacement evidence cannot trigger enforcement, even at the suspicion cap.
- NaN/Infinity in XZ reports and invalidates the baseline until a finite position
  arrives. No Y coordinate is evaluated.
- A server frame gap longer than one second, or a nonpositive interval, discards
  partial measurements and reacquires a baseline. This is inconclusive, not a speed
  accusation. It retains existing suspicion. Packet silence alone does **not**
  trigger this reset, because server frames continue to run.
- On a server-approved decrease from 9 to 6, allow the prior ceiling for one second
  for delivery delay and deceleration. It is a higher budget, not an exemption.
  The deadline starts once on that state change; continued fast movement cannot
  renew it. Increases apply immediately. Old and new limits are integrated across
  windows; boosts do not reset suspicion or sampling.
- Spawn/server-approved ownership change: 1.5 seconds of baseline acquisition.
- Server health death, invalid owner, match-ended state: discard measurements and
  suspicion. Pregame/postgame remain observable while Health permits gameplay.
- Respawn: Health calls `AuthorizeServerRespawn(token)` immediately before applying
  its server-selected destination. This starts a fixed 1.5-second deadline once
  for the issued token. The existing acknowledgment validations and RPCs are unchanged.
  After reapplying the destination on valid acknowledgment, Health calls
  `CompleteServerRespawn(token)` to rebase only; it **does not extend the deadline**.
  If acknowledgment is delayed, the existing dead state continues to suppress
  observation until the server revives the player. There is no new client exemption RPC.
- Despawn/server stop resets measurements. Death and match end already clear boosts.

Evidence logs use `[Movement AC]`, identify owner/object, server uptime, rule,
distance, actual elapsed time, allowance, current approved limit, peak limit used in
the measurement, suspicion, XZ, and number of suppressed reports. Warnings are capped
at one every five seconds **per player**, across all rules. Measurement continues
while logging is throttled. State is constant-sized; no unbounded evidence list exists.

Optional server enforcement evaluates each fresh sustained-speed result before log
throttling, at a configurable threshold default 8, bounded 5–10 (reporting starts at
4; cap remains 10). Non-overlapping window elapsed time still drives accumulation
and decay. A single displacement/invalid position cannot disconnect. Spawn/death/
respawn, boosts/tails and match resets are unchanged. Local-host violations are
reported without disconnect. The user has now reported dynamic enforcement above
threshold 8 and its MainMenu explanation, plus the instructed disconnect-recovery
checklist as passed. This is user-tested manual evidence, separate from automated
measurement checks; broader network stress and unreported power-up regressions remain
pending. No detection threshold or gameplay code changed for the end-of-day handoff.

## Windows basic smoke evidence supplied after implementation

The user performed the Windows two-player smoke using `Desktop/DevelopmentBuild`
(Mono). Startup entries: owner 0/object 1 and owner 1/object 2, limits 6/9.
Remote owner 1 produced sustained-horizontal-speed reports at roughly 34-35m over
about 2s against roughly 16m allowed; F8 produced displacement reports around 30m.
The user reports the basic test seemed to work. The full boost, respawn, match-cycle,
three-player, late-join and latency matrix below remains pending. No threshold or
enforcement changes were made during that original smoke test. Current committed
policy defaults remain ReportOnly, with opt-in enforcement described above.

## Latest Windows manual results and remaining coverage

The user reports the recovery tests passed and screenshots confirm sustained-speed
disconnect above threshold 8 and the movement MainMenu explanation. Earlier server
evidence was suspicion 8.07, threshold 8, followed by remote-stop; client recovery
was subsequently tested after the fix. The user-reported recovery checklist pass
must not be generalized into unreported Speed expiry/replacement, Shield/Damage/Jump,
boosted-respawn, broader latency/burst/stress or macOS passes.

Windows is currently preferred. Mac Touch Bar F-key controls were inconvenient;
alternative development-only controls are optional future work, not implemented.
AC02 clean/test/trusted-manifest workflow and the short next-session checklist are
maintained in AntiCheatHandoff.md. Historical 33 deterministic movement checks passed;
they were not rerun merely for this documentation update.

## Editor + standalone runtime test procedure (full matrix still pending)

1. Let Unity import/recompile. Open the gameplay prefab and confirm its root has one
   enabled `SpeedHackDetector` override. NetworkTransform should still be client
   authoritative, CharacterController mode, interval 1, interpolation/extrapolation 2.
2. Make a **Development Build** for each standalone platform from these same sources.
   Start the Editor from MainMenu as host and connect at least two standalone clients
   through the existing lobby flow. For LAN, use the existing LAN hosting option and
   the host address. Start the match normally.
3. In the host Console, enable warnings, disable Collapse, and filter `[Movement AC]`.
   Expect one `observer started` entry for each of the three players, with distinct
   owner/object IDs and limits 6/9. Wait at least two seconds after spawn.
4. Identify each runtime player root on the **host** using its NetworkObject owner ID
   or the startup log's clickable context. On that player's SpeedHackDetector component,
   open its component context menu and choose `Development/Grant ... (8 seconds)`.
   These grants work only on the server during GAME and for a living player. They
   replace the normal server effect slot and use the normal effect broadcasts; no
   client can grant them through a new RPC. Watch the existing client HUD's boost name.
5. Keep the tested standalone window focused for movement and F keys. Sprint with
   Shift + movement; use Space for jumps. Mac keyboards may require **Fn + F7/F8**.
   Keep F7 off for all legitimate tests; its toggle writes a `[Movement AC TEST]` log.
6. Run each row below. Use fresh rounds as needed (the current GAME phase is 75s).
   Avoid unintended extra pickups, which legitimately replace the effect under test.
   Repeat for both a remote client and the host-owned player.

| Test | Actions | Expected server evidence/gameplay |
| --- | --- | --- |
| Normal sprint | Sprint, strafe, turn, jump, stop/start for 15s | No movement warning; normal combat/input |
| Legitimate Speed | Grant Speed; sprint throughout its 8s | No warning; 9 m/s ceiling, normal controls |
| Expiration while sprinting | Continue sprinting at least 8s after Speed expires | No warning; normal deceleration, 6 m/s ceiling resumes |
| Speed replaced by Shield | Grant Speed, sprint, grant Shield before 8s | No warning; returns to 6, shield still blocks damage |
| Speed replaced by Damage | Repeat with Damage | No warning; returns to 6, boosted damage still works |
| Speed replaced by Jump | Repeat with Jump; jump while sprinting | No warning; horizontal limit 6, boosted jump works |
| Jump while moving | Grant Jump without prior Speed; repeatedly sprint/jump | No horizontal warning; vertical motion ignored |
| Other boost types | Grant Shield and Damage separately; sprint and exercise combat | No horizontal bonus; immunity/damage behavior preserved |
| Death/respawn while boosted | Grant Speed/Jump/Damage; have another player kill the target before expiry; repeat Shield after it expires or is replaced | No teleport warning; death clears effects; existing placement/token handshake completes, normal HP/ammo/control return |
| Excess normal speed | Press F7 on owning client; sprint unobstructed for 8–12s; press F7 again | Local 3x values (18 m/s sprint); server sustained-speed report, no kick/correction; clean windows decay suspicion |
| Excess boosted speed | Grant Speed, press F7, sprint unobstructed immediately for 6–8s | 27 m/s exceeds 9; server sustained report while boost is active; observer never disabled |
| Horizontal jump | F7 off, wait 6s since any warning, press F8 once | Local +30m X; server displacement report, no rubberband/disconnect; subsequent normal movement stays quiet |
| Recovery after anomaly | After F8, sprint normally for 10s, then exercise F7 again | No repeated teleport accusation from old baseline; later excessive speed still detectable |
| Three-player isolation | Cheat on one remote client while other two move normally | Correct owner attribution; other players remain connected and quiet |
| Match cycle/late join | Let match return to lobby; start again; join late where supported | Old player state discarded; new observer starts once per new spawn |
| Delivery delay/bursts | Repeat normal and boost-transition tests with available network simulation | Tune only from server evidence; no expectation of tolerating unlimited stalls |

F8 bypasses collision only for the intentional test teleport. Use an area whose
destination (+30m world X) remains within generated terrain; otherwise a later fall
may confuse the demonstration. It does not authorize an exemption. If less than
five seconds has elapsed since another warning, its report may be suppressed by the
shared rate limit; wait and try again. F7 resets on death/control lock and component
disable. Verify `[Movement AC TEST]` to avoid confusing a still-enabled test toggle
with a legitimate boost failure.

Also make a **non-development** player build: F7/F8 and host grant methods must not
exist/work, while the observer still reports on the server. Editor code is always
development-enabled even when connecting to a release client.

## Checks actually run in this environment

- Repository comparison: branch changed, relevant source at the same starting commit.
- Compiled the project's Assembly-CSharp sources against the installed Unity 6.3
  references with Editor, DEVELOPMENT_BUILD, and release symbols, using Unity's bundled
  Roslyn compiler and cached project response file. All passed. Existing unrelated
  LobbyCodeUI/UpdatableData/FishNet-upgrade warnings remain. This is compilation,
  **not** Unity import, FishNet IL postprocessing, or a standalone player build.
- Ran 26 deterministic C# checks against the actual `HorizontalMovementObservation`
  and shared limits with Unity's bundled Mono runtime. Covers normal/boosted movement,
  effect transitions, bounded tails, replication bursts, sustained suspicion/decay,
  single-jump rebasing, lifecycle reset primitives, server gaps, invalid coordinates,
  and nonpositive time. Non-Speed boost fixtures feed the 6 m/s selected by the server;
  they do not instantiate MatchBoosts or simulate health RPCs.
- Checked serialized prefab override references, unchanged inheritance/network/controller
  settings, and release-vs-development test-control exclusion in compiled metadata.
- `git diff --check` passed. No FishNet vendor code or static-integrity code changed.

The original implementation session did not run Unity Play Mode/network/player tests.
Subsequently the Windows Editor import/compilation/FishNet processing was inspected,
and the user supplied the basic two-player evidence above. This does not establish
the remaining runtime table or latency tests as passed.

To repeat the pure measurement checks on this Mac from the project root:

```sh
movement_unity_scripting=/Applications/Unity/Hub/Editor/6000.3.8f1/Unity.app/Contents/Resources/Scripting
"$movement_unity_scripting/NetCoreRuntime/dotnet" "$movement_unity_scripting/DotNetSdkRoslyn/csc.dll" \
  -nologo -target:exe -nostdlib+ -langversion:9.0 -out:/tmp/fps-movement-checks.exe \
  "-r:$movement_unity_scripting/MonoBleedingEdge/lib/mono/4.8-api/mscorlib.dll" \
  "-r:$movement_unity_scripting/MonoBleedingEdge/lib/mono/4.8-api/System.dll" \
  "-r:$movement_unity_scripting/MonoBleedingEdge/lib/mono/4.8-api/System.Core.dll" \
  Tests/MovementObserverChecks.cs Assets/Scripts/ApprovedMovementLimits.cs Assets/Scripts/HorizontalMovementObservation.cs
"$movement_unity_scripting/MonoBleedingEdge/bin/mono" /tmp/fps-movement-checks.exe
```

## Initial tuning and limits

At exactly two seconds, sustained allowance is 15.8m normal (~7.9 m/s average)
or 23.45m boosted (~11.73 m/s). At a .25s sample, displacement thresholds are about
15.75m normal and 17.63m boosted. Actual logged elapsed time/budget governs each result.
These conservative tolerances intentionally leave some modest speed advantage undetected.

Tune using clean-client distance/allowance ratios, window durations, boost transition
timing, network conditions, and server frame stalls, separately for host and remote
players. First reproduce false positives; adjust burst/transition tolerance before
raising trusted base gameplay speeds. Do not use client-reported latency to raise limits.

This observes a smoothed, owner-supplied transform after acceptance. It is not packet
validation, physics enforcement, rollback, or a guarantee against cheating. Loops
between samples can be missed; very long delivery backlogs can look suspicious;
server gaps are deliberately inconclusive. Invalid movement can still affect combat
and proximity pickups because this chunk only reports.

The existing respawn handshake has no timeout: a player who never acknowledges remains
dead. No new indefinite alive exemption was introduced. An acknowledgment arriving after
the fixed grace deadline rebases but grants no additional grace; unusually late stale
transform delivery should be tested. Future approved teleports/recovery systems must add
their own bounded server-authorized hooks; TerrainSpawnPlacer is currently not attached.
