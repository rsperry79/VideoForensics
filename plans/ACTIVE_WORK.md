# Active VideoForensics Plans - Current Status

**Last Updated:** 2026-10-03

---

## Summary

**Total Plans:** 8 active, 14 completed (archived)

- **In Progress/Blocked:** 2 plans
- **Pending/Not Started:** 6 plans

---

## IN PROGRESS / BLOCKED (2 plans)

### 1. **scheduled-background-sync-tasks-linux.md** 🔄 BLOCKED
- **Title:** Multi-provider-account scheduled tasks + Linux service
- **Issue:** Racing condition on `SessionProvider.GetSession()` when running concurrent Ring account syncs
- **Mitigation:** Sequential processing within provider type (follow-up refactor needed)
- **Status:** Awaiting decision on sequential vs. refactored concurrent approach

### 2. **simple-mode-embedded-mcp-chat.md** 🔄 IN PROGRESS
- **Title:** Simple Mode + Embedded MCP Chat
- **Dependencies:** Blocked by Phase 3 (utils reorganization) completion
- **Status:** Awaiting Phase 3 merge to dev/main to unblock

---

## PENDING / NOT STARTED (6 plans)

### Design Phase (Future Features)

**3. maui-layout-theme-localization.md** 📋
- **Title:** Docked MAUI/Blazor Layout + Theme + Localization + Per-Operator Preferences
- **Status:** Design stage - not yet implemented

**4. case-filters-auto-numbering-jamming.md** 📋
- **Title:** Case Filters & Auto-Generated Case Numbers (with Jamming Auto-Detection)
- **Status:** Design stage - not yet implemented

**5. ui-framework-migration-syncfusion.md** 📋
- **Title:** Migrate UI framework: Radzen.Blazor → Syncfusion Blazor
- **Scope:** Large UI framework replacement
- **Status:** Design stage - lower priority due to scope

**6. ci-cd-versioning-update-check.md** 📋
- **Title:** GitHub CI/CD Pipeline, Nerdbank.GitVersioning, and Update-Check Feature
- **Status:** Design stage - infrastructure work

### Implementation Ready

**7. phase-3-project-reorganization.md** ⏳ PARTIALLY DONE
- **Title:** Project Reorganization (Utils Folder Consolidation)
- **Status:** Branch exists with commits, **awaiting merge to dev**
- **Branch:** `claude/phase-3-reorganization-71bdb2`
- **Unblocks:** Simple Mode + MCP Chat work

**8. api-error-logging-per-attempt.md** ⏳ PENDING
- **Title:** Persist Provider API Errors (Status Code + Response Body) per Event
- **Status:** Schema designed, implementation not yet started
- **Note:** `ProviderApiErrorLog` entity already in codebase (partial completion)

---

## Key Dependencies

1. **Phase 3 Merge** blocks: Simple Mode + MCP Chat (`spicy-conjuring-penguin`)
2. **Scheduled Tasks Decision** unblocks: `kind-plotting-storm` implementation

---

## Git References

- **Phase 3 Branch:** `claude/phase-3-reorganization-71bdb2`
- **Dev Branch:** Main integration target for new PRs
- **All Feature Branches:** Fork from `dev`, PR back to `dev`, then promote dev→main

---

## File Legend

- 📋 **PLAN/DESIGN** — Scope document, not yet implementing
- ⏳ **PENDING** — Ready for work, not started
- 🔄 **IN PROGRESS/BLOCKED** — Active but needs unblocking
- ✅ **COMPLETE** → moved to `archive/` folder
