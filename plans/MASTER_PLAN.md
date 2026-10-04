# VideoForensics Consolidated Master Plan

**Last Updated:** 2026-10-03  
**Status:** 8 active plans, organized for sequential execution  
**Approach:** Lazy-load details to prevent token bloat

---

## Execution Sequence (Dependency Order)

### PHASE A: UNBLOCK SUBSEQUENT WORK (Week 1-2)

#### 1️⃣ **phase-3-project-reorganization** ⏳ CRITICAL BLOCKER
- **Scope:** Move CLI tools to `src/utils/`, consolidate namespaces
- **Branch:** `claude/phase-3-reorganization-71bdb2` (ready for merge)
- **Effort:** ~2 hours (code review + merge)
- **Unblocks:** Simple Mode + MCP Chat work
- **Status:** `src/utils/` folder structure exists, branch has commits
- **Details:** [phase-3-project-reorganization.md](phase-3-project-reorganization.md)
- **Next:** Code review, lite gate, merge to dev, promote to main

---

### PHASE B: CORE FEATURES (Week 2-3)

#### 2️⃣ **simple-mode-embedded-mcp-chat** 🔄 UNBLOCKED BY PHASE A
- **Scope:** Simplified UI mode for non-forensic users + MCP chat integration
- **Depends On:** Phase 3 (utils for MCP bridge)
- **Effort:** ~3-4 days
- **Outcome:** Toggle between full/simple UI, embedded MCP chat in sidebar
- **Details:** [simple-mode-embedded-mcp-chat.md](simple-mode-embedded-mcp-chat.md)
- **Next:** Dispatch Haiku once Phase 3 merges

#### 3️⃣ **scheduled-background-sync-tasks-linux** 🔄 BLOCKED (DECISION NEEDED)
- **Scope:** Per-provider background sync (events + snapshots), Linux service support
- **Blocker:** Racing condition on `SessionProvider.GetSession()` with concurrent Ring syncs
- **Options:**
  - Option A: Accept sequential processing mitigation (less optimal, faster to ship)
  - Option B: Refactor provider services for true concurrency (larger effort)
- **Decision Needed:** Before proceeding
- **Effort:** ~2 weeks (Option A) or ~4 weeks (Option B)
- **Details:** [scheduled-background-sync-tasks-linux.md](scheduled-background-sync-tasks-linux.md)
- **Next:** Confirm which option, then dispatch

#### 4️⃣ **api-error-logging-per-attempt** ⏳ PENDING
- **Scope:** Log every API error attempt (status code, response body, classified category)
- **New Entity:** `ProviderApiErrorLog` (schema already exists in codebase)
- **Effort:** ~2-3 days
- **Implementation:** Schema + migration already designed
- **Details:** [api-error-logging-per-attempt.md](api-error-logging-per-attempt.md)
- **Next:** Dispatch Haiku (no blockers, can start anytime after Phase A)

---

### PHASE C: INFRASTRUCTURE & RELEASE (Week 4+)

#### 5️⃣ **per-user-login-password-passkey** 📋 DESIGN
- **Scope:** Per-user login (password + passkey) separate from device pairing
- **Current State:** Device-bound pairing only (WebAuthn), one Operator per device
- **Goal:** One Operator identity usable from many browsers, device pairing for services
- **Effort:** ~1 week (design + implementation)
- **Details:** [per-user-login-password-passkey.md](per-user-login-password-passkey.md)
- **Dependency:** None (independent feature)
- **Next:** Review design, confirm approach

#### 6️⃣ **ci-cd-versioning-update-check** 📋 DESIGN
- **Scope:** GitHub CI/CD automation, Nerdbank.GitVersioning, auto-update capability
- **Components:**
  - Semantic versioning from git tags
  - Automated release notes generation
  - Client-side update checker (periodic, with user notification)
- **Effort:** ~1 week (setup + integration)
- **Details:** [ci-cd-versioning-update-check.md](ci-cd-versioning-update-check.md)
- **Dependency:** None (infrastructure work)
- **Next:** Infrastructure decision (tag strategy, release automation)

---

### PHASE D: UI ENHANCEMENTS (Parallel, Lower Priority)

#### 7️⃣ **maui-layout-theme-localization** 📋 DESIGN
- **Scope:** Enhanced MAUI/Blazor layout system, theming, multi-language support
- **Effort:** ~2-3 weeks (large feature)
- **Priority:** Medium (improves UX)
- **Details:** [maui-layout-theme-localization.md](maui-layout-theme-localization.md)
- **Next:** Prioritization discussion

#### 8️⃣ **ui-framework-migration-syncfusion** 📋 DESIGN
- **Scope:** Replace Radzen.Blazor with Syncfusion Blazor throughout codebase
- **Effort:** ~3-4 weeks (large refactor)
- **Priority:** Low (technical debt cleanup)
- **Details:** [ui-framework-migration-syncfusion.md](ui-framework-migration-syncfusion.md)
- **Note:** Block for other work, lower priority
- **Next:** Postpone until after Phase A-C complete

