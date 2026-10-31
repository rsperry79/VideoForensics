# Plan: flagged media tracking and review subpages

Status (2026-10-31): PLAN, reviewed by a Sonnet pass against the code. Nothing built. Batch 0 decisions come first. Review claims marked (R) were taken from the review and not re-checked by hand; confirm in B0.

## Goals
1. Track flagged media for every flag type, not only RSSI and bit rate. A new flag type plugs in without schema or shell changes.
2. A reusable review control set shared by every report that has flagged media (Signal History and Bit rate history now; more later).
3. On Signal History and Bit rate history: a table of flagged media. A row click opens a review subpage for that flag.
4. Each report type gets its own review subpage built from the shared controls, with a breadcrumb.

## Current state (audited 2026-10-31)
- Per-clip red flags exist only as `MediaItem.RedFlags` (comma list of `RedFlag`: BitrateCollapse, ClippedAudio, ElevatedNoiseFloor, UnusualLighting, AudioDropout) and `JammingSuspected`, written by `MediaAnalysisService`. They also drive the baseline (`RedFlags == string.Empty` filter, MediaAnalysisService.cs:121) (R). There is no review state and no SQL way to list unreviewed flagged clips.
- RSSI: 2-sigma alerts are already persisted as `Notice` rows by `SignalDeviationAlerts.CheckAsync` (EventType "SignalDeviation", Severity Alert, `ActionPath` to `/signal-history?...`, dedupe by a text marker `sig-dev:{provider}:{id}:{yyyyMMddHH}` in `Notice.Details`, 24 h look-back). 1-sigma windows and low/missed periods exist only on the fly in `SignalAnalysisService` (R).
- Notice dismissal is per operator "mark as read" (`INoticeDismissalRepository`). The Signal History banner dismissal is page-local (`_dismissedAlertSignature`) and the banner is built from on-the-fly windows, not stored flags.
- Cases: `ICaseRepository.AddItemAsync(caseId, kind, targetId, reason, addedBy)` pins only `CaseItemKind` Event or Media; it throws on duplicate pin, missing target, closed case. `ListCasesContainingAsync` shows existing pins. Reuse `ReviewCaseSection`, `ReviewPendingTags`, `CaseState`, `ReviewEvidenceTagger` (R). Legal hold: `ILegalHoldRepository` (Admin), writes ActionLog in the same operation; retention skips held clips. Jamming has its own `JammingIncident` and auto-case path.
- Player and timeline: `ReviewPlayer` is a bare `<video controls>` with `Cursor`, `SeekToUtcAsync`, and time readers only: no play/pause/rate API and no progress or ended event. `TimelineStrip` has Horizontal/Vertical orientation, window, `Cursor`, `OnSeek`, clips, but no marker layer, a fixed 600 px length, and the old horizontal click-offset bug (see `media-page-rework.md` follow-ups 1, 2, 5).
- Syncfusion: `SfTimeline` is in `Syncfusion.Blazor.Layouts` (already referenced, 35.1.42; Themes referenced; only bootstrap5 themes kept). It is an evenly spaced item list with orientation and templates; no time-proportional axis, scrubber, cursor or playback (R).
- Layout: Signal and Bit rate pages render inside `MainLayout` (nav, top bar, resizable right panel via `PageRightPanelContent`). `/media` hides the left nav with a transient flag in `MainLayout.razor.cs`. `viewport.js` only does a 600 px match; there is no resize-measure module.
- Wire/client rules: `client-split.md` (Remote twins in `Hosting/src/Remote`, `ToDto/ToDomain`, `/api/v1`, bearer token), `client-live-data.md` (IObservable source; `IEvidenceChangedSource` + `RemoteEvidenceChangedSource` is the template; MAUI missed registering it before), `providers.md` (typed columns, SQL paging), `ui-shared.md` (localization in the same change as each component, mobile logic only in `MobileLayout`/`ResponsiveLayout`), `tests.md` (xUnit v3, `<Feature>_Tests`, `<Class>_<Scenario>_<Expected>()`).
- Bit rate chart: `MeasuredBitrate` has no MediaItemId and excludes estimated bit rates, so a chart marker cannot link to a clip yet.

## Design

