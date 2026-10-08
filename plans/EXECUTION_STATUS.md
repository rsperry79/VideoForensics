# Execution Status: VideoForensics Planning & Implementation

## Archived Completed Plans

### Phase C: Authentication & Release Infrastructure

#### ✅ **Phase C Item 2: Per-User Login (Password + Passkey)** — COMPLETED 2026-10-05
- **Plan:** `plans/archive/per-user-login-password-passkey.md`
- **Status:** Merged to dev (PR #166)
- **Implementation:** 4 milestones completed
- **Testing:** All passing (260 WebApp + 438 Hosting + 578 Database)

#### ✅ **Phase C Item 3: CI/CD Versioning & Update Check** — COMPLETED 2026-10-05
- **Plan:** `plans/archive/ci-cd-versioning-update-check.md`
- **Status:** Merged to dev (PR #159)

#### ✅ **WebApp Log Viewer** — COMPLETED 2026-10-06
- **Plan:** `plans/archive/webapp-log-viewer.md`
- **Status:** Ready for PR to dev
- **Implementation:** In-memory log buffer + `/api/v1/logs` history & SSE + SuperAdmin Server Logs page in Ui.Shared; removed WPF and MAUI LoggerViewer projects; M6 (local MAUI logs) deferred
- **Note:** 5 `NavGroups_Tests` were already failing before this work (stale group expectations) and are tracked separately


#### ✅ **Case Filters & Auto-Generated Case Numbers** — COMPLETED 2026-10-07
- **Plan:** `plans/archive/case-filters-auto-numbering-jamming.md`
- **Status:** Ready for PR to dev
- **Implementation:** Server-generated collision-safe case numbers (manual/detected/suspected), jamming hook in JammingRepository (insert path only), CaseId links incident to case for idempotent creation, auto-alert creation with case
- **Testing:** Case numbering (29 tests), jamming case automation (11), JammingRepository case automation (9), JammingRepository CaseId (6) — full gate 4070 tests, 0 failures

#### ✅ **Simple Mode + Embedded MCP Chat** — COMPLETED 2026-10-08
- **Plan:** `plans/archive/simple-mode-embedded-mcp-chat.md`
- **Status:** Ready for PR to dev
- **Implementation:** Per-operator admin lock on UI mode (migration, repository enforcement), Simple/Standard user toggle, admin ui-mode endpoints + Display mode section on operator details, Simple home (7-day plain-language timeline + evidence list), plain-language formatter fallback
- **Testing:** UI mode lock repository (8), UiModeService/user menu/SimpleLayout (+38 Ui.Shared), admin UI-mode endpoints (21 pipeline/unit) and remote service (5), operator details Display mode (11), Simple home builder (24) and component/routing (35) - full gate 4195 tests, 0 failures
---

## Previous Phase Plans

### Phase 1–4: Navigation, Data Fetching, Project Reorganization, Logger Viewer
- **Location:** `plans/phase-*-*.md` and `plans/archive/`
- **Status:** Completed/In Progress as documented in their respective files

---

## Current Work State

### Phase 1: Navigation & Layout

**Status:** ✅ COMPLETE

**Completed:**
- ✅ Item 1 (Consolidated Collect Page): `Collect.razor` created with full functionality
  - Single `/collect` page with merged videos/snapshots UI
  - Device/account selector dropdown
  - Date pickers with defaults (LastCollectCompleteUtc or 7 days)
  - Tabs/toggles for Videos vs Snapshots collection
  - Single "Collect All" button with unified progress console
  - DownloadProgressPanel updated to show shared output

- ✅ Item 5 (Evidence Date Filter): `EvidenceDatesFilter.razor` created with full functionality
  - Back button, preset buttons (24h/3d/7d/month), custom date pickers, Apply button
  - Correctly updates ScopeState and navigates back to Evidence

- ✅ Item 6, 7, 8 (NavGroups.cs Navigation Reorganization): Complete
  - Consolidated `/collect/videos` and `/collect/snapshots` into single `/collect` entry
  - Moved Analyze under Evidence as submenu
  - Moved Chat and Import/Export under Evidence
  - Moved Import/Export to Admin group
  - Sources tab simplified (Provider Accounts, Device Configuration, Query API, API Tester)
  - Evidence now hub: Evidence, Collect, Dates Filter, Analyze (with subitems), Chat, Import/Export

- ✅ Item 9 (LLM Settings URI Fix): `LlmSettings.razor` modified
  - Added error handling for missing server address

- ✅ ProviderAccount entity: `LastCollectCompleteUtc` field added
  - EF Core migration `20261002204131_AddLastCollectCompleteUtcToProviderAccount` created
  - Tracks when media collection last completed successfully
  - Used to default media collection date range

**CI Status:** ✅ ALL TESTS PASSING
- Test Results: 2,943 passed, 0 failed, 22 skipped
- All 25 test assemblies passing including previously-failing SQLite tests
- Migration fully integrated into CI pipeline

### Phase 2: Data Fetching
**Status:** READY TO START
- EventPullService not yet created
- Manual sync endpoint not yet created
- AccountDetails.razor "Sync Now" button not yet added
- **Blockers:** None - Phase 1 complete and CI passing

### Phase 3: Project Reorganization
**Status:** NOT STARTED
- CLI tools still in old locations (src/selftest, src/tools/*)
- Namespace renames not done

### Phase 4: Logger Viewer
**Status:** NOT STARTED
- NamedPipeLoggerProvider not created
- LoggerViewer WPF app not created
- MAUI integration not added

---

## Next Steps

1. **Execute Phase 2: Data Fetching** (READY TO START)
   - Dispatch Haiku subagent to create EventPullService with tests
   - Dispatch Haiku subagent to add sync endpoint + UI button
   - Dispatch Haiku subagent to update AccountDetails.razor with "Sync Now" button
   - Run lite gate on Hosting and UI projects

2. **Execute Phase 3: Project Reorganization**
   - Dispatch Haiku subagent to reorganize utils folder
   - Run full gate (clean rebuild + all tests)

3. **Execute Phase 4: Logger Viewer**
   - Dispatch Haiku subagent to create LoggerViewer project + NamedPipeLoggerProvider
   - Run lite gate on logging + WPF projects
   - Dispatch Haiku subagent to add MAUI integration

---

## Commands

**Resume a paused Haiku agent:**
```powershell
SendMessage -to <agent-id> -message "Continue"
```

**Start Phase 1 completion (after checking this status):**
```powershell
# Fix NavGroups.cs consolidation, create Collect.razor, update model
```

**Start Phase 2 (once Phase 1 passes lite gate):**
```powershell
# Create EventPullService, add sync endpoint, update AccountDetails.razor
```

---

## Git State

**Current branch:** `claude/plan-save-execution-09a6e3`

**Phase 1 Commits:**
- `b56a429` - Add EF Core migration: LastCollectCompleteUtcToProviderAccount
- `a4e44f1` - Consolidate media collection pages into unified Collect.razor
- `836a990` - Polish Phase 1 implementation: cleanup formatting and minor fixes

**Modified/New files (Phase 1):**
- `src/client/ui/VideoForensics.Ui.Shared/Layout/NavGroups.cs` (complete consolidation)
- `src/client/ui/VideoForensics.Ui.Shared/Pages/Collect.razor` (new - unified collection page)
- `src/client/ui/VideoForensics.Ui.Shared/Pages/EvidenceDatesFilter.razor` (new - date filter subpage)
- `src/client/ui/VideoForensics.Ui.Shared/Pages/LlmSettings.razor` (error handling)
- `src/data/database/sqlite/data.database.sqlite/Migrations/20261002204131_AddLastCollectCompleteUtcToProviderAccount.cs` (new migration)
- `src/data/database/sqlite/data.database.sqlite/Migrations/VideoForensicsDbContextModelSnapshot.cs` (updated)
- `src/client/host/VideoForensics.Hosting/VideoForensics.Hosting.csproj` (EF Core Design package)
