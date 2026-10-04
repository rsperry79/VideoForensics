# Plan Staleness & Viability Assessment

**Assessment Date:** 2026-10-03

---

## File Age Analysis

| Plan | Last Modified | Age | Freshness | Status |
|------|---------------|-----|-----------|--------|
| phase-3-project-reorganization | 2026-10-02 | 1 day | ✅ FRESH | Ready to merge |
| case-filters-auto-numbering-jamming | 2026-09-27 | 6 days | ✅ RECENT | Design OK |
| simple-mode-embedded-mcp-chat | 2026-09-24 | 9 days | ✅ RECENT | Waiting on Phase 3 |
| ci-cd-versioning-update-check | 2026-09-20 | 13 days | ⚠️ SLIGHTLY OLD | Design might need review |
| per-user-login-password-passkey | 2026-09-19 | 14 days | ⚠️ SLIGHTLY OLD | Design might need review |
| scheduled-background-sync-tasks-linux | 2026-09-14 | 19 days | ⚠️ OLDER | Blocked on decision |
| ui-framework-migration-syncfusion | 2026-09-06 | 27 days | ⚠️ OLDER | Design OK but lower priority |
| api-error-logging-per-attempt | 2026-09-05 | 28 days | ⚠️ OLDER | Schema exists in codebase |
| maui-layout-theme-localization | 2026-09-05 | 28 days | ⚠️ OLDER | Design OK but large scope |

---

## Viability Check

### ✅ VIABLE - Keep in Active Plans

**phase-3-project-reorganization** (1 day old, FRESH)
- **Status:** Branch exists with commits, ready to merge
- **Reason:** Actively worked on, unblocks other work
- **Action:** Proceed to code review & merge

**simple-mode-embedded-mcp-chat** (9 days old, RECENT)
- **Status:** Waiting on Phase 3
- **Reason:** Relevant feature, recent planning
- **Implementation Needed:** Once Phase 3 merges
- **Action:** Keep in active plans, dispatch after Phase 3

**case-filters-auto-numbering-jamming** (6 days old, RECENT)
- **Status:** Design phase, ready for prioritization
- **Reason:** Forensic workflow enhancement, recent planning
- **Action:** Keep in active plans, prioritize if time permits after Phase B

### ⚠️ REVIEW NEEDED - Might Require Updates

**per-user-login-password-passkey** (14 days old, SLIGHTLY OLD)
- **Status:** Design document with detailed schema changes
- **Current Reality:** Auth infrastructure exists (OperatorAuthEndpoints, WebAuthn)
- **Question:** Has auth strategy evolved since 2026-09-19?
- **Recommendation:** Quick review to confirm design still valid before implementation
- **Action:** Confirm design with user, update if needed, then dispatch

**ci-cd-versioning-update-check** (13 days old, SLIGHTLY OLD)
- **Status:** Design document
- **Current Reality:** CI/CD likely evolved since early Sept (recent commits show merge strategies)
- **Question:** Does release strategy still match current main/dev branch flow?
- **Recommendation:** Review against current `.github/workflows/` to confirm plan is still valid
- **Action:** Confirm CI/CD approach, verify against current workflows, then dispatch

**scheduled-background-sync-tasks-linux** (19 days old, OLDER)
- **Status:** Blocked on sequential vs. refactored decision
- **Current Reality:** EventPullService (Phase 2) now exists, may have addressed some concerns
- **Question:** Does EventPullService solve the SessionProvider racing issue?
- **Recommendation:** Review against current implementation to see if blocker still applies
- **Action:** Verify blocker still valid, make decision, then proceed

### 📋 LOWER PRIORITY - Consider Deferring

**ui-framework-migration-syncfusion** (27 days old, OLDER)
- **Status:** Design phase, large refactor scope
- **Reason:** Technical debt cleanup, not critical for core features
- **Recommendation:** Defer to end of roadmap (after Phase A-C complete)
- **Action:** Move to backlog, revisit after Core Features phase

**api-error-logging-per-attempt** (28 days old, OLDER) 
- **Status:** Schema already exists in codebase (`ProviderApiErrorLog` found)
- **Partial Completion:** Entity exists but integration/implementation unclear
- **Recommendation:** Verify actual completion status before proceeding
- **Action:** Check if implementation is done, archive if complete or update plan with current status

**maui-layout-theme-localization** (28 days old, OLDER)
- **Status:** Design phase, large scope feature
- **Reason:** Multiple sub-features (layout, theming, localization)
- **Recommendation:** Potentially split into smaller chunks if prioritizing
- **Action:** Defer or break into smaller plans

---

## Recommendations

### IMMEDIATE (Do Now)
1. ✅ **Keep Phase 3** — Fresh, ready to merge
2. ⚠️ **Review per-user-login & CI/CD** — Quick design confirmation (30 min each)
3. ⚠️ **Verify scheduled-sync blocker** — Has Phase 2 solved the SessionProvider issue?

### SHORT-TERM (After Phase 3 merges)
1. ✅ **Proceed with Simple Mode** — Design is recent, Phase 3 unblocks it
2. ⏳ **Proceed with API Error Logging** — IF not already done (verify first)
3. ✅ **Keep Case Filters** — Recent design, relevant feature

### MEDIUM-TERM (Phase C, infrastructure phase)
1. ⚠️ **Per-User Login** — After review (auth feature, important)
2. ⚠️ **CI/CD** — After review (infrastructure, enables releases)

### DEFER (Lower Priority)
1. 📋 **MAUI Layout/Theme/Localization** — Large scope, split into smaller plans
2. 📋 **Syncfusion Migration** — Technical debt, do after core features complete

---

## Actions Required Before Proceeding

| Plan | Action | Owner | Est. Time |
|------|--------|-------|-----------|
| per-user-login-password-passkey | Confirm auth design still valid | Code review | 30 min |
| ci-cd-versioning-update-check | Review against current workflows | Code review | 30 min |
| scheduled-background-sync-tasks-linux | Verify EventPullService doesn't solve blocker | Code review | 15 min |
| api-error-logging-per-attempt | Check if ProviderApiErrorLog is complete | Code inspection | 15 min |

---

## Summary

**8 Active Plans Assessment:**
- ✅ **2 plans DEFINITELY KEEP** (Phase 3, Simple Mode) — fresh, ready
- ⚠️ **4 plans REVIEW FIRST** (per-user-login, CI/CD, scheduled-sync, API error logging) — verify current status
- 📋 **2 plans DEFER** (UI framework, MAUI layout) — lower priority, larger scope

**Estimated Time to Full Clarity:** ~1.5 hours (4 × 15-30 min code reviews)

**Outcome:** Most plans are viable, just need confirmation that architecture/strategy hasn't shifted significantly since early September.
