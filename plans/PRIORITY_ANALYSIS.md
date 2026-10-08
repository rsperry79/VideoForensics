# Plan Prioritization Analysis

**Based on:** Project goals from CLAUDE.md, forensic workflow requirements, operational needs

---

## Project Goals (Strategic Drivers)

From CLAUDE.md and codebase analysis:

1. **Core Forensic Workflow** — Investigators need complete, reliable data collection & analysis
2. **End-User Support** — Victims/home-owners need simple, accessible evidence viewing
3. **Operational Reliability** — Automated background sync, cross-platform (Windows + Linux)
4. **Auth Model** — Proper separation of human login vs. device/service pairing
5. **Data Integrity** — All provider API data persisted to DB (no ephemeral storage)

---

## Phase B Plans: Impact vs. Strategic Alignment

### 🔴 CRITICAL (Must do)

**scheduled-background-sync-tasks-linux** ⏳
- **Why:** Operational reliability is PROJECT CRITICAL
  - Investigators today manually click "Sync Now" for every account
  - Multi-provider household deployments (Ring + Uniview) have no automation
  - Linux deployment requires parity with Windows service
- **Alignment:** Directly supports Goal #3 (Operational Reliability)
- **Impact:** Makes product usable for multi-account setups
- **Blocker:** Decision needed on sequential (2 weeks) vs. refactored (4 weeks) approach
- **Risk:** Medium (session racing issue, but mitigation acceptable)
- **Recommendation:** **DECIDE ON APPROACH FIRST**, then dispatch immediately after

---

### 🟠 HIGH (Do next)

**simple-mode-embedded-mcp-chat** 🚀
- **Why:** End-user support is product-critical but currently missing
  - Current UI is "investigator-only" with role gates (no ReadOnly victim access)
  - Victims are confused by device config, jamming telemetry, forensic dashboards
  - MCP chat provides natural-language access to evidence (big UX win)
- **Alignment:** Directly supports Goal #2 (End-User Support) — NEW USER SEGMENT
- **Impact:** Opens product to victims/home-owners (market expansion)
- **Risk:** LOW (reuses existing MCP, layout pattern already proven)
- **Status:** Ready to dispatch immediately (not blocked)
- **Recommendation:** **START NOW while waiting for scheduled-sync decision**

---

### 🟡 MEDIUM (Do in parallel)

**api-error-logging-per-attempt** 📊
- **Why:** Observability for provider API failures
  - Currently only logs latest attempt (overwrites per event)
  - Hard to diagnose intermittent Ring 404s vs. actual failures
  - Supports debugging complex provider issues
- **Alignment:** Supports Goal #5 (Data Integrity) — enables error categorization
- **Impact:** Reduces MTTR on provider API issues
- **Risk:** LOW (schema already exists in codebase)
- **Status:** Ready to dispatch immediately
- **Recommendation:** **PARALLEL with simple-mode** (doesn't block other work)

---

## Phase C Plans: Viability & Timing

### ✅ COMPLETED (2026-10-05)

**per-user-login-password-passkey** 🔐 **DONE**
- **Completed:** 2026-10-05
- **What shipped:** Per-user password + passkey authentication with operator approval workflows
  - Operator.Username, Role, PasswordHash, SecurityStamp, MustChangePassword enforcement
  - OperatorCredential table with IsApproved per-credential model
  - Password login, passkey sign-in, credential approval workflows
  - Password reset with forced change-on-next-login
  - Self-service registration (bootstrap SuperAdmin, others ReadOnly+unapproved)
  - Admin UI for credential approval, operator approval, password reset
  - Rate limiting, security audit logging, timing-attack mitigation
- **Test coverage:** 260 WebApp + 438 Hosting + 578 Database tests passing
- **Backwards compatibility:** Device pairing, service credentials unchanged
- **Branch:** `ci/per-user-login-password-passkey` (ready for PR to dev)

### 🔵 SHOULD DO (After Phase B)

**Note:** Phase C Item 2 completed; Phase C Item 3 (ci-cd-versioning) previously completed in PR #157

**ci-cd-versioning-update-check** 🔄
- **Why:** Release automation & auto-update capability
  - Current process: manual GitHub release + manual installer signing
  - Need: Semantic versioning, release notes generation, client update checker
- **Alignment:** Supports operational reliability & deployment
- **Impact:** Reduces release friction, enables auto-updates
- **Risk:** Medium (CI/CD infrastructure)
- **Recommendation:** **After Phase B** (independent of feature work)

---

## Recommendation: EXECUTION ORDER

### ✅ IMMEDIATE (This week)

**1. CONFIRM DECISION: scheduled-background-sync-tasks-linux approach**
   - Sequential (faster, ~2 weeks) or Refactored (better, ~4 weeks)?
   - Once decided, dispatch immediately to Haiku

**2. DISPATCH: simple-mode-embedded-mcp-chat**
   - **Start:** Now (not blocked)
   - **Effort:** 3-4 days
   - **Why:** Unblocks victim support feature, high strategic value
   - **Parallel:** Run with #1 decision-making

**3. [OPTIONAL] DISPATCH: api-error-logging-per-attempt**
   - **Start:** After simple-mode (or parallel if capacity)
   - **Effort:** 2-3 days
   - **Why:** Observability + data integrity improvements
   - **Note:** Can run in parallel with scheduled-sync once that decision is made

### ⏳ NEXT (After Phase B complete)

**4. REVIEW & PLAN: per-user-login-password-passkey**
   - Complex auth refactor, needs careful review
   - High-risk changes (touched by all auth flows)

**5. PLAN & SETUP: ci-cd-versioning-update-check**
   - Infrastructure work, likely 1-2 weeks of setup

### 📋 LOWER PRIORITY (Backlog)

- **maui-layout-theme-localization** — Large feature (2-3 weeks), nice-to-have
- **case-filters-auto-numbering-jamming** — Forensic workflow enhancement, medium priority
- **ui-framework-migration-syncfusion** — Technical debt, defer to end

---

## Strategic Summary

| Phase | Item | Why | Blocker | Start |
|-------|------|-----|---------|-------|
| B | scheduled-sync | **CRITICAL** operational reliability | Decision needed | After decision |
| B | simple-mode | **HIGH** end-user support (new segment) | None | DONE 2026-10-08 ✅ |
| B | api-error-logging | **MEDIUM** observability | None | After simple-mode |
| C | per-user-login | **MEDIUM** auth model | None | Week 3+ |
| C | ci-cd | **MEDIUM** automation | None | Week 3+ |

---

## The Ask

**For the user:**

1. **Decide on scheduled-sync approach:** Sequential or Refactored?
   - Sequential: Ship faster (2 weeks), accept session racing mitigation
   - Refactored: Better design (4 weeks), true concurrency, but larger scope
   - **Impact:** Enables operational reliability goal, multi-account support

2. **Confirm simple-mode priority:** Start immediately?
   - This is the "victim support" feature (product differentiator)
   - No blockers, ready to dispatch to Haiku
   - 3-4 days elapsed time

3. **Optionally:** api-error-logging in parallel?
   - Improves observability (debugging provider issues)
   - 2-3 days elapsed time
   - Can run simultaneously with other work

**Recommendation:** Dispatch simple-mode NOW, decide scheduled-sync approach, then do both in phase.
