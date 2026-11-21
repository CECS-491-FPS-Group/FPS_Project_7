# Tracked gameplay / validation follow-ups

Current Windows checkpoint: user reports latest **AC07 batch passed in aggregate**,
including its required disconnect -> MainMenu -> manual rejoin regression. No logs,
measurements or optional-fixture breakdown supplied; no agent-run runtime pass. AC06's
earlier skipped recovery test is superseded by this aggregate report. AC08/new movement
and adverse-network acceptance remains pending; see MovementExpansionRuntime.md.

| Follow-up | Evidence / disposition |
| --- | --- |
| Shift appears to trigger sprint and ADS; right mouse also ADS | Prior narrow sprint-FOV fix included in user-reported aggregate AC07 pass; no separate measurement supplied. Defaults Shift=Sprint, RMB=Aim; saved overrides preserved. No migration/reset required. Intentional Aim=Shift still zooms; inspect/rebind just Aim if undesired. |
| Crouch compresses character instead of proper animation | Confirmed source: PlayerPosture.Apply scales visual height alongside approved physical capsule. Cosmetic animation/rig work deferred; do not change collider/eye validation as a cosmetic workaround. |
| Power-up durations need balancing, especially invisibility | User suspected a shared duration, not a runtime-established fact. Source review now confirms MatchBoosts.duration defaults to 20s and is used by normal timed pickups and streak rewards; development grants use 8s. Armor is points, not a timed effect. Balance/individual durations remain deferred; no durations changed in this batch. |
| Limited spawn variety | Earlier distinct central AC04 positions; broader distribution is separate gameplay work. |
| AC07 recovery / second-client isolation | AC07 latest batch user-reported passed in aggregate; no individual logs. Repeat in AC08 after new movement-probe cleanup changes, leave that new acceptance pending. |
| Movement characterization | New report-only received-goal observations implemented with conservative freshness/support limits. AC08 tests crouch/boost transitions, terrain/falls/respawn, each probe and two documented outgoing-delay/loss profiles. Silent hovering/slow flight and missing contacts remain gaps; no relaxed thresholds/new punishments. |
| Wider platforms / networking | Windows preferred. macOS, broad latency/stress/release runtime remain pending. Touch Bar F-key alternatives optional. |
| Bots / secure resume / full match replay | Deferred dependencies documented in CombinedIntegration.md; no parallel local Health/round/auth systems. Theater currently presents local movement only. |

Prioritize the AC08 gate, then movement evidence and justified protections. Cosmetic
crouch/balance work must not obscure unfinished enforcement/recovery validation.
