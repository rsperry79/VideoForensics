# Plan: platform split and media viewer

Status (audited 2026-10-28): Parts 1, 2, and 3 are implemented and merged to `wip`. Remaining: verify WebView2 range playback on the MAUI Windows head (no test or note exists in code), and the Frigate-style review page that uses these components (see `media-page-rework.md`, Step 2). The viewer components are built and tested but no page uses them yet. Targets: Windows desktop (MAUI) and the web server (WebApp). iOS and Android are TODO.

## Status (2026-10-25)

Reviewed by a read-only Sonnet pass against wip. Findings are recorded per item below.

### Part 1: done
- `Platform.Windows` holds the Windows implementations: storage overrides, TPM and DPAPI key storage, Event Log, directory ACL, firewall, update installer, OneDrive paths, WebView2 locator, hotswap and dev launcher.
- Common seams: `ILicenseKeySource`, `IBackupStagingLocator`, `IFirewallRuleManager`, `IUpdateInstaller`.
- Core seams: `IStorageOverrideSource`, `IMachineGuidSource`, `IDataDirectoryAccess`, `IKeyStorageProvider` platform registration, and a delegate for the Event Log.
- Heads register the Windows implementations: WebApp, MAUI, dbrepair, dbsetup, selftest.

### Part 1: not done or deviated
- `Platform.Web` was created in `e34882b5` (2026-10-24): `src/client/platform/VideoForensics.Client.Platform.Web` holds `LinuxUpdateInstaller` and has a test project; `WebApp.csproj` references it. The media range code stays in WebApp, which is a head, so it is allowed there.
- Remaining platform calls in shared layers, found by review:
  - `Ui.Shared/Pages/ImportExport.razor` (`File.*`, lines near 143, 191, 195).
  - `Ui.Shared/Dialogs/FolderBrowserDialog.razor:53` and `Services/DirectoryBrowserService.cs:18,28`.
  - `Hosting/BackgroundServices/UpdateCheckService.cs:347` (`LocalApplicationData`, Linux download path).
  - `Client.Core/Tools/ConfigToolsOrchestrator.cs:205` (`UserProfile`).
  - `Client.MCP/ServerDiscovery/FileApiKeyStore.cs:24` (`LocalApplicationData`). Decision: stays in MCP.
  - `Core.Helpers` `StorageLocationProvider` and `PlatformDirectoryService` (`OperatingSystem.Is*`). Core is the generic default; Windows branches are the registered overrides.
- Not verified: Uniview `Process.Start` calls; whether Ring ffmpeg calls go through `IFfmpegToolLocation`; cloudflared and installer-asset seams; `.snk` and csproj naming for new projects.

### Part 2: done
- Backend: `GetPageByDevicesAndDateRangeAsync` (SQL paging, purged excluded, take 1-500); `/api/v1/media-items/page` with validation; media rate limit 30 per tile per minute across 9 tiles.
- Common: `ReviewTimeline` (time to pixel, pixel to time, `ClipAt`), `CameraSync` (`DriftSeconds`, `NeedsResync`, `MaxTiles` = 9).
- Ui.Shared: `ReviewPlayer`, `TimelineStrip`, `CameraGrid` with bUnit tests; strings localized.
- Decided: playback across clips is a playlist (no server stitch); thumbnail strip uses existing `ThumbnailPath`; max tiles 9; audit records one entry per clip open (range requests not starting at byte 0 are skipped).

### Part 2: resolved (merged to `wip`; the `feat/finish-parts-1-2` branch no longer exists)
- Grid drift resync: `CameraGrid` checks each tile against tile 0 once a second (`CameraSync.NeedsResync`), and seeks a drifting tile. Commit `866a4dac`.
- 206 and 416 range tests on `/media/{id}/content`: `MediaContentRangeTests`. Commit `03875b3e`.
- `RemoteMediaItemRepository` page query test: commit `74c7f729`.
- Thumbnails on the timeline strip: `ClipWindow.ThumbnailUrl`, drawn as the segment background. Commit `255e8df4`.
- Still open: WebView2 range playback on the Windows head is unverified. This needs a manual check on the MAUI app.

### Part 1: decisions recorded
- `Platform.Web` now exists (see above); it holds server-only platform code (`LinuxUpdateInstaller`).
- `DirectoryBrowserService` drive enumeration moved behind `IFileSystemRootProvider` (Common). Windows drives in `Platform.Windows`, POSIX root in Core. Commit `06b96c88`.
- Accepted as portable .NET, not platform code: `ImportExport` `File.*`, `ConfigToolsOrchestrator` user profile and Pictures folder, `Ring` and `Uniview` `Process.Start` (ffmpeg paths come from `IFfmpegToolLocation`, which I did not re-verify in every call site).
- `UpdateCheckService` Linux download path (`LocalApplicationData`) is Linux-only. Accepted for now.
- `FileApiKeyStore` (MCP) stays in MCP, as decided.

