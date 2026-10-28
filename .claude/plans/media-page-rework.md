# Plan: media page rework (Frigate-style)

Status (2026-10-28): Step 1 (hide the nav on `/media`, `/` redirects there) done, `ed1030a8`. The active-account side task done, `e0da8bd3`. The preview pipeline and the viewer components (Part 3 of `platform-split-and-viewer.md`) are already built, but no page uses them yet. Step 2 (the review page itself) is not started. Audited against the code on 2026-10-28.

## Goals
1. On `/media`, hide the left nav (done).
2. Rework the media page to resemble Frigate's review UI: top bar (Back, Live, camera select, date range, filter, actions, Timeline/Events/Detail tabs), large player, camera preview strip below the player, and a vertical timeline in the right panel. The review UI replaces the Evidence list.

## Current state (audited 2026-10-28)
- `NavGroups.cs`: the `media` group has one section, "Media" -> `/media`. The left nav is hidden on `/media` (transient flag in `MainLayout.razor.cs`, tests in `MainLayoutAutoCollapseTests`).
- `Pages/Evidence.razor` is `@page "/media"` only; `/` redirects to it (`Pages/Root.razor`). It holds the timeline, grid, gallery, and device-time views and the `MediaViewer` overlay.
- `Components/Evidence/MediaViewer.razor` has a `<video controls>` and a footer with prev/next/close. It is a clip viewer, not a Frigate-style player.
- Built and tested, but used by no page: `Components/Viewer/ReviewPlayer`, `SingleCameraView` (main player plus a strip of muted looping preview tiles; click raises `OnSelect(index)`), `TimelineStrip`, `CameraGrid`, `CameraTile`. Tests in `Ui.Shared/tests/Viewer/ViewerComponentTests.cs`. There is no `/review` page; the only `/review` route is `/review/export`.
- Preview pipeline is done: `MediaItem.PreviewPath` (migration `AddMediaItemPreviewPath`), `MediaPreviewService` (320 px, 1 fps, `.preview.mp4`), generated after download in `EventPullService` and by `MediaPreviewBackfillService`, served by `GET /api/v1/media/{id}/preview` with tests.
- Clip paging exists: `GET /api/v1/media-items/page` (`RemoteMediaItemRepository`). `CameraSync` and `ReviewTimeline` are in Client.Common.

## Step 1: sidebar on /media (done)
- Left nav hidden on `/media` via a transient collapse (the saved preference is not changed; the hamburger flips the pane for the visit; leaving restores the saved state). `/` redirects to `/media`. Commit `ed1030a8`.

## Step 2: the review page (not started)
Replaces the Evidence list (user decision). Route: build it at `/media`; check `NoticeActions.ReviewLink` and bookmarks before moving anything (not verified).
- Top bar: Back, Live, camera selector (`SfDropDownList` multi-select, "All cameras" toggle plus per-camera switches, Apply/Reset), range (Last 24 hours), Filter, Actions, tabs Timeline / Events / Detail.
- Main area: `ReviewPlayer`; under it `SingleCameraView` for the other cameras; `CameraGrid` when more than one camera is on.
- Vertical timeline: `TimelineStrip` in the right panel (`RightPanelHost`).
- Filter: provider, account, and camera filters. Actions: tag evidence, and Start new case on evidence.
- Keep the Review role gate on grab-still (`MediaViewer.razor:141-149`) in the new player.

### Data sources per tab (from the audit)
- **Timeline tab:** clip pages from `/api/v1/media-items/page`.
- **Events tab (every provider event; no stills or time-lapses):**
  - `IEventRepository` with `RemoteEventRepository`; routes `GET /api/v1/events/by-device/{id}?fromUtc&toUtc`, `/by-location/{id}`, and `/api/v1/events?pageNumber&pageSize`.
  - Gap: no multi-device, date-ranged, SQL-paged event query (`data.md` requires server-side paging). Add one with its route and remote client, like `GetPageByDevicesAndDateRangeAsync` for media.
  - Exclude stills and time-lapses by `EventType`; the values were not inspected.
  - Live refresh: `IEvidenceChangedSource.EvidenceChanged` (carries only the device id, so re-query). Not confirmed that it fires on every event write.
- **Detail tab, sub-events ("Person detected" / "Person left"):**
  - Gap: `EventDetection`, `EventDetectedPerson`, and `EventSecurityAlert` have no read path (no repository, route, or DTO); `EventDto` carries only `MetadataJson`. There is no "person left" concept (`EventDetection.PersonDetected` is a boolean). Decide: add a read route and DTO for the child rows, or parse `MetadataJson`.
