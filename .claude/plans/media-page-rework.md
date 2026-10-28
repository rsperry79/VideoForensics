# Plan: media page rework (Frigate-style)

Status: Step 1 done and pushed to `wip` (ed1030a8). Steps 2 and 3 not started. Builds on `platform-split-and-viewer.md` Parts 2 and 3.

## Goals
1. On `/media`, move the Media section out of the left nav and auto-close the left nav while on this page.
2. Rework the media page to resemble Frigate's review UI: top bar (Back, Live, camera select, date range, filter, actions, Timeline/Events/Detail tabs), large player, camera preview strip below the player, and a vertical timeline on the right.

## Current state (as found)
- `NavGroups.cs:70-72`: the `media` group has one section, "Media" -> `/media`.
- `MainLayout.razor:145-155`: the left nav is `SfSplitter` pane 0 (`_leftNavWidth`, collapsible via `ToggleLeftNavAsync`, persisted in `LayoutPreferencesState`).
- `Pages/Evidence.razor` is routed at `/` and `/media`. It holds the timeline, grid, gallery and device-time views, and the `MediaViewer` overlay.
- `Components/Evidence/MediaViewer.razor` has a `<video controls>` and a footer with prev/next/close. It is a clip viewer, not a Frigate-style player.
- `Components/Viewer/ReviewPlayer`, `TimelineStrip`, `CameraGrid` exist in the working tree from Part 2 (`feat/finish-parts-1-2`, not yet merged to wip). This plan assumes that branch lands first. Part 3 `SingleCameraView` is not started.
- `Evidence.razor` is routed at both `/` and `/media`, so `/` also shows the page with the nav expanded.

## Step 1: sidebar on /media
- Hide the whole left nav on `/media` (decided by user). The Media section stays in the nav for the other groups' pages. Collapsing to the 48px Min is the hide; state this in the PR.
- Scope: `/media` only. `/` redirects to `/media` (decided by user), so the page has one route and the nav rule applies once.
- Redirect: remove `@page "/"` from `Evidence.razor` and add a redirect for `/` (e.g. a small `Root.razor` with `NavigationManager.NavigateTo("/media", replace: true)`). Check the sign-in return URL and `NoticeActions` links that target `/` before removing it.
- Callers that navigate to `/` (keep working through the redirect, no edits needed): `DeviceSignIn.razor:16,123,152,172`, `Pair.razor:188`, `Notices.razor:146` (breadcrumb Home). `AuthGate.razor:85` already handles trailing slashes.
- Verify the auth flow: an unauthenticated `/` must still reach sign-in through `/media`, since `Evidence.razor` shows "not signed in" and the AuthGate decides the redirect.
- Transient flag, not `LayoutPrefs`: add `_autoCollapsed` in `MainLayout.razor.cs`. Set it when the path enters the auto-collapse set; do not write `LayoutPrefs.LeftNavCollapsed`.
- Toggle fix: `ToggleLeftNavAsync` (`MainLayout.razor.cs:198`) must compute the next state from the real pane state, not the saved pref. While `_autoCollapsed` is set, the hamburger expands the pane once and clears the flag. Decide whether a manual expand on `/media` persists; default: no.
- Apply after render, not in `OnLocationChanged`. CollapseAsync throws if the splitter JS is not ready (`IsSplitterNotReady`, `MainLayout.razor.cs:130`). Reuse the pending-and-retry pattern in `OnAfterRenderAsync`. Do not reuse `_leftNavRestorePending`: it only collapses, and a new `_autoCollapsePending` must handle both directions.
- Leaving the page: if `_autoCollapsed` is set, expand only if `LayoutPrefs.LeftNavCollapsed` is false. If the pref says collapsed, leave it collapsed. Also cover the first-load case where restore and auto-collapse are both pending.
- Tests: bUnit for the toggle while auto-collapsed (one click expands), leaving `/media` restores the saved state, and the pending retry path.