### Part 3: done (audited 2026-10-28)

## Goals
1. Split client code into a common layer and per-platform sublibs.
2. Build a Frigate-style review viewer: single-camera and multi-camera, native HTML5 `<video>` with a custom timeline strip.

Part 1 (platform split) comes first. The viewer is built on the common layer.

## Current state (as found)
- `VideoForensics.Client.Common` (net10.0): `Contracts/`, `ProviderType.cs`.
- `VideoForensics.Client.Core` (net10.0): `Contracts/`, `Licensing/SyncfusionLicenseRegistrar`, `Services/`, `Tools/`, `Utilities/`, `ConfigurationLoader.cs`.
- `VideoForensics.Ui.Shared` (net10.0): Razor pages and components, shared by MAUI and WebApp.
- `VideoForensics.MauiApp`: Windows target `net10.0-windows10.0.19041.0`. An iOS target exists behind `BuildIOS`; out of scope for now.
- `VideoForensics.WebApp` (net10.0): ASP.NET host. Serves Ui.Shared to the browser. Has `Api/`, `Hubs/`, `Services/`, `Infrastructure/`.
- Platform-specific code found by grep (non-test): `SyncfusionLicenseRegistrar` (`%ProgramData%` path), `FfmpegPathResolver` use in `Tools/`, `FileServerLocationSettingsStore` in MCP, `Program.cs` in WebApp and MauiProgram.
- Recordings: downloaded MP4 clips indexed in `MediaItem` (`FilePath`, `DeviceId`, `RecordedAtUtc`, `DurationSeconds`, `ThumbnailPath`).
- Browser video already works: `GET /api/v1/media/{id}/content` streams with range processing (`WebApp/src/Api/MediaApiEndpoints.cs:70,212`). Per-item tickets come from `POST /media/tickets` (`:66`), via `IMediaAccessTicketService` and `IMediaContentUrlProvider`. `Ui.Shared/src/Components/Evidence/MediaViewer.razor:98` already has a `<video>`.
- Clip listing: `GET /api/v1/media-items?deviceId&from&to` is unpaged (breaks `.claude/rules/data.md`); the paged `GET /api/v1/media-items/page` was added in Part 2 and is the one to use.

## Part 1: platform split

### Projects
- `VideoForensics.Client.Platform.Windows` (new): Windows-only code. Paths under `%ProgramData%`, ffmpeg resolution, license file location, MAUI Windows services.
- `VideoForensics.Client.Platform.Web` (new): server-side concerns for the WebApp. HTTP media serving with range requests, server storage paths. No browser APIs (those stay in Ui.Shared).
- `VideoForensics.Client.Common` and `Ui.Shared`: no platform APIs. Platform code reached only through interfaces defined in Common.

### Rules
- A platform API may appear only in a `Platform.*` project or a head (`MauiApp`, `WebApp`).
- Reuse existing seams before adding new ones: `IFileDialogService`, `IAppLockPreferencesStore`, `ILocalAuthGate`, `IServerLocationSettingsStore`, `ISyncfusionLicenseRegistrar`, `IFfmpegToolLocation`, `IMediaStorageProvider` (in `data.common`, not Common), `IMediaContentUrlProvider`.
- New seams only where none exists: `ILicenseKeySource` (extracted from `SyncfusionLicenseRegistrar.cs:91-94`). Extend `IFfmpegToolLocation` instead of adding `IFfmpegLocator`.
- Heads register the matching set.
- Adding iOS or Android later = one new `Platform.*` project plus one head registration. No change to Common or Ui.Shared.

