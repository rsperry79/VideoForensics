# VideoForensics Consolidation & Reorganization Plan

## Context

This plan consolidates media collection flows, reorganizes the solution structure, adds new utility tooling, and restructures the navigation menu to create a more intuitive Evidence-centric workflow. The changes address:

- **User experience**: Single consolidated page for media collection with shared console output
- **Data strategy**: Automatic event/snapshot pull on account connect + manual refresh buttons
- **Project organization**: Unified `src/utils` folder for all CLI tools with consistent naming
- **Observability**: New GUI logger viewer for SuperAdmin debugging (Windows localhost)
- **Navigation clarity**: Evidence becomes the hub (with Analyze, Chat, dates, device filters); Admin consolidates config/security

---

## Changes by Category

### 1. Consolidate Media Collection (Single Page + Shared Console)

**Scope:** Merge `/collect/videos` and `/collect/snapshots` into a single page with device/account detection

**Strategy:** Track last successful collect timestamp per account; default to collecting anything new since that timestamp. First-time collect defaults to last 7 days.

**Files to create/modify:**
- Modify: `ProviderAccount` entity (data model)
  - Add field: `LastCollectCompleteUtc` (nullable DateTime) - tracks when last collect finished successfully
  
- New: `src/client/ui/VideoForensics.Ui.Shared/Pages/Collect.razor` (unified page)
  - Device/account selector dropdown
  - Start/end date pickers
    - **Default behavior:** Start date = `LastCollectCompleteUtc` (or last 7 days if first-time); End date = now
  - Device filter checklist with device type filtering:
    - Show only devices from active account
    - **For video collection:** Show all video-capable devices
    - **For snapshot collection:** Filter to camera devices only (devices with snapshot capability)
  - Force full rescan checkbox
  - Tabs or toggles to select collection type:
    - **Videos tab:** Show all devices, collect videos
    - **Snapshots tab:** Show only camera devices, collect snapshots
  - Single "Collect All" button (pulls selected media type from applicable devices)
  - Unified progress console (monospace output, shared DownloadProgressPanel)
  - Both collection types can be run separately or in sequence
  - **On successful completion:** Update `ProviderAccount.LastCollectCompleteUtc` to current time
  
- Modify: `src/client/ui/VideoForensics.Ui.Shared/Components/DownloadProgressPanel.razor`
  - Remove device-specific path display; hide filesystem paths
  - Show only: file count progress, device name, collection type (Videos/Snapshots), eta
  - Consolidate error/warning output into single shared log
  
- Delete: `src/client/ui/VideoForensics.Ui.Shared/Pages/CollectVideos.razor`
- Delete: `src/client/ui/VideoForensics.Ui.Shared/Pages/CollectSnapshots.razor`
- Modify: `src/client/ui/VideoForensics.Ui.Shared/Layout/NavGroups.cs`
  - Update Evidence group: replace `/collect/videos` and `/collect/snapshots` with single `/collect` entry

**API changes:** None required (endpoints already exist separately)

**Testing:**
- Verify both video and snapshot collection work on merged page
- Verify device selector filters to active account only
- Verify shared console output displays both collection types correctly
- Verify navigation links work in NavGroups

---

### 2. Automatic Event/Snapshot Pull on Account Connect + Manual Refresh

**Scope:** Pull events/snapshots when user connects account; expose manual refresh on account details page

**Strategy:**
- On successful account auth → trigger background pull of all events since last successful pull
- Track `ProviderAccount.LastSuccessfulAuthUtc` and pull from that timestamp onward
- First-time connect: pull all available data from provider
- Add "Sync Now" button to AccountDetails.razor to manually trigger pull
- Pull should be non-blocking (fire-and-forget with error notification)
- Also pull device config on account connect and sync now

**Files to modify:**
- `src/client/web/VideoForensics.WebApp/Api/AccountEndpoints.cs`
  - After successful auth (in `POST /api/v1/accounts/add`), call new `EventPullService.PullAccountEventsAsync()` 
  - Parameters: account ID, pull from LastSuccessfulAuthUtc (or null for first-time = all data), include device config