- **Detail tab, suspected jamming:** `IJammingRepository.ListIncidentsAsync(deviceId?, from?, to?)` (unpaged), route `GET /api/v1/jamming/incidents`, `RemoteJammingRepository`. `JammingIncidentRecord` has start, end, confidence, affected event count, source. No observable.
- **Detail tab, anomalies:** `ISignalAnalysisService.AnalyzeAsync(deviceIds, from, to)` (deviation windows by level), route `GET /api/v1/signal-analysis`, `RemoteSignalAnalysisService`; raw health from `IDeviceHealthRepository.GetHistoryAsync` via `GET /api/v1/devices/{id}/health`. Live: `IDeviceHealthChangedSource` and `INoticeFeed.NewNotices` (deviation notices).
- Existing `IObservable` sources: `IEvidenceChangedSource`, `IDeviceHealthChangedSource`, `INoticeFeed`, `IThumbnailReadySource`, `IDownloadProgressSource`, `ILiveViewSessionSource`, `ISelfTestStatusSource`. The event, jamming, and signal-analysis repositories have none, so refresh them off `IEvidenceChangedSource` and `IDeviceHealthChangedSource`.

### Build batches (Sonnet plan review, 2026-10-28)
The new page lives at `/media/review` until the last batch, so `/media` keeps working. Paths are under `src/client/ui/VideoForensics.Ui.Shared`; tests in `tests/Review/`; tests written first.
- **B1 state and range model (no UI):** `Services/Review/ReviewPageState.cs`, `ReviewUrl.cs`, `ReviewClipBuilder.cs`. One state object (cameras up to `CameraSync.MaxTiles`, range, `Cursor`, active tab, provider and account filters, selected event) with one `OnChange`. The `Cursor` is the single source of truth for player, strip, grid, and tabs. URL query: the existing scope query plus `t` (cursor) and `tab`.
- **B2 shell and top bar:** `Pages/ReviewPage.razor` + `.resx` at `/media/review`, `Components/Review/ReviewTopBar.razor`, `ReviewCameraSelector.razor`; tab switch with placeholder bodies. The camera selector writes `ScopeState`, which `ReviewPageState` reads.
- **B3 Timeline tab:** `ReviewTimelineTab.razor`, `ReviewClipLoader.cs` (pages `GetPageByDevicesAndDateRangeAsync`, skips time-lapses and stills), wiring `ReviewPlayer`, `SingleCameraView`, `CameraGrid`; reloads on `IEvidenceChangedSource` (throttled 750 ms). Riskiest batch.
- **B4 vertical timeline in the right panel:** `TimelineStrip` through `PageRightPanelContent`, driven by the cursor; needs a vertical orientation parameter on `TimelineStrip` (today it takes `WidthPx`).
- **B5 Events tab:** `ReviewEventsTab.razor`, `ReviewEventPager.cs` over `ListPageByDevicesAndDateRangeAsync` with `excludeEventTypes`; click a row to seek.
- **B6 Detail tab and filters:** `ReviewDetailTab.razor`, `ReviewDetailLoader.cs` (`IEventDetailRepository`, `IJammingRepository`, `ISignalAnalysisService`), `ReviewFilterPanel.razor` (provider, account, camera; reuse the `ScopeRail` account filters). Jamming and anomalies have no observable; refresh them off `IEvidenceChangedSource` and `IDeviceHealthChangedSource`.
- **B7a pending tags (before B7b):** `Services/Review/PendingEvidenceTags.cs`, a scoped in-memory collection of tagged items (media item or event ids with device and time) with `OnChange`; tag/untag from the review page; `CaseState` (find where the open case is held and where `PinAsync` lives) attaches the pending items when a case becomes open and clears them; if a case is already open, tagging pins directly. Tests first, including: tag with no case keeps it, opening a case attaches once and clears, tagging with an open case pins immediately, duplicates ignored.
- **B7b actions menu, grab-still, swap:** `ReviewActionsMenu.razor` (Tag evidence, Start new case with the selected clip, legal hold, integrity check, export link); move the Review role gate (`MediaViewer.razor:141-149`) and keyboard map into the player area; take `@page "/media"`, retire `Evidence.razor` and port or delete its tests; update `EventsRedirect.razor` (sends `view=grid`, ignore it) and `CasesPagesTests.cs:838`. `NoticeActions.ReviewLink` targets are `/signal-history`, `/accounts/{id}/sync`, and `/analyze/chain-of-custody`, none `/media`, so the swap breaks no notice links.

