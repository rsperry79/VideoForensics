# Design: one-off account download jobs

Status: draft for review. Parent plan: `media-page-rework.md` (side task: remove the active account).

## Problem
Manual downloads read a stored "active account" (`IForensicsConfiguration.ActiveProviderAccountId`) to decide which provider account they belong to. That field is a leftover from the account switcher, which is being removed. Scheduled work already carries its account explicitly. Manual work should too, so the stored field can go.

## Goals
1. Every download, scheduled or manual, runs as a job bound to one provider account.
2. Manual download requests name the account. Nothing reads a stored "active" account.
3. Progress and results remain visible in the Evidence and Accounts UI.
4. Client/server split holds: jobs and their state live on the server; clients call versioned `/api/v1` routes with paired-device tokens.

## Non-goals
- Changing the download engine itself (`IMediaDownloadService`, Ring/Uniview implementations).
- Changing the recurring `SyncSchedule` model. It already works per account.
- Removing `ActiveProviderAccountId` from Ring auth (`AuthForm`, `AddAccountWizard`). Those set it as a post-login hint and are removed in phase 4 once nothing reads it.

## Current state (from mapping)
- Manual path: `VideoDownloadServiceAdapter.DownloadVideosAsync` (`Client.Core/src/Services/VideoDownloadServiceAdapter.cs:415`) reads the stored account at `:441`. It calls `SetActiveProviderAccountId` at `:462` and updates the watermark at `:647-651`. `DownloadSnapshotsAsync` reads it at `:696`.
- Progress: polled from the adapter (`GetProgress`, `DrainActivityLog`, `GetRemainingCount`, `GetCurrentDevice`, `:832-933`).
- Scheduled path: `ScheduledSyncService` (`Hosting/src/BackgroundServices/ScheduledSyncService.cs`) runs `RunSnapshotSyncAsync` and `RunEventSyncAsync` per `SyncSchedule`. Each run passes its own account.
- Remote: `RemoteVideoDownloadService` (`Hosting/src/Remote/`) posts manual downloads to the server.

## Design

### Job model (server)
New entity `DownloadJob` in `data/common`:
- `Id` (Guid), `ProviderAccountId` (Guid, required, FK), `Kind` (`Videos` | `Snapshots`), `StartDateUtc`, `EndDateUtc`, `Force` (bool).
- `State`: `Queued` | `Running` | `Completed` | `Failed` | `Cancelled`. Stored as text, enum-mapped.
- `CreatedUtc`, `StartedUtc?`, `FinishedUtc?`, `ErrorMessage?` (no stack traces; see `csharp.md`).
- Counters: `FilesDownloaded`, `FilesSkipped`, `FilesFailed` (ints).
- `RequestedBy` (operator id) for audit.

Repository: `IDownloadJobRepository` with `EnqueueAsync`, `GetAsync`, `ListForAccountAsync(accountId, take)` (SQL paging, take 1-500), `ClaimNextQueuedAsync` (atomic state change to `Running`), `UpdateProgressAsync`, `CompleteAsync`.

### Runner
- A `DownloadJobRunner` hosted service in `Hosting`, next to `ScheduledSyncService`. It polls `ClaimNextQueuedAsync` every few seconds and runs one job at a time per account. Concurrency across accounts is capped (start with 1, configurable).
- Each job runs the same download code the adapter uses today, with the account from the job row. This replaces `SetActiveProviderAccountId` on the manual path. The scheduled path keeps its own call.
- Watermark: updated only when the job completes with `FilesDownloaded > 0` for `Videos` kind, using the job's `ProviderAccountId`. This matches today's rule at `:647-651`.
- Crash recovery: on startup, jobs left in `Running` are set back to `Queued`, so they resume. Downloads are idempotent per file (existing skip logic), so re-runs are safe.

### API (`/api/v1/download-jobs`)
- `POST /download-jobs` body: `{ accountId, kind, startDateUtc, endDateUtc, force }`. Validates the account exists and is linked, dates are ordered, and the range is bounded. Returns `202` with the job id.
- `GET /download-jobs/{id}` returns the state and counters.
- `GET /download-jobs?accountId=&take=` lists recent jobs for an account.
- `POST /download-jobs/{id}/cancel` sets `Cancelled` for `Queued` jobs, and sets a cancel flag for `Running` ones that the runner checks between files.
- DTOs in `Api.Contracts`. Mapping by `ToDto()` / `ToDomain()` only.
- Rate limit: reuse the existing per-tile media limiter pattern. Add a separate `downloads` bucket.

