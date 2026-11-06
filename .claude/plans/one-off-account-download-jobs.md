# Downloads through sync (was: one-off account download jobs)

Status (2026-10-28): done; the remaining items are optional or belong to other work (see TODO). Parent plan: `media-page-rework.md` (side task: remove the active account, finished).

## Decision
Downloads run through each source's existing account sync, not a separate job queue.
- A manual pull is the account's "Sync now" (`POST /provider-accounts/{id}/sync-now`, `AccountEndpoints.cs`). Types: All, Events, Snapshots. Ranges: last N days (max 3650), custom, all history.
- The schedule is the existing per-account `SyncSchedule`, run by `ScheduledSyncService`.
- Files go to the existing Media storage location (`StorageLocationProvider`, Media category). No new folder setting.
- No separate Collect pages.
- A video pull downloads the whole window per device (`EventPullService` calls `DownloadVideosAsync` per device), so "every clip in range" is how sync already works. Whether the provider returns every clip or only event clips is not verified (TODO 4).
- A custom range is split into batches of at most 7 days, oldest first. The one-week limit is per batch, not per request.

## What happened (commits on `wip`)
- A download job queue (entity, repository, runner, executor, `/api/v1/download-jobs`) was built, then removed once sync was found to cover the work: `59e9097b`. Migrations `AddDownloadJobs` and `AddDownloadJobOutputPath` are kept (they may be applied); `RemoveDownloadJobs` drops the table.
- Custom sync ranges batched in 7-day windows, clamped to one year back: `98165784`. Tests: `AccountSyncRunnerCustomRangeTests`.
- The Sync tab progress now comes only from `ISyncActivityLog`, with no dependency on the download service: `3762b52e`.
- The manual path removed: Collect pages, `/api/v1/downloads` routes, `VideoDownloadServiceAdapter`, `RemoteVideoDownloadService`, `IVideoDownloadService`: `5528f11d`. A guard test fails if the old `/collect` routes return.
- The active account removed: right-panel switcher, `AccountSelectionState`, `IForensicsConfiguration.ActiveProviderAccountId`, the config DTO field, and the `AppSettings` column (migration `RemoveActiveProviderAccountId`): `e0da8bd3`. Breaking for the config API DTO. `SetActiveProviderAccountId` on the media download services stays: it is per-run account context set by sync from an explicit id.

## Done since the first version (commits on `wip`)
- Live per-file progress on the Sync tab: a singleton `ISyncProgressStore`, polled from `GetStatus()` by `EventPullService` while each device downloads, read by `DownloadProgressDtoMapping`, `LocalDownloadProgressSource`, `DownloadProgressBroadcastService`, and `LiveHub`: `bfa8ca8f`. DTO shapes and hub contracts unchanged. Known limit: parallel pulls share one slot, so a finishing pull blanks the display until the other pull's next reading (about 500 ms at most).
- Scheduled catch-up after downtime is split into windows of at most 7 days (start clamped to one year): `a5f19788`. If one window fails the schedule still advances to now, so that week is skipped; this matches the single-pull failure path.
- Sync now range limit lowered to 366 days (days and custom): `a5f19788`.
- Sync writes under the Media folder set on the Storage Settings page (`IForensicsConfiguration.DownloadLocation`), resolved at use time, for `EventPullService`, `SnapshotCaptureService`, and `SnapshotRollupService`. `MediaStorageMigrator` (it moved files out of `DownloadLocation` at every startup) is retired: `19490230`. Installs with a leftover legacy folder keep using it as their media root.
- Verified: Ring has only event-attached recordings, so sync already downloads every clip Ring stores in a window; adjacent windows are idempotent by path.
- End-to-end characterization tests of a full sync against fake providers (`AccountSyncEndToEndTests`): windows in order, folders under the media root, `LastDownloadTimeUtc` not moved, progress store idle after, one failing device does not stop the rest.
- Cleanup: removed the unused `SettingServerOnly` resource key; reworded the Ring comments that named the deleted adapter.

## TODO (remaining)
1. **Unrelated failing tests (other sessions), left alone on request.** Eight `AccountDetails` Remove and Purge tests in Ui.Shared do not register `ISyncScheduleRepository` (added by `f2f4c5f0`); `CaseDetailsExportEntryTests.CaseDetails_ExportCaseButton_NavigatesToExportPageWithCaseId` does not register `RightPanelContentService` (added by `004c9803`).
2. **Failed catch-up window keeps the gap skipped.** Decide whether the scheduled sync should hold `EventLastRunUtc` back when a window fails, so the next run retries it.
3. **Progress slot per pull.** Key the progress store by pull or device if parallel pulls become common.
4. **Verify off Windows.** The Media folder on a non-Windows host: `GetEffectiveRoot` with the configured value is used by the sync writers now, but no run on Linux was done.
5. **Optional data cleanup.** Existing databases keep an unused `ActiveProviderAccountId` row in the settings key/value table; browsers keep an unused `vf.account.selected` localStorage key.
6. **Snapshot sync custom ranges** are not batched (snapshots are a current fetch per device, so no change was made).

## Rules that applied
- Client/server split (`client-split.md`), localization (`ui-shared.md`), tests first with scoped runs (`tests.md`), no stack traces in logs (`csharp.md`).
- Work happens in a worktree, merged to `wip` after a lite gate (`code-workflow`).
