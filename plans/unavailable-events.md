# Store and show unavailable events

Status: proposed (not started)

## Decisions (from the user)
- Store every provider event in a pull window that has no downloaded media. This covers events skipped for having no string ding id and events whose media download failed.
- Show them in the Review timeline, marked unavailable and not playable.
- Show them in the Review event detail view only.

## Current state (what the code does now)
- `EventPullService` fetches events per device and only counts them. It writes nothing.
- `RingMediaDownloadService` upserts an `Events` row at discovery, but skips events with no string ding id (`RingMediaDownloadService.cs` ~1000). Those are never stored.
- `Event` already has `DownloadStatus` (`NotAttempted`, `DownloadFailed`, downloaded) and `DownloadFailedAtUtc`, so the failure state can be recorded without a schema change. Confirm before relying on it.

## Units
1. **Store skipped events.** For events with no string ding id, write an `Events` row keyed by the provider's own event id, with `DownloadStatus = NotAttempted` and no media. Test first: a discovery test with a no-ding-id event expects one row.
2. **Mark failed downloads.** Set `DownloadStatus = DownloadFailed` and `DownloadFailedAtUtc` on the row when the media download fails. Test first.
3. **Unavailable in the Review timeline.** The timeline feeder maps `Event` rows with no playable media to an "unavailable" item. Render it as not playable. Localize all text (`rules/ui-shared.md`). Test first with a bUnit render.
4. **Unavailable in the Review event detail.** Show the status and failure time, with no player. Test first.

## Open items
- Confirm `Events` rows written from the provider path reach the Review timeline on the remote (MAUI) host through `RemoteEventRepository`.
- Confirm the status field is set for every failure path, not just the download call.

## Done when
- A pull over a window with a no-media event stores one `Events` row for it.
- The Review timeline shows that row as unavailable.
- The Review event detail shows its status and no player.
