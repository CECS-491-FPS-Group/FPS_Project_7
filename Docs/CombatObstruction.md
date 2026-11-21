# Server combat obstruction

AC09 review, based on AC08 `384a6c6`. `HitscanShooter.ProcessServerShot` is the
guarded path used by the owned RPC: admission budget, server weapon snapshot,
living shooter, finite unit direction, pitch/facing, reload/ammo/cadence. The origin
comes from `PlayerCameraSetup.ServerShotOrigin` / `PlayerPosture.AimOrigin`, applying
approved crouch immediately. ADS/recoil affect the requested direction, not damage
or server origin. There is currently **one eye-based hitscan origin**, no separate
muzzle-origin rule and no intentional penetration.

`CombatRayGeometry.FirstHit` preserves the nearest non-self/non-dead solid ray hit.
World collision stops damage; triggers and Ignore Raycast/cosmetic corpses are excluded.
`FireServerRay` only damages a spawned living opponent through server Health and
approved damage/effect state. No client hit, damage value or origin is accepted.

The native fixture reproduced a concrete Unity blind spot: a ray starting **inside**
a box misses that enclosing box and can reach a target beyond it. The new bounded
origin overlap/ClosestPoint guard rejects an enclosed-world origin before ammo or
damage. Saturated overlap is conservatively rejected, not a cheating verdict. It
does not punish ordinary blocked shots, change thresholds or add movement correction.

Active RoundImplementation structure settings select Building_01 with one solid
BoxCollider (about 5.16 x 3.5 x 5.33m). Rendered doors/windows are **not necessarily
collision openings**. Roads/bridges use generated static MeshColliders; water has no
collision. A legal opening means a gap in actual collision geometry. No terrain,
prefab, GUID, health model or penetration rule was changed.

## Automated evidence versus runtime acceptance

`Tools > Anti-Cheat > Validate Server Obstruction` (`CombatServerChecks.Run`) starts
an isolated server-only Play Mode session using MainMenu's existing NetworkManager
and real registered Player prefab. Owners are simulated authenticated connection
objects, not remote transport/authentication tests. The harness exercises the same
guarded shot method and checks server HP/ammo: full wall blocks; opening allows 40
damage; exposed edge allows damage; enclosed origin rejects without ammo; immediate
approved crouch and stand retain wall obstruction. Its initial server start and
seven state assertions passed. Cadence is reset between independent geometry cases;
this harness does not measure real latency or test shot delivery timing.

Native geometry fixtures separately reproduce enclosing-origin failure and check
the guard, solid wall, opening and partial cover. Further aim admission assertions
are described in OccludedAimObservation.md after that observation is installed.

**Pending interactive Windows AC09:** actual map collision/visual agreement, remote
HP outcomes around corners/openings/partial exposure, immediate crouch/stand, delayed
shots, clean second-client isolation, and normal combat/recovery. There is no lag
rewind: server current geometry/replicated pose decides hits. Client-authoritative
root positions can still be falsified; this guard is not position attestation.
