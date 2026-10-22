# Snapshot loop with 15-minute MP4 roll-up

Status: all five steps built and pushed to `wip` (last change `d3d4e18a`, plan commit `0cda778b`). Not yet verified against a live Ring account.

## Decisions (user)

- Snapshots are written on the schedule's snapshot interval (default 1 min, entered in seconds or minutes).
- Originals (JPEGs) are kept after roll-up. Nothing is deleted.
- The roll-up interval is per account, in minutes, default 15, on the account's schedule.
- The roll-up reads snapshot Media rows, not the capture table.

## Steps

1. DONE (`77e4ffab`): scheduled sync pulls snapshots when `IsSnapshotDue`, restoring the session first and recording the next run. Snapshot rows reach Media through `SnapshotSyncService`.
2. DONE (`02373892`): `SyncSchedule.SnapshotRollupIntervalMinutes` (default 15, minimum 1) and `LastRollupUtc`. Migration `AddSyncScheduleSnapshotRollup` adds both (existing rows get 15). Schedule page has a minutes input.
3. DONE (`9f8bd70c`, `8d4f8f12`): `SnapshotRollupService` builds one MP4 per device from the window's snapshot Media rows, using `ITimelineVideoBuilder`. Each frame is held until the next snapshot. `RecordRollupAsync` sets `LastRollupUtc` only when every device succeeds. A failed roll-up waits `RollupRetryBackoff` (5 min) before retrying, per account.
4. DONE (`0d166014`): the Devices tab shows only the newest snapshot and the newest loop per device, through `LatestDeviceMedia`. Roll-up loops carry the `snapshot-rollup` marker in their metadata.
5. DONE (`0612ee5e`, `d3d4e18a`): periodic capture also writes a snapshot Media row (full path, SHA-256, `"type":"snapshot"` marker). Capture spacing follows the schedule's effective snapshot interval. A stored `SnapshotIntervalSeconds` raised by a Ring rate-limit incident remains a floor.

Also done along the way:
- `RingMediaDownloadService` records a snapshot Media row even when the SHA-256 hash fails (`daba18f2`).

## Known gaps

- Nothing has been run against a live Ring account. The end-to-end path (pull or capture, Media row, roll-up, Devices tab) is covered by unit tests only.
- ffmpeg must be present on the host. A missing binary fails the roll-up with a logged error; it does not crash the tick.
- The 1-second snapshot floor needs a Ring rate-limit review before production.
- Retries are throttled to one per 5 minutes per account, but there is no cap on total retries.
- A roll-up window only covers snapshots whose Media row exists. Snapshots dropped before a row was written are not recovered.

## Risks to check on first live run

- Snapshot cadence against Ring's rate limits (the 1 s floor).
- Disk growth: JPEGs are kept, and the MP4 loops are added on top.
- The capture spacing floor: a raised `SnapshotIntervalSeconds` keeps the form from lowering the interval until that value is reset.
