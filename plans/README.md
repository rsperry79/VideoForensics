# VideoForensics Plans

All project plans organized by status.

## Quick Navigation

### 📋 Active Plans (8 files)

**In Progress / Blocked:**
- `scheduled-background-sync-tasks-linux.md` — Per-provider background sync + Linux service (blocked)
- `simple-mode-embedded-mcp-chat.md` — UI simplification + MCP chat integration (blocked by Phase 3)

**Pending / Design Phase:**
- `phase-3-project-reorganization.md` — Utils folder consolidation (branch ready, awaiting merge)
- `api-error-logging-per-attempt.md` — Track every API error attempt with status & response
- `maui-layout-theme-localization.md` — Responsive layout, theming, multi-language support
- `case-filters-auto-numbering-jamming.md` — Case management, auto-numbering, jamming detection
- `ui-framework-migration-syncfusion.md` — Replace Radzen with Syncfusion (large refactor)
- `ci-cd-versioning-update-check.md` — Automated versioning, releases, auto-update feature

**Additional Context:**
- `per-user-login-password-passkey.md` — Per-user login (password + passkey) vs device pairing
- `EXECUTION_STATUS.md` — Original Phase 1-4 framework docs (kept for reference)

---

### ✅ Completed Plans (12 files in archive/)

**Core Features (Merged to main):**
- `phase-1-navigation-layout.md` — Navigation reorganization, consolidated media collect
- `phase-2-data-fetching.md` — Auto-pull events on account connect + sync button
- `phase-4-logger-viewer.md` — Named pipe logging, WPF + MAUI LoggerViewer, password recovery
- `superadmin-provider-binding.md` — SuperAdmin provider account binding for authentication

**UI & Architecture:**
- `maui-blazor-hybrid-conversion.md` — WinUI desktop app + web backend conversion
- `frontends-mcp-api-only-architecture.md` — All clients use typed DTOs + versioned /api/v1 routes
- `consolidation-framework.md` — Project consolidation strategy

**Features (Merged to dev):**
- `automatic-event-snapshot-pull.md` — Implementation details for Phase 2 event/snapshot pull
- `external-auth-smb-integration.md` — LDAP/AD/SMB group auth + SuperAdmin toggle
- `on-demand-live-view.md` — Live view with jamming-triggered sustained mode
- `installer-overhaul-inno-setup.md` — Windows installer with first-run setup + configurable paths
- `security-perf-fixes.md` — Code-review findings: encryption hardening, memory buffering, pagination

---

## Status Definitions

- 📋 **PLAN/DESIGN** — Scope & design document, implementation not started
- ⏳ **PENDING** — Ready for work but not yet started
- 🔄 **IN PROGRESS/BLOCKED** — Active but awaiting unblocking
- ✅ **COMPLETE** — Implemented & merged (archived)

---

## Key Dependencies

1. **Phase 3** (utils reorganization) unblocks Simple Mode work
2. **Simple Mode** unblocks other UI improvements
3. **Scheduled Tasks** decision needed before proceeding (sequential vs. refactored)

---

## For More Details

- `ACTIVE_WORK.md` — Current status summary with next actions
- `EXECUTION_STATUS.md` — Original Phase 1-4 status (archived folder has the detailed docs)