- New: `src/client/host/VideoForensics.Hosting/Services/EventPullService.cs`
  - `PullAccountEventsAsync(accountId, fromTimestampUtc, cancellationToken)` 
  - If fromTimestampUtc is null: pull all available data from provider
  - If fromTimestampUtc is set: pull from that timestamp to now
  - Calls IEventAndConfigService.GetEventsAsync for all devices in account
  - Also calls GetDeviceConfigAsync for each device
  - Stores error in ProviderAccount.LastErrorMessage if pull fails
  - Records success timestamp in ProviderAccount.LastSuccessfulAuthUtc

- Modify: `src/client/ui/VideoForensics.Ui.Shared/Pages/AccountDetails.razor`
  - Add "Sync Now" button next to account name
  - On click, POST to new endpoint: `POST /api/v1/accounts/{id}/sync-now`
  - Show spinner during pull; toast on completion or error

- New endpoint: `src/client/web/VideoForensics.WebApp/Api/AccountEndpoints.cs`
  - `POST /api/v1/accounts/{accountId}/sync-now` 
  - Calls EventPullService.PullAccountEventsAsync
  - Returns 200 on success, error code if pull fails

**Testing:**
- Link new account → verify events and device config are pulled automatically
- Click Sync Now → verify events and config refreshed
- Verify no UI blocking during pull (async/await pattern)
- Verify errors are captured and displayed in AccountDetails

---

### Infrastructure: Named Pipe Logging (Supporting item 4)

**Scope:** Enable configurable NamedPipe logging in server; LoggerViewer consumes live stream with save capability

**Architecture:**
- **Configurable:** NamedPipe logging can be enabled/disabled via server settings (default: enabled on Windows)
- **Pipe name:** `\\.\pipe\VideoForensics.Logger`
- **Format:** Newline-delimited JSON entries with timestamp, level, message
- **Buffer size:** Keep last 500 entries in memory (circular buffer, ~5MB typical) so late-connecting LoggerViewers see recent history
- **Max pipe size:** Windows named pipe 64KB buffer (system default, reasonable for real-time streaming)

