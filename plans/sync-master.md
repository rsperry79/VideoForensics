# Sync master plan: throttling, seven-day windows, unavailable events, backfill queue

Status: proposed (not started). Supersedes the order in the three plans below; they remain the detailed unit specs.
- `plans/unavailable-events.md`
- `plans/backfill-queue.md`
- `plans/seven-day-rule.md`

## Why
Ring is rate-limiting the server. The trace found the amplifiers: pulls keep running after a throttle, the ban is never checked, snapshots are not budgeted, and long requests walk many windows that each hit every device. Events that have no media are also dropped, and nothing revisits them. The work below fixes the load first, then the data gaps.

## Order and dependencies

### A. Stop hammering Ring (first; everything after adds calls)
1. **Ban gate.** Before any event pull, snapshot pull or download for an account, check `RingMediaDownloadService.GetRateLimitBanUntilUtc` (today unused outside the download service). While a ban is active, skip the call and log the reason once per run.
2. **Stop the account pull on the first throttle.** `EventPullService` (per-device catch ~236–247), `AccountSyncRunner.PullWeeklyAsync` (~441–460) and `ScheduledSyncService.PullCatchUpAsync` (~645–670) keep going after a throttle. Make each stop at the first throttle, record it once, and leave the watermark where it was.
3. **Budget covers real calls.** Count one unit per Ring call (history and per-device), not one per pull. The scheduled snapshot path (`ScheduledSyncService.cs` ~440–470) and `SnapshotCaptureService` must consult the same guard.
4. **Find the double pull.** Confirm the trigger for back-to-back pulls (06:36:22 in the log). Needs the account's `SyncSchedule` row and `ProviderSyncRun` rows. Fix after the trigger is known.
- Tests first: a throttled device stops the account pull; a ban active skips the pull and logs once; a snapshot call is refused when the budget is spent; two pulls for one account do not run in the same window.

### B. Seven-day windows (after A)
5. Requests of seven days or less: one pull.
6. Requests longer than seven days: `WeeklyWindows`, oldest first, the same as the year backfill. This changes Sync Now's 30-day default (now clamped) to five windows.
7. Log each window.
- Tests first: a 5-day request is one pull; a 30-day request is five windows, oldest first.

### C. Store events without media (after the pull changes)
8. Store skipped events (no string ding id) as `Events` rows with `DownloadStatus` `NotAttempted`. (`unavailable-events.md` unit 1.)
9. Mark failed downloads `DownloadFailed` with `DownloadFailedAtUtc`. (unit 2.)

### D. Show unavailable events (after C)
10. Review timeline: unavailable item, not playable, localized. (unit 3.)
11. Review event detail: status and failure time, no player. (unit 4.)

### E. Backfill queue, pull gate, batch download (after C)
12. Queue as data: `DownloadAttempts`, `NextAttemptAtUtc`, migration. Terminal at 3 attempts. (`backfill-queue.md` 1–2.)
13. Batch pass grouped by device and window, reusing `DownloadVideosAsync`, under the ban gate from A. (3.)
14. Pull gate per account, with a reason shown in Sync Now and the sync log. (4–5.)

## Open items (decide before the unit that needs them)
- **Double-pull trigger** (A4): need the `SyncSchedule` row and recent `ProviderSyncRun` rows, or the log lines before each burst.
- **Retry cap** (C, E): 3 passes proposed; confirm.
- **Terminal state** (E): new enum value, or `DownloadFailed` with the attempt count?
- **Range choices in Manual Sync** (B): keep All and custom and show the window count, or remove them?
- **Windows vs throttle** (B, A): a 365-day request is 52 windows. Confirm A ships before B.
- **Snapshots during a ban** (A1, A3): pause snapshot capture while a ban is active, or only budget it?

## Done when
- A ban or throttle stops Ring calls for that account until it clears.
- Each request makes no more Ring calls than the windows it covers.
- Events without media are stored, shown as unavailable, and queued for retry; no event pull runs for an account until its queue has cleared.

## Decisions (answered)
- Unmatched events: row keyed by doorbot ID, linked to the ding-ID row when it arrives.
- Retry cap: 3 passes, then unavailable.
- Final state: new Unavailable value on the download status enum, separate from DownloadFailed.
- Manual Sync range choices: keep All, Custom and the day choices; show the window count before a run.