#### 9️⃣ **case-filters-auto-numbering-jamming** 📋 DESIGN
- **Scope:** Case management, auto-generated case numbers, jamming auto-detection
- **Effort:** ~1-2 weeks
- **Priority:** Medium (forensic workflow enhancement)
- **Details:** [case-filters-auto-numbering-jamming.md](case-filters-auto-numbering-jamming.md)
- **Next:** Prioritization discussion

---

## Dependency Graph

```
PHASE A (CRITICAL):
  phase-3-project-reorganization
    ↓
PHASE B (CORE):
  ├─ simple-mode-embedded-mcp-chat (unblocked by Phase A)
  ├─ scheduled-background-sync-tasks-linux (blocked: decision needed)
  └─ api-error-logging-per-attempt (independent)
    ↓
PHASE C (INFRASTRUCTURE):
  ├─ per-user-login-password-passkey (independent)
  └─ ci-cd-versioning-update-check (independent)

PHASE D (PARALLEL, LOWER PRIORITY):
  ├─ maui-layout-theme-localization
  ├─ case-filters-auto-numbering-jamming
  └─ ui-framework-migration-syncfusion (defer to end)
```

---

## Recommended Execution Plan

### Week 1-2: Unblock Pipeline
1. **Merge Phase 3** to dev/main (1-2 days)
   - Code review existing PR
   - Run lite gate
   - Merge to dev, then promote to main

### Week 2-3: Core Work
2. **Decide on Scheduled Tasks approach** (0.5 days)
   - Confirm Option A (sequential) or Option B (refactored)
   - Dispatch implementation

3. **Implement Simple Mode + MCP Chat** (3-4 days)
   - Dispatch Haiku once Phase 3 merges
   - Dependent files updated, full PR flow

4. **Implement API Error Logging** (2-3 days, parallel)
   - Schema + migration + entity updates
   - Integration with existing error tracking

### Week 4: Infrastructure
5. **Design & Plan Remaining Features** (1-2 days)
   - Per-user login approach finalization
   - CI/CD infrastructure decisions

### Weeks 5+: UI Features & Refactors (as priority allows)
- Case Filters + Auto-Numbering
- MAUI Layout/Theme/Localization
- Syncfusion Framework Migration (defer to end)

---

## Critical Decisions Needed

| Decision | Affects | Timeline |
|----------|---------|----------|
| Scheduled Tasks: Sequential vs. Refactored? | Phase B Timeline | Before dispatch |
| Phase 3 Code Review Status | Week 1 Timeline | ASAP |
| Per-User Login vs. Current Device-Pairing | Phase C Feature Set | Before implementation |
| CI/CD Automation Strategy | Release Process | Before implementation |

---

## How to Use This Plan

**Lazy Loading:**
1. Start with this document for overview
2. Click into specific plan file only when ready to implement
3. Refer back here for dependency checks before starting work

**Progress Tracking:**
- Update status markers as work completes (✅ → ✓)
- Move completed plans to archive
- Mark blockers when encountered

**Team Communication:**
- Use Phase labels when discussing ("We're in Phase B now")
- Reference plan file names in PR descriptions
- Link to this document in discussions

---

## Files Summary

| File | Type | Status |
|------|------|--------|
| phase-3-project-reorganization.md | Implementation | Ready (branch exists) |
| simple-mode-embedded-mcp-chat.md | Implementation | Blocked (Phase 3) |
| scheduled-background-sync-tasks-linux.md | Implementation | Blocked (decision) |
| api-error-logging-per-attempt.md | Implementation | Pending (ready) |
| per-user-login-password-passkey.md | Design | Pending (review) |
| ci-cd-versioning-update-check.md | Design | Pending (review) |
| maui-layout-theme-localization.md | Design | Pending (prioritize) |
| case-filters-auto-numbering-jamming.md | Design | Pending (prioritize) |
| ui-framework-migration-syncfusion.md | Design | Pending (defer) |

---

## Navigation

**Quick Jumps to Full Plans:**
- [Phase 3: Utils Reorganization](phase-3-project-reorganization.md)
- [Simple Mode + MCP Chat](simple-mode-embedded-mcp-chat.md)
- [Scheduled Sync Tasks](scheduled-background-sync-tasks-linux.md)
- [API Error Logging](api-error-logging-per-attempt.md)
- [Per-User Login Design](per-user-login-password-passkey.md)
- [CI/CD + Versioning](ci-cd-versioning-update-check.md)
- [MAUI Layout + Theme](maui-layout-theme-localization.md)
- [Case Filters](case-filters-auto-numbering-jamming.md)
- [Syncfusion Migration](ui-framework-migration-syncfusion.md)

**Other References:**
- [ACTIVE_WORK.md](ACTIVE_WORK.md) — Simpler status overview
- [README.md](README.md) — Quick navigation
- [archive/](archive/) — Completed plans (14 files)