### Known platform-specific code (from review; not exhaustive)
- Core: `Licensing/SyncfusionLicenseRegistrar.cs:93` (ProgramData); `Tools/ConfigToolsOrchestrator.cs:204,215` (SpecialFolder); `Tools/LocalRingSelfTestService.cs:103`; `Utilities/PathUtilities.cs:1,45,74,96-135` (Microsoft.Win32 registry, largest miss); `Services/ForensicsConfigurationService.cs:31-32` (ffmpeg).
- Hosting: `BackgroundServices/UpdateCheckService.cs:383`; `VideoForensicsHostingExtensions.cs:278,458,488,549` (ffmpeg wiring, db path); `CloudflaredTunnelService.cs:98`; `NetworkTierConfigReader.cs:40` (registry).
- Ui.Shared (breaks "no platform APIs"): `Pages/ImportExport.razor:121-122,146,193-198` (MyDocuments, temp, File.*); `Dialogs/FolderBrowserDialog.razor:53`.
- MCP: `FileServerLocationSettingsStore`, `FileApiKeyStore`.
- Already isolated in MAUI: `MauiAppLockPreferencesStore`, `MauiServerLocationSettingsStore`, `MauiFileDialogService`, `MauiProgram.cs:86`.

### Steps
1. Inventory every platform-specific call in Core, Hosting, MCP, Ui.Shared, MauiApp, WebApp. Record file:line. Start from the list above.
2. Define platform interfaces in Common for each one found.
3. Move implementations into `Platform.Windows` and `Platform.Web`. Behaviour unchanged.
4. Re-point heads to the new projects.
5. Tests: unit tests per interface implementation. Existing tests must still pass.

### Dependency rules
- `Platform.Web` is referenced only by WebApp. Never by MauiApp or Ui.Shared.
- MauiApp and WebApp both reference `Hosting`, which holds ffmpeg wiring. See Decision: Hosting below.
- Non-test code follows TDD: write failing tests first.
- New public interfaces need XML docs. Log failures with context.
- MAUI must go through `RemoteMediaContentUrlProvider` (the API), not local disk.

### Decision: Hosting
- `Hosting` is **not** a platform project. It is the backend composition root (providers, data, SQLite, realtime, auth). MauiApp runs it in-process and WebApp uses it, so it stays shared.
- Platform-specific pieces inside it move out behind seams. Hosting keeps the interface and a generic default. The platform project supplies the real implementation.
  - `FirewallRuleManager.cs` (netsh): already an interface with a non-Windows no-op. Move the netsh impl to `Platform.Windows`.
  - `NetworkTierConfigReader.cs:40-42` (ProgramData and registry override): take the config root from the platform.
  - `CloudflaredTunnelService.cs:98` (exe lookup): platform supplies the exe locator.
  - `UpdateCheckService.cs:304-317` (picks `.exe` asset) and `:383` (LocalApplicationData): platform supplies the installer asset selector and the app data root.
  - `VideoForensicsHostingExtensions.cs:278,458,488,549` (default db path under ProgramData, ffmpeg wiring): platform supplies the data root and ffmpeg locator.
- Each head registers its platform project in its own `Program.cs` or `MauiProgram.cs`. Hosting never references a platform project.
- Known layering issue, out of scope: `Hosting` references `Ui.Shared`. Fix separately.

### Decision: PathUtilities
- `Client.Core/src/Utilities/PathUtilities.cs` (Windows registry, OneDrive and shell folders) moves to `Platform.Windows`. Core keeps no Windows code. Core code that needs these paths gets them through an interface defined in Common.

### Open questions
- Does `Client.MCP` belong to Web, Windows, or neither?
- Namespace, csproj naming, `.snk` signing for new projects, and solution folder under `src/client/`.
- net10.0 or net10.0-windows for `Platform.Windows`.

## Part 2: media viewer

### Backend
- Reuse `/media/{id}/content` and the ticket flow. Do not add a new stream endpoint.
- Batch tickets for a playlist or multi-camera view: `POST /media/tickets` already accepts several items. Confirm TTL per item.
- Clip listing: add paged, sorted, multi-device repository methods with SQL-side paging. Do not copy the existing unpaged `GetByDeviceAndDateRangeAsync` / `ListAsync` pattern (`data/common/src/Contracts/IMediaItemRepository.cs:12,18`).
- Audit: `MediaApiEndpoints.cs:182-205` logs each clip view. A scrub would flood the log. Decide on one audit entry per session.
- Rate limit "media" must cover N concurrent tiles.
- Time zones: `RecordedAtUtc` is UTC. Convert only in the UI.

