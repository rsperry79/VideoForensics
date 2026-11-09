# Manual sync account selector: one filter source, saved to localStorage

## Context

On `/accounts/manual-sync` the right-panel Account selector does not let the operator sync. The page keeps its own copy of the provider and account filter in page memory (`_selectedProviders`, `_accountFilterValue` in `ManualSync.razor`). The scope rail on the review pages keeps a different copy in `ScopeState`, which is synced to the URL and not saved. The two copies can disagree, and the page's copy is lost on reload.

Goal: the provider and account chosen in the rail are the same values the manual-sync controls show. Filters a page does not show are ignored by that page. The values are saved to localStorage.

Cause (not yet reproduced): `TargetRow` needs both the account and its provider to be in `_selectedProviders`. `AccountFilterOptions` is rebuilt from that list, and `LoadAccountsAsync` clears the account after every sync. Step 1 confirms this with a failing test before any fix.

## Decisions (from you)

- Shared source: `ScopeState` provider and account, plus localStorage.
- Manual-sync uses a single provider, matching the rail. The multi-select is removed on this page.

## Steps

1. **Red test first.** In `src/client/ui/VideoForensics.Ui.Shared/tests/ManualSyncPageTests.cs`, add a test: select an account from the Account control, then assert Sync Now is enabled and posts to `.../sync-now` for that account. Run it and record the red result before changing code.
2. **Persist the shared filter.** New `Services/Scope/ScopeFilterStore.cs`, modelled on `Services/Review/ReviewSettingsStore.cs` (localStorage through `IJSRuntime`, try/catch on `JSException`). Key `vf.scope.filters`. Stores provider, account and location. Saved whenever `ScopeState` changes.
3. **Restore on load.** On first load, when the URL has no provider, account or location, restore the saved values. A URL that carries them wins, as `ScopeRail` already does for other settings.
4. **ManualSync uses ScopeState.** Remove `_selectedProviders` and `_accountFilterValue`. Read provider and account from `ScopeState.Current`. Replace the multi-select with the existing `FilterProviderSelect` and `FilterAccountSelect` (the rail's components), so the controls are identical. Use `ScopeRailRules.ApplyProviderPick` when the provider changes. `TargetRow` is the account from the shared value. Device, location and date values are not read on this page.
5. **Live updates.** Subscribe to `ScopeState.OnChange` in `OnInitializedAsync`, refresh through `InvokeAsync`, unsubscribe in `Dispose` (`client-live-data.md`).
6. **Localization.** Any new text goes in `ManualSync.resx` in the same change (`ui-shared.md`). Reuse existing `SharedResources` keys where they exist.
7. **Client split.** No server or DTO change. Nothing new is sent over HTTP. Local-only UI state.

## Critical files

- `src/client/ui/VideoForensics.Ui.Shared/src/Pages/ManualSync.razor` (main change)
- `src/client/ui/VideoForensics.Ui.Shared/src/Services/Scope/ScopeState.cs` (existing shared state; confirm it is circuit-scoped and injectable here)
- `src/client/ui/VideoForensics.Ui.Shared/src/Services/Scope/ScopeRailRules.cs` (reuse `ApplyProviderPick`)
- `src/client/ui/VideoForensics.Ui.Shared/src/Components/Scope/ScopeRail.razor` (reference for control wiring and the restore pattern)
- `src/client/ui/VideoForensics.Ui.Shared/src/Services/Review/ReviewSettingsStore.cs` (localStorage pattern to copy)
- `src/client/ui/VideoForensics.Ui.Shared/tests/ManualSyncPageTests.cs` (red test)

## Verification

- Build the Ui.Shared test project to a scratch OutDir with `-m:1`.
- Run `dotnet test` with a namespace filter (`FullyQualifiedName~ManualSync`), confirm with `--list-tests`, and read the summary.
- Confirm the red run before the fix, then green after.
- Rerun the failing test on `wip` for a baseline if it fails under load (`task_failure_log.md` rule).
- Browser check on a local dev host: pick an account on the rail, open manual-sync, confirm the same account is selected, reload, confirm it persists.

## Open point to confirm before implementation

Saving the shared filter means the rail's provider, account and location also survive a reload on every review page, not only on manual-sync. Is that what you want?

---

## Decisions recorded (2026-11-05)

- **RSSI and deviation flags, event type:** match the event whose clip overlaps the flag's window, using the RSSI review page's search pad. Show "No event" when nothing overlaps.
- **Bit rate Dev baseline:** add a camera-wide all-time bit rate mean and sd query, so the value matches the grid and signal figures.
- **Push route:** build `/api/v1/notifications/preferences` once, in Issue 10, covering push and email. Issue 8 only fixes the subscribe parse and page loading.
- **Recipient on export:** remove it from the export manifest as well. This changes the manifest format; older exports will not match.
- **Import / Export:** move to a new top-level Tools group.
- **test-support:** move into `src/utils/testing/src/`, and add a Utils.Testing reference to every test project.
- **Dependabot alerts:** review each alert, then fix criticals and moderates, one package per commit. Stop before any major version bump.
- **My Notifications email:** on/off plus minimum severity, the same as push.
- **Passphrase (Issue 25):** remove it. Exports are no longer passphrase-protected; another protection is needed before this ships (not yet planned).
- **Dev backfill (Issues 17 and 18):** backfill existing flags from the current all-time baseline, as a one-off job.

# Issue 2: settings for Jamming, codec and flag backfills on /settings/services

## Context

`ServicesSettings.razor` exposes intervals for log prune, update check and media backfill (thumbnail and preview). The Jamming, codec and flag backfills are not on the page. They run on a hard-coded `PassInterval = 15 min` (`JammingAnalysisBackfillService.cs:21`, `MediaCodecBackfillService.cs:20`, `MediaFlagBackfillService.cs` in the same pattern), so the operator cannot change them.

The existing page only takes whole hours (1 to 168), so it cannot hold a 15-minute value.

Decisions (from you): store in **minutes** (1 to 1440, default 15). **One setting per service** (three inputs).

## Steps

1. **Red test first.** In `ServiceIntervalToolsTests.cs` (Client.Core), add cases for the three new service keys and the minute range, and a case that the default is 15. Record the red run.
2. **Config model.** Add `JammingIntervalMinutes`, `CodecIntervalMinutes`, `FlagIntervalMinutes` (default 15) to `IForensicsConfiguration` (`Client.Common/src/Contracts/IForensicsConfiguration.cs`), `ConfigDtos.cs` (the Api DTO, both directions), and `ForensicsConfigurationService.cs` (get and set, same as `MediaBackfillIntervalHours` at lines 148 and 226).
3. **Orchestrator.** Extend `ConfigToolsOrchestrator.SetServiceIntervalHoursAsync` (or add `SetServiceIntervalMinutesAsync`, since the unit differs) with the three keys and a 1 to 1440 minute check. Also add the matching method to `IConfigToolsOrchestrator` and `GuardedConfigToolsOrchestrator` (WebApp circuit guard).
4. **Services read config.** Each of the three services takes `IForensicsConfiguration` (as `MediaThumbnailBackfillService` does via `CurrentInterval`) and uses its own minutes value for `PeriodicTimer`. Use a `CurrentInterval` property read on each pass. Keep the 5-minute startup delay fixed. Note: `PeriodicTimer` is created once, so a change needs a re-created timer on the next pass. Plan the loop around that.
5. **UI.** In `ServicesSettings.razor`, add three `input type="number"` rows (min 1, max 1440) with Save buttons, next to the existing backfill row. Follow the existing `SaveIntervalAsync` pattern. Add resource keys to `ServicesSettings.resx` in the same change (`ui-shared.md`). Remove or reword `FixedBackfillNote`, since these services are no longer fixed.
6. **Client split.** The setting goes through the existing config route and the paired-device bearer token. No new endpoint, and no new DTO shape beyond the three fields.

## Critical files

- `src/client/host/VideoForensics.Hosting/src/BackgroundServices/JammingAnalysisBackfillService.cs`
- `src/client/host/VideoForensics.Hosting/src/BackgroundServices/MediaCodecBackfillService.cs`
- `src/client/host/VideoForensics.Hosting/src/BackgroundServices/MediaFlagBackfillService.cs`
- `src/client/common/VideoForensics.Client.Common/src/Contracts/IForensicsConfiguration.cs`
- `src/core/api/src/ConfigDtos.cs`
- `src/client/core/VideoForensics.Client.Core/src/Services/ForensicsConfigurationService.cs`
- `src/client/core/VideoForensics.Client.Core/src/Tools/ConfigToolsOrchestrator.cs`
- `src/client/web/VideoForensics.WebApp/src/Services/CircuitGuards/GuardedConfigToolsOrchestrator.cs`
- `src/client/ui/VideoForensics.Ui.Shared/src/Pages/ServicesSettings.razor` and `.resx`

## Verification

- Red then green for `ServiceIntervalToolsTests` (namespace filter, `--list-tests` to confirm).
- Hosting tests: add a test that a changed minute value is used on the next pass (the service reads config, not a constant).
- Ui.Shared tests: `ServicesPageAccessTests` plus a test that the three inputs save and show errors.
- Build each touched project with `-m:1` to a scratch OutDir. Rerun any load-sensitive failure on `wip` for a baseline.
- Local browser check on `/settings/services`: change a value, reload, confirm it stays.

## Open point

Existing deployments have no stored value for the three new keys. Defaults of 15 match today's behaviour, so no migration is needed. Confirm this is fine.

---

# Issue 3: /tools/import-export (menu location and empty export)

## Findings

- **Menu:** `NavGroups.cs:117` lists "Import / Export" under Admin > Storage & Data, but the route is `/tools/import-export`. Gated by `PageAccessRules.cs:93` (`SuperAdminLocal`). The target group is still open (pending your answer).
- **Empty backup:** `RemoteBackupExportService.ExportBackupAsync` (`Hosting/src/Remote/RemoteBackupExportService.cs:71-83`) returns a zip result with every count hard-coded to 0. The page therefore shows a successful export with zero accounts, locations, devices, events and media, even when the zip has data.
- Also check: the page passes the full local staging path (`StagingLocator.GetStagingDirectory()`), but `BackupEndpoints.cs:49-57` rejects rooted paths with "Invalid OutputDirectory". Whether this happens depends on which `IBackupExportService` the web page resolves. Confirm in a test.
- Server-side `BackupExportOrchestrator.ExportBackupAsync` (`Client.Core/src/Services/BackupExportOrchestrator.cs:108-198`) builds the zip correctly. The counts come from the repositories, so a real empty DB would also produce zeros. Check which case applies before changing code.

## Steps (provisional)

1. **Red tests.** (a) `RemoteBackupExportServiceTests`: a zip response must report the real counts (read them from `manifest.json` inside the zip). (b) A test that a rooted staging path is handled or rejected with a clear message. Record the red run.
2. **Counts.** Read `manifest.json` from the zipped response in `RemoteBackupExportService`, or add counts to a response header, so the page shows the real totals.
3. **Path.** Decide whether the client sends a folder name only (server writes under its temp root) or the server accepts the operator's chosen folder. Fix the mismatch so the file reaches the save dialog.
4. **Nav.** Move the Import / Export entry to the group you choose. Update `NavGroups`, `PageAccessRules` if needed, and the nav tests (`AdminNavConfigurationTests`, `PageAccessRulesTests`, `NavGroupsTests`).
5. **Resources.** Any new label goes in `ImportExport.resx` in the same change.

## Pending

- Which group Import / Export should move to. Your last reply did not come through as text, so I have not chosen one.
- The "Save or download" and "Buttons navigate" menu options were not chosen; only "Nav item is in the wrong group" was.

---

# Issue 4: /settings/security-events unknown-login filter

## Decisions (from you)

- "Unknown login" = `AuthFailure` rows where no operator could be matched (`OperatorId` is null, so the query uses `IS NULL`).
- The filter moves to the right-side panel (`PageRightPanelContent`, as on manual-sync).
- The setting is saved in localStorage.

## Findings

- `SecurityEvents.razor` filters only by operator (SuperAdmin picker, server-side via `ISecurityEventsService.GetEventsAsync(operatorId, offset, pageSize)`). The Status column shows success or failure, so there is no unknown filter.
- `SecurityAuditEventTypes.AuthFailure` already exists (`Data/Common/src/Entities/SecurityAuditLogEntry.cs:41`).
- Per `providers.md`, the filter must run in SQL, not after `ToListAsync()`.
- Remote path: `RemoteSecurityEventsService` calls `/api/v1/security-events`.

## Steps

1. **Red tests first.** (a) `SecurityAuditLogRepositoryTests` (data/database): an unknown-only query returns only `AuthFailure` rows with null `OperatorId`. (b) `RemoteSecurityEventsServiceTests`: the new query flag is sent. (c) `SecurityEvents` page test: the toggle is in the right panel and sends the flag. Record the red runs.
2. **Server.** Add an `unknownOnly` (bool, default false) query parameter to the security-events endpoint in `WebApp` and apply it in the repository query (SQL `WHERE`), not in memory.
3. **Contract.** Extend `ISecurityEventsService.GetEventsAsync` with the flag (interface name unchanged, per `client-split.md`). Update `RemoteSecurityEventsService` to send it. Update any existing call sites and fakes.
4. **Local storage.** Add a small store on the same pattern as `LayoutPreferencesState` (`vfLayoutPrefs.get/set`, try/catch on `JSException`). Key `vf.securityEvents.unknownOnly`. Restore on load; save on change.
5. **Right panel.** Move the unknown-login toggle into `PageRightPanelContent` (as `ManualSync.razor` does). Keep the operator picker and refresh where they are unless you say otherwise.
6. **Localization.** New keys go in `SecurityEvents.resx` in the same change.
7. **Live updates.** Not needed beyond the current load. If the page subscribes to any observable source, follow `client-live-data.md`.

## Critical files

- `src/client/ui/VideoForensics.Ui.Shared/src/Pages/SecurityEvents.razor` (and `.resx`)
- `src/data/database/src/Repositories/` (security audit log repository; confirm its exact file name)
- `src/client/web/VideoForensics.WebApp/src/Api/` (security-events endpoint)
- `src/client/host/VideoForensics.Hosting/src/Remote/RemoteSecurityEventsService.cs`
- `src/client/common/VideoForensics.Client.Common/src/Contracts/` (`ISecurityEventsService`)
- `src/client/ui/VideoForensics.Ui.Shared/src/Services/LayoutPreferencesState.cs` (localStorage pattern)

## Verification

- Red then green for each new test (namespace filter, `--list-tests`).
- Build touched projects with `-m:1` to a scratch OutDir.
- Browser check on a local dev host: toggle the filter, reload, confirm it persists and the list changes.

## Open point

The operator picker is still on the page body. Should it also move to the right panel, to match the request?

---

## Decisions: Issue 4 (2026-11-07, from the operator)

- The unknown-login filter shows failed logins whose username matches no operator, as "Invalid user: <typed name>".
- Store the typed username on those failures: nullable `AttemptedUsername` on `SecurityEvent`, plus nullable `OperatorId` (EF migration).
- Write unknown-username failures to `SecurityEvent` (in addition to the audit log) with a null operator. Today only `OperatorAuthEndpoints.cs` ~line 779 logs them, and only to `SecurityAuditLogEntry`.
- The unknown-login filter is `OperatorId IS NULL` in SQL, in `SecurityAuditService.GetOperatorEventsAsync`.
- The filter moves to the right panel, saved through `RightPanelFilterStore`.
- Supersedes the step list below where it conflicts.

## Review findings: Issue 4 (2026-11-07, Sonnet read-only review)

- **Wrong table.** `/api/v1/security-events` reads `SecurityEvent` (`ISecurityAuditService.GetOperatorEventsAsync` → `SecurityAuditService.cs`), not `SecurityAuditLogEntry`. `AuthFailure` exists only on `SecurityAuditLogEntry`. `SecurityEvent.OperatorId` is non-null, so "`OperatorId IS NULL`" matches no rows. Decision needed (see open question).
- **Wrong test class.** Use `src/client/web/VideoForensics.WebApp/tests/Api/SecurityEventsEndpointsTests.cs` for the endpoint, not `SecurityAuditLogRepositoryTests`.
- **Endpoint is POST.** The flag goes on `SecurityEventsQueryRequest` (`src/core/api/src/SecurityEventsDtos.cs`), not a query string.
- **Contract breadth.** `ISecurityEventsService.GetEventsAsync` callers: `RemoteSecurityEventsService`, `SecurityEvents.razor` (5 sites), `SecurityEventsPageTests.cs` (~8 Moq setups). `ISecurityAuditService`: `SecurityEventTools.cs:104`, `SecurityEventsEndpointsTests.cs:98,139`, `McpToolAuthorizationTests.cs:89,112`.
- **Restore pattern.** Use `RightPanelFilterStore` (`vf.filters.<pageKey>`), not `LayoutPreferencesState`. Check `Notices.razor` for the restore precedent.
- **Ordering.** Add the contract parameter before the red tests, or the red run is a compile failure.

## Answers to open points (2026-11-07, from the operator)

- Issue 2: defaults of 15, no migration.
- Issue 3: move the operator picker to the right panel.
- Issue 5: show LocalHost in exports and the database too (plan changed from display-only; recheck the Issue 5 steps).
- Issue 7: no redirect.
- Issue 10: email choice is a list of event types (plan changed from on/off plus severity; recheck the Issue 10 steps).
- Issues 11–14: absolute d; dev = row and column sigma; colour uses exact d.
- Issue 16: flag type as a second line; RSSI flags use overlap, else "No event".
- Issues 17–18: backfill existing flags. Dev backfill for collapse flags: yes.
- Issue 18: new all-time bit rate baseline query.
- Issue 19: change the aria-label as well as the visible labels.
- Issue 20: label shows the clip under the playhead.
- Issue 21: auto-advance goes to next (older).
- Issue 23: tooltip shows clock time.
- Issue 25: remove passphrase and Recipient description.
- Issue 27: Zeroconf (3.3) and IPNetwork2 (4.2).
- Issue 30: review and fixes.
- Issue 31: move test-support into VideoForensics.Utils.Testing.
- Issue 4 passkey: the passkey sign-in form requires a username first, so a failed attempt is recorded with that name.
- Issue 8/10: replace `/api/v1/push/preferences` with `/api/v1/notifications/preferences`; Issue 8's endpoint and tests are rewritten to match.
- Issue 10: remove the old push preferences route (no compatibility code). Add a `Remote*` service for the notifications page.
- Issue 12: confirm the Api.Contracts DTO change for the all-time calendar and baseline.
- Issue 28: archive destination is `docs/plans/archived`.
- Issue 29: check each plan's status against the code before moving it.

## Status: Issue 4 (2026-11-07)

- Done on `wip` (2363f6cb). Gap tests added on `feature/invalid-user-events`: threat-intel block, blocked country, GeoIP fail-closed, password sign-in disabled. All four record the unknown-user event. 9/9 pass in `OperatorAuthEndpoints_SecurityAuditIntegrationTests`.
- Red run not taken for these four: the paths already existed, so a failing test first would have needed a code change that is not there. Recorded here, per implement-plan step 3.
- Open: passkey paths (no typed username); browser check of `/settings/security-events` on a local host.

# Issue 5: show loopback IP as "LocalHost"

## Findings

- Both places that show a source IP render the raw value: `SecurityEvents.razor:101` (`@evt.IpAddress`) and the `SourceIp` grid column in `SecurityAuditLog.razor:74`.
- Loopback forms to map: `::1`, `127.0.0.1`, `::ffff:127.0.0.1`. The existing `IPAddress.IsLoopback` covers all three, and `LocalNetwork.cs` already has loopback logic.

## Scope confirmed

The same LocalHost display applies on /settings/security-log (`SecurityAuditLog.razor`) as well as /settings/security-events.

## Decision (default, unless you say otherwise)

- Display only. The stored audit value stays as it is, so audit records are not changed.
- Show the label "LocalHost" (localized as a resource key) for loopback addresses on both pages.

## Steps

1. **Red test first.** A unit test for a new formatter (e.g. `SourceIpLabel.Format(ip, localHostLabel)`) in `Ui.Shared/tests`: `::1`, `127.0.0.1` and `::ffff:127.0.0.1` give "LocalHost"; a LAN address and null/empty pass through unchanged. Record the red run.
2. **Formatter.** Add a small static helper in `Ui.Shared/src/Services/` (pure function, no I/O). Use `IPAddress.TryParse` and `IsLoopback`, not string comparison.
3. **Pages.** `SecurityEvents.razor` and the `SecurityAuditLog.razor` column use the formatter. For the grid column, use a template that calls the formatter instead of the raw field.
4. **Localization.** Add a `LocalHost` key to `SharedResources.resx` (shared between both pages) and the page `.resx` files where needed, in the same change.

## Critical files

- `src/client/ui/VideoForensics.Ui.Shared/src/Pages/SecurityEvents.razor`
- `src/client/ui/VideoForensics.Ui.Shared/src/Pages/SecurityAuditLog.razor`
- New helper in `src/client/ui/VideoForensics.Ui.Shared/src/Services/`
- `src/client/ui/VideoForensics.Ui.Shared/src/Resources/SharedResources.resx`

## Verification

- Red then green for the formatter tests.
- Ui.Shared test filter for the two page test classes, confirm with `--list-tests`.
- Browser check on a local dev host (the local host itself uses `::1` or `127.0.0.1`): the Source IP shows "LocalHost".

## Open point

Should the stored value also show as "LocalHost" in exports or the database, or only on screen? The plan changes the display only.

---

# Issue 6: Event Type column is redundant on /settings/security-events

## Findings

- `SecurityEvents.razor` shows an **Event Type** column (`<td>@evt.EventType</td>`, line 90) next to Status and Reason.
- Once the unknown-login filter (Issue 4) selects `AuthFailure` rows, the event type adds nothing for that view. Status already shows success or failure.

## Interpretation (default, unless you say otherwise)

Remove the Event Type column from the Security Events table only. The data stays in the API response and the Reason column. The Security Audit Log page (`SecurityAuditLog.razor`) is not changed.

## Steps

1. **Red test first.** In the Security Events page test, assert that no Event Type column header is rendered and the row shows Status, IP, Date and Reason. Record the red run.
2. **Remove the column.** Delete the `EventTypeColumn` header and the `<td>@evt.EventType</td>` cell in `SecurityEvents.razor`. Remove `EventTypeColumn` from `SecurityEvents.resx` only if no other page uses it (check first).
3. **Keep the API.** No DTO or endpoint change; `EventType` stays in `SecurityEventSummary`.

## Critical files

- `src/client/ui/VideoForensics.Ui.Shared/src/Pages/SecurityEvents.razor` (line 90 and header)
- `src/client/ui/VideoForensics.Ui.Shared/src/Pages/SecurityEvents.resx`

## Verification

- Red then green for the page test (namespace filter, `--list-tests`).
- Browser check on a local dev host: the table no longer shows Event Type, and Reason still shows.

## Open point

Confirmed: the Event Type column is the one to remove (you chose this over removing Reason).

---

# Issue 7: move /admin/pending-credentials into /settings/operators

## Findings

- `/settings/operators` (`SecurityOperators.razor`, from line 109) already has a "Pending credentials" section, with its own grid, loading, and error states.
- The standalone page `PendingCredentials.razor` (`@page "/admin/pending-credentials"`) duplicates it, and has its own approve/reject actions and step-up flow.
- Nav entry: `NavGroups.cs:122` ("Pending Approvals", Admin > Security). Access rule: `PageAccessRules.cs:87` (`SuperAdminLocalOrPasskey`).

## Decision (confirmed)

Merge into the existing section on /settings/operators. Add any actions the standalone page has that the operators section lacks. Then remove the `/admin/pending-credentials` route, its nav entry, and its access rule.

## Steps

1. **Gap check (read-only, before the red test).** Compare the two pages' row actions and step-up calls, and list anything the operators section is missing.
2. **Red tests first.** In the operators page tests, assert that the pending section renders the approve/reject actions (or whatever the gap check finds). Record the red run.
3. **Merge actions.** Add the missing actions to the operators pending section, reusing the standalone page's `WebAuthnClient` and `StepUpService` calls. No new interface.
4. **Remove the standalone page.** Delete `PendingCredentials.razor` and its `.resx` only after step 3 is green and no other page links to it (grep first). Remove the `NavGroups.cs:122` entry and the `PageAccessRules.cs:87` rule. Update the nav tests (`AdminNavConfigurationTests`, `PageAccessRulesTests`) that list `/admin/pending-credentials`.
5. **Redirect.** Decide whether the old route redirects to `/settings/operators`. The default is no redirect, because the route was admin-only and not linked from outside the app. Confirm before adding one.
6. **Localization.** Any new labels go into `SecurityOperators.resx` in the same change. Keys used only by the removed page are deleted from its `.resx`.

## Critical files

- `src/client/ui/VideoForensics.Ui.Shared/src/Pages/SecurityOperators.razor` (pending section from line 109)
- `src/client/ui/VideoForensics.Ui.Shared/src/Pages/PendingCredentials.razor` (to remove after the merge)
- `src/client/ui/VideoForensics.Ui.Shared/src/Layout/NavGroups.cs` (line 122)
- `src/client/ui/VideoForensics.Ui.Shared/src/Layout/PageAccessRules.cs` (line 87)
- `src/client/ui/VideoForensics.Ui.Shared/tests/AdminNavConfigurationTests.cs` and `tests/Layout/PageAccessRulesTests.cs`

## Verification

- Red then green for the operators page test (namespace filter, `--list-tests`).
- Ui.Shared full test filter for Layout and Admin nav, to confirm the removed route is no longer referenced.
- Browser check on a local dev host: the pending section on /settings/operators shows the approve/reject actions, and /admin/pending-credentials returns not found.

## Open point

Confirm the redirect decision in step 5 (default: no redirect).

---

# Issue 8: PushNotifications on /settings/notifications is broken

## Findings

1. **Save preferences always fails.** `Notifications.razor:423` posts to `api/v1/push/preferences`, but no such endpoint exists. `PushEndpoints.cs` maps only `vapid-public-key`, `subscribe` and `unsubscribe`. Result: "SavePreferencesFailed", and `pushEnabled` and `minimumSeverity` are never stored.
2. **The server already reads the preferences.** `WebPushNotificationProvider.cs:82-99` loads `OperatorNotificationPreference` through `IOperatorNotificationPreferenceRepository` (`GetAsync`, `UpsertAsync` exist in `data/common/src/Contracts`). Only the write path is missing.
3. **Enable may throw.** `WebPushClient.SubscribeAsync` (`Ui.Shared/src/Services/WebPushClient.cs:58-79`) reads the JS result as `dynamic` and indexes it with `result["endpoint"]`. A `JsonElement` has no string indexer, so this can throw `RuntimeBinderException`. The `catch` only handles `JSException`, so the error surfaces as "EnablePushFailed". Not yet reproduced. The red test below confirms it.

## Steps

1. **Red tests first.**
   - (a) `PushEndpoints` test: POST `/api/v1/push/preferences` with `{pushEnabled, minimumSeverity}` for an authenticated operator upserts the preference row (currently 404). Record the red run.
   - (b) `WebPushClient` test: the JS result, given as a JSON object, maps to endpoint, p256dh and auth without a `dynamic` cast (currently throws). Record the red run.
2. **Add the endpoint.** `POST /api/v1/push/preferences` in `PushEndpoints.cs`, with `RequireAuthorization()`. Read the operator id from the claim (as `subscribe` does), validate `minimumSeverity` against the enum range, and call `UpsertAsync`. Add a `PushPreferencesRequest` record next to the existing request records. Add the audit log only if the other push endpoints log (they do not today, so do not add it).
3. **Fix the subscribe parse.** Replace the `dynamic` casts in `WebPushClient.SubscribeAsync` with a typed `JsonElement` read or a small record, and handle a missing field with an `InvalidOperationException` message.
4. **Read existing state (confirmed, in this change).** Add `GET /api/v1/push/preferences` and have the page load it, so the checkbox and severity show the saved values after reload. Without this, the page resets to defaults every visit. Add a red test for the GET as well.
5. **Localization.** Any new text goes into `Notifications.resx` in the same change.
6. **Client split.** The new endpoint uses the existing paired-device bearer token and `/api/v1` route. No new auth mechanism, no new DTO outside the request record.

## Critical files

- `src/client/web/VideoForensics.WebApp/src/Api/PushEndpoints.cs` (add the preferences route)
- `src/client/ui/VideoForensics.Ui.Shared/src/Services/WebPushClient.cs` (subscribe parse, lines 58-79)
- `src/client/ui/VideoForensics.Ui.Shared/src/Pages/Notifications.razor` (lines 290-449, the push section)
- `src/data/common/src/Contracts/IOperatorNotificationPreferenceRepository.cs` (existing `UpsertAsync`)
- `src/client/web/VideoForensics.WebApp/src/Hubs/WebPushNotificationProvider.cs` (read side, lines 82-99; no change expected)

## Verification

- Red then green for (a) and (b) (namespace filter, `--list-tests`).
- WebApp endpoint tests for the new route: authenticated upsert, and unauthenticated 401.
- Browser check on a local dev host: enable push, set severity, save, reload, confirm the values remain and "PreferencesSaved" shows.

## Open point

Resolved: step 4 (GET so saved values load on reload) is included in this change.

---

# Issue 9: move Notifications and Reports to the System & Network section

## Findings

- `NavGroups.cs:104` lists "Notifications" (`/settings/notifications`) under `Section: "General"`.
- `NavGroups.cs:105` lists "Reports" (`/settings/reports`) under `Section: "General"`.
- The target section, "System & Network", already holds Network Access, Infrastructure, Database and Services (`NavGroups.cs:111-114`).

## Steps

1. **Red test first.** In `NavGroupsTests` (Ui.Shared/tests/Layout), assert that the Notifications and Reports items have `Section == "System & Network"`. Record the red run.
2. **Change the section.** In `NavGroups.cs`, set `Section: "System & Network"` on the two items. Keep the item order inside the section sensible (suggest after Services).
3. **Check the General section still has content.** General keeps General, App Lock. Confirm nothing else depends on the General section being empty.
4. **No route, access or page change.** The URLs and `PageAccessRules` entries are unchanged.

## Critical files

- `src/client/ui/VideoForensics.Ui.Shared/src/Layout/NavGroups.cs` (lines 104-105)
- `src/client/ui/VideoForensics.Ui.Shared/tests/Layout/NavGroupsTests.cs`

## Verification

- Red then green for the NavGroups test (namespace filter, `--list-tests`).
- Browser check on a local dev host: Admin > System & Network shows Notifications and Reports, and General no longer does.

---

# Issue 10: per-user notifications page under Admin > General

## Decision (confirmed)

- New General page for the signed-in operator's own email and push choices, including registering this device for push. Suggested route: `/settings/my-notifications`, label "My Notifications" (confirm the name when implementing).
- The System & Network `/settings/notifications` page keeps only system-wide settings. Its push controls (enable, severity, check, save) move to the new page (Issue 8's fixes apply there).

## Findings

- Push preferences are per operator already: `OperatorNotificationPreference` (`PushEnabled`, `MinimumSeverity`) with `IOperatorNotificationPreferenceRepository` (`GetAsync`, `UpsertAsync`). Push subscriptions are per operator (`PushSubscription`, `/api/v1/push/subscribe`).
- Email is configured at `/settings/integrations/email` (system level). The per-user email choice needs a field on `OperatorNotificationPreference` (e.g. `EmailEnabled`, minimum severity) and a migration. Confirm the email preference granularity before implementing (see open point).
- `NavGroups.cs` General section (lines 102-105) gets the new item.

## Steps

1. **Data.** Add `EmailEnabled` (bool, default false) and `EmailMinimumSeverity` (int) to `OperatorNotificationPreference`, with EF migration (`dotnet ef migrations add`, then `ef_` checks per the existing migration rules; restore the snapshot on conflict). Red test first in `data/database/tests`.
2. **Server.** Extend `POST/GET /api/v1/push/preferences` (Issue 8) into a `/api/v1/notifications/preferences` pair covering push and email for the signed-in operator. Keep the old push route name only if other callers use it (none found).
3. **Email send path.** The server sends email only for operators with `EmailEnabled`. Reuse the existing email sender behind `/settings/integrations/email`; add no new provider interface. Red test that a below-minimum event is skipped.
4. **UI.** New `MyNotifications.razor` page under General, with: email on/off and minimum severity; push section moved from `Notifications.razor` (enable this device, severity, check, save); localized via `MyNotifications.resx`.
5. **System page.** Remove the push section from `Notifications.razor` (System & Network) and keep system-wide notification settings there.
6. **Nav.** Add the new item to `NavGroups.cs` General. Add its `PageAccessRules` entry (signed-in operator; no admin gate). Update `AdminNavConfigurationTests`, `PageAccessRulesTests` and `NavGroupsTests`.
7. **Client split.** Use the paired-device bearer token and `/api/v1` routes. No new auth mechanism.

## Critical files

- `src/data/common/src/Entities/OperatorNotificationPreference.cs` and its EF configuration and migration (`data/database/sqlite/src/Migrations`)
- `src/client/web/VideoForensics.WebApp/src/Api/PushEndpoints.cs` (or a new notifications endpoint file)
- `src/client/web/VideoForensics.WebApp/src/Hubs/WebPushNotificationProvider.cs` (email branch alongside push)
- `src/client/ui/VideoForensics.Ui.Shared/src/Pages/Notifications.razor` (remove push section)
- `src/client/ui/VideoForensics.Ui.Shared/src/Pages/MyNotifications.razor` (new)
- `src/client/ui/VideoForensics.Ui.Shared/src/Layout/NavGroups.cs` and `PageAccessRules.cs`

## Verification

- Red then green for the data, endpoint, email-skip and page tests (namespace filter, `--list-tests`).
- Migration check: `InitialCreate_Schema_MatchesCurrentModel` style test passes.
- Browser check on a local dev host: General > My Notifications saves email and push choices, and the System & Network page no longer shows push.

## Open point

What should the per-user email choice cover: on/off with a minimum severity (as push does), or a list of event types? The plan assumes on/off plus minimum severity.

---

## Review note: access policy, client pattern and Issue 8 overlap

- Access: `AccessPolicy` (`PageAccessRules.cs:9`) has no "any signed-in operator" value; the lowest is `ReadOnly`. Choose one, and check `SettingsAccessGateTests.cs` for how General items are gated for non-admins.
- Client pattern: `Notifications.razor:423` posts with a raw `HttpClient`. `client-split.md` expects a `Remote*` service with `CancellationToken`. Decide whether a small service is needed.
- Overlap with Issue 8: Issue 8 adds `/api/v1/push/preferences`; this issue replaces it with `/api/v1/notifications/preferences`. Decide whether 8's endpoint is the final route (fields added here), or whether 8 is dropped in favour of this issue. The tests for 8 would otherwise be rewritten.
- Backwards compatibility: keeping the old push route needs a decision (CLAUDE.md asks before any compat code).

# Issue 11: cap Signal and Bitrate alert lists at 5, with show-more

## Findings

- `SignalHistory.razor:62-80` and `BitrateHistory.razor:61-78` each render the 2-sigma alert banner as a `<ul>` with one `<li>` per alert, with no limit.
- The two banners are the same shape: a `data-testid` (`signal-alert` / `bitrate-alert`), a dismiss button, a title, and a list. Their dismiss state (`_dismissedAlertSignature`, `AlertSignature`, `DismissAlerts`) is also the same.

## Decision (confirmed)

Show at most 5 items. If there are more, show a link "Show N more" that expands the list, and "Show less" to collapse it again.

## Steps

1. **Red tests first.** In `SignalHistory_Tests` and `BitrateHistory` tests: with 7 alerts, 5 `<li>` items render and a "Show 2 more" link is present. Clicking the link renders all 7 and shows "Show less". Record the red run.
2. **Shared component.** Create `Components/Review/DeviationAlertBanner.razor`, taking `Alerts` (a list of `(string Name, string Line)` or a render fragment per line), `TestId`, `DismissTestId`, `OnDismiss`, and a `Limit` (default 5). This follows the `ui-shared.md` rule to reuse controls rather than copy one per page. Both pages use it, so the cap and expand logic is written once.
3. **Expand state.** The component keeps a private `_expanded` bool. It resets when the alert signature changes, so a new set of alerts starts collapsed.
4. **Pages.** `SignalHistory.razor` and `BitrateHistory.razor` replace their inline banner with the component. The dismiss signature logic stays on each page, unchanged.
5. **Localization.** Add `ShowMoreAlerts` ("Show {0} more") and `ShowLessAlerts` keys to `SharedResources.resx`, since both pages use them. Each page's existing `AlertLine` keys are unchanged.

## Critical files

- `src/client/ui/VideoForensics.Ui.Shared/src/Components/Review/DeviationAlertBanner.razor` (new)
- `src/client/ui/VideoForensics.Ui.Shared/src/Pages/SignalHistory.razor` (lines 62-80)
- `src/client/ui/VideoForensics.Ui.Shared/src/Pages/BitrateHistory.razor` (lines 61-78)
- `src/client/ui/VideoForensics.Ui.Shared/src/Resources/SharedResources.resx`

## Verification

- Red then green for the two page tests and one component test (namespace filter, `--list-tests`).
- Browser check on a local dev host with more than 5 alerts: 5 show, "Show N more" expands all, "Show less" collapses, and dismiss still hides the banner.

---

# Issue 12: Week by hour grid, all-time values and three-band colours (Signal and Bit rate)

## Findings

- The grid is `section.Calendar` (`SignalCalendar.cs`), rendered at `SignalHistory.razor:221-260` and the matching bitrate block in `BitrateHistory.razor`. Rows are weekdays, columns are hours, and each box shows the mean (`cell.Average`) with a `title` of its reading count. Empty boxes show "-".
- The grid is built from the server analysis for the **selected range** (`SignalHistory.razor:425`, `SignalAnalysisService.AnalyzeAsync(inScopeIds, _from, _to, ...)`).
- The existing all-time baseline (`SignalDeviation.Windows` and `SignalStats baseline`) already uses the camera's all-time mean and sample std dev. It is used for the timeline bands only, not the grid.
- The current deviation colours are: `None` (no colour), `OneSigma` (yellow, > 1 sd), `TwoSigma` (red, > 2 sd). The green band does not exist, and grid boxes have no colour at all.

## Decisions (confirmed)

- **Colour rule:** for each box, `d = |boxMean − allTimeMean| / allTimeStdDev`. Green if `d < 1`, yellow if `d < 2`, red if `d >= 2`. Empty boxes stay uncoloured.
- **Range (revised in Issue 14):** the grid box means use the selected time range. The day (row) and hour (column) std devs and the colour baseline use all stored readings for the camera.
- Applies to both Signal History (RSSI, dBm) and Bit Rate History (kbps).

## Steps

1. **Red tests first.**
   - (a) SUPERSEDED by Issue 14: grid means follow the selected range, and only the row, column and baseline sd use all samples. Analysis test for that split, with the baseline mean and sd match an all-time calculation.
   - (b) Colour classifier test (pure function, next to `SignalDeviation.Classify`): boundaries at exactly 1 and 2 sd (1.0 is yellow, 2.0 is red), and an empty box returns no colour.
   - (c) Page test: a box with `d < 1` renders the green class, and a box with `d >= 2` renders red. Record the red runs.
2. **Analysis change (server side).** Compute the calendar and the baseline over all samples for the in-scope cameras, separate from the range-filtered timeline data. Keep one analysis call; add the all-time calendar and baseline to the response (server-side aggregation, not in-memory filtering on the client). Confirm the DTO change with the client-split rule (`Api.Contracts` DTO, versioned route).
3. **Classifier.** Add `SignalDeviation.ClassifyBox(double boxMean, double allTimeMean, double allTimeStdDev)` returning Green, Yellow, Red, or None when there is no reading or no spread. Keep `Classify` for the timeline unchanged.
4. **UI.** In `SignalHistory.razor` and `BitrateHistory.razor`, add a CSS class per box from `ClassifyBox` (e.g. `calendar-box-green|yellow|red`), using theme tokens (`--bs-success`, `--bs-warning`, `--bs-danger`), not literal colours, per `ui-shared-theme`. Add a small legend next to the grid ("< 1 sd", "1–2 sd", "≥ 2 sd").
5. **Localization.** Legend labels and the "All time" note go in `SignalHistory.resx` and `BitrateHistory.resx` in the same change.
6. **Unchanged.** The timeline bands, the alert banners and the alert logic keep their current behaviour.

## Critical files

- `src/client/ui/VideoForensics.Ui.Shared/src/Services/Signal/SignalDeviation.cs` (new `ClassifyBox`)
- `src/client/ui/VideoForensics.Ui.Shared/src/Services/Signal/SignalCalendar.cs`
- `src/client/host/VideoForensics.Hosting/src/Signal/SignalAnalysisMapping.cs` and `src/core/api/src/SignalAnalysisDtos.cs` (all-time calendar and baseline in the response)
- `src/client/ui/VideoForensics.Ui.Shared/src/Pages/SignalHistory.razor` (lines 221-260)
- `src/client/ui/VideoForensics.Ui.Shared/src/Pages/BitrateHistory.razor` (matching grid block)
- The matching bitrate analysis path (`Hosting/src/Signal/BitrateAnalysisMapping.cs` and its service)

## Verification

- Red then green for (a), (b), (c) (namespace filter, `--list-tests`).
- Hosting analysis tests: an all-time request returns a calendar built from every sample, not just the range.
- Browser check on a local dev host: changing the time range picker leaves the grid unchanged (it is all-time). Boxes show green, yellow and red, and the legend matches.

## Open point

The boundary is "less than 1 sd" green, "less than 2 sd" yellow, and "2 or more" red. Confirm that `d` is measured with the absolute difference (both above and below the mean are treated the same), as the timeline already does.

---

# Issue 13: hover tooltip on Week by hour boxes

## Reading (default, unless you say otherwise)

On hover, each box shows:
- its **value** (the box mean, in dBm or kbps), and the reading count,
- its **deviation for the week**: the std dev of that weekday's row (`Calendar.DayOfWeek(day)`), and
- its **deviation for the hour**: the std dev of that hour's column (`Calendar.HourOfDay(hour)`).

These are the same row and column std devs the grid already shows in its footer cells, so the tooltip uses existing figures and adds no new calculation.

## Steps

1. **Red test first.** Page test (Signal and Bit rate): hovering or reading a box's `title` (or a tooltip element) contains the value, the week std dev and the hour std dev for that cell. Record the red run.
2. **Tooltip text.** Build it in the page's code block from `section.Calendar` (no new calculation). Use the `title` attribute, matching the existing count tooltip, so it works without JS. Format the figures with the existing `StdDevText` and dBm/kbps formatters. Show "–" when a figure is missing (fewer than two readings).
3. **Localization.** Add tooltip labels ("Value", "Week σ", "Hour σ", "readings") to `SignalHistory.resx` and `BitrateHistory.resx`. Use a format string with placeholders, not string concatenation.
4. **Unchanged.** The box colour, the grid values and the footer std dev cells keep their current behaviour.

## Critical files

- `src/client/ui/VideoForensics.Ui.Shared/src/Pages/SignalHistory.razor` (box `<td>`, currently `title="@cell.Count readings"` at line 243)
- `src/client/ui/VideoForensics.Ui.Shared/src/Pages/BitrateHistory.razor` (matching box)
- `src/client/ui/VideoForensics.Ui.Shared/src/Pages/SignalHistory.resx` and `BitrateHistory.resx`

## Verification

- Red then green for the page tests (namespace filter, `--list-tests`).
- Browser check on a local dev host: hover a box and confirm the value, week σ and hour σ match the footer cells for its row and column.

## Open point

Confirm that "dev" means the std dev (σ) of the weekday row and the hour column, as the plan assumes, rather than the deviation of the box from the all-time mean.

Resolved by Issue 14: the tooltip's week and hour σ are all-time figures.

---

# Issue 14: Reading / Deviation radio set on the Week by hour grid

## Decisions (confirmed)

- **Radio set** above the grid: "Reading (current view)" and "Deviation". Default is Reading.
- **Reading mode:** each box shows its mean (dBm or kbps) for the **selected time range**.
- **Deviation mode:** each box shows its distance in std devs, `d = |box mean − all-time mean| / all-time sd`, to one decimal place.
- **All-time values (both modes):** the day (row) std dev and hour (column) std dev in the footer cells, the tooltip σ from Issue 13, and the colour baseline (all-time mean and sd).
- **Colour rule (both modes):** compared to all time, green `d < 1`, yellow `d < 2`, red `d >= 2`. Empty boxes stay uncoloured.
- This revises Issue 12's range decision: the grid means follow the time range picker. Its baseline and footer σ stay all-time.

## Steps

1. **Red tests first.**
   - (a) Analysis test: the grid cell means come from the selected range, and the row, column and baseline σ come from all samples. Build the fixture so the two sets differ.
   - (b) Page test: with Reading selected, a box shows its mean. Selecting Deviation shows `d` to one decimal for the same box. Record the red runs.
   - (c) Page test: changing the time range picker changes Reading values and leaves the footer σ unchanged.
2. **Analysis (server).** Return two sets from the same call: range-filtered cell means for the grid, and all-time row/column σ and baseline. Keep the server doing the aggregation (no in-memory filtering on the client). Update the DTO under the client-split rule.
3. **Deviation value.** Add `SignalDeviation.DeviationSigma(boxMean, allTimeMean, allTimeSd)` returning `d`, and reuse it for the colour class from Issue 12 so the two always agree. Return null when there is no reading or no spread.
4. **UI.** Add a Bootstrap radio group (`btn-group` with `btn-check` inputs, matching the existing bucket picker) above the grid, with `data-testid` on each option. Switch the box text between the mean and `d` on selection. Keep the colour classes unchanged.
5. **Tooltip (Issue 13).** Shows the mode's value too: the mean and `d` for both modes, plus the all-time week and hour σ.
6. **Localization.** Radio labels, the "Reading (current view)" and "Deviation" options, and the legend text go into `SignalHistory.resx` and `BitrateHistory.resx`.
7. **Bit rate.** Apply the same radio set and logic to Bit Rate History (kbps).

## Critical files

- `src/client/ui/VideoForensics.Ui.Shared/src/Services/Signal/SignalDeviation.cs` (`DeviationSigma`, shared with the Issue 12 classifier)
- `src/client/ui/VideoForensics.Ui.Shared/src/Services/Signal/SignalCalendar.cs`
- `src/client/host/VideoForensics.Hosting/src/Signal/SignalAnalysisMapping.cs` and `src/core/api/src/SignalAnalysisDtos.cs`
- `src/client/ui/VideoForensics.Ui.Shared/src/Pages/SignalHistory.razor` (grid at lines 221-260, bucket picker pattern at line 265)
- `src/client/ui/VideoForensics.Ui.Shared/src/Pages/BitrateHistory.razor` (matching grid)
- `src/client/ui/VideoForensics.Ui.Shared/src/Pages/SignalHistory.resx` and `BitrateHistory.resx`

## Verification

- Red then green for (a), (b), (c) and the `DeviationSigma` unit tests (namespace filter, `--list-tests`).
- Browser check on a local dev host: toggle Reading and Deviation on a box, change the time range, and confirm Reading changes while the footer σ and colours do not.

## Open point

`d` shown as a one-decimal figure can round a value near the boundary (for example 0.95 shows as 0.9 or 1.0). Confirm the colour uses the exact `d`, not the rounded figure, which is the plan's default.

---

# Issue 15: "Signal deviation flags" heading shows its raw key

## Findings

- `SignalHistory.razor:331` renders `@L["SignalFlagsHeading"]`, and `L` is `IStringLocalizer<SignalHistory>` (`SignalHistory.razor:26`).
- The key `SignalFlagsHeading` exists only in `Resources/SharedResources.resx:1073`. It is missing from `Pages/SignalHistory.resx`, so the localizer returns the key name and the heading shows "SignalFlagsHeading".

## Steps

1. **Red test first.** Page test (Signal History): the rendered `data-testid="signal-flags-heading"` text equals the localized "Signal deviation flags", not the key. Record the red run.
2. **Fix.** Add a `SignalFlagsHeading` entry with the value "Signal deviation flags" to `Pages/SignalHistory.resx`. Keep the `SharedResources.resx` entry only if another page uses it (grep first, and use `SharedL` there if so).
3. **Sweep the page.** Check every `L["..."]` key in `SignalHistory.razor` against `SignalHistory.resx` for other missing keys. Add any found, in the same change (`ui-shared.md`). The same check applies to `BitrateHistory.razor`, since it shares the same grid code.

## Critical files

- `src/client/ui/VideoForensics.Ui.Shared/src/Pages/SignalHistory.razor` (line 331)
- `src/client/ui/VideoForensics.Ui.Shared/src/Pages/SignalHistory.resx`
- `src/client/ui/VideoForensics.Ui.Shared/src/Resources/SharedResources.resx` (line 1073)

## Verification

- Red then green for the heading test (namespace filter, `--list-tests`).
- Browser check on a local dev host: the heading reads "Signal deviation flags" on /signal-history.

---

# Issue 16: Type column in the flagged media table shows event type

## Findings

- `FlaggedMediaTable.razor:37-41` shows the **flag type** (`TypeName(FlagType)`: RSSI deviation, Bit rate collapse). The user wants the **event type** (e.g. `person`).
- `MediaFlag` has `DeviceId` and an optional `MediaItemId` (`data/common/src/Entities/MediaFlag.cs:11-15`). It has no event link, so the event type must be looked up.
- Event type route: `MediaItem.DownloadEventId` → `DownloadEvent` (`ProviderEventId`, `DeviceId`) → `Event.EventType`. Repository calls already used in `BackupExportOrchestrator.PrepareForExportAsync`: `GetByDownloadEventIdAsync` and `GetByProviderEventIdAsync`.
- Flags with no `MediaItemId` (e.g. some signal flags raised from an hour window) have no clip, so they have no event type.

## Decisions

- Show the event type of the flag's clip in the Type column. Keep the flag type as a second line or tooltip so the flag kind is not lost. (Assumed; confirm if you want the flag type dropped.)
- For a flag with no clip, show a localized "No event" placeholder, not a blank cell.

## Steps

1. **Red test first.** `FlaggedMediaTable` test: a flag whose clip belongs to an event of type `person` shows "person" in the Type column; a flag without a clip shows the placeholder. Record the red run.
2. **Server-side join.** Add the event type to the flag list response (`MediaFlag` DTO) by joining `MediaItem → DownloadEvent → Event` in the repository query, not by per-row calls from the client (per `providers.md`: filter, sort and join on the server). Add a red repository test for the join.
3. **DTO and client.** Add `EventType` (nullable string) to the flag DTO in `Api.Contracts`, and map it in the Remote repository (`ToDomain`). Keep the interface names unchanged (`client-split.md`).
4. **UI.** `FlaggedMediaTable.razor` Type column: event type first, flag type as a smaller second line. Placeholder when null.
5. **Localization.** Add the placeholder ("No event") and any new labels to `SharedResources.resx`, since `FlaggedMediaTable` uses the shared localizer. Check the keys exist (Issue 15 lesson).

## Critical files

- `src/client/ui/VideoForensics.Ui.Shared/src/Components/Review/FlaggedMediaTable.razor` (lines 37-41)
- `src/data/common/src/Entities/MediaFlag.cs`
- `src/data/database/src/Repositories/` (the media flag repository; confirm the file name)
- `src/core/api/` (flag DTO; add `EventType`)
- `src/client/host/VideoForensics.Hosting/src/Remote/` (remote flag repository mapping)
- `src/client/ui/VideoForensics.Ui.Shared/src/Resources/SharedResources.resx`

## Verification

- Red then green for the table test, the repository join test and the remote mapping test (namespace filter, `--list-tests`).
- Browser check on a local dev host: a flag from a person event shows "person" in the Type column on /signal-history.

## Open point

Should the flag type stay visible (as a second line or tooltip), or should the column show only the event type as requested?

---

## Review note: RSSI flags have no clip

`SignalDeviationAlerts` (line 98) and `BitrateDeviationAlerts` (lines 98-100) create flags with `MediaItemId = null`. Only `BitrateCollapse` flags have a clip. The join `MediaItem → DownloadEvent → Event` therefore returns no event type for RSSI and deviation flags, and Issues 16 and 20 would show "No event" for every RSSI flag.

Also: `IMediaFlagRepository` returns the `MediaFlag` entity, which has no `EventType` field. Add it as a `[NotMapped]` property or an agreed projection, and populate it in `GetAsync`, `ListPageAsync` and `GetNeighbourAsync` (`MediaFlagOperations.cs`), then map it in `MediaFlagMapping.cs` and `MediaFlagDtos.cs`.

Open point (decision needed before Issue 16 starts): how an RSSI flag gets an event type. Options: (a) look up the event whose clip overlaps the flag's window, using the same search pad the RSSI review page uses; (b) show "No event" for RSSI flags and show the event type only for bit rate flags.

# Issue 17: Dev column in the flagged media table

## Findings

- `MediaFlag` stores no deviation value. The RSSI flag is created by `SignalDeviationAlerts` (`Hosting/src/BackgroundServices/SignalDeviationAlerts.cs`), which has the hour window's `WorstRssiDbm` and the all-time baseline at that moment, but does not save a sigma value.
- `SignalDeviation.DeviationSigma` (Issue 14) gives the same figure from a box mean, so a flag's dev can use the same definition: `d = |worst reading − all-time mean| / all-time sd`.

## Decisions (assumed, confirm in the open point)

- **Value:** the flag's worst-reading distance in std devs, to one decimal, using the same `DeviationSigma` function as the grid.
- **Stored at creation:** `SignalDeviationAlerts` saves `DeviationSigma` on the flag, so the value does not change if the baseline moves later. A new nullable column `DeviationSigma` on `MediaFlag`, with EF migration.
- **Bit rate flags:** `BitrateCollapse` flags are not sigma-based, so their Dev cell shows "-".
- **Existing flags:** rows created before the migration have no value, so they show "-" until backfilled (see open point).

## Steps

1. **Red tests first.**
   - (a) `DeviationSigma` on a flag: the table shows the value to one decimal for an RSSI flag, and "-" for a bitrate flag or a flag with no value.
   - (b) `SignalDeviationAlerts` test: a new RSSI flag stores its `DeviationSigma`.
   - (c) Migration test: `InitialCreate_Schema_MatchesCurrentModel` style check passes with the new column.
   Record the red runs.
2. **Entity and migration.** Add `double? DeviationSigma` to `MediaFlag`, add the EF configuration, and create the migration. If the snapshot conflicts, restore it from the first branch's commit, as in earlier migration notes.
3. **Write path.** `SignalDeviationAlerts` computes `d` for the flag's worst reading and saves it.
4. **Read path.** Add `DeviationSigma` to the flag DTO in `Api.Contracts`, map it in the Remote repository, and the server query selects it. No client-side calculation.
5. **UI.** `FlaggedMediaTable.razor` adds a "Dev" column (one decimal, "-" when null). Tooltip shows "std devs from the all-time mean". Labels go in `SharedResources.resx`.

## Critical files

- `src/data/common/src/Entities/MediaFlag.cs` (new `DeviationSigma`)
- `src/data/database/src/Configurations/` (MediaFlag EF configuration) and `sqlite/src/Migrations` (new migration)
- `src/client/host/VideoForensics.Hosting/src/BackgroundServices/SignalDeviationAlerts.cs` (write path)
- `src/core/api/` (flag DTO)
- `src/client/host/VideoForensics.Hosting/src/Remote/` (remote mapping)
- `src/client/ui/VideoForensics.Ui.Shared/src/Components/Review/FlaggedMediaTable.razor`
- `src/client/ui/VideoForensics.Ui.Shared/src/Resources/SharedResources.resx`

## Verification

- Red then green for (a), (b) and (c) (namespace filter, `--list-tests`).
- Build the touched projects with `-m:1` to a scratch OutDir.
- Browser check on a local dev host: a new RSSI flag shows a Dev value, and a bitrate flag shows "-".

---

## Open point

Existing RSSI flags have no stored value. Either backfill them from the current all-time baseline (a one-off job), or leave them as "-" until they are re-flagged. The plan defaults to leaving them as "-".

## Review note: write paths and DTO

- `UpsertRaisedAsync` refreshes an existing Unreviewed row. The new `DeviationSigma` must be copied in that refresh path too (`MediaFlagOperations.cs`).
- `BitrateDeviationAlerts.cs` also writes flags that appear in the bit rate table (Issue 18). It must store `DeviationSigma` as well.
- `MediaFlagDto` is a positional record (`MediaFlagDtos.cs`). Adding a field breaks every constructor call and test. Update `MediaFlagMapping.cs` and the affected tests in the same change.

# Issue 18: Bit rate flags table on /bitrate-history

## Decisions (confirmed)

- **Heading:** "Bit rate collapse flags" becomes "Bit rate alerts" (`BitrateHistory.resx:173-174`, key `BitrateFlagsHeading`). Keep the `data-testid="bitrate-flags-heading"` so tests still find it.
- **Type column:** shows the event type (e.g. `person`) for both the signal and bit rate flag tables, using the Issue 16 join. The flag type stays as the second line, as Issue 16 defaults.
- **Dev column:** applies to bit rate flags too. For a collapse flag, store the std-dev distance of the clip's measured bit rate from the camera's all-time bit rate mean, at creation (same approach as Issue 17).

## Findings

- `BitrateCollapse` flags are raised per clip in `MediaAnalysisService` (`data/core/src/Services/MediaAnalysisService.cs:214-222`) and backfilled in `MediaFlagBackfill.cs:58`. They carry no std dev, so `DeviationSigma` (Issue 17) is null for them today.
- `BitrateDeviation` flags also exist and are listed in this table (`BitrateHistoryPageTests.cs:167`). They already come from a sigma window, so they use the Issue 17 path.

## Steps

1. **Red tests first.**
   - (a) `BitrateHistory` page test: the heading text is "Bit rate alerts" and the `data-testid` is unchanged.
   - (b) `FlaggedMediaTable` test: the bit rate table shows the event type and a Dev value for a collapse flag with a stored sigma.
   - (c) Analysis test: `MediaAnalysisService` stores the clip's distance in std devs on a new `BitrateCollapse` flag, computed against the camera's all-time bit rate mean and sd, and stores null when the camera has no spread.
   Record the red runs.
2. **Write path.** In `MediaAnalysisService` (and `MediaFlagBackfill` for the backfill), compute `d` for the clip's measured bit rate using the all-time baseline, and save it on the flag through the Issue 17 `DeviationSigma` column. No new column is needed.
3. **Heading.** Change the `BitrateFlagsHeading` value in `BitrateHistory.resx` to "Bit rate alerts". Check the key exists in `BitrateHistory.resx` (Issue 15 lesson).
4. **Table.** The Type and Dev columns come from the shared `FlaggedMediaTable` changes in Issues 16 and 17, so this page gets them without its own changes.
5. **Backfill.** Existing collapse flags have no stored value and show "-", the same as Issue 17's open point. A backfill covers both tables if you choose it.

## Critical files

- `src/client/ui/VideoForensics.Ui.Shared/src/Pages/BitrateHistory.resx` (line 173-174) and `BitrateHistory.razor` (line 319)
- `src/data/core/src/Services/MediaAnalysisService.cs` (lines 214-222)
- `src/data/core/src/Services/MediaFlagBackfill.cs` (line 58)
- `src/client/ui/VideoForensics.Ui.Shared/src/Components/Review/FlaggedMediaTable.razor` (shared Type and Dev columns from Issues 16-17)

## Verification

- Red then green for (a), (b) and (c) (namespace filter, `--list-tests`).
- Browser check on a local dev host: /bitrate-history heading reads "Bit rate alerts", Type shows the event type, and a new collapse flag shows a Dev value.

## Open point

Do you want existing collapse flags backfilled with a Dev value, or should they stay "-" until re-raised (the default)?

---

## Review note: bit rate baseline

`MediaAnalysisService` judges a clip against the device's own earlier clips, with a seasonal and lighting baseline (`MediaAnalysisService.cs:128-138`). It has no camera-wide all-time bit rate mean or sd, which this issue needs. Either add a new query for the all-time baseline, or decide to reuse the existing baseline. Open point, to settle before Issue 18 starts.

Also add the bit rate DTO change to `BitrateAnalysisDtos.cs` (Issue 12 names only `SignalAnalysisDtos.cs`).

# Issue 19: flag navigator on the review pages (x of x, button labels, layout)

## Findings

- The "1 of 40" readout and the Previous and Next buttons are one shared component, `Components/Review/ClipNavigator.razor`. It is used on both review pages: `RssiReview.razor:28` and `BitrateReview.razor:29`, so one change covers both.
- Button text comes from `SharedResources.resx:1055-1059` (`ClipNavigatorPrevious` = "Previous flag", `ClipNavigatorNext` = "Next flag").
- The readout is `Readout` (`ClipNavigator.razor:41-43`), rendered in the same row as the buttons (lines 10-12).
- The navigator sits in the right pane below `FlagActionsPanel` (`RssiReview.razor:21-29`).

## Decisions (default, unless you say otherwise)

- **Readout to the top:** the "x of x" line moves above the buttons, as the first item in the navigator. The navigator itself moves above `FlagActionsPanel` in the right pane, so the readout sits at the top of the pane on both review pages.
- **Buttons side by side:** the buttons stay in one row, wrapping only when the pane is too narrow. Use a flex row with wrap, and do not stack them by default.
- **Labels without "flag":** "Previous" and "Next". The `aria-label` ("Flag navigation") is left as is, since it is not a visible button label. Say if you want it changed too.

## Steps

1. **Red tests first.** `ClipNavigator` bUnit test: the button texts are "Previous" and "Next", and the readout element comes before the buttons in the markup. Record the red run.
2. **Resources.** Change `ClipNavigatorPrevious` to "Previous" and `ClipNavigatorNext` to "Next" in `SharedResources.resx`. Keep the keys, so no code changes for labels.
3. **Markup.** In `ClipNavigator.razor`, move the readout `<span>` above the button row. Make the row `d-flex flex-wrap align-items-center gap-2` so the buttons sit side by side and wrap only when the pane is narrow.
4. **Pane order.** In `RssiReview.razor` and `BitrateReview.razor`, move the `<ClipNavigator>` block above `<FlagActionsPanel>` in `PageRightPanelContent`, so the readout is at the top of the pane.
5. **Stale comment.** `app.css:18` mentions "Previous/Next flag" in a comment. Update the comment to match, no style change.
6. **Browser check.** Confirm the wrap at the pane's narrowest width, as the screenshot shows the buttons stacked at the current width.

## Critical files

- `src/client/ui/VideoForensics.Ui.Shared/src/Components/Review/ClipNavigator.razor`
- `src/client/ui/VideoForensics.Ui.Shared/src/Resources/SharedResources.resx` (lines 1055-1059)
- `src/client/ui/VideoForensics.Ui.Shared/src/Pages/RssiReview.razor` (lines 21-30)
- `src/client/ui/VideoForensics.Ui.Shared/src/Pages/BitrateReview.razor` (matching block, around line 29)
- `src/client/ui/VideoForensics.Ui.Shared/src/wwwroot/app.css` (line 18 comment)

## Verification

- Red then green for the `ClipNavigator` test (namespace filter, `--list-tests`).
- Browser check on a local dev host on both /signal-history review and /bitrate-history review: "x of x" is the first line in the right pane, the buttons read "Previous" and "Next" side by side, and they wrap only at a narrow width.

## Open point

Should the `aria-label` on the navigator change from "Flag navigation" too, or only the visible button labels?

---

# Issue 20: show the event type next to the playhead time in the histogram player

## Findings

- The playhead readout is `timeline-now` (`Components/Review/HistogramTimeline.razor:18-21`): "Now 2026-11-04 13:47:04", in the transport row beside Play, Back 10 seconds and Speed.
- `HistogramTimeline` is shared by both review pages, so one change covers Signal and Bit rate review.
- The component has no event type today. Its parameters are the player, window, markers and mark times (lines 58-90).
- The event type of the clip being played comes from the flag's clip join added in Issue 16 (`MediaItem → DownloadEvent → Event`).

## Interpretation (assumed)

The red line in the screenshot points at the empty space right of the "Now" readout in the transport row. The plan adds the event type there, after the time, as "Now 2026-11-04 13:47:04 · Person". If you meant another place, tell me.

## Decisions

- New optional parameter `EventType` (string?) on `HistogramTimeline`. The host page passes the current flag's clip event type. Hidden when null.
- The label uses a localized event type name where one exists (e.g. "person" → "Person"), and falls back to the raw value with the first letter capitalized.
- The event type comes from the Issue 16 join, so no new per-frame lookups.

## Steps

1. **Red tests first.**
   - (a) `HistogramTimeline` test: with `EventType = "person"`, `timeline-now` is followed by a `timeline-event-type` element reading "Person". With null, the element is absent. Record the red run.
   - (b) Review page test (Signal and Bit rate): the review passes the current flag's event type into the timeline.
2. **Parameter and markup.** Add `[Parameter] public string? EventType { get; set; }` to `HistogramTimeline.razor`. Render `<span class="playback-timeline-event-type" data-testid="timeline-event-type">` after the Now span, only when `_nowSeconds` is set and the value is not blank.
3. **Label.** Format with the existing event-type label lookup used by `EventPlainLanguageFormatter` (`person` case), falling back to capitalising the raw value.
4. **Host pages.** `RssiReview.razor` and `BitrateReview.razor` pass `EventType="@_flag?.EventType"` (the Issue 16 field) to `HistogramTimeline`.
5. **Localization.** Any new label text goes in `SharedResources.resx` (the component uses the shared localizer). Check the key exists (Issue 15 lesson).
6. **Styling.** Use the existing `playback-timeline-now` style for spacing. Add a small separator (" · ") in the format string, not a hard-coded character in markup.

## Critical files

- `src/client/ui/VideoForensics.Ui.Shared/src/Components/Review/HistogramTimeline.razor` (lines 18-21 and the parameter block)
- `src/client/ui/VideoForensics.Ui.Shared/src/Pages/RssiReview.razor` (the `HistogramTimeline` usage)
- `src/client/ui/VideoForensics.Ui.Shared/src/Pages/BitrateReview.razor` (matching usage)
- `src/client/ui/VideoForensics.Ui.Shared/src/Formatting/EventPlainLanguageFormatter.cs` (existing event type label)
- `src/client/ui/VideoForensics.Ui.Shared/src/Resources/SharedResources.resx`

## Verification

- Red then green for (a) and (b) (namespace filter, `--list-tests`).
- Browser check on a local dev host: a clip from a person event shows "Now … · Person" in the transport row on both review pages, and the label hides when no clip is loaded.

## Open point

Should the label show the event type of the clip under the playhead (as planned), or the event type of the flag the page is reviewing? They are the same for single-clip flags, but can differ if a flag spans clips.

---

# Issue 21: after Keep or Dismiss, move to the next unreviewed flag

## Findings

- Review state is `MediaFlagStatus` (`Unreviewed`, `Kept`, `Dismissed`; `data/common/src/Entities/MediaFlagStatus.cs`).
- `FlagActionsPanel` raises `OnChanged` with the updated flag after Keep or Dismiss. Both review pages handle it in `OnFlagChanged` (`RssiReview.razor:199-203`), which only updates the current flag and stays on the page.
- Moving through flags uses `IMediaFlagRepository.GetNeighbourAsync(flagId, filter, direction)` (`data/common/src/Contracts/IMediaFlagRepository.cs:28`), which the `ClipNavigator` already calls. The list order is newest first, and "Next" goes to the older flag.

## Decisions (default, unless you say otherwise)

- After Keep or Dismiss, the page moves to the next flag whose status is `Unreviewed` in the same filter. "Next" follows the navigator's direction.
- If there is no unreviewed flag after the current one, look before it. If there is none at all, stay on the current flag and show "No unreviewed flags left".
- Undoing a review (if the panel allows it) does not move the page.

## Steps

1. **Red tests first.**
   - (a) Repository test: `GetNeighbourAsync` with `Statuses = [Unreviewed]` returns the next unreviewed flag, skipping reviewed ones, and still works when the current flag is itself no longer unreviewed (it has just been dismissed). Record the red run.
   - (b) Review page test (Signal and Bit rate): after Dismiss, the page loads the next unreviewed flag; with none left, it stays and shows the message.
   - (c) `ClipNavigator` or page test: an undo does not change the flag.
2. **Repository (server).** If (a) fails, add a server-side query that finds the next and previous `Unreviewed` flag from the current flag's list position, in SQL (per `providers.md`), not in memory.
3. **Page logic.** In `RssiReview.razor` and `BitrateReview.razor`, `OnFlagChanged` checks whether the flag became reviewed (Kept or Dismissed) and, if so, calls `GetNeighbourAsync` with the page's filter plus `Statuses = [Unreviewed]`. It navigates to the result through the existing navigation path (the same one `ClipNavigator.OnNavigate` uses).
4. **Message.** Add a localized "No unreviewed flags left" text to `SharedResources.resx`, and check the key exists (Issue 15 lesson).
5. **Shared logic.** Put the "next unreviewed" decision in one place that both review pages call, rather than copying it into each page.

## Critical files

- `src/data/common/src/Contracts/IMediaFlagRepository.cs` (`GetNeighbourAsync`)
- `src/data/database/src/Repositories/` (media flag repository implementation; confirm the file name)
- `src/client/ui/VideoForensics.Ui.Shared/src/Pages/RssiReview.razor` (`OnFlagChanged`, lines 199-203)
- `src/client/ui/VideoForensics.Ui.Shared/src/Pages/BitrateReview.razor` (matching handler)
- `src/client/ui/VideoForensics.Ui.Shared/src/Components/Review/FlagActionsPanel.razor` (raises `OnChanged`; no change expected)
- `src/client/ui/VideoForensics.Ui.Shared/src/Components/Review/ClipNavigator.razor` (existing navigation path to reuse)

## Verification

- Red then green for (a), (b) and (c) (namespace filter, `--list-tests`).
- Browser check on a local dev host: Dismiss a flag on /signal-history review, and the page opens the next unreviewed flag. With no unreviewed flags left, the page stays and shows the message.

## Open point

Should the auto-advance go to the next (older) unreviewed flag, as the navigator's Next does, or to the previous (newer) one? The plan assumes Next.

---

# Issue 22: Previous and Next flag buttons do nothing

## Root cause (found)

- `ClipNavigator` looks up the neighbour correctly, then raises `OnNavigate` with it (`ClipNavigator.razor:105-108`).
- Neither review page passes `OnNavigate` (`RssiReview.razor:28`, `BitrateReview.razor:29`: `<ClipNavigator Filter=... CurrentFlagId=... />`). The event has no handler, so the neighbour is found and then dropped. The buttons look enabled, but clicking them changes nothing.
- The page never navigates to the neighbour's review URL. `FlaggedMediaTable` already builds those URLs (`RssiReviewPathPrefix + flag.Id` and `BitrateReviewPathPrefix + flag.Id`, `FlaggedMediaTable.razor:76-90`).

## Steps

1. **Red test first.** Review page test (Signal and Bit rate): clicking "Next" calls the neighbour lookup and navigates to that flag's review URL. Clicking "Previous" does the same in the other direction. Record the red run (it should fail today, with no navigation).
2. **Handle `OnNavigate`.** Add `OnNavigate="OnNeighbourNavigate"` to the `ClipNavigator` in `RssiReview.razor` and `BitrateReview.razor`. The handler calls `NavigationManager.NavigateTo(<review path prefix> + neighbour.Id)`, using the same path prefix the table uses. Reuse the existing prefix constants; do not write new string paths.
3. **Shared navigation.** Put the "go to flag" path building in one helper that the table, the navigator and the auto-advance (Issue 21) all call, so the three never disagree.
4. **Check the disabled state.** After navigating, `ClipNavigator` reloads its count and position for the new flag (`OnParametersSetAsync`). Confirm the "x of x" readout updates and the buttons disable correctly at either end of the list.
5. **Keyboard.** The arrow keys in `ClipNavigator.OnKeyDown` go through the same `MoveAsync`, so they are fixed by the same change. Add a test for ArrowLeft and ArrowRight.

## Critical files

- `src/client/ui/VideoForensics.Ui.Shared/src/Pages/RssiReview.razor` (line 28, add `OnNavigate`)
- `src/client/ui/VideoForensics.Ui.Shared/src/Pages/BitrateReview.razor` (line 29, add `OnNavigate`)
- `src/client/ui/VideoForensics.Ui.Shared/src/Components/Review/ClipNavigator.razor` (lines 75-108, no change expected)
- `src/client/ui/VideoForensics.Ui.Shared/src/Components/Review/FlaggedMediaTable.razor` (lines 76-90, path prefixes to reuse)

## Verification

- Red then green for the page tests and the keyboard test (namespace filter, `--list-tests`).
- Browser check on a local dev host: on /signal-history review, Next and Previous open the neighbouring flag, the "x of x" readout changes, and the buttons disable at the ends. Repeat on /bitrate-history review.

## Open point

None. This is a wiring fix. Issue 21's auto-advance reuses the same navigation, so it should land with this change.

---

# Issue 23: hover over the histogram shows the value

## Findings

- The bit rate histogram is `Components/Review/BitrateSeriesChart.razor`. Each bar is a `<rect class="bitrate-bar">` (line 22) with no tooltip. The bar height is `Points[i].Kbps`, one bar per second, and the second is `Points[i].Second`.
- The RSSI review histogram has the same shape (one bar per second of RSSI, in dBm). Its component is confirmed during implementation, and the same tooltip is added there.
- The chart has no hover text, so the only value a viewer can read is the bar height.

## Decisions (default, unless you say otherwise)

- Each bar gets a hover tooltip, using an SVG `<title>` child, so it works without JavaScript and matches the existing `title` tooltips elsewhere.
- Bit rate tooltip: the time (clock time from `StartUtc` plus the second offset, in the user's zone), the value in kbps, and the std dev, e.g. "13:47:04 · 4,210 kbps · σ 310 kbps". If `StartUtc` is not set, show the offset in seconds instead.
- RSSI tooltip: the same, with dBm, e.g. "13:47:04 · −78 dBm · σ 4.2 dBm".
- **Std dev:** the camera's all-time sample std dev (the same baseline used for Dev elsewhere, Issues 13-17), not the sd of the clip. This keeps one definition of σ across the app. The value is the bar's own reading, with no recalculation on the client.
- The std dev is passed into the chart from the server, not computed in the browser (per the client-split and providers rules).

---

## Steps

1. **Red tests first.** `BitrateSeriesChart` test: a bar's `<title>` text contains the kbps value for that second, and the clock time when `StartUtc` is set. Add the matching test for the RSSI histogram. Record the red runs.
2. **Tooltip.** Add `<title>` inside each `<rect>` in `BitrateSeriesChart.razor`, formatted with the invariant culture for numbers and the user's zone (`UserTimeZone`, as `HistogramTimeline` does) for the time.
3. **RSSI histogram.** Apply the same change to the RSSI review chart. Confirm its file and bar markup first.
4. **Localization.** The tooltip format ("{0} · {1} kbps", "{0} · {1} dBm") goes in `SharedResources.resx`. Use placeholders, not string concatenation. Check the keys exist (Issue 15 lesson).
5. **Performance.** A clip has one bar per second, so one `<title>` per bar is cheap. Do not add a separate hover overlay or JS.

## Critical files

- `src/client/ui/VideoForensics.Ui.Shared/src/Components/Review/BitrateSeriesChart.razor` (line 22)
- The RSSI review histogram component (confirm the file name during implementation)
- `src/client/ui/VideoForensics.Ui.Shared/src/Resources/SharedResources.resx`

## Verification

- Red then green for both chart tests (namespace filter, `--list-tests`).
- Browser check on a local dev host: hovering a bar on the bit rate and RSSI review histograms shows the time and value.

## Open point

Should the tooltip show the clock time (as planned) or only the seconds offset into the clip?

# Issue 24: Notes panel: no empty message, and a one-line note box that can expand

## Findings

- One shared component, `Components/Notes/NotesPanel.razor`, is used by the case, jamming and flag notes (`CaseNotes.razor`, `JammingNotes.razor`, `FlagNotes.razor`). One change covers all three.
- The empty message is `notes-empty` (`NotesPanel.razor:19-22`, text `NotesEmpty`).
- The note box is an `SfTextBox` with `Multiline="true"` (`NotesPanel.razor:48`). It opens at the multi-line default height, which is why it shows several lines.

## Decisions (default, unless you say otherwise)

- **Remove "No notes yet."** When the list is empty, render nothing in its place. The list and the form stay as they are.
- **One line by default, still expandable.** The note box opens at one line, and the operator can drag the resize handle down to make it taller. The multi-line behaviour and the 4000-character limit are unchanged.

## Steps

1. **Red tests first.** `NotesPanel` tests: with no notes, the `notes-empty` element is absent. The note box opens at one row (a `rows="1"` attribute, or the single-line min-height), and a resize handle is still present (`resize: vertical` in the style). Record the red runs.
2. **Remove the empty message.** Delete the `else` branch that renders `notes-empty` (`NotesPanel.razor:19-22`). Drop the `NotesEmpty` key from `SharedResources.resx` only if no other component uses it (grep first).
3. **One-line default.** Set the textarea to one row with `HtmlAttributes` (`rows = 1`) on the `SfTextBox`, and keep `Multiline="true"` so it still expands. Set `resize: vertical` (or the Syncfusion resize option) so the handle still works. Confirm the exact Syncfusion attribute during implementation, since the component may need its own min-height instead.
4. **Existing tests.** Update any test that asserted `notes-empty` text, since the element no longer exists. Keep the empty-list check on `notes-list` if one exists.
5. **Localization.** No new text. Check that the remaining keys still exist (Issue 15 lesson).

## Critical files

- `src/client/ui/VideoForensics.Ui.Shared/src/Components/Notes/NotesPanel.razor` (lines 19-22 and 48)
- `src/client/ui/VideoForensics.Ui.Shared/src/Resources/SharedResources.resx` (`NotesEmpty`, only if unused elsewhere)
- Tests for `NotesPanel` (the notes page and component tests in `ui.shared/tests`)

## Verification

- Red then green for the panel tests (namespace filter, `--list-tests`).
- Browser check on a local dev host: a flag with no notes shows no "No notes yet." text, the note box opens one line tall, and dragging the handle makes it taller.

---

# Issue 25: remove Recipient description and Passphrase from /review/export

## Findings

- `ExportEvidence.razor:53-58` has two boxes: "Recipient description" (`_recipientDescription`) and "Passphrase" (`_passphrase`, a password-type box).
- The page code passes both to `EvidenceExportService.ExportEvidenceAsync` (`ExportEvidence.razor:165-170`). A blank passphrase is sent as `null`, so the service already accepts an export with no passphrase.
- There is no user name field in the page code.

## Decisions (confirmed)

- Remove both boxes, and the `_recipientDescription` and `_passphrase` fields with them. The page keeps the case picker, the status messages and the Export button.

## Risk to confirm (noted, not blocking)

- Removing the passphrase means exports go out with no passphrase protection. The evidence archive is no longer encrypted by the operator. The plan keeps the change to the UI and the call, so the service's handling of a `null` passphrase is unchanged. If the archive must stay encrypted, the service needs another protection before this ships.

## Steps

1. **Red test first.** `ExportEvidence` page test: no recipient or passphrase input is rendered, and clicking Export still calls `ExportEvidenceAsync` with the case id and `null` for both values. Record the red run.
2. **Remove the boxes.** Delete the two `<div class="mb-3">` blocks at `ExportEvidence.razor:53-58`, and the `_recipientDescription` and `_passphrase` fields (lines ~101-102). Update the call at lines 165-170 to pass `null` for both, so the service signature is unchanged.
3. **Resources.** Remove `RecipientDescriptionPlaceholder` and `PassphrasePlaceholder` from `ExportEvidence.resx` only after grepping that no other page uses them.
4. **Existing tests.** Update any test that typed into or asserted those two inputs.

## Critical files

- `src/client/ui/VideoForensics.Ui.Shared/src/Pages/ExportEvidence.razor` (lines 53-58, 101-102, 165-170)
- `src/client/ui/VideoForensics.Ui.Shared/src/Pages/ExportEvidence.resx` (lines 65-70)
- Tests for `ExportEvidence` in `ui.shared/tests`

## Verification

- Red then green for the page test (namespace filter, `--list-tests`).
- Browser check on a local dev host: /review/export shows only the case picker and the Export button, and an export completes.

---

## Open point

Confirm that removing the passphrase is intended, given that the archive will no longer be passphrase-protected. If the archive must stay encrypted, I'll plan a different protection before this change.

## Open point (added in review)

The Recipient description is written into the export manifest (`EvidenceExportOrchestrator.cs:265`). Removing the box removes that chain-of-custody field. Confirm that dropping it is acceptable, separately from the passphrase question above.

# Issue 26: archive the Cloudflare remaining-work doc

## Decision (confirmed)

Move `CLOUDFLARE_TUNNEL_REMAINING_WORK.md` into the existing archive folder and remove it from the open-items list. The Cloudflare code is not changed.

## Steps

1. **Find the target.** Use the existing archive convention (`plans/archive/` or `.claude/plans/archive/`, whichever the other archived plans use). Confirm before moving.
2. **Move the file.** Move it with `git mv` (keeps history), not a copy and delete.
3. **Update references.** Grep the repo for `CLOUDFLARE_TUNNEL_REMAINING_WORK` and point any links at the new archive path. Check `plans/deferred-items.md` and `.claude/` files that might list it.
4. **Open-items list.** Remove any open entry for the Cloudflare redesign from the trackers (`plans/deferred-items.md`, `plans/todo.md`, `TASKS.md`), or mark it archived with a date, following the rule in `deferred-items.md` (do not leave completed or archived items in the open sections).
5. **Commit.** Commit only after you confirm (one push question, per your rules).

## Critical files

- `CLOUDFLARE_TUNNEL_REMAINING_WORK.md` (move)
- `plans/archive/` or `.claude/plans/archive/` (destination, confirm)
- `plans/deferred-items.md` (open-items list), and any other tracker that references it

## Verification

- `git status` shows the file as renamed, not deleted.
- A repo-wide grep for `CLOUDFLARE_TUNNEL_REMAINING_WORK` finds no live link to the old path.

## Note

This is a file move, not a code change, so it needs no tests. Executing it requires leaving plan mode, since plan mode blocks file moves.

---

# Issue 27: finish IMPLEMENTATION_PLAN tasks 3.2, 3.3, 4.1 and 4.2

## Findings

- **3.2 Platform directory abstraction:** `System.IO.Abstractions` 22.3.0 is referenced by several projects, but `PlatformDirectoryService.cs` (`src/core/helpers/src/Platform/`, per the plan) is not confirmed wrapped. Not verified.
- **3.3 mDNS replacement:** not done. Neither Zeroconf nor Manatee.Mdns is in any project file. The custom logic is still in `MdnsAdvertisementService.cs` (WebApp `Discovery/`).
- **4.1 System.CommandLine (selftest CLI):** not done. Not in any project file.
- **4.2 IP matching (IPNetwork):** not done. Not in any project file. Target is `BannedIpMatchService.cs`.

## Steps

Each task follows the plan's TDD order: a failing test first, then the change, then a scoped test run. Each is its own branch off `dev`, per the plan's branching rules.

1. **3.2 Platform directory abstraction.**
   - Red test: `PlatformDirectoryService` tests run against an in-memory file system (`MockFileSystem`) and assert the same paths as today.
   - Wrap `PlatformDirectoryService` with `IFileSystem` (no behaviour change). Register it in DI.
   - Confirm the file's current location before editing (the plan's path may be out of date).
2. **3.3 mDNS replacement.**
   - Red test: an advertisement test that the service registers the expected service type and port, against a fake discovery seam.
   - Evaluate Zeroconf against the existing behaviour. Add the package only after the choice is confirmed, since it changes network discovery on every platform.
   - Replace the custom logic in `MdnsAdvertisementService.cs`. Keep the paired-device discovery contract and the local-only address rule (`client-split.md`: local address is discovered fresh, never stored).
3. **4.1 System.CommandLine (selftest only).**
   - Red test: the selftest CLI parses its existing options and rejects unknown ones, with the same exit codes as today.
   - Add `System.CommandLine` and move the selftest argument parsing onto it. Scope is the selftest CLI only.
4. **4.2 IP matching with IPNetwork.**
   - Red test: `BannedIpMatchService` matches a CIDR range, an exact address, and rejects addresses outside the range, including IPv6 and the loopback forms from Issue 5.
   - Add `IPNetwork2` (or the package the plan names) and replace the hand-written matching in `BannedIpMatchService.cs`.

## Decisions to confirm

- **3.3:** confirm Zeroconf (the plan's first choice) over Manatee.Mdns before adding it.
- **4.2:** the plan's package is `IPNetwork`, but the current maintained package is `IPNetwork2`. Confirm which to use.

## Verification

- Red then green for each task's test (namespace filter, `--list-tests`).
- Build touched projects with `-m:1` to a scratch OutDir.
- The plan's lite gate per task, and the full gate before any PR to `dev`.
- Manual check for 3.3 on a LAN: the server is found by a paired client through discovery.
- Manual check for 4.2 in the Admin banned-IP screen: a range ban blocks an address inside it.

## Open point

Confirm the two package choices above (Zeroconf for 3.3, and IPNetwork2 for 4.2).

---

# Issue 28: move fully done plans to archive

## Decision (confirmed)

Move only the fully done plans. Partly done plans stay where they are.

- `.claude/plans/snapshot-loop-mp4.md` → `.claude/plans/archive/`
- `.claude/plans/one-off-account-download-jobs.md` → `.claude/plans/archive/`
- `plans/EXECUTION_STATUS.md` → `plans/archive/`
- `docs/plans/ui-forensic-workflow.md` → `docs/plans/archive/` (or the nearest existing archive folder; confirm first)

## Steps

1. **Confirm destinations.** Check each target archive folder exists, and that it matches the convention the other archived plans use (`plans/archive/` and `.claude/plans/archive/`). Create `docs/plans/archive/` only if the convention already exists there.
2. **Move with `git mv`** so history follows the files.
3. **Fix references.** Grep for each moved file name and update links in trackers (`plans/deferred-items.md`, `plans/todo.md`, `TASKS.md`) and in other plans.
4. **Check the one-off plan's parent.** `one-off-account-download-jobs` names `media-page-rework.md` as its parent. The parent stays where it is; only the link path changes.
5. **Commit after confirmation** (one push question, per your rules).

## Notes

- Executing this needs plan mode off, since plan mode blocks file moves.
- The "done" statuses come from the plan headers, dated up to 2026-10-28. I didn't re-verify each plan's code. Say if you want a check first.

## Verification

- `git status` shows each file as renamed.
- A grep for each old path returns no live links.

---

# Issue 29: move open and partly done plans into the repo's plans folder

## Scope (as read)

- "Open or partly" plans that live outside `F:\VideoForensics\plans\` move into it. Plans already there stay where they are.
- Partly done plans found under `.claude/plans/`:
  - `platform-split-and-viewer.md`
  - `media-page-rework.md`
  - `flagged-media-review.md`
  - `db-field-coverage.md`
  - `in-place-service-update.md`
  - `device-pair-auth-operators.md`
- Open plans already under `plans/` need no move.

## Steps

1. **Check each plan's status before moving.** The `.claude/plans/` list above comes from header dates and was not re-checked against the code. Confirm `in-place-service-update` and `device-pair-auth-operators` are open or partly done, not done.
2. **Move with `git mv`** to `F:\VideoForensics\plans\`, keeping file names. Check for name clashes first.
3. **Fix links.** Update relative links in moved files and in trackers (`plans/deferred-items.md`, `plans/todo.md`, `TASKS.md`, `.claude/rules/*.md`, `.claude/project-layout.md`, `CLAUDE.md`).
4. **Keep the parent-child pairs together.** `one-off-account-download-jobs` (done, archived under Issue 28) points at `media-page-rework.md`, so its link must follow the move.
5. **Commit after confirmation** (one push question).

## Notes

- Executing this needs plan mode off, since plan mode blocks file moves.
- Status is from header dates only; a file that turns out done goes to the archive instead (Issue 28).

## Verification

- `git status` shows renames, not deletes.
- A grep for each old `.claude/plans/` path returns no live links.

---

# Issue 30: GitHub reports 5 dependency vulnerabilities (2 critical, 3 moderate)

## Status

Reported by GitHub on push to `wip` (2026-11-05), on the default branch. Not reviewed yet.

## Steps

1. **Review the alerts.** Open the Dependabot list (`https://github.com/rsperry79/VideoForensics/security/dependabot`) and record each alert: package, version in use, fixed version, severity, and whether it is in a runtime or test-only project.
2. **Check reachability.** For each alert, confirm the package ships in a server or client runtime project, not only in tests or tooling. Test-only findings are lower priority.
3. **Fix the criticals first.** Bump each affected `PackageReference` to the fixed version, one package per commit, following the NuGet rules in `IMPLEMENTATION_PLAN.md` (red test or scoped build first, then the change, then a scoped test run).
4. **Fix the moderates.** Same approach. If a fix needs a major version bump, stop and ask before changing it.
5. **Record the result.** Re-run the Dependabot check after the push and note which alerts are closed and which remain, with the reason.

## Open point

Do you want the review (step 1 and 2) only, or the fixes as well?

---

# Issue 31: move test-support into VideoForensics.Utils.Testing (decision pending)

## Finding

- `test-support/TestTempRedirect.cs` is not in `src/utils/testing/`. It is compiled into every test project as linked source through `Directory.Build.targets` (line 5). It redirects each test's temp folders into the checkout's git-ignored `.testtemp`.
- It is not in Utils.Testing because not every test project references Utils.Testing, and the redirect has to apply before any test code runs. A library reference does not guarantee that.

## Options

1. **Keep as is (no change).** Document the reason in `.claude/project-layout.md`.
2. **Move into Utils.Testing.** Move the file to `src/utils/testing/src/`, add a reference to Utils.Testing in every test project, and update the path in `Directory.Build.targets`. Bigger change, and it changes the build for every test project.

## Steps (if option 2 is chosen)

1. **Red check first.** Build every test project and record the current result, so any change in the build can be compared.
2. **Move the file** with `git mv` to `src/utils/testing/src/TestTempRedirect.cs`, and update `Directory.Build.targets` to point at it.
3. **Add references.** Add a `ProjectReference` to Utils.Testing in each test project that does not have one. Keep the redirect applied to every test project.
4. **Verify.** Run a full build with `-m:1` to a scratch OutDir, then a test run, and confirm temp folders still land in `.testtemp`.
5. **Commit after confirmation** (one push question).

## Verification

- Every test project builds.
- Test temp folders land in `.testtemp` and are git-ignored.

## Open point

Keep it in place (option 1) or move it (option 2)?

## Status: units started 2026-11-07 (feature/plan-ui-units)

- Issue 11 bd2769e4, Issue 12 cd9bc3c6, Issues 13-14 2997c867, Issue 21 and 22 17a4f09c, Issue 24 17a4f09c, Issue 12 server ceefcb14, .gitignore 900ff0d5.
- Tests: Hosting analysis filter and Ui.Shared review, notes, signal and bitrate filters pass (TokenSaver run_tests).
- Not verified: browser checks on a local dev host for Issues 11-14, 21, 22, 24.
- Not merged to wip, not pushed.
