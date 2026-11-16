# Snapshot thread and true-to-time timeline

Status: planned. Probe not run. No code written.

## Goal

A separate always-running thread per Ring account pulls fresh snapshot images and health data on a schedule and records them. When an event is pulled, build a true-to-time MP4 that uses snapshots to fill the gaps between events.

## Decisions

| Topic | Decision |
|---|---|
| Interval | From the account's schedule setting on the Sync Schedule page. A new per-account snapshot interval, next to the config interval (which is in hours, too coarse). |
| Capture | Fresh capture on each snapshot. Battery cost accepted; the probe measures it first. |
| Privacy | Always capture. Not limited to an alarm mode. |
| Devices | All devices by default. Kill switch per device, stored with the device settings. Device config page comes later. |
| Storage path | `C:\ProgramData\VideoForensics\Snapshots` |
| Free-space guard | None. Capture does not stop on disk space until retention exists. |
| Retention | Deferred. Build a no-op extension point now so a real policy can be added later. Tracked as a TODO. |
| Record per snapshot | Captured time in the DB, link to the media file path, HTTP status of the fetch. Only a 200 counts as a current snapshot. |
| Timeline frame rate | 1 fps. |
| Timeline resolution | Highest resolution among the inputs in that timeline. Clips and stills are re-encoded to match. |
| Timeline timing | Each still holds for its full capture interval (true to time). |
| Outages | Marked as gaps, not held. |
| Restart | Resume from the last captured time. |
| Health | `DeviceHealthSyncService` stays the only health writer. Snapshot rows link to its rows. |
| Audio | Stills have none. Silent audio filler on the timeline. |

## Still open

- **Probe approval.** One device, 60-second polling for one hour, with and without fresh capture. Measures image freshness, whether a real capture time exists, 429 behavior, and battery cost. It decides the interval floor, the fresh-capture default, and the timestamp fields.
- **Relation to `SnapshotRssiIntervalMinutes`.** Keep it separate or fold it in. Needs a decision before step 2.

## Steps

0. **Probe** (needs approval). Read-only polling plus fresh-capture requests on one device. Output decides steps 1–2.
1. **Data model.** Snapshot table: device, captured-at (UTC), fetched-at, capture source (fresh or latest), HTTP status, media path, hash, link to health row. Per-account snapshot interval and per-device kill switch. One migration.
2. **Snapshot service.** One hosted service per account, separate from event sync. Captures on the interval, writes the image and the snapshot row, and links the health row. Uses one per-account request gate shared with video calls, with 429 and Retry-After handling. Ring calls sit behind a new interface in `core/providers/common/src/Contracts`. Does not reuse `DownloadSnapshotsAsync` or `DownloadEvent`.
3. **Background sync jobs.** A job ID and a log buffer on the existing background task. Account Details reads the buffer, so the console reattaches after navigating away.
4. **Timeline builder.** Shared interface in `core/providers/common`, ffmpeg implementation via `ProcessRunner` with an explicit long timeout. Concat demuxer with explicit per-still durations and the last file repeated. Explicit `-vsync cfr` and `-g`. Built and tested with synthetic stills and ffprobe checks before any UI or live capture.
5. **UI.** Snapshot status on the account page. Timeline view marking real clips versus stills. Gap markers for outages. Localized strings.
6. **Retention.** Separate TODO. See the memory note `todo-retention-policy`.

## Testing

- Each step: tests first, confirm they fail, then implement.
- Timeline: ffprobe checks on duration and frame timing.
- Live Ring calls only after approval.

## Sources

- Review: Sonnet review of this plan, 2026-10-17. Adopted changes are listed above.
- Memory: `project-snapshot-timeline`, `todo-retention-policy`, `todo-past-snapshots`.
