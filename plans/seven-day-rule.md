# Seven-day windows for every sync method

Status: proposed (not started)

## Decision (from the user, revised)
- A request of seven days or less is one pull.
- A request longer than seven days is split into seven-day windows, oldest first, the same way the year backfill already works.

## Current state
- Full history (`AccountSyncRunner.cs` ~342) and custom ranges (~348–356) already walk `WeeklyWindows` oldest first. These match the rule.
- Any other range, including the 30-day default from Sync Now, is clamped by `OneWeekFloor` (~360, 399). It is cut to seven days instead of split. This does not match the rule.
- Scheduler backfill (`ScheduledSyncService.cs` ~575–590) pulls one window per tick and advances `BackfillCursorUtc`. It matches the rule.
- Scheduler catch-up (`ScheduledSyncService.cs` ~645–670, `PullCatchUpAsync`) walks weekly windows oldest first. It matches the rule.

## Units
1. **Sync Now non-full ranges longer than seven days.** Replace the `OneWeekFloor` clamp with `WeeklyWindows(from, to)`, oldest first. The 30-day default then runs five windows. Test first: a 30-day request yields five windows, oldest first, each at most seven days.
2. **Requests of seven days or less stay one pull.** Test first: a 5-day request yields one pull.
3. **Log each window.** Log the window count and each window's start, so a long request is visible in the sync log.

## Open items
- Each window is one more set of Ring calls. A 365-day request is 52 windows. Confirm this is acceptable, or require the stop-on-throttle change (earlier trace) to ship first.

## Done when
- A request longer than seven days runs in seven-day windows, oldest first.
- A request of seven days or less runs as one pull.