### Frontend (Ui.Shared)
- `ReviewPlayer` component: one `<video>`, source switched by clip.
- `TimelineStrip` component: draws clips and events on a time axis. Click or drag seeks. Built with CSS/SVG, no Syncfusion control needed.
- `CameraGrid` component: N `<video>` tiles sharing one master clock. Each tile resyncs to master when drift exceeds a threshold.
- Clip stitching across consecutive clips in one camera: decide between a playlist of clips or a server-built continuous file (`FfmpegTimelineVideoBuilder` exists; check what it does before choosing).
- Syncfusion used only for the surrounding controls (`SfDropDownList`, `SfButton`, `SfSlider`).
- All new user-visible strings use `IStringLocalizer<SharedResources>` and `.resx` (`.claude/rules/ui-shared.md`). Add keys in the same change.
- Stitching: `ITimelineVideoBuilder` / `FfmpegTimelineVideoBuilder` (`core/providers/core/src/FfmpegTimelineVideoBuilder.cs:35`, registered at `data/core/.../ServiceCollectionExtensions.cs:38`) runs ffmpeg server-side. If used, it belongs in `Platform.Web` only. Read it before choosing playlist vs stitch.

### Open questions
- Playback across clip boundaries: playlist or server stitch?
- Thumbnail strip: use existing `ThumbnailPath` or generate sprites?
- Multi-camera max tile count.
- Does the WebView on the Windows head play MP4 range requests the same as a browser? Verify.

## Part 3: preview strips (single-camera view): implemented

Status: all of this is done. `MediaItem.PreviewPath` with migration `AddMediaItemPreviewPath`; `MediaPreviewService` (320 px, 1 fps, `.preview.mp4`, temp file moved into place after ffmpeg succeeds); generation after download in `EventPullService` (two at a time, failures logged and not failing the pull); `MediaPreviewBackfillService`; `GET /api/v1/media/{id}/preview` with range requests and tests; `SingleCameraView` with 6 tests. The text below is the original plan, kept for reference. The preview strip is not on any page yet.

Goal: in single-camera view, show thumbnail previews of the other cameras for the same time window. Clicking one switches the main player to that camera at the same time offset.

### Existing pieces to reuse
- `FfmpegTimelineVideoBuilder` already runs `fps=1,scale=W:H:force_original_aspect_ratio=decrease,pad=...` (`core/providers/core/src/FfmpegTimelineVideoBuilder.cs:115-117`). Reuse that filter chain.
- `FfmpegClipColorAnalyzer` (`FilterChain` with `fps=1`) shows the same 1 fps pattern.
- `IMediaThumbnailService` / `MediaThumbnailBackfill` (`data/core/src/Services/`) store one still per clip in `ThumbnailPath`. Mirror that pattern for the preview.
- `EventPullService` (`client/host/VideoForensics.Hosting/src/Services/EventPullService.cs:156-162`) downloads clips per device. Preview generation goes right after a successful download.

### Format
- One H.264 MP4 per clip, 1 fps, no audio, `yuv420p`, even dimensions.
- Width: 320 px (strip tile width, decided). Height `-2` keeps aspect ratio.
- File next to the clip: `<name>.preview.mp4`.
- Column `PreviewPath` on `MediaItem` (typed, not JSON, per `data.md`). Migration required.

### Pull-time step
1. After `DownloadVideosAsync` succeeds for a device, generate previews for the clips just downloaded. Skip clips that already have one.
2. Run ffmpeg through `ProcessRunner`, with a concurrency cap (e.g. 2 at a time) so a large pull cannot overload the server.
3. Failures are logged with clip context and do not fail the device pull. This matches how config history is handled in `EventPullService`.
4. Backfill job for existing clips, following `MediaThumbnailBackfill`.

### Serving
- `GET /api/v1/media/{id}/preview`, same ticket and range-request pattern as `/media/{id}/content` (`MediaApiEndpoints.cs:70,212`).
- Previews are small; clients may fetch many at once. Cache headers allowed, since the file is immutable once written.

### Size estimate (to verify)
- At 1 fps, a 10-minute clip is about 600 frames. At small width, expect a few MB or less per clip. Measure on a real clip before committing to a width.

### UI
- `SingleCameraView` (new, Ui.Shared): main `<video>` on top, preview strip of other cameras for the same window below. Clicking a tile swaps the main source and seeks to the same offset.
- Strip tiles are muted and loop. The main player uses the full clip.
- All strings localized (`.resx`).

### Decided
- Multi-camera grid tiles use the same 320 px preview asset as the strip. One preview per clip, no second size.

### Open questions
- None for Part 3.

## Tests
- Common: sync math (master clock, drift correction), timeline time-to-pixel mapping, clip lookup for a time.
- Platform projects: each interface implementation.
- Ui.Shared: bUnit tests for ReviewPlayer and TimelineStrip seek behaviour, following existing page tests.
- Endpoints: range request returns 206 with correct `Content-Range`.

## Out of scope now
- iOS and Android sublibs.
- Live view changes.
- Any Syncfusion Scheduler or Charts use.
