# Plan: media page rework (Frigate-style)

Status (2026-10-28): the review page is built and live at `/media` (also `/media/review`); the old Evidence page is retired. All batches B1 to B8 are done and pushed to `wip` (see "Built"). Nothing has been looked at in a browser yet (every batch was verified by builds and bUnit tests only): do a manual pass first. Open follow-ups are listed under "Follow-ups".

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

## Built (commits on `wip`)
- B1 state, URL, clip model: `d625cb85`. B2 shell and top bar: `ce0109b5`. B3 Timeline tab: `6b125c33` (+ media DTO duration and preview path, `bbf80073`). B4 vertical timeline in the right panel: `6a5f03bc`, B4b page-level feeder so it works on every tab: `5aa3c3e6`. B5 Events tab: `e0939e6d`. B6a Filter panel: `f8ee2211`. B6b Detail tab: `39b9c7e8`. B7a pending tags kept in memory and attached when a case opens: `fb0668cb`. B7b1 Actions menu: `77bc9fd2`. B7b2 grab-still: `bad204bc`. B7b3 `/media` swap, Evidence retired: `459504d3`. B8 case "Review selected evidence": `80801a66`.
- Read paths added for the page: paged multi-device events (`/api/v1/events/page`) and event detail rows (`/api/v1/events/details`): `0da25690`.
- Query parameters: `devices`, `from`, `to`, `q` (scope), `t` (cursor), `tab`, `provider`, `account`, and `reviewcase` (case view). `case` is the app-wide active-case context (`CaseUrl`, "Work on this case") and is NOT the case view.

## Browser pass (2026-10-28, signed-in Edge via Claude in Chrome, build `1.1.742-alpha+7595cfa575`)
Works with real data: tabs, the 16 Motion events, the Detail anomalies list, the vertical timeline with clip segments and the cursor line (it stays on every tab), the Filter panel (provider, account, "3 cameras match"), the Grab still button in single-camera mode, and the Actions menu.
Problems found, to fix:
1. **No CSS for the viewer components.** `.review-player` has no sizing, so the video renders at its natural 1920x1080 and the page scrolls horizontally; `.camera-grid` is never `display:grid` (the inline `grid-template-columns` has nothing to act on), so 2 to 9 cameras stack as full-width tiles; `.camera-tile`, `SingleCameraView` and the preview strip have no layout. Only the vertical `TimelineStrip` has a stylesheet. Add scoped CSS: video `width:100%; max-height` (about 60vh) with `object-fit:contain` on a black background, a real grid, a compact strip.
2. **Popups stay open.** Camera selector, Filter, and Actions do not close on Escape or on an outside click, and several can be open at once (the Actions menu covered the others). Only one popup at a time; close on outside click and Escape. The Actions items look like text inputs, not menu items.
3. **"1 cameras"**: the count label has no singular form.
4. **Stale stylesheet after a deploy.** `VideoForensics.Client.WebApp.styles.css` (the scoped-CSS entry point, not fingerprinted) is served with `Cache-Control: max-age=14400` behind Cloudflare, so the browser kept the old file that imports the OLD scoped bundle: the new strip rendered with no CSS until a cache-bypassing reload. Send `no-cache` (revalidate) for that file in the WebApp's static file options, or add a Cloudflare cache rule.
5. Minor: the cursor line crosses its hour label in the vertical strip.

## Follow-ups
1. **Browser pass (attempted 2026-10-28, not done).** Starting the WebApp from this branch on `http://localhost:5162` failed: that port is already served by another running instance (it answers `/media`; it is an older build, not this branch). Nothing was started on another port, to avoid two instances on the same database (`C:\ProgramData\VideoForensics`, which Windows cannot redirect without a registry change). The failed start ended with an unhandled `InvalidOperationException` ("No process is associated with this object") from `CloudflaredKit.CloudflaredProcess.StopAsync` during host disposal: stopping a tunnel that never started should be a no-op (separate small bug). The log showed no migration SQL (no DROP, ALTER, or history insert), so the database was not changed. Later the same day the installed service was updated to `wip` `7595cfa5` with `deploy/windows/stage-update.ps1 -Build` (applied as `1.1.742-alpha+7595cfa575`, status.json Outcome 1), and `http://localhost:5162/media` now serves this build. It redirects to sign-in, so the visual pass still needs a signed-in browser session (not done: signing in is the operator's step). Layout, popups (camera selector, filter, actions, pending tags), the vertical strip, and the right panel were never viewed. Fixed 600px strip length; filling the panel needs JS measurement.
2. **Cursor does not follow playback.** `ReviewPlayer` and `CameraGrid` take the cursor as input only and report no progress, so the timeline marker does not move while a clip plays.
3. **Orphaned Evidence code.** These have no non-test references outside their folders (some may be used inside their folder or by tests): `DeviceTimeBucket`, `DeviceTimeCellClickArgs`, `DeviceTimeGrid`, `DeviceTimeGridResult`, `DeviceTimeGridView`, `DeviceTimeRow`, `EvidenceDayGroup`, `EvidenceDeviceGroup`, `EvidenceInspectorMapper`, `EvidenceItemCounts`, `EvidenceKind`, `EvidenceLoadResult`, `EvidenceLoader`, `EvidenceTimeline`, `EvidenceTimelineStyle`, `EvidenceView`, `EvidenceViewModel`, `ImageViewport`, `RssiPlot`, `RssiSample`, `SnapshotStrip`, `SnapshotWindowFallback`, `SnapshotWindowThinning`, `TimeLapseCoverage` (plus `MediaViewer`, `MediaThumb`, `MediaViewerKeys`, `MediaViewerAction`, `MediaStillCaptureStatus`, `EvidenceItem`, which are kept). Remove what is truly dead together with its tests. `Pages/EvidenceDatesFilter.razor` (`/media/dates-filter`) has no inbound link; decide keep or remove.
4. **Test gaps.** Timeline tab live refresh on evidence changes, grab-still signed-out and service failure, and the old "view details" behaviour have no named review test. Several throttle tests use real 750 ms waits (`Thread.Sleep`/`Task.Delay`): possible flakes.
5. **Known behaviours.** Start new case with a case already open pins to that case and then opens the new-case page. In case view the Detail tab's jamming and anomaly lists are not limited to the case; pins added while a case is being viewed appear only after re-entering. Load more can repeat a row if an event arrives mid-paging. The feeder ignores the loader's truncation flag. A camera-list change reloads the right-panel timeline even when the primary camera is unchanged. The ownership data for the filters loads once per page open. The horizontal `TimelineStrip` has an old click-offset issue (the page does not use it). Verify integrity re-checks every file of the primary camera (slow on large devices).
6. **Heads.** MAUI does not register `IEvidenceChangedSource`, so no live reload there. Wyze `GetEventsAsync` is not implemented, so Wyze cameras have no events. In Simple mode `/media` shows the plain-language home; the review page is at `/media/review`.
7. **Role gates in the Actions menu are stricter than the old Evidence page** (they follow the server: hold and release Admin, export SuperAdmin, verify any signed-in operator, tag and new case Review).
8. **Pending tags are in memory only** and are lost when the session or circuit restarts (the UI says so). `CaseState` swallows an unexpected attach exception without logging (per-item failures are logged).

## Step 1: sidebar on /media (done)
- Left nav hidden on `/media` via a transient collapse (the saved preference is not changed; the hamburger flips the pane for the visit; leaving restores the saved state). `/` redirects to `/media`. Commit `ed1030a8`.

## Step 2: the review page (done; see Built)
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

## Step 3: remaining items (preview pipeline done; WebView2 check still open)
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