**Server-side implementation:**
- Modify: `src/core/core/core.logging/` (add NamedPipeLoggerProvider)
  - Create named pipe on server startup only if enabled in config (Windows platform)
  - Write all log entries to pipe asynchronously (non-blocking, don't slow down app if reader disconnects)
  - Handle client disconnect gracefully (keep writing, wait for reconnect)
  - Use circular buffer (500 entries max) to retain recent entries for new connections
  - If on non-Windows platform or disabled: named pipe disabled (graceful no-op)

- Modify: `src/client/host/VideoForensics.Hosting/DependencyInjection/` or logging setup
  - Add NamedPipeLoggerProvider to logging pipeline conditionally
  - Configuration setting: `Logging:NamedPipeEnabled` (boolean, default true on Windows)

- Modify: Error handling for failed account syncs
  - When `EventPullService.PullAccountEventsAsync()` fails: log as **Error** level (will appear in LoggerViewer)
  - Include account name, error message, and timestamp

**Client-side (LoggerViewer) features:**
- LoggerViewer is Windows-only (WPF requires Windows)
- **Live display:** Real-time scrolling list of log entries (auto-scroll to bottom)
- **Level filter:** Dropdown (All, Debug, Info, Warning, Error, Critical)
- **Clear button:** Clear current display (doesn't affect server-side logs)
- **Save button:** Export visible logs to file (CSV or TXT with timestamp, level, message)
- **Connection status:** Display in title bar or footer (Connected/Disconnected/Reconnecting)
- Auto-reconnect on disconnect (exponential backoff, max 30s)
- Should not be integrated into MAUI on non-Windows platforms
- MAUI launcher: check `RuntimeInformation.IsOSPlatform(OSPlatform.Windows)` before showing "Dev Tools → Logger Viewer" menu item

**Security:**
- Named pipe permissions: Restrict to `SYSTEM` and local `Administrators` group (Windows ACLs)
- LoggerViewer must run with sufficient privileges to connect (Admin or same user as server)
- Consider allowing authenticated app users to read logs (not just Admins) if security allows

**Packaging:**
- LoggerViewer.exe installed as part of `VideoForensics.Utils.LoggerViewer` package/installer
- Server (WebApp) ships with NamedPipeLoggerProvider built-in (no separate install)

**Testing:**
- Start server with named pipe enabled → verify pipe created and listening
- Connect LoggerViewer → verify recent log entries displayed (up to 500)
- Generate log entries (auth, data pull, etc.) → verify appear in real-time
- Trigger account sync failure → verify appears as Error level in LoggerViewer
- Filter by Error level → verify only error entries shown
- Click Save → verify export file created with correct format
- Disconnect LoggerViewer → verify server continues logging
- Reconnect LoggerViewer → verify sees recent backlog
- Disable named pipe in config → restart server → verify LoggerViewer can't connect
- Verify LoggerViewer not shown in MAUI on non-Windows platforms

---

### 3. Reorganize Utils Folder (SelfTest, DbRepair, DbSetup, Diagnostics)

**Scope:** Consolidate all CLI tools under single `src/utils` folder with consistent naming

**Migration plan:**
```
Before:
  src/selftest/VideoForensics.Providers.Ring.SelfTester.csproj
  src/tools/DbSetup/DbSetup.csproj
  src/tools/DbRepair/DbRepair.csproj
  src/tools/VideoForensics.Diagnostics/VideoForensics.Diagnostics.csproj

After:
  src/utils/
    selftest/
      VideoForensics.Utils.SelfTest.csproj (rename from SelfTester)
    setup/
      VideoForensics.Utils.DbSetup.csproj (rename from DbSetup)
    repair/
      VideoForensics.Utils.DbRepair.csproj (rename from DbRepair)
    diagnostics/
      VideoForensics.Utils.Diagnostics.csproj (rename from Diagnostics)
    logger/
      VideoForensics.Utils.LoggerViewer.csproj (new - see item 4)
```

**Namespace updates:**
- `VideoForensics.Providers.Ring.SelfTester` → `VideoForensics.Utils.SelfTest`
- `VideoForensics.DbSetup` → `VideoForensics.Utils.DbSetup`
- `VideoForensics.DbRepair` → `VideoForensics.Utils.DbRepair`
- `VideoForensics.Diagnostics` → `VideoForensics.Utils.Diagnostics`

**Files to modify:**
- Rename and move all .csproj files as above
- Update all `RootNamespace` in .csproj files
- Update using statements in all .cs files for renamed namespaces
- Update VideoForensics.sln project references
- Update any build scripts that reference old paths

**Testing:**
- Verify each tool still compiles and runs from new location
- Verify namespaces are correct
- dotnet build on whole solution succeeds

---

### 4. New Logger Viewer GUI App (Named Pipe Consumer)

**Scope:** New Windows WPF desktop tool to consume logger named pipe in real-time; integrate into MAUI for SuperAdmin on localhost

**Architecture:**
- Server (WebApp) opens named pipe for logging: `\\.\pipe\VideoForensics.Logger`
- Logger writes entries as newline-delimited JSON: `{ "timestamp": "...", "level": "...", "message": "..." }`
- LoggerViewer connects to pipe and displays **live incoming messages** in real-time

**New projects:**
- `src/utils/logger/VideoForensics.Utils.LoggerViewer.csproj` (WPF desktop app)
  - Connects to named pipe on startup
  - Displays log entries in scrollable list with auto-scroll to bottom (live stream)
  - Shows: Timestamp, Level (colored), Message
  - Filter by level dropdown (All, Debug, Info, Warning, Error, Critical)
  - Clear button to clear current display
  - Auto-reconnect on disconnect (reconnect with exponential backoff, max 30s)
  - Show connection status in title bar or footer (Connected/Disconnected/Reconnecting)

- `src/core/core/core.logging/NamedPipeLoggerProvider.cs` (new or extend existing)
  - Opens named pipe sink for logging infrastructure
  - Writes JSON-formatted entries asynchronously
  - Graceful handling of pipe disconnects

**MAUI integration:**
- Modify: `src/client/maui/VideoForensics.MauiApp/MauiProgram.cs`
  - Add menu item: "Dev Tools → Logger Viewer" (visible only if SuperAdmin role + server URL is localhost)
  - On click, launch: `start http://localhost:5000/tools/logger-viewer` (or embed WPF viewer in MAUI if preferred)

- New endpoint: `src/client/web/VideoForensics.WebApp/Pages/LoggerViewer.razor`
  - HTML version of logger viewer (JavaScript auto-fetches from named pipe streaming endpoint)
  - Or: desktop WPF app launched separately

**Testing:**
- Start server, run LoggerViewer
- Generate log entries (e.g., authenticate an account)
- Verify entries appear in real-time
- Verify filter dropdown works
- Verify reconnect on pipe disconnect works

---

### 5. Evidence Sidebar: Date Range + Device Dropdown with Subpage

**Scope:** Add structured date/device filtering to Evidence page, with dedicated subpage for date picker

**Current state:** ScopeRail already has From/To date inputs + quick buttons + device checklist. Need to refactor for clarity.

**Files to modify:**
- Modify: `src/client/ui/VideoForensics.Ui.Shared/Components/ScopeRail.razor`
  - Remove inline date pickers (From/To inputs) from ScopeRail
  - Add "Dates" button that navigates to `/evidence/dates-filter`
  - Keep device checklist in ScopeRail
  - Keep quick range buttons (24h, 7d, 30d, Reset)

- New: `src/client/ui/VideoForensics.Ui.Shared/Pages/EvidenceDatesFilter.razor` (`/evidence/dates-filter`)
  - Back button (returns to Evidence)
  - Quick presets: Last 24h, Last 3 days, Last 7 days, Last month, Custom
  - Custom section: two SfDatePickers (Start date, End date) + Apply button
  - On apply, update scope state and navigate back to `/evidence`

- Modify: `src/client/ui/VideoForensics.Ui.Shared/Layout/NavGroups.cs`
  - Add `/evidence/dates-filter` as child route under Evidence (hidden from nav, only via button)

**Testing:**
- Click "Dates" button on Evidence page → navigate to filter page
- Select "Last 7 days" quick preset → back to Evidence, verify scope updated
- Select custom dates → back to Evidence, verify scope updated
- Verify date state persists when navigating away from Evidence and back

---

### 6. Move Analyze under Evidence Menu

**Scope:** Move Analyze from top-level tab to submenu under Evidence

**Current state:** Analyze is a top-level nav group with forensic reports, signal anomalies, etc.

**Files to modify:**
- Modify: `src/client/ui/VideoForensics.Ui.Shared/Layout/NavGroups.cs`
  - Move all Analyze subitems (Forensic Reports, Signal Anomalies, Access Control, Jamming) under Evidence group
  - Remove top-level Analyze tab
  - Evidence group now contains: Evidence, Collect, Dates Filter, Analyze (with subitems)

**Testing:**
- Verify Evidence tab shows Analyze as submenu
- Verify all Analyze pages still load correctly
- Verify no routing breaks

---

### 7. Move Import/Export and Chat under Evidence; Move them from Sources

**Scope:** Reorganize navigation: Evidence absorbs Import/Export and Chat; Sources becomes simpler

**Current state:**
- Sources tab has: Provider Accounts, Device Configuration, Query API, Chat, API Tester, Import/Export
- Analyze is separate tab
- Evidence is separate tab

**Target state:**
- Evidence tab: Evidence, Collect, Dates Filter, Analyze (with subitems), Chat, Import/Export
- Sources tab: Provider Accounts, Device Configuration, Query API, API Tester
- (Analyze and Chat and Import/Export move out of Sources, Analyze moves under Evidence)

**Files to modify:**
- Modify: `src/client/ui/VideoForensics.Ui.Shared/Layout/NavGroups.cs`
  - Evidence group: add Chat and Import/Export items
  - Remove Analyze from top-level (moved under Evidence as submenu)
  - Remove Chat and Import/Export from Sources group
  - Sources group now contains: Provider Accounts, Device Configuration, Query API, API Tester

**Testing:**
- Verify Evidence nav shows all new items
- Verify Sources nav no longer shows Chat, Import/Export, Analyze
- Verify all pages load correctly from new locations
- Verify links/routes still work

---

### 8. Move Import/Export to Admin Menu

**Scope:** Move Import/Export from Evidence (new location from #7) to Admin/Settings

**Note:** Item #7 moves Import/Export under Evidence; this item moves it again to Admin. Clarify with user if Import/Export should be in Evidence or Admin (not both).

**Assuming Admin is the final destination:**

**Files to modify:**
- Modify: `src/client/ui/VideoForensics.Ui.Shared/Layout/NavGroups.cs`
  - Admin group: add Import/Export item
  - Remove Import/Export from Evidence group

**Testing:**
- Verify Admin/Settings nav shows Import/Export
- Verify page loads correctly

---

### 9. Fix Settings Page Break (LLM Settings URI Error)

**Scope:** Fix LLM settings loading error

**Issue:** LLM settings page shows "Failed to load settings: An invalid request URI was provided. Either the request URI must be an absolute URI or BaseAddress must be set."

**Root cause:** Client (MAUI/thin client) trying to load LLM settings without BaseAddress configured, likely in RemoteLlmService or similar HTTP client initialization.

**Files to investigate/modify:**
- `src/client/host/VideoForensics.Hosting/Remote/RemoteLlmService.cs` (or similar)
  - Verify HttpClient has BaseAddress set correctly
  - Ensure client can resolve server address before making request
  
- `src/client/ui/VideoForensics.Ui.Shared/Pages/Settings.razor` (or LLM-specific settings page)
  - Add error handling for missing server address
  - Display user-friendly message if server unreachable

**Testing:**
- Open LLM settings page
- Verify no URI errors
- Verify settings load correctly

---

## Implementation Order

1. **Phase 1: Navigation & Layout** (Day 1-2)
   - Reorganize NavGroups (items 6, 7, 8, partial)
   - Create EvidenceDatesFilter page (item 5)
   - Create merged Collect page (item 1)
   - Fix Settings page break (item 9)

2. **Phase 2: Data Fetching** (Day 3-4)
   - Create EventPullService (item 2)
   - Add sync endpoint and UI button (item 2)
   - Test account connect → auto pull flow

3. **Phase 3: Project Reorganization** (Day 5)
   - Move tools to utils folder (item 3)
   - Update namespaces and references
   - Verify all tools compile and run

4. **Phase 4: Logger Viewer** (Day 6-7)
   - Create LoggerViewer project (item 4)
   - Add named pipe support to logging infrastructure
   - Integrate into MAUI menu
   - Create web-based viewer if needed

---

## Critical Files

**Navigation:**
- `src/client/ui/VideoForensics.Ui.Shared/Layout/NavGroups.cs`

**UI Pages:**
- `src/client/ui/VideoForensics.Ui.Shared/Pages/Collect.razor` (new)
- `src/client/ui/VideoForensics.Ui.Shared/Pages/EvidenceDatesFilter.razor` (new)
- `src/client/ui/VideoForensics.Ui.Shared/Pages/AccountDetails.razor`
- `src/client/ui/VideoForensics.Ui.Shared/Components/ScopeRail.razor`
- `src/client/ui/VideoForensics.Ui.Shared/Components/DownloadProgressPanel.razor`

**Data Services:**
- `src/client/web/VideoForensics.WebApp/Api/AccountEndpoints.cs`
- `src/client/host/VideoForensics.Hosting/Services/EventPullService.cs` (new)

**Project Structure:**
- `VideoForensics.sln`
- All .csproj files in src/selftest and src/tools directories

**Logging:**
- `src/core/core/core.logging/` (add NamedPipeLoggerProvider)
- `src/utils/logger/` (new LoggerViewer project)

---

## Verification Steps

1. **Navigation reorganization**: Load each page, verify no 404s, verify menu structure matches intent
2. **Media collection**: Collect videos and snapshots from single page, verify shared console
3. **Event pull**: Link account → verify events auto-pulled; click Sync Now → verify manual pull works
4. **Date filtering**: Set custom dates on Evidence → apply → verify scope persists
5. **Utils reorganization**: Verify all tools compile from new location and run
6. **Logger viewer**: Start server, launch viewer, generate log entries, verify display and filtering
7. **Full solution build**: `dotnet build` with no warnings; `dotnet test` for relevant test projects

---

### 13. Fix Security Events Operator Display

**Scope:** Display human-readable operator name in security events log instead of ID

**Issue:** `/settings/security-events` shows operator field with non-human-readable value (likely ID or raw data)

**Files to modify:**
- `src/client/ui/VideoForensics.Ui.Shared/Pages/SecurityEvents.razor` (or SecurityEventsTable component)
  - Modify the operator column binding to display `operator.Name` or `operator.DisplayName` instead of raw operator ID
  - Ensure operator entity is eagerly loaded or use a lookup service to resolve ID → name
  
- `src/client/web/VideoForensics.WebApp/Api/SecurityEndpoints.cs`
  - If fetching security events via API, ensure endpoint includes operator name in response
  - Modify response DTO to include operator name: `new { EventId, Timestamp, OperatorName, Action, Details }`

**Testing:**
- Navigate to `/settings/security-events`
- Verify operator column shows human-readable names (e.g., "Alice Smith", "admin") instead of IDs or raw values
- Verify all security events display operator names correctly

---

### 14. Improve Infrastructure Encryption Tip (Closable + Generic Storage Paths)

**Scope:** Make encryption warning on `/settings/infrastructure` dismissible and reference generic storage locations

**Issue:** 
- Encryption tip is static and always shown; should be dismissible (closable)
- Tip references hardcoded `%ProgramData%\VideoForensics` but data may be in multiple locations

**Files to modify:**
- `src/client/ui/VideoForensics.Ui.Shared/Pages/Infrastructure.razor` (or similar infrastructure settings page)
  - Convert encryption tip from static alert to a closable tip/banner component
  - Add close button (X) that dismisses the tip
  - Store dismissal state in user preferences or local storage
  - Reword tip to reference "drive(s) where the data is stored" instead of hardcoded path
  - Include list of configured storage locations (database drive, download location drive)

**New tip text:**
```
Neither the database nor downloaded media files are encrypted by this application. 
An application-level database encryption approach (SQLCipher) was evaluated and found 
to silently fail to apply schema changes under this platform's current SQLite provider 
version - a data-integrity risk unacceptable for evidence storage, so it was not shipped. 
Until a safe approach is available, protecting this data at rest is the owner's 
responsibility: enable full-disk encryption (BitLocker on Windows, LUKS on Linux) on 
the drive(s) where the data is stored, and secure physical access to this machine.
```

**Storage location reference:**
- Show detected storage drives: Database location drive, Download location drive, etc.
- Update dynamically if user changes storage paths

**Testing:**
- Navigate to `/settings/infrastructure`
- Verify encryption tip displays with close button
- Click close button → tip dismissed and doesn't reappear (in same session)
- Navigate away and back → verify tip state persists (if stored in preferences)
- Verify tip references correct storage location(s), not hardcoded path

---

## Clarifications Resolved

1. **Import/Export location:** Admin menu (under Settings/Admin) ✓
2. **Settings page break:** LLM settings URI error (fixed in item 9) ✓
3. **Logger viewer:** WPF desktop app ✓
4. **Event pull scope:** All data since last successful pull (first-time = all available data) ✓
5. **Logger viewer display:** Live incoming messages over named pipe in real-time ✓

### 10. Updates: Add Channel Selection

**Scope:** Allow users to select update channel (stable, beta, nightly, etc.)

**Files to modify:**
- Modify: `src/client/ui/VideoForensics.Ui.Shared/Pages/Settings.razor` (Admin section)
  - Add new "Updates" section
  - Dropdown for channel selection: Stable | Beta | Nightly (or user-configured channels)
  - Current version display
  - "Check for Updates" button
  - Last check timestamp
  - Save selection to server settings

- Modify: `src/client/host/VideoForensics.Hosting/BackgroundServices/UpdateCheckService.cs`
  - Read selected channel from settings
  - Query update endpoint with channel parameter: `GET /api/v1/system/updates?channel={selected}`
  - Notify user when update available based on selected channel

- Modify: `src/client/web/VideoForensics.WebApp/Api/SystemEndpoints.cs` (or create if missing)
  - `GET /api/v1/system/updates?channel={channel}` endpoint
  - Return latest version available for requested channel
  - Compare against current version

**Testing:**
- Change update channel in Settings → verify setting persists
- Check for updates on different channels → verify correct version offered
- Verify update notifications respect channel selection

---

### 11. Health Dashboard

**Scope:** Single-page overview of system, database, device, and account health

**Target location:** New top-level or Admin menu item → `/health` or `/admin/health`

**Dashboard sections:**

1. **Server Health**
   - Status: Running/Offline
   - Uptime
   - CPU usage (%)
   - Memory usage (MB / Total)
   - Disk usage for database (%)
   - Named pipe logging: Connected/Disabled

2. **Database Health**
   - Connection pool status (active/total connections)
   - Database size (MB)
   - Last backup timestamp (if applicable)
   - Row counts: Events, Devices, Accounts, Evidence items
   - Slow query alerts (if any queries > 5s)

3. **Background Services**
   - Health sync service: Last run, next run, status (Idle/Running)
   - Update check service: Last run, next run
   - Event pull service: Last run timestamp, any pending pulls

4. **Provider Accounts**
   - Summary: Total linked, Active, Failed (showing auth errors)
   - Table: Account name, Provider, Status (Active/Error), Last auth, Last pull, Error message if failed
   - Quick action: Sync account button per row

5. **Devices**
   - Summary: Total discovered, Online/Offline, Low RSSI (< -80dBm)
   - Mini-map or list: Device name, Provider, Status, RSSI, Last event time

6. **Alerts / Recent Errors**
   - Last 10 errors from named pipe logger or error table
   - Color-coded: Error (red), Warning (yellow), Info (gray)
   - Click to expand full error message with timestamp

**Files to create/modify:**
- New: `src/client/ui/VideoForensics.Ui.Shared/Pages/Health.razor` (`/health`)
  - Dashboard page with multiple sections
  - Auto-refresh every 10 seconds (configurable via settings)
  - Color coding: Green (healthy), Yellow (warning), Red (error)

- New: `src/client/web/VideoForensics.WebApp/Api/HealthEndpoints.cs`
  - `GET /api/v1/health/system` → Server, database, service metrics
  - `GET /api/v1/health/accounts` → Account summary and list
  - `GET /api/v1/health/devices` → Device summary and list
  - `GET /api/v1/health/alerts` → Recent errors/warnings (last 10)

- New: `src/client/host/VideoForensics.Hosting/Services/HealthCollectionService.cs`
  - Collects system metrics (CPU, memory, disk, connections)
  - Queries database for counts and health status
  - Queries background service status (last run times, next scheduled)
  - Aggregates into health summary objects

- Modify: `NavGroups.cs`
  - Add `/health` as Admin sub-item (or top-level if preferred)
  - Visible to SuperAdmin role only

**Testing:**
- Navigate to Health dashboard
- Verify all sections load data correctly
- Verify auto-refresh updates metrics
- Trigger account error (bad auth) → verify appears in Accounts section and Alerts
- Verify dashboard displays health warnings (high CPU, low disk, auth failures)
- Verify colors match health status

**Additional consideration:** Should health data be logged/archived for historical trend analysis?

---

### 12. Investigate & Fix Scheduler Issues

**Scope:** Diagnose and fix background scheduling service failures

**Background services to investigate:**
- `DeviceHealthSyncService` - 15-minute health polling (RSSI, jamming detection)
- `UpdateCheckService` - Update availability checking
- `EventPullService` - Scheduled event pulls (if background)
- `SyncSchedule` entity - Per-account configurable poll intervals

**Investigation strategy:**
1. **Logging:** Add detailed diagnostics to scheduler services
   - Log each scheduled run attempt: timestamp, service name, result (success/failure)
   - Log next scheduled run time
   - Log any exceptions with full stack trace
   - Use named pipe logger so we can see this in real-time via LoggerViewer

2. **Health Dashboard integration:** 
   - Show scheduler status in Health dashboard (item 11)
   - Display last run and next scheduled run for each background service
   - Alert if service hasn't run in expected interval (e.g., health sync should run every 15 min)

3. **Database audit:**
   - Check `SyncSchedule` table: verify records exist for accounts
   - Check `DeviceHealth` table: verify it's being populated
   - Verify timestamps are updating correctly

4. **Configuration check:**
   - Verify services are registered in DI container
   - Verify services are started during app startup
   - Check for any cancellation token issues (premature cancellation?)

**Files to add diagnostics to:**
- `src/client/host/VideoForensics.Hosting/BackgroundServices/DeviceHealthSyncService.cs`
  - Add Info-level logging: "Health sync starting", "Health sync completed", "Health sync failed: {error}"
  - Log last run and next run times
  
- `src/client/host/VideoForensics.Hosting/BackgroundServices/UpdateCheckService.cs`
  - Similar logging pattern

- Verify: `src/client/host/VideoForensics.Hosting/DependencyInjection/` 
  - Confirm services are registered as `IHostedService`
  - Verify startup order is correct

**Verification (once fixed):**
- Start server → check named pipe logger for scheduler startup messages
- Wait 15+ minutes → verify Health dashboard shows updated last-run timestamps
- Verify `DeviceHealth` records being created
- Verify `SyncSchedule` entries are being processed
- Monitor LoggerViewer during first sync cycle for any errors

**Note:** This investigation will inform Health Dashboard implementation and ensure scheduled operations actually run as expected.

---

## Outstanding Discussion: Improvements to Consider

Beyond the 10 items above, what other improvements should we prioritize? Consider:

1. **Evidence browsing & filtering:**
   - Current timeline/grid views good, or need refinement?
   - Multi-device timeline view useful or cluttered?
   - Device time heatmap useful or should it be simplified?

2. **Account management:**
   - Device config sync—should device settings changes in app push back to provider account?
   - Provider account re-linking workflow—currently requires full re-auth; could improve?
   - Multi-account switching—currently in AccountSwitcher; clear enough?

3. **Performance:**
   - Large event/snapshot collections—pagination needed?
   - Media grid rendering performance for thousands of items?
   - Database query optimization for evidence queries?

4. **Admin/Operations:**
   - Health sync service visibility (when did it last run? next run?)
   - Storage/retention management—visual feedback on database size, cleanup options?
   - Operator audit log—currently exists, but visibility in UI good?

5. **MAUI app:**
   - Current thin-client model working well?
   - Local search/caching of evidence when offline?
   - Notification strategy—how should alerts reach mobile users?

6. **Other:**
   - Something else you've been thinking about?

What should we add to the plan?