### D1. Data: `MediaFlag` is review state; analysis output stays where it is
- `MediaItem.RedFlags`/`JammingSuspected` remain the detection result and baseline input. `MediaFlag` holds review state only. Dismissing a flag never edits `RedFlags` (a dismissed false positive stays out of the baseline unless decided otherwise).
- Columns (typed, no JSON): `Id`, `FlagType` (enum column, string converter), `DeviceId`, `MediaItemId` (nullable; no FK cascade, restrict), `WindowStartUtc`/`WindowEndUtc`, `RaisedAtUtc`, `Severity`, `Status` (Unreviewed, Kept, Dismissed), `ReviewedBy`, `ReviewedAtUtc`, `ReviewNote`, `RowVersion`, `NoticeId` (nullable link to the originating Notice).
- Rules: `MediaItemId` null requires DeviceId + window (RSSI). Unique indexes (MediaItemId, FlagType) and (DeviceId, FlagType, WindowStartUtc). Index (Status, DeviceId, RaisedAtUtc) for the default Unreviewed list. Purge keeps the flag; the review page shows "media unavailable".
- RSSI flags = the existing 2-sigma alert only (flag type `RssiDeviation`). 1-sigma stays chart shading, not flags. `RssiLowSignal` is not added until a writer and semantics exist. Jamming links to the existing `JammingIncident` rather than duplicating it (decide in B0 whether it also gets a flag row).
- Writers: `MediaAnalysisService` reconciles, not just inserts: upsert on (MediaItemId, FlagType); close or delete still-Unreviewed rows no longer raised on re-analysis; never touch Kept or Dismissed rows; the incomplete-analysis path (`RedFlags = null`) must not wipe reviewed rows. `AnalyzeAsync` saves through `_items.UpdateAsync`, so flags need the same transaction or an idempotent derive step; a crash between two saves must not lose flags. `SignalDeviationAlerts` writes the RSSI flag and sets `NoticeId`, replacing the text-marker dedupe with the unique index.
- Backfill: a new pass over `RedFlags != ''` / `JammingSuspected` (the existing `JammingAnalysisBackfill` only re-analyses unanalysed media). Idempotent. Decided (Q8): the backfill only creates flags for clips from the last 7 days, as Unreviewed; older flagged clips get no row and so cannot bury the table. The 7-day cutoff is a named constant for the testing phase; widening it later is a re-run of the idempotent pass.
- Concurrency and audit: compare-and-set `UpdateStatusAsync(id, expectedStatus, newStatus, reason, operator)`; 409 shows "already reviewed by X". The status change and its ActionLog entry (flag id, media id, old and new status, reason, operator from the token) commit in one SaveChanges, as `LegalHoldRepository` does. Dismiss requires a reason; Keep records an optional note. Re-open Dismissed -> Unreviewed is Admin-only and audited.
- Notice link: Kept or Dismissed also dismisses the linked Notice for all operators (a global dismiss; per-operator "read" and global "review state" are different things). Remote clients need new routes for this; no Remote twin exists for the notice repositories today.

### D2. Flag-type registry
`IFlagTypeDescriptor` (Ui.Shared): flag type, localized label key, icon, default severity, owning report (Signal, Bitrate, later Audio, Lighting, Jamming), review route. A new flag type = descriptor + writer (+ review page if it has its own context).

### D3. Flagged-media table (`FlaggedMediaTable`)
Syncfusion Grid, server-side paging/sort/filter. Columns: time, device, flag type, severity (icon + text, not colour only), status, clip duration, linked case. Parameters: flag types, scope from `ScopeState`, `OnRowClick`. Default filter Unreviewed. Signal History shows `RssiDeviation`; Bit rate history shows `BitrateCollapse`. Row click goes to the report's review route and carries the parent scope in the URL. Shows an unreviewed count; empty, loading and error states. The Signal History banner must read persisted flags (matched to on-the-fly windows by device + hour) instead of page-local dismissal.

