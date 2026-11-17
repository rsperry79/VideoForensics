# Plan: media review layout and shared shell

Status (2026-11-15): PLAN. Nothing built. Written from a read-only code review of `/media` and the flag review subpages; no browser check was done. Decisions U-D1 to U-D3 are made (user, 2026-11-15). Supersedes the open layout items in `media-page-rework.md` (follow-ups 1 to 3), `flagged-media-review.md` (mobile hand-off, browser checks) and `platform-split-and-viewer.md` (grid preview asset, drag seek).

Status update (2026-11-16): U1 to U11 are built on `feature/media-review-layout` and merged to `wip`. Divergence from U6a/U6b (user, 2026-11-16): the tabs, time range, camera selector (under Locations in the filter panel) and pending tags live in the right sidebar, not the header; the Filters button is removed. U7 is complete (percent positions, hover time, drag to seek). U12 code cleanup is done (orphaned Evidence code removed, CHANGELOG line, failure-log entry). Still open: browser pass at 1920, 1366 and 375 wide (camera grid previews, single-camera keyboard map, Grab still, phone width), WebView2 range playback, the live `MediaKind` count query, and an Edge check of the pending-tags text and camera popup.

Paths: `UI` = `src/client/ui/VideoForensics.Ui.Shared/src`, `T` = `src/client/ui/VideoForensics.Ui.Shared/tests`.

## Goals
1. One shell (`ReviewShell`) for `/media` and the flag subpages: header with breadcrumb slot, stage, bottom strip.
2. A stable player stage: fixed aspect box, `object-fit: contain`, no layout jump in gap, loading or empty states.
3. Popups that work in the right panel and in the phone sheet.
4. A usable timeline: fills its column, readable ticks, keyboard, follows playback.
5. A phone layout (under 600px).
6. Restore the viewer keyboard map; basic accessibility.

## Decisions (user, 2026-11-15)
- U-D1: the timeline control stays the custom `TimelineStrip`. No `SfSlider` on `/media`.
- U-D2: the left nav collapses on `/media`. Tabs move to the page header.
- U-D3: media fit is `contain`, never `cover`, on every review page.

## Current state (audited 2026-11-15)
- `UI/Components/Viewer/ReviewPlayer.razor.css:3` compiles to `[scope] .review-player`. `ReviewPlayer` renders no element of its own, so the rule never matches in single-camera mode.
- The gap box is `min-height: 3rem` (`MediaPlayer.razor.css:45`). `ReviewTimelineTab.razor:28-50` replaces the player with a `<p>` while loading.
- The popup backdrop is in `ReviewTopBar.razor:26-30` (`z-index: 1040`). The popups are in the right panel, and on a phone in `.settings-sheet` (`z-index: 301`), below the backdrop.
- `MainLayout.razor.cs:142` forces the left nav open on `/media`. `NavGroups.cs:73-77` holds the tabs.
- `ReviewTimelinePanel.razor:34` fixes the strip at 600px. `TimelineStrip.razor:122` emits one tick per hour. The strip has no `tabindex` or key handler. `ReviewPlayer` does not pass `OnProgress`.
- `CameraGrid.razor:20` plays full content with native controls in every tile. `CameraGrid.razor.css:10` uses a viewport breakpoint.
- `MediaFit.razor.css:11-28` uses `cover`. `FlagReviewShell` is a separate three-row grid.
- `T/Review/ReviewLayoutStylesheet_Tests.cs` asserts the current CSS text, including the 900px media query.

## Steps
Every unit: worktree from `wip`, failing test first (`tdd`), filtered test run, new strings in `ReviewPage.resx` or `SharedResources` in the same change. Every unit also checks for no horizontal scroll at 1920, 1366 and 375 wide.

**U1. Stage box and contain fit.**
- Files: `ReviewPlayer.razor` (wrap in `<div class="review-stage">`), `ReviewPlayer.razor.css`, `MediaPlayer.razor.css` (gap and missing states fill the parent), `MediaFit.razor.css`, `MediaFit.razor` (`Fit` to Contain).
- Tests (`T/Viewer/ReviewStage_Tests.cs`): `ReviewPlayer_Render_WrapsPlayerInStageElement`, `ReviewPlayer_CursorInGap_KeepsStageElement`, `MediaFit_Video_UsesContainFit`. Update `ReviewPlayerStylesheet_Video_FitsItsColumnAndKeepsItsAspect` to assert the `.review-stage` rule.
- Done when: the stage element exists in every state and no stylesheet under `Components/Review` or `Components/Viewer` contains `object-fit: cover`.

**U2. Stylesheet guard.** (moved up from additions)
- Test (`T/Review/ReviewStylesheets_Tests.cs`): `ReviewStylesheets_DeepRule_HasOwnedElementPrefix`, failing on any `.razor.css` with a bare `::deep` rule.

