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