### D4. Review shell (`FlagReviewShell`) and shared controls
Layout:
```
+--------------------------------------------+------------------+
| breadcrumb                                 |  FlagActions     |
+--------------------------------------------+  (add to case,   |
|                                            |   keep, dismiss) |
|        media, fit to window                +------------------+
|                                            |  ClipNavigator   |
|                                            |  (prev / next)   |
+--------------------------------------------+------------------+
| PlaybackTimeline: window, markers, transport                  |
+---------------------------------------------------------------+
```
- Fit: CSS grid/flex with `min-height:0` and `object-fit:contain`, sized to the layout content box, not `100dvh` (the pages live inside `MainLayout`). Decided (Q9): the existing right panel (scope/filter rail, `PageRightPanelContent`/`ScopeRail`) stays as it is on the review subpages. The whole shell, including its actions/navigator column and bottom timeline, renders inside the middle content window; that column is pinned to the right edge of the middle window, not in the app's right panel. Size the shell to the middle window's box (resize-measured), so it still fits without scrolling when the right panel is resized. The left nav is not changed by this plan; hiding it like `/media` is optional and not decided (ask before doing it). JS measurement only where needed (the timeline's pixel length); a new small resize-observer module, not a copy of `signal-chart.js`.
- `FlagActionsPanel`: built on `ReviewSidebarSection` and `ReviewCaseSection` (not `ReviewActionsPanel`, which is bound to `ReviewPageState`). *Add to case*: choose an open case or start a new one via the existing hand-off; for an RSSI flag it pins every clip and every snapshot in the flag's window (decided, Q7), listing the count first; show "already in case X" from `ListCasesContainingAsync` and skip items already pinned; catch and show duplicate, closed-case and missing-target errors. *Keep flagged*: Status Kept; optional "also place legal hold" for Admin only. *Dismiss*: reason required. Roles mirror the Actions menu (`OperatorRole.Review`), enforced on the server, not UI-only.
- `ClipNavigator`: takes a flag-list source (the table's filter and sort), not the table. "3 of 17" via keyset neighbours (`GetNeighbourAsync(flagId, filter, direction)` + count), so it stays correct as other operators change the list. Auto-advance after Keep/Dismiss; at the end go back to the table. Left/right arrows only when the shell, not the `<video>`, has focus; `aria-live` for position and status changes.
- `MediaFit`: wraps `ReviewPlayer` for one clip; states for purged, missing file, deleted device, flag id not found (deep link from a Notice), and no clip in the hour (`ReviewPlayerGap`). RSSI flags resolve to playable media with `GetPageByDevicesAndDateRangeAsync` (paged, SQL-side, non-purged) over window +/- a defined pad (the Notice scope uses +/- 1 h); the add-to-case action pins ALL of them (see Q7).
- Mobile (<600 px): the shell hands off to `MobileLayout`/`ResponsiveLayout` (drawer for the side column); no mobile logic in `MainLayout`.

### D5. Playback timeline (`PlaybackTimeline`)
- Track: `SfSlider` scrub bar plus `SfTimeline` flag list, per the Syncfusion section (the `TimelineStrip` marker layer is only an approved-exception fallback). Cap or page the flag list for a busy week. Times shown in the user's zone (`UserTimeZone`).
- Window: toggle between "unreviewed flagged events" (earliest to latest unreviewed flag in the current filter) and "1 week" (ending at the selected flag).
- `SfTimeline` is used only for an optional discrete "unreviewed events" list (verify item-click support in the B0 spike). It cannot be the time-frame control or scrubber. No Syncfusion component provides play/pause/seek, so the transport is custom, localized buttons.
- Transport: play/pause; fast-forward = `playbackRate` 2/4/8 within the clip, muted above 4x; rewind = step back 10 s and, at clip start, the previous clip in the device window (browsers and WebView2 do not support negative `playbackRate`). Cross-flag navigation stays in `ClipNavigator`.
- `ReviewPlayer` changes: `Play()`, `Pause()`, `PlaybackRate`, drop native controls when the external transport is used, `OnProgress` (throttled about 4 Hz via a JS `timeupdate` and `DotNetObjectReference`, no per-frame polling over SignalR) and `OnEnded`. Unify the two existing JS time readers (`review-player.js`, `media-viewer.js`). Closes `media-page-rework.md` follow-up 2.

### D6. Per-type review subpages and breadcrumb
- Routes: `/signal-history/review/{flagId}` (RssiReview) and `/bitrate-history/review/{flagId}` (BitrateReview); verify the router does not treat them as the parent page. Each composes `FlagReviewShell` plus a type-specific context panel (RSSI: reading, sigma, hour mini chart; bit rate: kbps vs baseline, lighting mode, collapse flag).
- Breadcrumb: `Signal history > Review: {device}, {time}`. `SignalDrillTrail` is cleared when scope changes, so pass the parent scope in the URL and add a review crumb type; the parent crumb restores that scope and the table filter.

### D7. Reports integration
- Bit rate chart collapse markers link to the review page: add `MediaItemId` (and flag id) to `MeasuredBitrate`, `GetMeasuredBitrateByDeviceAsync`, its DTO, `RemoteBitrateAnalysisService` and the chart model (own small batch). Estimated-bit-rate clips have no marker (known).
- Unreviewed counts on both pages; nav badge optional (SQL `COUNT` on the index, pushed through the changed source).

### D8. One reusable `MediaPlayer` control (user requirement: change player behaviour in one place)
- A single component, `MediaPlayer` (Ui.Shared `Components/Viewer`), is the only place in the app that renders a `<video>` or plays a clip. The review shell, the media page, `SingleCameraView`, `CameraGrid` tiles that play, and `MediaViewer` all use it. Existing `<video>` sites, found by grep on 2026-10-31: `Components/Viewer/ReviewPlayer.razor`, `Components/Viewer/SingleCameraView.razor`, `Components/Evidence/MediaViewer.razor`, `Pages/AccountDevicesTab.razor`, `Pages/LiveView.razor`. All are migrated onto it; `LiveView` (a live stream, not a clip) gets a `Live` source mode in the same control, or is listed as a named exception in the source-scan test if that proves unsafe (decide in B0). `ReviewPlayer` becomes a thin wrapper or is replaced.
- API (stable, so later behaviour changes touch only this component): parameters `Source` (clip id or URL), `Cursor`, `PlaybackRate`, `Muted`, `Controls` (native/none/external), `Loop`, `Autoplay`, `Fit` (contain/cover), `Poster`; methods `PlayAsync`, `PauseAsync`, `SeekToUtcAsync`, `StepAsync(seconds)`; events `OnProgress` (throttled ~4 Hz from JS `timeupdate`), `OnEnded`, `OnError`, `OnReady`; states for loading, error, purged/missing, and gap (`ReviewPlayerGap`). One JS module (`media-player.js`) replaces `review-player.js` and `media-viewer.js` time readers.
- Extension points for future behaviour (decided now so the change is made once): an `IMediaPlaybackPolicy` seam for speed caps, mute-above-4x and sequencing across clips; a content slot for overlays (context panels, markers); keyboard handling in the component with the focus rule from D4.
- Tests: bUnit for every parameter, method and event through the one component; a source-scan test fails the build if a `<video` element appears in any other `.razor` file.

## Syncfusion-only controls (user requirement, 2026-10-31)
Every interactive control in the new tables, shell and subpages is a Syncfusion Blazor component; follow skill `ui-shared-theme` (colours, SfDropDownList binding gotchas). Mapping:
- Flagged table: `SfGrid` (Grid, referenced). Row click via its row-select event.
- Buttons (keep, dismiss, add to case, transport, previous/next): `SfButton` (Buttons, referenced). Checkbox "also place legal hold": `SfCheckBox`.
- Case picker and flag-type/status filters: `SfDropDownList` / `SfMultiSelect` (DropDowns, referenced). Reason and note fields: `SfTextBox` (Inputs, referenced). Speed choice: `SfDropDownList` or `SfButtonGroup`-style `SfButton`s.
- Confirm dialogs (dismiss reason, add-to-case summary with clip and snapshot counts): `SfDialog` (Popups, referenced). Result and error messages: `SfToast` / `SfMessage` (Notifications, referenced). Tooltips: `SfTooltip` (Popups).
- Window toggle (unreviewed events / 1 week): `SfButtonGroup`-style toggle or `SfSwitch` (Buttons).
- Breadcrumb: `SfBreadcrumb`, namespace `Syncfusion.Blazor.Navigations` (CONFIRMED by Syncfusion docs; package NOT referenced today; add at 35.1.42, same as the others). Use `EnableNavigation="false"` with the `ItemClick` event + `NavigationManager` so the parent crumb restores scope; `ItemTemplate` for the review crumb; `MaxItems`/`OverflowMode` for long trails. Icons need the `e-bicons` @font-face in app css (docs). Theme coverage by bootstrap5 still to confirm in B0.
- Layout of the middle window: `SfSplitter` (Layouts, referenced) where panes are resizable, per `ui-shared.md`.
- Playback scrub bar and window: `SfSlider` (Inputs; `Type=Range` or `MinRange`, `@bind-Value`, `SliderTicks` with Placement, ShowSmallTicks, LargeStep, SmallStep, tooltip) with min/max = the time window and value = playhead.
- Flag list: `SfTimeline` (Layouts) with `TimelineItem` `Content`/`OppositeContent` templates, `Orientation`, `Alignment`, `Reverse`, and an `ItemRendered` event. Syncfusion docs show NO item-click event, so each item's content holds an `SfButton` that switches the flag; spacing is even, not time-proportional.
- Documented limits (Syncfusion docs search, 2026-10-31): neither `SfSlider` nor `SfTimeline` documents per-position custom markers on a time-proportional axis. So flag marks on the scrub bar are not available from Syncfusion alone. Design: the scrub bar is `SfSlider` (range = window, ticks = day/hour); the flags are the `SfTimeline` list directly above or beside it, each with its time; the playhead and the flag a clip belongs to are highlighted. APPROVED (user, 2026-10-31): the exception is allowed. Clickable, time-proportional flag marks sit on the scrub bar via ONE non-Syncfusion element, `FlagMarkerLayer`: a thin absolutely-positioned overlay on top of the `SfSlider` track that maps each flag time to a percentage of the window and raises `OnMarkerClick(flagId)`. Marks use `SfTooltip`; marker buttons are plain `<button>` elements only inside this layer (keyboard focusable, localized aria-labels, severity shown by icon/shape as well as colour). It is the only non-Syncfusion interactive element on the page apart from the `<video>`, and the bUnit rule allows it by name. The `SfTimeline` flag list stays as the discrete, accessible list.
- Not available as Syncfusion Blazor components, so they stay plain elements: the HTML5 `<video>` (Syncfusion has no Blazor video player; only an open feature request), `FlagMarkerLayer` (approved exception, above) and non-interactive structure (grid containers, labels). The existing custom `TimelineStrip` is NOT used by the review pages; D5's `TimelineStrip` work (marker layer, click-offset fix, 600 px length) is dropped. The player changes live in the reusable `MediaPlayer` (D8).
- Tests: bUnit asserts the Syncfusion component types render (`FindComponent<SfGrid<...>>` etc.) so a plain `<button>` or `<select>` regression fails a test.

## Cross-cutting requirements (every batch)
- Worktree from `wip`, failing test first, lite gate, hand off to `wip` (`code-workflow`).
- Localization keys added in the same change as each component; tests assert by key.
- Remote twin, DTO `ToDto/ToDomain`, `/api/v1/media-flags` routes, bearer token, server-side role checks, operator taken from the token.
- `IMediaFlagChangedSource` in `Contracts/` (template `IEvidenceChangedSource`), published on every status change and every writer insert; registered on server, client and MAUI, with a DI test per host.
- Tests named per `tests.md`: repository, endpoint, DTO round trip, Remote service, DI registration, bUnit for each control, keyboard and focus rules.
- Accessibility: labelled transport buttons, visible focus, severity not colour only, `aria-live` regions.
- Per-batch acceptance includes a no-scroll fit check at three window sizes once the shell exists (do not leave one trailing browser pass: it slipped on the media rework).

## Batches
- B0 Decisions and spikes (below) plus a throwaway spike: `SfSlider` as a time-mapped scrubber (range, ticks, seeking a `<video>`) with the `SfTimeline` flag list and `SfButton` item content; `ReviewPlayer` progress/rate; `SfBreadcrumb` + Navigations package add and theme check; `SfGrid` row-select. If the slider-plus-list is not enough, ask the user about the marker exception.
- B1a Data: entity, config, migration, indexes, repository (compare-and-set, keyset neighbours, count), ActionLog in one commit. data.* projects only. **DONE (2026-10-31, on wip: MediaFlag entity/config/AddMediaFlags migration/IMediaFlagRepository; 33 tests. Migration only run on in-memory SQLite).**
- B1c Case snapshot kind: `CaseItemKind.Snapshot`, migration, DTOs, export, case details, tests (needed by B6). **DONE (2026-10-31: CaseItemKind.Snapshot, CaseItem.SnapshotCaptureId, AddCaseItemSnapshot migration, repository pin/dup/list, DTO mapping, CaseState.HasActivePin, integrity NotApplicable, CaseDetails generic 'Snapshot' label. NOT done: case export does not include pinned snapshot files; case details shows a generic label, no device/time lookup).**
- B1b Wire: DTOs, endpoints, Remote twin, `IMediaFlagChangedSource`, DI on server/client/MAUI, notice-dismiss routes.
- B2 Writers and backfill (needs B1a): `MediaAnalysisService` reconcile, `SignalDeviationAlerts` flag + `NoticeId`, backfill pass.
- B4a `MediaPlayer` (D8): build the control, JS module, tests, then migrate every existing `<video>` site onto it; add the source-scan test. No data dependency. **DONE (2026-10-31, on wip: MediaPlayer + JS module + policy seam + source-scan test; five video sites migrated, LiveView via Live mode. Unit/bUnit only, never played in a browser).**
- B4 Shell: `FlagReviewShell`, `MediaFit` (uses `MediaPlayer`), resize module, CSS (no data dependency; after B4a; parallel with B1b/B2).
- B5 `PlaybackTimeline`: `SfSlider` scrub bar, `FlagMarkerLayer`, `SfTimeline` flag list, transport buttons driving `MediaPlayer` (needs B4a, B4 and a marker DTO shape).
- B3a `MeasuredBitrate` + chart gains MediaItemId/flag id (needs B1b). **DONE (2026-10-31): `MeasuredBitrate.MediaItemId`, `BitrateSample`/DTO `MediaItemId` + `FlagId` (flag looked up per camera via `IMediaFlagRepository`); FlagId stays null until B2 writes flags. Estimated-bit-rate clips still have no marker.**
- B3 `FlaggedMediaTable` + wire into both reports (needs B1b, B3a). Touches `SignalHistory.razor` and `BitrateHistory.razor` and their resx: never parallel with B7.
- B6 `FlagActionsPanel` + `ClipNavigator` (needs B1b, B4).
- B7 `RssiReview`, `BitrateReview`, context panels, breadcrumb, nav (needs all above).
- B8 Final acceptance: window-size matrix, keyboard/a11y, MAUI check, docs/CHANGELOG, full gate.
Parallel-safe sets: after B1a: B2 and B4. After B1b: B3a, B6. Do not run in parallel: B1b with B2 (DI files), B3 with B7.

## Questions for B0 (answered where the review recommends; confirm with the user)
1. Timeline: markers on an extended `TimelineStrip`; `SfTimeline` only for a discrete unreviewed list. (Recommended.)
2. Keep flagged = Status + audited note; legal hold is a separate explicit Admin action; nothing is pinned to a case implicitly. (Recommended.)
3. Rewind = step back 10 s / previous clip; fast-forward = 2/4/8x within the clip. (Recommended; browsers cannot play backwards.)
4. Dismissal is global, records who/when/why, dismisses the linked Notice for everyone, re-open is Admin + audited. (Recommended.)
5. Flags per report: Signal = `RssiDeviation` (2-sigma only); Bit rate = `BitrateCollapse`; `UnusualLighting` and audio flags wait for their own report; Jamming links to `JammingIncident`. (Recommended.)
6. Purged or missing media: keep the flag, show "media unavailable"; no FK cascade. (Agreed.)
7. DECIDED (user, 2026-10-31): an RSSI flag adds all clips and all snapshots in its window to the case, no manual pick. Consequence: `CaseItemKind` is only Event and Media today and snapshots are `SnapshotCapture` rows, so a `Snapshot` kind must be added (enum, migration, DTOs, case export, case details page, Remote twin) before B6. Window = flag window +/- the pad from D4; snapshots via `ISnapshotCaptureRepository.GetCurrentForDeviceAsync`. Add as its own batch B1c (needs B1a), and confirm the pad with the user. Bit rate and other clip flags keep pinning their one clip.
8. DECIDED (user, 2026-10-31): backfill 1 week back only, for testing; flags are created Unreviewed, no Legacy status. Older flagged clips stay visible through `MediaItem.RedFlags` and the existing reports but are not in the review queue until the cutoff is widened.
9. DECIDED (user, 2026-10-31): the right filter window stays; the new review shell is the middle window, with its own pinned side column inside it. Left nav behaviour stays as is unless the user asks otherwise.

## Out of scope
New report pages for audio and lighting flags; threshold changes; live-view flags; 1-sigma and low-signal flag persistence.