**U3. Popup host.**
- Files: new `UI/Components/Review/ReviewPopupHost.razor` and `.razor.css` (backdrop plus Escape); remove the backdrop from `ReviewTopBar.razor`; render the host inside `PageRightPanelContent` on `ReviewPage`, `BitrateReview`, `RssiReview`. The Actions popup opens inline, not `position: absolute`. Popups become `role="group"`; the toggle button keeps focus.
- Tests (`T/Review/ReviewPopupHost_Tests.cs`): `ReviewPopupHost_PopupOpen_RendersBackdrop`, `ReviewPopupHost_BackdropClick_ClosesPopup`, `ReviewPopupHost_Escape_ClosesPopup`, `ReviewTopBar_PopupOpen_RendersNoBackdrop`.
- Done when: the backdrop and the popups share one parent container.

**U4. Review shell.** (moved up before the state and timeline units)
- Files: new `UI/Components/Review/ReviewShell.razor` and `.razor.css` (slots `Header`, `Stage`, `Side`, `Bottom`; grid rows `auto / minmax(0,1fr) / auto`); `FlagReviewShell.razor` becomes a wrapper over it; `ReviewPage.razor` and `.razor.css` use it (timeline in `Side`, preview strip in `Bottom`); `SingleCameraView.razor` exposes the strip as a separate fragment.
- Tests (`T/Review/ReviewShell_Tests.cs`): `ReviewShell_Render_HasHeaderStageAndBottomRegions`, `ReviewShell_NoSide_OmitsSideColumn`, `FlagReviewShell_Render_UsesReviewShell`, `ReviewPage_TimelineTab_PlacesTimelineInSideSlot`.
- Done when: both page families render through `ReviewShell` and existing `FlagReviewShell_Tests` pass.

**U5. States inside the stage.**
- Files: `ReviewTimelineTab.razor` and `.razor.css` (states as overlays in a stage-sized box; grab-still status moves under the stage with `aria-live="polite"`).
- Tests (`T/Review/ReviewTimelineTab_Tests.cs`): `ReviewTimelineTab_Loading_RendersStagePlaceholder`, `ReviewTimelineTab_NoClips_RendersStagePlaceholder`, `ReviewTimelineTab_GrabStillStatus_RendersAfterStage`.
- Done when: every state renders one `.review-stage` element.

**U6a. Left nav collapsed; tabs in the header.**
- Files: `MainLayout.razor.cs` (`/media` and `/media/review` become collapse paths; fix comments), `NavGroups.cs` (drop the three tab items), `ReviewTopBar.razor` (tabs as `role="tablist"`).
- Tests: `MainLayout_MediaPath_CollapsesLeftNav` (`MainLayoutAutoCollapseTests`), `NavGroups_MediaGroup_HasNoTabItems`, `ReviewTopBar_TabClick_SetsActiveTab`.

**U6b. Camera, range and Filters in the header.**
- Files: `ReviewTopBar.razor` (camera selector, range select, Filters button), `ReviewPage.razor`, `ReviewFilterPanel.razor` and `ReviewTimeRange.razor` (remove what moved).
- Tests: `ReviewTopBar_Render_ShowsCameraAndRange`, `ReviewTopBar_RightPanelCollapsed_FiltersButtonExpandsIt`.
- Done when: camera and range can be changed with the right panel collapsed.

