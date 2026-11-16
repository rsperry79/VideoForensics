# Service interval settings on /settings/services

## Context

Background services ran on hard-coded intervals, and the operator could not change them. Goal: every repeating background interval that an operator should control gets a setting on `/settings/services`.

## Decisions (from the operator)

- Store intervals in minutes (1 to 1440) or seconds (5 to 3600) per setting. One setting per service.
- Defaults match today's behaviour, so no migration: 15 minutes for Jamming, Codec, Flag, device health and offline re-poll; 60 seconds for the scheduled sync tick.
- Flag backfill repeats on its interval (it previously ran once per startup).
- The scheduled sync tick setting replaces the `ScheduledTasks:TickSeconds` appsettings value.
- Operational log prune already uses the existing Log prune setting and needs no new setting.
- Line endings: LF.

## Units

| # | Unit | Status |
|---|------|--------|
| 1 | Config fields, orchestrator setters and WebApp guard for Jamming, Codec, Flag | Done (c2c509d9, merged 2f7c7661) |
| 2 | Jamming, Codec and Flag services read their interval before each wait | Done (c2c509d9) |
| 3 | Flag service repeats on FlagIntervalMinutes | Done (c2c509d9) |
| 4 | Services page inputs for Jamming, Codec and Flag | Done (c2c509d9) |
| 5 | Config fields and setters for device health, offline re-poll and scheduled sync tick | Done (3fc00e75, merged 07f5fd4b) |
| 6 | Device health sync and offline re-poll read their interval before each wait | Done (3fc00e75) |
| 7 | Scheduled sync reads its tick from config; appsettings TickSeconds removed | Done (3fc00e75) |
| 8 | Services page inputs for device health, offline re-poll and scheduled sync tick | Done (3fc00e75) |
| 9 | Operational log prune | No change needed (uses LogPruneIntervalHours) |

## Verification

- Client.Core `ServiceIntervalToolsTests`: 20 passed.
- Hosting tests for the three services, scheduled sync registration and the DB scheduler test: 120 passed.
- Ui.Shared `ServicesSettingsIntervalTests`: 7 passed.
- WebApp builds.

## Open

- Pushed: `wip` at 07f5fd4b on origin.
- Not reviewed by a Sonnet reviewer: the standing review was not run for this plan.
