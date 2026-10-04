# Phase 1: Navigation & Layout Consolidation

## Scope
Reorganize left navigation menu, consolidate media collection pages, fix LLM settings, and add date filtering.

## Changes by Item

### Item 1: Consolidate Media Collection (Single Page + Shared Console)

**Files to create/modify:**
- Modify: `ProviderAccount` entity
  - Add field: `LastCollectCompleteUtc` (nullable DateTime)
  
- New: `src/client/ui/VideoForensics.Ui.Shared/Pages/Collect.razor` (unified page)
  - Device/account selector dropdown
  - Start/end date pickers (default: LastCollectCompleteUtc or last 7 days)
  - Device filter checklist
  - Tabs: Videos tab (all devices) / Snapshots tab (camera devices only)
  - Single "Collect All" button
  - Unified progress console (use DownloadProgressPanel)
  - On success: Update `ProviderAccount.LastCollectCompleteUtc`
  
- Modify: `src/client/ui/VideoForensics.Ui.Shared/Components/DownloadProgressPanel.razor`
  - Remove device-specific path display
  - Show only: file count progress, device name, collection type, eta
  
- Delete: `src/client/ui/VideoForensics.Ui.Shared/Pages/CollectVideos.razor`
- Delete: `src/client/ui/VideoForensics.Ui.Shared/Pages/CollectSnapshots.razor`

### Item 5: Evidence Date Filter Subpage

**Status:** ✅ COMPLETE - `EvidenceDatesFilter.razor` created with all features

- New: `src/client/ui/VideoForensics.Ui.Shared/Pages/EvidenceDatesFilter.razor` (`/evidence/dates-filter`)
  - Back button
  - Quick presets: Last 24h, 3 days, 7 days, month, Custom
  - Custom date pickers with Apply button
  - Updates ScopeState and navigates back to `/evidence`

### Item 6, 7, 8: Navigation Reorganization

**Current state (partial):** NavGroups.cs modified but consolidation incomplete.

**Target structure:**
```
Evidence (top-level tab)
├── Evidence page
├── Collect (NEW - single page, replaces /collect/videos and /collect/snapshots)
├── Dates Filter (hidden nav, accessed via button on Collect)
├── Analyze (submenu)
│   ├── Forensic Reports
│   ├── Signal Anomalies
│   ├── Access Control
│   ├── Jamming Analysis
└── Chat Assistant

Cases (top-level tab)
├── All Cases
├── New Case
├── Chain of Custody
├── Validate Evidence
└── Export Evidence

Sources (top-level tab)
├── Provider Accounts
├── Device Configuration
├── Query API
└── API Tester

Admin (top-level tab)
├── General
├── Security Events
├── Infrastructure
├── Operators
├── Paired Devices
├── Network Access
├── Storage
├── App Update
├── Notifications
├── Security Audit Log
├── LLM API
├── Lockout Policy
├── App Lock
└── Import / Export (MOVED from Evidence)
```

**Files to modify:**
- `src/client/ui/VideoForensics.Ui.Shared/Layout/NavGroups.cs`
  - Replace `/collect/videos` and `/collect/snapshots` with single `/collect` entry
  - Add `/evidence/dates-filter` as hidden nav child under Evidence
  - Move "Import / Export" from Evidence to Admin group
  - Verify Analyze stays as submenu under Evidence
  - Verify Chat stays under Evidence

### Item 9: Fix Settings Page Break (LLM Settings URI Error)

**Issue:** LLM settings page errors with "An invalid request URI was provided. Either the request URI must be an absolute URI or BaseAddress must be set."

**Files to investigate/modify:**
- `src/client/host/VideoForensics.Hosting/Remote/RemoteLlmService.cs`
  - Verify HttpClient BaseAddress is set correctly
  
- `src/client/ui/VideoForensics.Ui.Shared/Pages/Settings.razor` (or LlmSettings.razor)
  - Add error handling for missing server address
  - Display user-friendly message if server unreachable

## Testing Checklist

- [ ] NavGroups.cs compiles without errors
- [ ] Navigation renders correctly with new structure
- [ ] /evidence route loads Evidence page
- [ ] /collect route loads consolidated page (single media collection interface)
- [ ] /evidence/dates-filter route loads date filter page
- [ ] Clicking date preset updates scope and returns to Evidence
- [ ] Both videos and snapshots collection work from single Collect page
- [ ] Snapshots tab shows only camera devices
- [ ] Videos tab shows all devices
- [ ] SharedDownloadProgressPanel displays unified console output
- [ ] LLM Settings page loads without URI errors
- [ ] Import/Export visible in Admin menu, not Evidence menu
- [ ] All existing routes still work (no 404s)

## Dependencies
- None (this phase is self-contained)

## Next Phase
Phase 2: Data Fetching (EventPullService, auto-pull on account connect)