**U7. Timeline: length, ticks, keyboard, drag and hover.**
- Files: `TimelineStrip.razor` (positions in percent, tick step of 1, 6 or 24 hours from the span, `role="slider"`, `tabindex="0"`, `aria-valuetext`, arrow and Page keys, cursor time label, drag to seek, hover time), `TimelineStrip.razor.css`, `ReviewTimelinePanel.razor` and new `.razor.css` (height from the shell row), one shared JS module `UI/wwwroot/scripts/element-size.js` (one element-size read for click and drag mapping; the flagged plan's resize observer uses this same module).
- Tests (`T/Viewer/TimelineStripVertical_Tests.cs`): `TimelineStrip_SevenDayWindow_UsesSixHourTicks`, `TimelineStrip_ThirtyDayWindow_UsesDailyTicks`, `TimelineStrip_ArrowDown_SeeksOneStepOlder`, `TimelineStrip_Vertical_HasSliderRoleAndValueText`, `TimelineStrip_Segments_UsePercentPositions`, `TimelineStrip_AnyWindow_KeepsLabelsAtLeast14pxApart`, `TimelineStrip_Drag_SeeksOnRelease`.
- Done when: no fixed 600px remains and the strip is reachable by Tab.

**U8. Cursor follows playback.**
- Files: `ReviewPlayer.razor` (forward `OnProgress`, `OnEnded`), `SingleCameraView.razor`, `CameraGrid.razor` (master tile only), `ReviewTimelineTab.razor`, `ReviewPageState.cs` (new `SetPlaybackCursor`, which raises a change that does not re-seek the player or rewrite the URL).
- Tests: `ReviewPageState_SetPlaybackCursor_DoesNotRaiseSeek`, `ReviewPlayer_Progress_RaisesOnProgressWithUtc`, `ReviewTimelineTab_PlayerProgress_MovesTimelineCursor`, `ReviewPage_PlaybackCursor_DoesNotWriteUrl`.
- Done when: the marker moves during playback with no seek loop.

**U9. Camera grid.**
- Files: `CameraGrid.razor` (non-master tiles use `PreviewUrlFor`, `Controls="None"`, muted; tile click raises `OnSelect`; play and pause state shared across tiles), `CameraGrid.razor.css` (`repeat(auto-fit, minmax(280px, 1fr))`, no inline style, reduced-motion rule for the looping preview tiles), `ReviewTimelineTab.razor`.
- Tests (`T/Viewer/CameraGrid_Tests.cs`): `CameraGrid_NonMasterTile_UsesPreviewUrlWithoutControls`, `CameraGrid_TileClick_RaisesOnSelect`, `CameraGrid_PlayPause_SharedAcrossTiles`. Replace `CameraGridStylesheet_Grid_IsDisplayGridAndSingleColumnOnNarrowScreens`.
- Done when: at most one full-content stream loads in grid mode.

**U10. Phone layout.**
- Files: `ReviewShell.razor.css` (container query under 600px: one column; `Side` becomes a row under the stage), `ReviewTimelinePanel.razor` (orientation from `ResponsiveLayout`'s cascaded viewport value), `SingleCameraView.razor.css` (120px tiles), `MobileLayout.razor` only if the sheet title needs a page-supplied label.
- Tests: `ReviewShell_Stylesheet_StacksUnderSixHundredPixels`, `ReviewTimelinePanel_MobileViewport_UsesHorizontalStrip`, `ReviewPopupHost_InsideSheet_RendersInSameParent` (DOM parentage only; the real stacking check is in U11).
- Done when: at 375px the stage is full width and nothing scrolls sideways.

**U11. Keyboard map and accessibility.**
- Files: `ReviewShell.razor` (key handler on the stage region using `MediaViewerKeys` after confirming it fits: space, arrows, `S` for grab still, ignored when the video or an input has focus), `ReviewFilterPanel.razor` (`role="group"`), `SingleCameraView.razor` (no `div` inside `button`), `MainLayout.razor` and `MobileLayout.razor` (`aria-label` on glyph buttons).
- Tests: `ReviewShell_SpaceKey_TogglesPlay`, `ReviewShell_SKey_RunsGrabStill`, `ReviewShell_KeyWithInputFocused_DoesNothing`, `MainLayout_GlyphButtons_HaveAriaLabels`.

**U12. Verification and cleanup (manual; needs operator sign-in).**
- Browser pass at 1920, 1366 and 375 wide for `/media` and both flag routes; WebView2 range playback on the MAUI head; `SELECT MediaKind, COUNT(*)` on the live database before deploy.
- Remove orphaned Evidence code with its tests (rework follow-up 3). Update the status lines of the five plans. Add a failure-log entry and a CHANGELOG line, since the navigation change is visible.

Order: U1, U2, U3, U4, U5, U6a, U6b, U7, U7b, U8, U9, U10, U11, U12. Parallel-safe: U3 with U1; U9 with U7. Not parallel: U6a with U4 (both edit `ReviewPage.razor`).

## Decisions (user, 2026-11-15, second round)
- U-D4: the Filters button expands the right panel (no header popover).
- U-D5: the phone sheet keeps the title "Settings" on review pages.
- U-D6: the flag pages' `PlaybackTimeline` (`SfSlider`) moves to `TimelineStrip`, so one timeline control is used app-wide. Unit U7b below.
- U-D7: in Simple mode the new header applies to `/media/review` only; `/media` keeps the plain-language home.

**U7b. Flag pages move to TimelineStrip (U-D6).**
- Files: `PlaybackTimeline` (flag pages) replaced by `TimelineStrip` in `ReviewShell`'s `Bottom` slot for `BitrateReview` and `RssiReview`; remove the `SfSlider` usage and its stylesheet rules; `FlagReviewShell.razor` passes the same props.
- Tests: `FlagReviewPage_Timeline_UsesTimelineStrip`, `PlaybackTimeline_Removed_NoSfSliderReference`. Update `FlagReviewShell_Tests` cases that assert the slider.
- Done when: all three review pages use `TimelineStrip` and no `SfSlider` remains on review pages.

## Risks
- `ReviewLayoutStylesheet_Tests` assert CSS text, so U1, U7 and U9 break them by design. Rewrite each in the same unit.
- U8 can create a seek loop if a progress update is treated as a user seek.
- U6a changes bookmarks that rely on left-nav tab links; `?tab=` URLs keep working.
- Scoped CSS: a `::deep` rule needs an element owned by the component. This was the cause of the dead rule in U1.
- Cloudflare caches `*.styles.css` for four hours; hard-refresh during U12.
- Container queries and `aspect-ratio` on WebView2 are unverified until U12.
- Not verified from code: `MediaViewerKeys` fit (U11) and the element-size JS module path under MAUI (U7).
