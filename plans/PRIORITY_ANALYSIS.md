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
- **Status (2026-10-08):** PARTIAL - `SyncSchedule` entity/repository and the `AccountSyncSchedule.razor` page exist, but no background service consumes them. The sequential-vs-refactored decision is overtaken by PR #152 (account-aware provider services).
- **Risk:** Medium (session racing issue, but mitigation acceptable)
- **Recommendation:** **DO NEXT** - build the per-account event-sync and snapshot/RSSI background services on top of `SyncSchedule`

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
- **Status:** DONE 2026-10-08 (Simple Mode shipped end to end)
- **Recommendation:** Done; remaining follow-ups are localization of its strings and a browser check

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
- **Status:** DONE (PR #172)
- **Recommendation:** Done

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
- **Status:** DONE (merged to dev, PR #159). Recommendation: none, complete.

---

## Current Execution Order (2026-10-08)

Done: simple-mode (2026-10-08), api-error-logging (PR #172), per-user-login (PR #166), ci-cd versioning and update check (PR #159).

1. **Scheduled per-account sync (CRITICAL, partial)** - `SyncSchedule` storage, `AccountSyncSchedule.razor`, keyed DI and `UseSystemd` exist; the event-sync and snapshot/RSSI background services that consume the schedules do not. Sequential vs. refactored: PR #152 (account-aware provider services, `ISessionProvider.GetSession(Guid providerAccountId)`) already addressed most of the SessionProvider race the refactored plan feared, so the refactor is mostly done; only about 10 parameterless `GetSession()` call sites remain to re-audit. Plans: `scheduled-background-sync-tasks-linux.md` (full scope) and `SCHEDULED_SYNC_REFACTORED_PLAN.md` (overlaps it).
2. **Radzen removal (small)** - `FolderBrowserDialog.razor`, the `Radzen.Blazor` package reference, `@using Radzen` in `_Imports.razor` and stale comments (`ui-framework-migration-syncfusion.md`).
3. **Localization of the last 14 pages (mechanical)** plus the hard-coded Simple Mode strings (`maui-layout-theme-localization.md`).
4. **Backlog** - MAUI log viewer M6 (local logs), streaming chat, operator details page SuperAdmin-only load for plain Admins, browser check of Simple Mode / log viewer / case numbering.