## Step 2: Frigate-style layout (Evidence page or new `MediaReview` page)
- Top bar: Back, Live, camera selector (`SfDropDownList` multi-select), range (Last 24 hours), Filter, Actions, tabs Timeline / Events / Detail.
- Main area: `ReviewPlayer` (single camera, native `<video>`).
- Under the player: `SingleCameraView` preview strip (Part 3) of the other cameras at the same time offset.
- Right or bottom: `TimelineStrip` as a vertical timeline with time labels and event bands (Frigate style).
- Camera selector: "All cameras" toggle plus per-camera switches, Apply/Reset. Maps to `CameraGrid` when more than one camera is on.
- Decide: new route `/media` replaces the Evidence list, or Evidence list moves to Review. The screenshot shows the current Evidence list as `/media`. Recommend a new page for the review UI and keep the list under a "Grid" or "Gallery" tab, so nothing is lost.

## Step 3: remaining Part 2 and Part 3 items
- Verify WebView2 range playback on the MAUI Windows head (manual, still open from Part 2).
- Part 3 `SingleCameraView`: preview strip, click to switch camera at the same offset. Requires the preview pipeline (`PreviewPath`, `GET /media/{id}/preview`, backfill). That is server work in `Platform.Web`/Hosting and is a separate PR.

## Decisions (user, 2026-10-28)
- The Evidence list/timeline page is replaced by the review UI. Do not keep the list as a tab (overrides the earlier recommendation).
- Events tab: every provider event (the Ring/Uniview/etc. event feed). No stills, no time-lapses.
- Detail tab: each event expanded to show its provider sub-events (e.g. "Person detected" / "Person left") and suspected jamming or anomalies for that window.

- Actions: a tag action for evidence, and "Start new case" on evidence.
- Filters: provider, account, and camera filters.
- Vertical timeline: in the right panel.

