# Tracked gameplay / validation follow-ups

Incident closeout: bounded match-scoped occluded-aim capture and Theater review/notes
implemented at1c7f5b7.337 pure/118 native/60 lifecycle/22 server-fixture +29 incident
integration assertions pass; costs measured with16-actor fixtures, not live load.
No AC09 live pass supplied. Next acceptance is AimIncidentRuntime.md (next unused
AC09 at inspection): real exterior-corner clean behavior/map shots, saved fixture
review, clean second client and F7/rejoin. Full-match replay/ML/new detectors deferred.

AC09 update: **AC08 checklist passed per user aggregate report**, no supplied
measurements/per-case logs or optional breakdown. This supersedes AC08 pending labels
below, not broad latency/macOS. New bounded occluded-aim observation and enclosed-eye
shot rejection implemented; AC09 interactive gate in OccludedAimRuntime.md pending.
Ranked anti-cheat gaps/current prevention-versus-observation table in AntiCheatAudit.md.

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
| Recovery / second-client isolation | AC08 checklist passed per user aggregate report; no individual logs. New AC09 aim RPC/shot-guard regression and F7 recovery/rejoin/clean-client isolation still pending. |
| Movement characterization | AC08 checklist passed per user aggregate report; no measurements or optional breakdown invented. Report-only freshness/support limits unchanged. Slow/silent flight, missing contacts and broader latency characterization remain gaps. |
| Occluded aim acceptance | Conservative same-static-box correlation implemented, alwaysreport-only; synthetic positive is not human evidence. AC09 clean doorway/partial/cue/lifecycle, actual remote telemetry, map HP and live performance pending. |
| Wider platforms / networking | Windows preferred. macOS, broad latency/stress/release runtime remain pending. Touch Bar F-key alternatives optional. |
| Bots / secure resume / full match replay | Deferred dependencies documented in CombinedIntegration.md; no parallel local Health/round/auth systems. Theater currently presents local movement only. |

Prioritize AC09 runtime/capstone evidence and adverse-network characterization.
Cosmetic crouch/balance remain deferred; no broad architecture/ML/aim punishment.
