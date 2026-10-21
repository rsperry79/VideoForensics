# Snapshot loop with 15-minute MP4 roll-up

Status: step 1 done (scheduled snapshot pull). Steps 2-5 open.

## Decisions (user)

- Snapshots are written on the schedule's snapshot interval (default 1 min, set in seconds or minutes).
- Originals (JPEGs) are kept after roll-up. Nothing is deleted.
- The roll-up interval is per account, in minutes, default 15, on the account's schedule.

## Steps

1. DONE: scheduled sync pulls snapshots when `IsSnapshotDue`, then records the next snapshot run.
   Covered by `ScheduledSyncServiceTests` (due and not due). Snapshot rows land in Media through the existing `SnapshotSyncService` path.
2. Roll-up setting: add `SyncSchedule.SnapshotRollupIntervalMinutes` (default 15, minimum 1) and `LastRollupUtc`.
   Migration with default 15. Repository validation. DTO field. Schedule-page input (minutes only).
3. Roll-up job in `ScheduledSyncService`: when `now >= LastRollupUtc + interval`, collect the account's snapshot
   MediaItems since `LastRollupUtc`, sorted by RecordedAtUtc. Convert them to one MP4 with ffmpeg
   (`FfmpegPathResolver` already exists), using each frame's real duration. Write a MediaItem with MediaFormat "mp4".
   Keep the JPEGs. Record `LastRollupUtc`. If the roll-up fails, keep the JPEGs and retry next tick. Do not advance LastRollupUtc.
4. "Show only the last": the account's Devices tab shows the latest snapshot per device, plus the latest MP4 loop.
   Older snapshots stay in the database and on disk, but the UI does not list them.
5. "Add a new one as a loop": after a roll-up, the next snapshots start a new loop, which the next roll-up picks up.

## Open risks

- ffmpeg must be present on the host; a missing binary must fail the roll-up with a clear log line, not crash the tick.
- Ring snapshot pulls are rate-limited; the 1 s floor on the snapshot interval needs a rate-limit review before production.
- Snapshot Media rows are only written when a SHA-256 hash is computed. Failures there are logged only (see the earlier snapshot DB finding).