## Side task (separate from Step 2)
- Remove the active account from all pages. It is no longer used as a filter.
- Scope (searched 2026-10-28):
  - UI selector: `Layout/RightPanel/AccountSwitcher.razor`, rendered by `RightPanelHost.razor:8`. Only consumer of `AccountSelectionState`.
  - Service: `Services/AccountSelection.cs` (`AccountSelection.Resolve` and `AccountSelectionState`, localStorage key `vf.account.selected`). DI: `MauiProgram.cs:193`, `WebApp/src/Program.cs:532`.
  - Tests that register `AccountSelectionState` and must drop it: `AccountsPageTests.cs:95`, `AccountDetailsRemoveTests.cs:68`, `AccountDetailsPageTests.cs:158`, `SimpleModeLayoutTests.cs:55`, `OperatorGatedPagesTests.cs:69`. Delete `tests/Services/AccountSelectionTests.cs`.
  - Resource keys `Active Account` and `SwitchedActiveAccount` in `SharedResources.resx` (only the switcher uses them).
  - Backend `IForensicsConfiguration.ActiveProviderAccountId` (decision: include backend, pending confirmation). It is more than a filter:
    - Persisted as `AppSetting.ActiveProviderAccountId` (`data/common/.../AppSetting.cs:10`), with a DB column in every migration snapshot. Removing it needs an EF migration.
    - Download attribution: `VideoDownloadServiceAdapter.cs:197,441,462,696` and `EventPullService.cs:134` and `SnapshotSyncService.cs:61` pass the account to `SetActiveProviderAccountId`. The `IMediaDownloadService` comment says downloads fall back to a synthetic placeholder account without it. Removing it changes which account downloaded devices attach to.
    - Written by `AuthForm.razor:98`, `AddAccountWizard.razor:171`, `AccountDetails.razor:184`, and selftest `Program.cs:456`. Ring's `RingMediaDownloadService` and `RingDeviceIdentityResolver` cache it.
    - Decision (user, 2026-10-28): downloads run on a scheduler. Attribution comes from the scheduled job's account, not from a stored "active" account.
    - What already exists: `ScheduledSyncService` (`Hosting/BackgroundServices/ScheduledSyncService.cs`) runs per account. It builds `EventPullService` per account and calls `SetActiveProviderAccountId(accountId)` from the job's own account (`EventPullService.cs:134`, `SnapshotSyncService.cs:61`). The scheduled path does not need the stored active account.
    - What still reads the stored account: the interactive download path, `VideoDownloadServiceAdapter.cs:197,441,462,696`. Plan: route manual downloads through the same scheduler (one-off job per account), then remove the stored field and its EF migration.
    - Open: confirm that manual downloads should become scheduled jobs, rather than staying direct calls that take an account argument.
    - Mapped 2026-10-28 (no edits yet):
      - `VideoDownloadServiceAdapter` (Client.Core) is the manual path. `DownloadVideosAsync` (`:415`) reads the stored account at `:441` and stores it in `_currentBatchAccountId`, which also drives the watermark update at `:647-651`. `DownloadSnapshotsAsync` (`:670`) reads it at `:696`. Progress is polled through `GetProgress`, `DrainActivityLog`, `GetRemainingCount`, and `GetCurrentDevice` (`:832-933`), which the UI reads.
      - `ScheduledSyncService` runs per-account recurring jobs from `SyncSchedule` rows (`RunRollupAsync`, `RunSnapshotSyncAsync`, `RunEventSyncAsync`). It has no one-off job queue, so one-off manual downloads need new persistence (a job row or queue) and a runner.
      - `RemoteVideoDownloadService` (Hosting) POSTs manual downloads to the server, so the client/server split (`client-split.md`) applies: the job model and its API DTOs live on the server, and the client keeps the progress poll.
      - Scope is larger than one batch: new job entity and migration, a runner in `ScheduledSyncService`, API routes and DTOs, and the progress UI moved from direct calls to job status polling. Recommend a separate design doc before code.
  - Open: clear the stored `vf.account.selected` key on upgrade, or leave it as dead localStorage.

## Open
- None for Step 2 layout. Data sources for provider events and anomalies still need locating before the Events and Detail tabs are built.

## Data sources
- Clip pages: `/api/v1/media-items/page` (Part 2, SQL paging).
- Camera list and event bands: name the `IObservable<T>` contract in `Contracts/` for each before building the UI. Not yet named.
- Events and Detail tabs: no data source defined yet. Decide scope before Step 2.
- Filter and Actions: unspecified. Mark as out of scope for the first PR unless defined.

## Rules to follow
- `ui-shared.md`: every new string in `SharedResources` or the page `.resx`, including top-bar labels (Back, Live, Filter, Actions, tab names, range text). Desktop-first; `MobileLayout` handles narrow screens.
- `client-live-data.md`: new data on the page comes from an `IObservable<T>` source, subscribed in `OnInitializedAsync`, disposed in `Dispose`. Refresh through `InvokeAsync`.
- `client-split.md`: Ui.Shared uses `IMediaContentUrlProvider` and the API, not local disk.
- `csharp.md`: CancellationToken on async methods; log failures with context via the error message and exception type, not stack traces.
- Authorization: the Review role gate on grab-still (`MediaViewer.razor:141-149`) must carry over to the new player.
- Right panel: `RightPanelHost` already occupies the right side; decide whether the vertical timeline goes in the panel, in the body, or replaces it.

## Open questions
1. Resolved: hide the whole left nav on `/media` (user choice).
2. Does the Evidence list stay at `/media` or move to a new page?
3. Preview strip (Part 3) before or after the layout rework? (Plan assumes layout first, with a placeholder strip: hidden, or a localized empty state.)
4. Route: `/media` or a new page? Confirm which paths `NoticeActions.ReviewLink` and bookmarks use before moving anything.
5. Step 1 ships independently of Steps 2 and 3.
