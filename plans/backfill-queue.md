# Backfill queue for unavailable events

## Context

Building on the unavailable-events plan (`docs/plans/unavailable-events.md`): events with no downloadable media are stored as `Events` rows with `DownloadStatus` `NotAttempted` or `DownloadFailed`. Today nothing revisits them. The next pull advances the watermark past them, so they are lost from the download path.

This change adds a database-backed backfill queue. Queued events are retried in batches. While an account has queued events, its event pulls wait. Retries that never succeed end as permanently unavailable, so the queue cannot block pulls forever.

## Decisions (from the user)

- Unmatched events (no string ding id) are retried for a fixed number of passes, then marked unavailable. Proposed: 3 passes. Confirm the number when implementing.
- The pull block applies to the account that has queued events only.
- Manual Sync Now follows the same block. The UI shows that the queue is blocking.
- Batch download groups by device and time window: one history query per device and window, then the queued events from it download in parallel.

## Current state

- `EventPullService` and `AccountSyncRunner` pull by watermark (`ScheduledSyncService.cs` ~533–541 uses `EventLastRunUtc`; `AccountSyncRunner.cs` ~307–360 uses the seven-day and one-year floors). Nothing checks for pending work before a pull.
- `RingMediaDownloadService.DownloadVideosAsync` (~568) already batches per device and window: one `GetHistoryEventsAsync` call, then per-event downloads gated by `SemaphoreSlim(_maxConcurrentFileDownloads)` (~635). It also honours the rate-limit ban and pacing (`MediaRequestPacer`, `GetRateLimitBanUntilUtc`).
- The schedule already keeps `BackfillCursorUtc` (`ScheduledSyncService.cs` ~581) for the year backfill. Reuse that pattern for queue state if it fits.
- Events without a string ding id have no history match, so `DownloadVideosAsync` cannot fetch them. Retrying only helps if the provider later assigns the id. This is why the retry cap exists.

## Approach

1. **Queue as data, not memory.** Queue membership is a query: `Events` where the account's device has no media and `DownloadStatus` is `NotAttempted` or `DownloadFailed`, with a `DownloadAttempts` count and `NextAttemptAtUtc`. Add the two columns (EF migration). This survives restarts, unlike the in-memory `Channel` used for exports.
2. **Attempts cap.** Each failed pass increments `DownloadAttempts`. At the cap (3), set `DownloadStatus` to a terminal unavailable state and stop retrying. Confirm whether this needs a new enum value or reuses `DownloadFailed` with the count.
3. **Batch pass.** A new backfill pass, similar to `MediaPreviewBackfillService`, groups queued events by device and window, then calls the existing `DownloadVideosAsync` for each group. It reuses the rate-limit ban and pacing, so no new throttling.
4. **Pull gate.** Before `EventPullService` or `AccountSyncRunner` pulls for an account, check the queue count for that account. If any event is queued and not terminal, skip the pull and log the reason. Manual Sync Now returns that reason to the UI instead of a silent success.
5. **Visibility.** The Sync page shows the queue depth for the account, and the sync log says a pull was held back.

## Critical files

- `src/client/host/VideoForensics.Hosting/src/Services/EventPullService.cs` (pull gate, watermark)
- `src/client/host/VideoForensics.Hosting/src/Services/AccountSyncRunner.cs` (manual gate, reason to UI)
- `src/client/host/VideoForensics.Hosting/src/BackgroundServices/ScheduledSyncService.cs` (scheduled gate)
- `src/client/host/VideoForensics.Hosting/src/BackgroundServices/MediaPreviewBackfillService.cs` (pattern for the new pass)
- `src/providers/ring/provider/src/Services/RingMediaDownloadService.cs` (`DownloadVideosAsync`, reused as-is)
- `src/data/common/src/Entities/Event.cs` (new `DownloadAttempts`, `NextAttemptAtUtc`)
- Data layer migration for the new columns (`src/data/database`)

## Verification

- Failing tests first, in this order: an event with no ding id is queued; a pull for an account with a queued event is held and logged; a manual Sync Now returns the held reason; a queued event that fails 3 passes becomes terminal and no longer blocks the pull; a batch groups two events from one device and window into one history call.
- Lite gate per the code-workflow skill: build and test the touched projects.
- Run the backfill pass once against a test database with one queued event and one that succeeds on retry.

## Open items

- Confirm the attempt cap (3) and whether terminal unavailable needs its own enum value.
- Confirm that a manual Sync Now should show the held reason and not run a partial pull.