### Client
- `IVideoDownloadService` keeps its shape for the UI. `RemoteVideoDownloadService` implements it by posting a job and polling `GET /download-jobs/{id}`. `GetProgress` and the other progress calls read the latest polled state.
- Interface names do not change (`client-split.md`), so `.razor` files need no edits at cutover.
- The account is a parameter on `DownloadVideosAsync` / `DownloadSnapshotsAsync` on the client side. The UI already knows which account is being downloaded for (the Accounts page). The switcher is gone, so the Accounts page is the only entry point.

### Migration
- Phase 2 adds `DownloadJob` with an EF migration.
- Phase 4 removes `AppSetting.ActiveProviderAccountId` with a second migration, after nothing reads it.

## Phases
1. **Model and tests.** `DownloadJob` entity, repository, and migration. Repository tests for claim, complete, cancel, and restart recovery. Failing tests first.
2. **Runner.** `DownloadJobRunner` in `Hosting`. Tests with a fake download service: one job per account, watermark only on completion, restart recovery, cancel between files.
3. **API and client.** Routes, DTOs, `RemoteVideoDownloadService` polling. Endpoint tests (202, 400 on bad dates, 404 on unknown account).
4. **Remove the stored field.** Drop `ActiveProviderAccountId` from `IForensicsConfiguration`, `AppSetting`, `AuthForm`, `AddAccountWizard`, `AccountDetails`, and selftest `Program.cs`. Second migration. Remove `SetActiveProviderAccountId` from the manual path only; the scheduled path keeps its call.

Each phase ends with a lite gate (`dotnet build` and `dotnet test --filter` on touched projects) and a commit to the worktree branch, then the batch handoff in `code-workflow`.

## Risks
- **Restart mid-download:** resumes from `Queued`; relies on per-file skip logic. Confirm the skip check is by path and hash, not by job id.
- **Progress UI latency:** polling every few seconds. Acceptable for now; SignalR is a later option.
- **Selftest:** `utils/selftest` uses `ActiveProviderAccountId` to restore sessions. Phase 4 needs an account argument there, or a test-only fallback. Decide before phase 4.
- **Ring cached account:** `RingMediaDownloadService` and `RingDeviceIdentityResolver` cache the account through `SetActiveProviderAccountId`. Phase 2 must set it from the job row before each run.

## Open questions
1. One job at a time globally, or one per account? Design assumes one per account, with a global cap of 1 at first.
2. Should a `Snapshots` job and a `Videos` job for the same account run in parallel? Design says no, to avoid double rate-limit use.
3. Retention for finished jobs: keep 90 days, or keep forever for audit?
4. Should cancelled `Running` jobs keep their partial files? Design says yes (idempotent re-run).

## Direction change (user, 2026-10-28): use sync and existing settings
- Downloads run through the source's existing sync, not a separate job queue. A manual pull is the account's existing "Sync now". The schedule is the existing sync schedule setting.
- The output folder is the existing download/media location setting. Do not add a new folder setting.
- No separate Collect pages. Their work moves to the account sync.
- Do not duplicate work: reuse the existing sync, schedule, and location code. Anything the job queue does that sync already does is redundant.
- Open: whether sync must download every clip in range (the earlier decision) or only event clips (what sync does today). The job queue and its runner are candidates for removal once sync covers the work. Not decided.

### Sonnet review (2026-10-28) of the direction change
- Sync already does the work. `AccountSyncRunner` (Sync now, `POST /provider-accounts/{id}/sync-now`) and `ScheduledSyncService` call `EventPullService`, which runs `DownloadVideosAsync` per device for the whole window into `MediaFolderLayout.GetDeviceFolder(<media root>, ...)`. The media root is the existing Media storage setting (`StorageLocationProvider`, which honours the configured override on Windows; the non-Windows branch was not checked). So "every clip in range" is the status quo.
- The schedule is the per-account `SyncSchedule` (`EventPollIntervalMinutes`), gated by `ScheduledTasks` options.
- One gap: a custom range is capped at one week (`OneWeekFloor`, `AccountSyncRunner.cs:313`); only full history backfills in weekly windows. Long custom ranges need the same weekly loop.
- Conflict: sync must never move `LastDownloadTimeUtc` (`AccountSyncRunner.cs:210`). The job executor's watermark write contradicts that.
- Duplicates to remove: `DownloadJob` entity, state and kind, repository, configuration, two migrations (drop the table), `DownloadJobRunner`, `DownloadJobExecutor`, the `/api/v1/download-jobs` routes and DTOs, `OutputPath` and its path check, and their tests. Also the runner registration in WebApp `Program.cs` and the shared hosting extension.
- Collect pages become a sync-now UI with a range picker, or are removed in favour of the account's existing Sync now.

