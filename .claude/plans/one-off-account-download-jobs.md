# Downloads through sync (was: one-off account download jobs)

Status (2026-10-28): done except the TODO list below. Parent plan: `media-page-rework.md` (side task: remove the active account, finished).

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

## TODO
1. **Progress (live per-file progress for sync downloads).** The Sync tab panel now shows activity lines only. The file-level fields (files completed, bytes, current file, speed, current device) are zero, because sync updates a different `IMediaDownloadService` instance than the one the panel used to read, so they never updated during a sync even before. Build a real source:
   - Candidate: `IMediaPullProgressStore` (core/providers/common), already passed into `EventMediaPuller.PullAsync`. Its lifetime and who populates it are not verified. Alternative: a singleton progress store written by `EventPullService` or `AccountSyncRunner`.
   - Feed it into `LocalDownloadProgressSource` (WebApp), `DownloadProgressBroadcastService` and `LiveHub` (hub), and `DownloadProgressDtoMapping`. Keep the DTO shapes and hub contracts; the panel and `RemoteDownloadProgressSource` should not need changes.
   - Done when the Sync tab bars update during both Sync now and the scheduled sync. Tests first.
2. **Scheduled catch-up batching.** `ScheduledSyncService` (~line 332) still limits an ongoing pull to one week back (`OneWeekFloor`). After downtime longer than a week, the gap is skipped. Batch from the last run in windows of at most 7 days, like the custom range.
3. **Align range limits.** The Sync now API accepts "last N days" up to 3650 (`MaxSyncDays`, `AccountEndpoints.cs:475`), but the runner clamps a custom range to one year back (not confirmed by the user). A 5-year request silently returns one year (noted on the activity log only). Lower the API maximum to 366, or lift the clamp.
4. **Verify provider video semantics.** Does `DownloadVideosAsync` (Ring) return every clip in the window, or only event clips? Check `providers/ring`.
5. **Verify the media root override off Windows.** `StorageLocationProvider.GetDefaultRoot` honors the configured override on Windows (confirmed). The non-Windows branch was not checked.
6. **End-to-end test.** A sync against a fake provider: batches called in order, files land under the media root, watermark (`LastDownloadTimeUtc`) not moved by sync.
7. **Cleanup.** Unused `SettingServerOnly` key in `AccountDetails.resx`. Stale comments in the Ring provider files that name `VideoDownloadServiceAdapter`. Existing databases keep an unused `ActiveProviderAccountId` row in the settings key/value table (optional data cleanup). The browser keeps an unused `vf.account.selected` localStorage key.
8. **Unrelated failing test (another session).** `CaseDetailsExportEntryTests.CaseDetails_ExportCaseButton_NavigatesToExportPageWithCaseId` fails because `CaseDetails` now needs `RightPanelContentService` (commit `004c9803`) and the test does not register it.

## Rules that applied
- Client/server split (`client-split.md`), localization (`ui-shared.md`), tests first with scoped runs (`tests.md`), no stack traces in logs (`csharp.md`).
- Work happens in a worktree, merged to `wip` after a lite gate (`code-workflow`).
