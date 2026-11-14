# Tracked gameplay / validation follow-ups

Current Windows checkpoint: AC06 combined gameplay smoke passed **per user report**
(rebinding, effects, streaks, ragdolls, replay, combat, match lifecycle). These are not
agent-run runtime tests or exhaustive latency/power-up acceptance. AC06's anti-cheat
disconnect -> MainMenu -> manual rejoin test was **not performed**, remains pending.

| Follow-up | Evidence / disposition |
| --- | --- |
| Shift appears to trigger sprint and ADS; right mouse also ADS | User observation. Inspected defaults map Shift only to Sprint, RMB only to Aim. Shooting.LateUpdate separately narrowed sprint FOV 90 -> 60. This batch removes sprint-only zoom, leaves actual Aim zoom and all saved overrides intact. Pending AC07 confirmation; no binding-key migration/reset required. If Aim was intentionally rebound to Shift it still zooms: inspect Controls/Rebind and change only Aim if desired. |
| Crouch compresses character instead of proper animation | Confirmed source: PlayerPosture.Apply scales visual height alongside approved physical capsule. Cosmetic animation/rig work deferred; do not change collider/eye validation as a cosmetic workaround. |
| Power-up durations need balancing, especially invisibility | User suspected a shared duration, not a runtime-established fact. Source review now confirms MatchBoosts.duration defaults to 20s and is used by normal timed pickups and streak rewards; development grants use 8s. Armor is points, not a timed effect. Balance/individual durations remain deferred; no durations changed in this batch. |
| Limited spawn variety | Earlier distinct central AC04 positions; broader distribution is separate gameplay work. |
| AC07 recovery / second-client isolation | Required runtime gate in AntiCheatHardeningRuntime.md, especially remote F7 kick, usable MainMenu, fresh manual join/Verified, host and another clean client unaffected. |
| Movement characterization | Next bounded batch: crouch/Speed/Jump replacement, ceilings/slopes/bridges/water, long falls, delayed/bursty updates and respawn/scene transitions. Measure first; no speculative vertical punishment or loosened thresholds. |
| Wider platforms / networking | Windows preferred. macOS, broad latency/stress/release runtime remain pending. Touch Bar F-key alternatives optional. |
| Bots / secure resume / full match replay | Deferred dependencies documented in CombinedIntegration.md; no parallel local Health/round/auth systems. Theater currently presents local movement only. |

Prioritize the AC07 gate, then movement evidence and justified protections. Cosmetic
crouch/balance work must not obscure unfinished enforcement/recovery validation.
