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

#### ✅ **Migration reset (alpha)** — COMPLETED 2026-10-08
- **Status:** Ready for PR to dev
- **Implementation:** Consolidated the five EF Core SQLite migrations into a single fresh `InitialCreate` (model snapshot byte-identical to the previous one, so the schema is unchanged). `DatabaseInitializer` now refuses to open a database whose `__EFMigrationsHistory` holds migration ids this build does not contain, logs an Error and throws `InvalidOperationException` naming the file.
- **Note:** Existing alpha databases must be deleted: first start after upgrade fails with an explicit message (nothing is deleted automatically; data in the old database is lost).
- **Testing:** DatabaseInitializer tests (+5): single `InitialCreate`, legacy history rejected and logged, current-only history accepted, empty file works, migrated schema equals `EnsureCreated` schema

#### ✅ **Jamming API for client hosts + MAUI jamming analysis** — COMPLETED 2026-10-08
- **Plan:** n/a - follow-up to the case-numbering and simple-mode work
- **Status:** Ready for PR to dev
- **Implementation:** `/api/v1/jamming` endpoints (reads any signed-in role, writes Admin; CaseId/DetectedAtUtc server-controlled, Source client-claimed by an Admin for new incidents only), DTOs, `RemoteJammingRepository` registered for client hosts, `JammingToolsOrchestrator` registered in client mode and switched to the ranged `GetHistoryAsync(deviceId, from, to)` so analysis works against the remote API
- **Testing:** WebApp.Tests 394, Hosting.Tests 490, Client.Core.tests 234 at that point; MAUI Windows target builds with 0 warnings/0 errors

#### ✅ **API error logging per attempt** — COMPLETED (PR #172)
- **Plan:** `plans/archive/api-error-logging-per-attempt.md`

#### ✅ **Project reorganization (utils)** — COMPLETED
- **Plan:** `plans/archive/phase-3-project-reorganization.md`

---

## Current Work State (2026-10-08)

### Phase 1: Navigation & Layout - COMPLETE
- Consolidated `/collect` page (`Collect.razor`) with device/account selector, date range defaults (`LastCollectCompleteUtc` or 7 days), videos/snapshots toggle and a shared progress console
- `EvidenceDatesFilter.razor` (presets, custom range, Apply)
- `NavGroups.cs` reorganized: Evidence hub (Collect, Dates Filter, Analyze, Chat), Import/Export under Admin, Sources tab simplified
- `LlmSettings.razor` missing-address handling; `ProviderAccount.LastCollectCompleteUtc` added

### Phase 2: Data Fetching - IMPLEMENTED
- `EventPullService` exists at `src/client/host/VideoForensics.Hosting/Services/EventPullService.cs` (registered in `VideoForensicsHostingExtensions.cs`; unit and integration tests in `src/client/host/tests/Services/`)
- Manual sync endpoint exists: `POST /api/v1/provider-accounts/{id:guid}/sync-now` in `src/client/web/VideoForensics.WebApp/Api/AccountEndpoints.cs` (`SyncNowResponseDto` in Api.Contracts)
- "Sync Now" button exists in `AccountDetails.razor` (`SyncNowAsync`)
- Scheduled (automatic) sync is NOT done - see Next Steps (1)

### Phase 3: Project Reorganization - COMPLETE
- See `plans/archive/phase-3-project-reorganization.md`; projects live under `src/utils/{selftest,setup,repair,diagnostics}`. The empty leftover folder `src/utils/logger` can be deleted.

### Phase 4: Logger Viewer - OBSOLETE
- Superseded by the WebApp Server Logs page (see "WebApp Log Viewer" above); the WPF and MAUI LoggerViewer projects were removed.

---

## Next Steps (priority order)

1. **Scheduled per-account background sync (CRITICAL, partial).** `SyncSchedule` entity/repository and the `AccountSyncSchedule.razor` page exist, keyed DI and `UseSystemd` are in place, but no background service consumes the schedules. See `plans/scheduled-background-sync-tasks-linux.md` and `plans/SCHEDULED_SYNC_REFACTORED_PLAN.md`.
2. **Finish Radzen removal.** `FolderBrowserDialog.razor`, the `Radzen.Blazor` package reference and stale comments. See `plans/ui-framework-migration-syncfusion.md`.
3. **Finish localization.** 14 pages plus the Simple Mode strings. See `plans/maui-layout-theme-localization.md`.
4. **Deferred / known issues:**
   - Log viewer M6 (local MAUI logs)
   - Streaming chat
   - Operator details page loads the operator through a SuperAdmin-only call, so a plain Admin sees an unauthorized alert
   - Nothing from the Simple Mode, log viewer and case numbering work has been checked in a browser yet
