# Provider details, sync, and Query API rework

Status: plan only. Branch for work: a worktree from `wip` (per `code-workflow`).
Already merged into `wip`: `sync-progress-title` (`e04c6f26`). It edits `AccountDetails.razor` (`DownloadProgressPanel` gets `CollectionType`, new `_runningSyncType` field). The tab-shell refactor must carry `_runningSyncType` into the Sync tab.
Worktree in progress: `F:\vf-last-download-fix` (branch `last-download-sync-fix`, no commits yet).

## Current state (grounded, reviewed)

- `Pages/AccountDetails.razor` (585 lines): remove-account button at L148 opens `AccountRemovePanel` at L144; `_confirmingRemove` at L282. Sync run history table at L165; second table at L228. Route `/accounts/{Id:guid}`. Last Successful Auth and Last Download already render at L50-61.
- `Pages/AccountSyncSchedule.razor` (526 lines): route `/accounts/{Id:guid}/schedule`.
- `Pages/QueryApi.razor` (133 lines): route `/query`; "API Tester" link at L22 to `/query/api-tester`. `/query` is `SuperAdminLocal` (`Layout/PageAccessRules.cs` L46).
- `/query/api-tester` is **`Pages/RingSelfTest.razor`** (L2), not a separate page. It has **no** `PageAccessRules` row, so any signed-in operator can open it today.
- `Pages/DeviceConfig.razor`: route `/devices/config`, `SuperAdminLocal` (`PageAccessRules.cs` L37).
- `PageAccessRules.cs` L50 `/accounts/{Id:guid}`, L51 `/accounts/{Id}/schedule`.
- `Components/Inspector/InspectorPanel.razor` (470 lines), `Services/QueryApi/QueryApiInspectorMapper.cs`.
- `Services/QueryApi/QueryApiFilterState.cs` holds only `LocationId` and `DeviceId`.
- Query API sub-pages: `QueryApiDevices`, `QueryApiDeviceDetails`, `QueryApiEvents`, `QueryApiConfig`.

## Scope, by request item

### 1. Provider details page: account details instead of Remove button
- Remove the bottom Remove Account button and `_confirmingRemove` flow from `AccountDetails.razor`. Remove Account stays on the Details tab.
- Replace the bottom block with a read-only "Account details" block (street address, account name, other stored fields).
- **Not verified:** whether the account DTO already has address and name fields. Check `src/core/api/src/AccountDtos.cs` first. If missing, adding them is a **contract change** to `src/core/api` (flag in the commit message).

### 2. Tab bar with subpages
- Convert `AccountDetails` into a tab shell: Details | Devices | Cases | Sync | (others as they exist).
- Each tab is route-backed under `/accounts/{Id}/...` so deep links and back navigation work.
- Retire `/devices/config` (`Pages/DeviceConfig.razor`): remove the route, its `PageAccessRules` entry, and its nav/links. **Before deleting, check other consumers** (`DeviceConfigEndpoints` callers, nav links). Its function moves to the Devices tab or is dropped; confirm which when this step starts.

### 3. Sync as a subpage
- New Sync tab absorbs `AccountSyncSchedule.razor` (schedule and controls) and the sync run history.
- Shows **Last Successful Auth**, **Last Download**, sync controls, sync history. Carries `_runningSyncType` from `152ddd66`.
- Retire `/accounts/{Id}/schedule` (redirect to the Sync tab). Update `PageAccessRules.cs` L51.

### 4. Per-device uptime / offline / last seen
- Devices tab: per device, Online / Offline status, offline warning past threshold, last-seen date/time.
- Threshold: configurable setting, default **5 minutes**, read in one place. No existing setting found; add one.
- Two device-health sources exist; choose one before building:
  - `src/providers/ring/common/src/Entities/DeviceHealth.cs` (Ring): `LastHeartbeatUtc`.
  - `src/data/common` entity: `IsOnline`, `CapturedAtUtc`.

### 5. Last Download and initial sync bug
- Reviewed: `LastDownloadTimeUtc` is written only by `VideoDownloadServiceAdapter.cs:660` (after a legacy batch video download, `totalFilesDownloaded > 0`) and by a client-supplied PUT at `AccountEndpoints.cs:228`. The sync path (`AccountSyncRunner.cs`) never touches it; it calls only `eventPullService.PullAccountEventsAsync` (L287). `LastSuccessfulAuthUtc` is set at `EventPullService.cs:100`.
- So the bug is "never populated by sync", not "not rendered".
- **Design decision, open:** the watermark moves only after a *video* download. Sync pulls events, snapshots and config, not videos. If sync writes `LastDownloadTimeUtc`, the next video download could skip files it never fetched. Options: (a) add a separate "last sync completed" field for display (recommended); (b) reuse `LastDownloadTimeUtc` and accept the risk.
- Requires a failing test before the fix (TDD per CLAUDE.md).
- Initial sync uses a fixed one-year backfill (`AccountSyncRunner.cs:282`), not the watermark.

### 6. Validate moves to Sync page
- `ValidateEvidence.razor` has routes `/analyze/validate/integrity` and `/analyze/validate/reconcile`. **Not yet decided:** keep, redirect, or remove them.
- Bulk-validate endpoint is `SuperAdminLocal` (`EvidenceEndpoints.cs`, around L173), which matches the Sync tab's access.
- Two labeled actions on the Sync tab: **Validate locally** and **Validate remote**. Each states what it checks in one line.