### Batching (user, 2026-10-28)
- The one-week limit is per batch, not per request. A custom sync range can be larger; the sync splits it into batches of at most 7 days, oldest first, using the same weekly loop as the first-sync backfill.
- My choices, to confirm: a custom range is clamped to one year back (same as the full-history backfill) to bound the number of batches; the default case (no explicit start) keeps the one-week floor.
- Work split: step A (batching) is a Haiku subagent in the media-page-rework worktree, tests first. Step B (remove the job queue, with a `RemoveDownloadJobs` migration) follows after A is verified, since both use the same build output.

## Decisions (user, 2026-10-28)
- Video jobs keep the operator's output folder. `DownloadJob` gets `OutputPath`.
- Video jobs download every clip in the range, not only event clips. The executor runs the per-device media download (`IMediaDownloadService.DownloadVideosAsync`) for each device of the account, not the event pull. The event pull stays for event sync.
- Per-device folder: `Path.Combine(OutputPath, <provider device id>)`. A friendlier name is a later change.
- Watermark rule unchanged: update the account's download watermark only when a Videos job completes with files downloaded.

## Phase 3 blockers (found 2026-10-28, before any phase 3 code)
The existing manual API does not match the job model. Decide these before the routes are written:
1. **Output path.** `POST /api/v1/downloads/videos` and `/snapshots` take `OutputPath` (a folder the operator picks), and the adapter writes there. `DownloadJob` has no output path, and the event pull writes under the configured media root. Options: (a) add `OutputPath` to the job and pass it through, or (b) drop it and store under the media root.
2. **What a video job downloads.** The manual adapter downloads every clip in the range since the device watermark. The event pull downloads clips only for events in the range. A video job built on the event pull would skip clips that have no event. Options: (a) accept event-only semantics, or (b) add an all-clips account pull (the adapter's `DownloadVideosAsync` loop, moved behind an account-scoped service).
3. **Progress.** The UI reads progress through `/downloads/progress`, `/status`, `/remaining`, and the SignalR hub, all from the adapter's in-memory state. Job progress needs `GET /download-jobs/{id}` and a client poll. Decide whether the existing progress UI moves to job polling, or stays on the adapter until phase 4.
4. **Coexistence.** `/api/v1/downloads/*` keeps working until the UI moves. Phase 4 cannot remove the stored active account while the adapter still reads it, so phase 4 waits for the UI move.

## Client polling: options (2026-10-28, found before any client code)
`IVideoDownloadService.DownloadVideosAsync(outputPath, start, end, force)` has no account parameter. The Collect pages (`CollectVideos.razor:122`, `CollectSnapshots.razor:105`) call it. Two ways to move them to jobs:
- **A (recommended): additive.** Add `IDownloadJobClient` (queue, get, cancel) with a `Remote` implementation that posts to `/api/v1/download-jobs` and polls. The Collect pages take the account and use this client. `IVideoDownloadService` stays as is for the WebApp's `/downloads` routes until phase 4. Touches 2 pages, 1 new interface, 1 remote class, tests.
- **B: change the signature.** Add an account parameter to `IVideoDownloadService` and its adapter. Touches 18 files, including the server adapter and the hub progress source.

Open: where the Collect pages get the account. Options: (1) a picker on the Collect page, (2) started from the Accounts page for one account, as the design says.

## Tests (summary)
- Repository: claim is atomic, complete and cancel transitions, restart recovery.
- Runner: one job per account, watermark only on completion, cancel between files.
- API: 202 on valid request, 400 on bad dates, 404 on unknown account, list paging bounds.
- Client: `RemoteVideoDownloadService` maps job states to the progress calls.
