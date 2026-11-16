# Operator details: Admin sees unauthorized alert

**Status on origin/dev (checked 2026-10-09):** PENDING. The fix exists only as local commit 67d7bf6 on `feature/operator-details-admin`; not on origin/dev (no remote branch contains it).

**Source:** plans/archive/simple-mode-embedded-mcp-chat.md line 15 ("Operator details still loads the operator through a SuperAdmin-only call, so a plain Admin sees an unauthorized alert above the working Display mode section").

## Verdict

PENDING. origin/dev's `OperatorDetails.razor` still renders `_isLoading` / `NotAuthorizedToView` for any viewer and has no `IsSuperAdminViewer` gate, so Admins still hit the 403 alert. Commit 67d7bf6 adds `IsSuperAdminViewer`, skips `LoadAsync` and the details chain for non-SuperAdmins (Display mode section remains), and ships `OperatorDetailsAdminViewTests.cs` (3 tests). It fully resolves the item functionally; the only remaining work is landing it on dev via PR. No new localized text is introduced (the alert is simply no longer shown to Admins; the existing `NotAuthorizedToView` key remains for the SuperAdmin path).

## Scope

- In: get 67d7bf6 into dev (PR from `feature/operator-details-admin` to `dev`, squash-merge).
- Out: new "details are SuperAdmin-only" informational text for Admins (optional UX follow-up, see risks); API changes.

## Code and file touchpoints

| file | change | why |
|---|---|---|
| src/client/ui/VideoForensics.Ui.Shared/Pages/OperatorDetails.razor | Already in 67d7bf6: `IsSuperAdminViewer`, early return in `LoadAsync`, empty first branch in markup | Stop 403 fetch and alert for Admins |
| src/client/ui/VideoForensics.Ui.Shared.Tests/OperatorDetailsAdminViewTests.cs | Already in 67d7bf6: new test class | Cover Admin and SuperAdmin behavior |

(Both paths verified on origin/dev for the page; the test file is new in the commit; sibling `OperatorDetailsDisplayModeTests.cs` exists on dev.)

## Test plan

TDD already followed in spirit by 67d7bf6; nothing new to write. Covered by the commit, in `VideoForensics.Ui.Shared.Tests`:
- `OperatorDetails_AdminViewer_DoesNotShowAuthErrorAlert` (no `.alert-danger`, no auth text, probe sees 0 requests) - fails on dev because the GET is attempted and 403 alert shows.
- `OperatorDetails_AdminViewer_DoesNotRenderOperatorDetails`
- `OperatorDetails_SuperAdminViewer_StillFetchesOperator` (regression guard).

Verify locally before PR: `dotnet test --filter "Class=OperatorDetailsAdminViewTests|Class=OperatorDetailsDisplayModeTests"` (scoped). Confirm the Admin display-mode tests still pass with the new gate.

## Risks and open questions

- Loopback TcpListener probe in tests: possible port or timing flakiness (uses a 300 ms delay); acceptable but watch CI.
- Admin now sees nothing but Display mode; no explanation of why details are absent. Optional follow-up needs a localized string.
- Gate relies on `OperatorRole` ordering (`>= SuperAdmin`); fine as SuperAdmin is the top role.
- Other callers of the SuperAdmin-only GET from Admin-visible pages not audited.

## Priority recommendation

Low: small UX polish (a spurious alert for Admins), no security exposure (server already enforces SuperAdmin; this only avoids a denied call), and effort is S since the code and tests already exist and only need a PR to dev.

Effort: S

## Implementation dispatch

No dispatch needed: open a PR for `feature/operator-details-admin` into `dev` after the scoped test run (lite gate passed; full gate requires user confirmation before the PR).
