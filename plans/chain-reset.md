# Chain of custody reset: revised plan

Status: draft for review. Not started except where marked.

## Goal

Re-pull provider event data from the provider and re-run initial sync, without touching the action log, evidence media, cases, legal holds, or audit rows. The historical hash-chain break stays on record; it is handled in a separate change.

## Decisions (answered by the user)

1. Scope: events only. Devices and locations are not reset.
2. Events referenced by cases are preserved and updated in place by the re-pull (`EventRepository.UpsertAsync` matches on `(DeviceId, ProviderEventId)` and keeps the existing `Id`). They are not deleted. Legal holds reference media, not events, so they add no exclusion.
3. The historical chain break is archived in a separate PR, after the reset. This plan does not add chain segments.
4. `ProviderReconciliationRecords` and `ProviderSyncRuns` are kept.

Defaults taken from the review (not yet confirmed by the user): accept losing data older than one year as long as the backup is kept; admin-only with a typed confirmation.

## Facts the plan relies on (from review)

- `ActionLogRepository.AppendAsync` (`src/data/database/src/Repositories/ActionLogRepository.cs`) reads the chain tail and inserts inside a plain transaction, with no lock. Concurrent appends can fork the chain. It also lacks the strictly-later timestamp bump that `UnitOfWork.AppendAsync` has.
- `CaseItems.EventId` to `Events` is RESTRICT. Deleting a referenced event fails.
- `CaseDevices.DeviceId` and `SnapshotCaptures.DeviceId` to `Devices` are RESTRICT.
- `ProviderAccountLocations` cascades from `Locations`.
- `SnapshotCaptures.DeviceHealthId` to `DeviceHealth` is SET NULL.
- `ReportGenerationService` resolves `MediaItem.DownloadEventId` to `DownloadEvent.ProviderEventId`.
- `InitialSyncCompleted=false` is set only on account creation (`AccountEndpoints.cs:200`). Manual sync and `InitialSyncResumer` both pick up accounts with the flag false.
- Full history is capped at one year back (`AccountSyncRunner.cs:292, 305, 376`).

Unverified (must be checked in the relevant phase): how the event pull assigns IDs; whether re-validated media collides with the unique index on `(DeviceId, RecordedAtUtc)` (`MediaItemConfiguration.cs:51`); whether legal holds reference events directly.

## Phase 0: fix the standalone append race

Goal: concurrent `ActionLogRepository.AppendAsync` calls cannot fork the chain.

- Serialise the tail read, insert, and commit with one process-wide lock (a static `SemaphoreSlim`). The lock must cover the commit: SQLite defers the write lock, so releasing it after the read lets a second transaction read a stale tail.
- Apply the same strictly-later timestamp rule as `UnitOfWork.AppendAsync`.
- Tests (database project, `ActionLogRepositoryTests`): concurrent appends produce a valid chain; a timestamp tie still verifies.
- Gap closed: one shared gate (`ActionLogAppendGate`) is now held by `UnitOfWork.ExecuteAsync` for the whole transaction and by the standalone append. Cost: unit-of-work writes in the process run one at a time. SQLite already allows one writer, so the cost is mainly waiting behind long units (pulls). Re-entry within one async flow is allowed, so nothing waits on itself.
- Limit found: a standalone append made from inside an open unit of work cannot write on a file database. EF opens SQLite transactions with an immediate write lock, so the inner append gets `database is locked` after the busy timeout. The gate cannot fix this. Callers must use the unit's `ActionLog` inside `ExecuteAsync`. Production code was not checked for nesting yet; check it before relying on that.
- Gate: lite gate on the database tests. Expected: all existing chain tests stay green.
- Status (done): gate and timestamp bump added. The concurrency tests (`ActionLogAppendConcurrencyTests`, pooled file database, 20 parallel appends; `ActionLogRepositoryTests`, 10 parallel appends on the shared fixture) passed on the code before the gate as well. The race did not reproduce in any run, including 200 appends. The gate is defensive; it is not shown to fix an observed fork.

## Phase 1: stop and back up

Before any clear:

- Stop `ScheduledSyncService`, `InitialSyncResumer`, and any running sync for all accounts. Confirm none is running.
- Take a backup with the SQLite backup API (not a file copy of a live database). Record the SHA-256 of the backup file.
- Append an action-log entry recording the reset request, operator, and backup hash, before any data is cleared.
- Restore procedure: documented and tested against a copy of the backup. Restore must verify the hash first.

## Phase 2: reset provider event data

Remap before clearing: record `(MediaItemId, DeviceId, ProviderEventId)` for every `MediaItem` with a `DownloadEventId`. After the re-pull, set `MediaItem.DownloadEventId` to the new `DownloadEvent.Id`. Without this, report links to media are lost (`ReportGenerationService.cs:91-103`). `MediaItem.DownloadEventId` has no FK, so nothing blocks the delete. `AiAnalysisSnapshots` (keyed on `DownloadEventId`, cascading to tags and motion zones) gets the same remap or is cleared; decide in review.

Clear, in one transaction. FK order is unverified for the `Event*` child tables; check the configurations before writing the delete:

- `EventDetection*`, `EventSecurityAlerts`, `EventDetectedPersons`, `Event` rows, except events referenced by `CaseItems` or legal holds.
- `DownloadEvents`.
- Ring provider tables (`Ring*`) that hold event data.
- `DeviceHealth`, `DeviceConfigSnapshots`, `DeviceSettingChanges`.

Do not clear: `ActionLogEntries`, `SecurityAuditLogEntries`, `ProviderReconciliationRecords`, `ProviderSyncRuns`, `ExportRecords`, `ExportAuditRecords`, `LegalHolds`, `Cases`, `CaseItems`, `CaseDevices`, `Credentials`, operators, paired devices, notices, `MediaItems`, `SnapshotCaptures`, `MediaStillCaptures`, `Devices`, `Locations`, `ProviderAccountLocations`.

Then:

- Reset `ProviderAccount.InitialSyncCompleted` to false and clear `SyncSchedule` backfill cursors.
- Start initial sync through the existing coordinator.

Re-linking: preserved events are updated in place, so their `Id` and case links are unchanged. Re-pulled events that were cleared get new `Id`s (`Guid.NewGuid()` at `RingMediaDownloadService.cs:812, 1252`); media and report links are restored by the `DownloadEventId` remap above, not by event `Id`. Events the provider no longer returns stay as they are; list any case-referenced ones in Phase 3.

## Phase 3: verification

- Compare re-pulled event counts per device with the backup.
- Confirm preserved case-referenced events still resolve from case items.
- Run the chain verifier: it should report the same historical break as before the reset, plus the reset entry, with no new break.
- No `MediaItem.DownloadEventId` dangles after the remap.
- List case-referenced events the provider did not return.
- Dry-run counts report before the clear, so the operator sees what will be removed.

## Authorisation

- Admin only. Typed confirmation (the reset name typed in full).
- Step-up: reuse `StepUpEndpointFilter` (`src/client/web/VideoForensics.WebApp/src/Auth/StepUpEndpointFilter.cs:15-60`), already applied to `BackupEndpoints.cs:89, 147`. No new mechanism.

## Gates

- Each phase: lite gate on touched projects (`dotnet build`, `dotnet test --filter`).
- Full gate only with the user's confirmation.

## Open questions for review

- Is the Phase 0 lock approach sound for SQLite under the app's DbContext factory?
- Is the re-link by `ProviderEventId` enough, or does `MediaItems` need the same treatment?
- Is step-up authentication available, or is it a new mechanism?