### 7. Query page: account status raw output and common filters
- `/query` (`QueryApi.razor`): add raw account status output (collapsible raw JSON).
- Common filters via `QueryApiFilterState` (currently LocationId, DeviceId only; extend), rendered as one reusable filter bar shared with `QueryApiDevices` and `QueryApiEvents`.

### 8. API tester output is unreadable
- Target page is `RingSelfTest.razor`. Table-driven: clicking a row selects it and shows details in a **bottom detail pane**.
- Detail pane lists issues for that row (errors/warnings, failing field and message).
- Remove the issue/detail wiring from the right-hand inspector on this page (`InspectorPanel` / `QueryApiInspectorMapper`). The inspector stays for other pages.
- `RingSelfTest.razor:468` already gates destructive actions at `SuperAdminLocal` in the UI.

### 9. Merge Query API and API Tester into one page, admin+ only
- Merge `/query` and `/query/api-tester` (`RingSelfTest.razor`) into one page. Retire `/query/api-tester` (redirect).
- Access: change `/query` from `SuperAdminLocal` to `Admin` in `PageAccessRules.cs` L46. **Add** a rule for the tester route, since it has none today (which is a current gap).
- Update references: `Layout/NavGroups.cs:98`; tests `NavGroupsTests.cs:101`, `QueryApiPageTests.cs:63`, `RingSelfTestPageTests.cs:416`.
- Server-side 403 risk: the `PageAccessRules.cs` comment above L46 says Query API calls go to provider services directly with **no server policy**. No `/api/v1/query` endpoints were found in `WebApp`. **Verify this before building**; if true, the 403 conflict does not exist.

## Decisions (resolved)

1. **Remove Account:** stays on the Details tab.
2. **Devices page to remove:** `/devices/config` (`Pages/DeviceConfig.razor`).
3. **Offline threshold:** configurable, default 5 minutes.
4. **Server policy for Query endpoints:** stays SuperAdmin. Do not change server policy.

## Answers (user, this round)

- **Last Download:** reuse `LastDownloadTimeUtc`, but sync must not cause videos to be skipped. Step 1 must design a guard (for example, the sync write only moves the watermark when no video download is pending, or the display reads a separate timestamp that never feeds the legacy watermark) and test it before the fix.
- **Item 6 validate routes:** remove `/analyze/validate/integrity` and `/analyze/validate/reconcile`. Check for links to them first.
- **Item 4 device health:** use the `data/common` entity (`IsOnline`, `CapturedAtUtc`).
- **Item 9 vs decision 4:** verify whether server `/api/v1/query` endpoints exist before step 4.

- **Guard design (chosen):** sync writes `LastDownloadTimeUtc` only when the account has no pending or unfinished video download. Still to find in code: what marks a video download as pending (not yet verified to exist for this account). Step 1's failing test must cover a synced account with a pending video download, and expect the watermark to stay put.

- **Pending signal (chosen, option 1):** `DownloadEvent` (`src/data/common/src/Entities/DownloadEvent.cs`) is a per-device, per-event download record with `DownloadStartedUtc` and nullable `DownloadCompletedUtc`. A video is pending when its `DownloadEvent` has `DownloadCompletedUtc == null`. The guard checks that no such row exists for any device of the account before sync writes `LastDownloadTimeUtc`. Still to confirm: the device-to-account link, and whether a failed attempt (`Success == false` with completion set) should count as pending.

- **Gap found in the chosen guard (not yet implemented):** a synced event has no `DownloadEvent` row until a legacy video download creates one. So "no pending row" is true for events the legacy download has never seen. Sync would then advance `LastDownloadTimeUtc` past them, and the next legacy download would skip them. The guard as written does not protect those events. Needs a decision before the failing test is written (see step 1 question to user).

- **Decision (user, final for step 1):** add a separate display field (last sync completed, on `ProviderAccount`) with a schema migration. Sync writes only this new field. `LastDownloadTimeUtc` stays owned by the legacy video download and is never written by sync. Step 1 becomes: failing test that a clean sync writes the new field and leaves `LastDownloadTimeUtc` unchanged; add the column and migration; write the field in `AccountSyncRunner` on a successful pull; show it on the Sync tab (step 2). The pending-download guard is no longer needed.

## Remaining open questions (superseded where answered above)

1. **Item 9 vs. decision 4 (blocks step 4):** if no server Query endpoints exist (see item 9), this is moot. Otherwise pick:
   - (a) Keep the merged page at SuperAdmin.
   - (b) Admin can open the page; server-restricted calls are hidden or disabled for Admin. `RingSelfTest.razor` already supports (b).
2. **Last Download (blocks step 1 fix):** option (a) separate sync-completed field, or (b) reuse the watermark (see item 5).
3. **Item 6:** keep, redirect, or remove `/analyze/validate/integrity` and `/analyze/validate/reconcile`.
4. **Item 4:** which device-health source to use (see item 4).
5. **Item 1:** do the account DTO fields exist? If not, approve the `src/core/api` contract change.

## Suggested order

1. Last Download / initial sync bug (item 5): resolve open question 2, failing test, fix. Unblocks item 3's display.
2. Account data + tab shell + Sync tab (items 1, 2, 3, 6). Carry `_runningSyncType`.
3. Devices tab uptime (item 4), including retiring `/devices/config` after checking consumers.
4. Query page merge, access change, filters, raw status (items 7, 9). Confirm open question 1 first.
5. Tester table + bottom detail pane (item 8).

Each step ships with tests (TDD). Breaking changes (access policy, removed routes, contract changes) update `CLAUDE.md` and the commit message.