- **B8 case "Review selected evidence":** on `CaseDetails`, an option that opens `/media` (the review page after the swap) in a case mode (query `case=<id>`): the Timeline, Events, and Detail tabs show only the case's pinned items (cameras and range derived from them), with the case name in the top bar and a way to leave case mode. Tests first.

### Findings that shape the batches
- Stills and time-lapses are media items, not events. Time-lapse: `MetadataJson` has `{"type":"snapshot-rollup"}` (`SnapshotRollupService.SnapshotRollupMarker`; `EvidenceItem.IsTimeLapse`). Timer stills: `{"type":"snapshot","source":"capture"}`. Ring writes a `DownloadEvent` with `EventType = "snapshot"`: that is the string for `excludeEventTypes`. Wyze strings were not found.
- `IEvidenceChangedSource` fires on `RecordDownloadEventAsync`, `UpsertEventAsync`, snapshot capture, and roll-up; the payload is only the device id. Jamming and signal analysis never fire it.
- The timeline event-band overlay has no data source yet (clips and events come from separate pages); it needs an event list passed down.
- Evidence.razor features: keep legal hold, integrity validation, and a link to export in the Actions menu; drop report generation (only used by `EventRowLoader`), case-pin tone on rows, and the device-time grid and gallery.
- There is no tagging concept for evidence (`AiAnalysisTag` is AI output). "Start new case" only takes the scope today; pre-filling the selected clip needs a new hand-off.

## Step 3: remaining items
- Verify WebView2 range playback on the MAUI Windows head (manual; no test or note exists in code). Server-side range tests exist (`MediaContentRangeTests`).
- The preview pipeline is done (see Current state), so there is no separate server work left for the preview strip.

## Decisions (user, 2026-10-28)
- The Evidence list/timeline page is replaced by the review UI (no list tab).
- Events tab: every provider event; no stills, no time-lapses.
- Detail tab: each event expanded to show its provider sub-events and suspected jamming or anomalies for that window.
- Actions: a tag action for evidence, and "Start new case" on evidence.
- Filters: provider, account, and camera.
- Vertical timeline: in the right panel.
- Tag evidence (user, 2026-10-28): tagging pins the item to the open case. When no case is open it is kept in memory (a pending-tags collection for the session). When the user opens a case, the pending items are attached to it. Cases get a "Review selected evidence" option that opens the review page on the case's pinned items. In-memory means the pending tags are lost if the session or circuit restarts; say so in the UI help text.
- Left nav hidden on `/media`; `/` redirects to `/media`.

## Side task: remove the active account (done)
- Removed the right-panel account switcher, `AccountSelectionState`, `IForensicsConfiguration.ActiveProviderAccountId`, the config DTO field, and the `AppSettings` column (migration `RemoveActiveProviderAccountId`). `e0da8bd3`. Breaking for the config API DTO.
- Downloads now run through account sync, which passes the account explicitly. Details, commits, and remaining work are in `one-off-account-download-jobs.md`.

## Rules to follow
- `ui-shared.md`: every new string in `SharedResources` or the page `.resx`, including top-bar labels (Back, Live, Filter, Actions, tab names, range text). Desktop-first; `MobileLayout` handles narrow screens.
- `client-live-data.md`: new data on the page comes from an `IObservable<T>` source, subscribed in `OnInitializedAsync`, disposed in `Dispose`. Refresh through `InvokeAsync`. Name the contract for each source above before building.
- `client-split.md`: Ui.Shared uses `IMediaContentUrlProvider` and the API, not local disk; new routes need DTOs in `Api.Contracts` and `Remote*` classes.
- `data.md`: filtering, sorting, and paging in SQL; typed columns, not JSON.
- `csharp.md`: CancellationToken on async methods; log failures with context via the error message and exception type, not stack traces.

## Open
1. Sub-events for the Detail tab: new read route and DTO for the detection child rows, or parse `MetadataJson`?
2. `EventType` values that identify stills and time-lapses (to build the Events filter).
3. Whether `IEvidenceChangedSource` fires on every event write, or the Events tab needs its own change source.
4. Route and bookmarks: confirm `NoticeActions.ReviewLink` targets before moving or renaming `/media`.
5. Known unrelated failing tests on `wip` (other sessions' changes): eight `AccountDetails` Remove and Purge tests, and `CaseDetailsExportEntryTests`. See `one-off-account-download-jobs.md`.
