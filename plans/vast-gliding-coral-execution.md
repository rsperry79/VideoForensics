# Implementation plan: VideoForensics UI fixes and plan cleanup (Issues 1–31)

Source: `docs/plans/vast-gliding-coral.md` (full issue detail, reviewed 2026-11-05). This file is the execution order. Revised after a Sonnet adversarial review.

## Ground rules (apply to every task)

- **Process (CLAUDE.md):** all edits and builds happen in a worktree, not the main checkout. Subagents are Haiku only. Log each failure in `.claude/task_failure_log.md` when it happens. Keep output terse. Ask before writing any backwards-compatibility code (fallbacks, legacy routes, migration shims).
- **TDD:** write the failing test first and record the red run, then implement, then a scoped green run. Exception: a test that passes on arrival is recorded as a regression test, not a red test (see Issue 21).
- **Tests:** `dotnet test` with a namespace or class filter (`FullyQualifiedName~X`), confirm with `--list-tests`, read the summary. Build touched projects with `-m:1` to a scratch OutDir.
- **Localization:** every new label goes into the page's `.resx` in the same change. Check each `L["Key"]` exists in the resx of the page that uses it (Issue 15 lesson).
- **Client split:** no new auth. Use `/api/v1` routes and `Api.Contracts` DTOs. Interface names stay the same. `Remote*` classes forward `CancellationToken`.
- **Line endings:** keep CRLF in existing files. Use `perl -pi` or the Edit tool for in-place edits, not `sed -i` or `awk` on CRLF files.
- **Commits:** one commit per issue. Commit message ends with the Co-Authored-By line.
- **Push:** one push question per push, never two. Push order: docs repo first, then the submodule pointer, then this repo.
- **Submodule:** `docs/` is a separate repo (VideoForensicsDocs). Commit there first, then update the pointer here.

## Dependency order

Shared data and components come first. Issues that touch the same file run in sequence, one commit each.

| Order | Issue | Depends on |
|---|---|---|
| 1 | 15 heading key | none |
| 2 | 5 LocalHost formatter | none |
| 3 | 6 remove Event Type column | none |
| 4 | 22 Previous/Next wiring | none (before 19 and 21) |
| 5 | 19 navigator layout and labels | 22 (same file) |
| 6 | 21 auto-advance | 22 |
| 7 | 24 notes panel | none |
| 8 | 25 remove recipient/passphrase | none (decisions first) |
| 9 | 7 merge pending credentials | none |
| 10 | 9 move Notifications and Reports | none |
| 11 | 8 push fixes | none (before 10) |
| 12 | 10 My Notifications page | 8, 9; decide the 8/10 overlap first |
| 13 | 2 backfill intervals | none |
| 14 | 3 Import / Export | none |
| 15 | 1 manual sync filter | none (before 16) |
| 16 | 4 unknown-login filter | 1 (localStorage pattern), 6 |
| 17 | 11 alert list cap | none |
| 18 | 12 grid colours and all-time values | none |
| 19 | 14 Reading/Deviation radio | 12 |
| 20 | 13 grid tooltip | 12, 14 |
| 21 | 16 flag Type = event type | RSSI decision (below) |
| 22 | 17 flag Dev column | 16 (same DTO) |
| 23 | 18 bit rate flags | 16, 17; bit rate baseline decision |
| 24 | 20 event type next to playhead | 16 |
| 25 | 23 histogram hover value and std dev | 12 (needs a server source for σ) |
| 26 | 27 NuGet tasks 3.2, 3.3, 4.1, 4.2 | one branch per task |
| 27 | 30 dependency alerts | review first |
| 28 | 31 test-support location | decision |

Issues 26, 28 and 29 of the original list are done. Issue 31 needs a decision before work starts.

## Phase A: UI fixes with no data change (orders 1–7)

- **15, heading:** add `SignalFlagsHeading` to `Pages/SignalHistory.resx`. Sweep the page's other `L[...]` keys. Test: heading text is the localized value.
- **5, LocalHost:** add a pure helper in `Services/` that maps loopback forms (`::1`, `127.0.0.1`, `::ffff:127.0.0.1`) to a "LocalHost" label. Use it in `SecurityEvents.razor` and the `SourceIp` column of `SecurityAuditLog.razor`. Stored value unchanged. Test: helper cases, and a page check.
- **6, Event Type column:** remove the `EventType` header and cell from `SecurityEvents.razor`. Keep the API field. Test: no Event Type header rendered.
- **22, Previous/Next wiring:** `FlaggedMediaTable.ReviewPath(MediaFlag)` already builds review URLs (public static, `FlaggedMediaTable.razor`). Reuse it, or move it to a service. It returns null for flag types with no review page; the handler must handle null. Add `OnNavigate` to `ClipNavigator` in `RssiReview.razor` and `BitrateReview.razor`. Test: click Next and Previous navigate; arrow keys too.
- **19, navigator layout:** in `ClipNavigator.razor`, put the readout first. Use `d-flex flex-wrap`. Change `ClipNavigatorPrevious` and `ClipNavigatorNext` to "Previous" and "Next". Move the navigator above `FlagActionsPanel` in both review pages' `PageRightPanelContent`. Test: readout is first; labels; wrap check in browser.
- **21, auto-advance:** after Keep or Dismiss, `OnFlagChanged` finds the next `Unreviewed` flag using `GetNeighbourAsync` with `Statuses = [Unreviewed]`, then navigates with the helper from 22. If none, show "No unreviewed flags left". The repository lookup already works when the current flag is no longer unreviewed (`MediaFlagOperations.GetNeighbourAsync` reads the anchor without a status filter), so that test is a regression test, recorded green on arrival. The red tests are the page behaviour tests.
- **24, notes panel:** in `Components/Notes/NotesPanel.razor`, remove the `notes-empty` branch. Set the text box to one row (`rows=1`) with `Multiline` kept and a resize handle. Update tests that asserted `notes-empty`. The exact Syncfusion attribute is unconfirmed: treat it as a short spike.
- **25, export page:** remove the Recipient description and Passphrase boxes and their fields in `Pages/ExportEvidence.razor`. Pass `null` for both to `ExportEvidenceAsync`. Remove the two resource keys only if unused. Test: no inputs rendered; export still calls the service. Confirm both open points first (see below).
- **7, pending credentials:** compare the standalone `PendingCredentials.razor` actions with the section in `SecurityOperators.razor` (from line ~109). Add missing actions to the operators section. Remove the `/admin/pending-credentials` route, its `NavGroups.cs` entry (line 122) and its `PageAccessRules.cs` rule (line 87). Update the nav tests. Backwards-compatible redirect: not included; ask first (CLAUDE.md).

## Phase B: notifications and settings (orders 8–14)

- **9, move sections:** in `Layout/NavGroups.cs`, set `Section: "System & Network"` on Notifications (line 104) and Reports (line 105). Test: section membership.
- **8, push fixes:** add `POST /api/v1/push/preferences` and `GET` in `Api/PushEndpoints.cs`, using the operator claim and `IOperatorNotificationPreferenceRepository.UpsertAsync`. Replace the `dynamic` read in `Ui.Shared/src/Services/WebPushClient.cs` (lines 58–79) with a typed read. The page loads saved values. Tests: endpoint upsert, 401 when unauthenticated, GET returns saved values, typed subscribe parse. The subscribe failure is unreproduced: the red test may pass on arrival, so check first.
- **10, My Notifications:** first decide the overlap with 8 (see below). Add `EmailEnabled` and `EmailMinimumSeverity` to `OperatorNotificationPreference`, with EF migration. Add a per-user endpoint pair. Add `Pages/MyNotifications.razor` and `.resx` under General. Move push controls from `Notifications.razor` to it. Email send branch reuses the existing email sender. Choose the access policy (`AccessPolicy` has no "any signed-in operator" value). Decide whether a `Remote*` service is needed (`client-split.md`). Tests: migration schema test, skip below minimum, page saves.
- **2, backfill intervals:** add `JammingIntervalMinutes`, `CodecIntervalMinutes`, `FlagIntervalMinutes` (default 15, range 1–1440) to `IForensicsConfiguration`, `ConfigDtos.cs` and `ForensicsConfigurationService.cs`. Add the three fields to `ConfigurationLoader.cs` (near line 50, which copies `MediaBackfillIntervalHours`), or saved values are dropped at load. Add orchestrator methods in `ConfigToolsOrchestrator` and the guard in `GuardedConfigToolsOrchestrator`. The three services read minutes per pass. The timer is created once (`PeriodicTimer`, e.g. `JammingAnalysisBackfillService.cs:41`), so a changed interval needs the timer re-created, and `ThreadHeartbeats.Beat` (line 44) reads `PassInterval`. Add three inputs to `ServicesSettings.razor`. Tests: range, default 15, loader keeps values, service uses config.
- **3, Import / Export:** move the nav entry in `NavGroups.cs` (line 117) to the group chosen (open point). Make `RemoteBackupExportService` take counts from the zip's `manifest.json` instead of zeros (lines 71–83). Confirm the zip contains the manifest first (`BackupExportOrchestrator.cs:108-198`). Check path handling in `BackupEndpoints.cs:49-57` (it rejects separators and `..`, not only rooted paths). Tests: counts from manifest, path handling.
- **1, manual sync filter:** add `ScopeFilterStore` modelled on `Services/Review/ReviewSettingsStore.cs` (localStorage via `IJSRuntime`, try/catch on `JSException`), key `vf.scope.filters`. Restore on load when the URL has no filter. Replace `_selectedProviders` and `_accountFilterValue` in `ManualSync.razor` with `ScopeState` provider and account. Use the rail's `FilterProviderSelect` and `FilterAccountSelect`. Subscribe to `ScopeState.OnChange`, dispose in `Dispose`. Red test first: select account, Sync Now enabled and posts for that account.

## Phase C: security events, alerts and grid (orders 16–20)

- **4, unknown-login filter:** add `unknownOnly` (default false) to the security-events endpoint in WebApp. Apply it in SQL in the repository: `AuthFailure` with `OperatorId IS NULL` (`OperatorId` is `Guid?`). Extend `ISecurityEventsService.GetEventsAsync` and `RemoteSecurityEventsService`. Move the toggle into `PageRightPanelContent`. Store it in localStorage (`vf.securityEvents.unknownOnly`). Tests: repository filter, remote sends the flag, page toggle.
- **11, alert cap:** create `Components/Review/DeviationAlertBanner.razor` with `Limit` 5, a "Show N more" and "Show less" link, and a per-signature expanded state. Use it in `SignalHistory.razor` (lines 62–80) and `BitrateHistory.razor` (lines 61–78). Keep the dismiss logic on each page. Keys `ShowMoreAlerts` and `ShowLessAlerts` in `SharedResources.resx`.
- **12, grid values and colours:** the analysis returns range-filtered cell means, and all-time row, column and baseline sd (Issue 14 governs the range split). Add `SignalDeviation.ClassifyBox`: green under 1 sd, yellow under 2, red 2 or more, none when empty. Apply the classes in both grid blocks. Add a legend. Add the bit rate additions to `BitrateAnalysisDtos.cs` as well as `SignalAnalysisDtos.cs`. Tests: boundaries at 1.0 and 2.0, empty box, page classes.
- **14, radio set:** add a `btn-group` of `btn-check` inputs (Reading, Deviation) above the grid, matching the bucket picker pattern. Reading shows the mean. Deviation shows `d = |mean − all-time mean| / all-time sd` to one decimal, from a shared `SignalDeviation.DeviationSigma`. Colour rule unchanged. Tests: both modes; range picker changes Reading only.
- **13, grid tooltip:** add a `title` on each box with value, week σ and hour σ (from the existing footer calculations). Replace the English literal `title="@cell.Count readings"` (`SignalHistory.razor`, ~line 243) with a localized format. Test: tooltip text.

## Phase D: flags data and display (orders 21–25)

- **RSSI decision first (blocks 16 and 20):** RSSI and bit rate deviation flags have no clip (`MediaItemId = null`, `SignalDeviationAlerts.cs:98`, `BitrateDeviationAlerts.cs:98-100`). The event join returns nothing for them. Options: (a) match the event whose clip overlaps the flag window, using the RSSI review page's search pad; (b) show "No event" for those flags. Pick one before 16.
- **16, event type:** `IMediaFlagRepository` returns the `MediaFlag` entity, which has no `EventType`. Add it as `[NotMapped]` or an agreed projection, and populate it in `GetAsync`, `ListPageAsync` and `GetNeighbourAsync` (`MediaFlagOperations.cs`). Map it in `MediaFlagMapping.cs` and `MediaFlagDtos.cs`. The interface name does not change. `FlaggedMediaTable.razor` Type column shows event type, with flag type on a second line. Null shows "No event". Red tests: join, remote mapping, table text.
- **17, Dev column:** add nullable `DeviationSigma` to `MediaFlag`, its EF configuration and a migration (restore the snapshot if the migration conflicts). Write it in `SignalDeviationAlerts` and `BitrateDeviationAlerts` at creation. Copy it in the refresh path of `UpsertRaisedAsync` (`MediaFlagOperations.cs`). `MediaFlagDto` is a positional record: update every constructor call and test. Table shows one decimal or "–". Tests: write paths, migration model check, table.
- **18, bit rate flags:** change `BitrateFlagsHeading` in `BitrateHistory.resx` to "Bit rate alerts". `MediaAnalysisService` (and `MediaFlagBackfill`) store the clip's `d` on `BitrateCollapse` flags. Baseline decision first: `MediaAnalysisService` uses a per-device seasonal and lighting baseline (lines 128–138), not a camera-wide all-time mean. Either add a query for the all-time baseline, or reuse the existing baseline. Tests: stored value, null when no spread, heading text.
- **20, playhead event type:** add an optional `EventType` parameter to `HistogramTimeline.razor`. Render after the Now readout as "· Person" (localized, using the `EventPlainLanguageFormatter` label). Review pages pass the flag's event type. Test: label shows, hidden when null.
- **23, histogram hover:** add an SVG `<title>` on each bar in `BitrateSeriesChart.razor` (line 22) and the RSSI review chart. Show time, value and the all-time σ. The σ needs a server source: the review pages load a clip series, not the analysis response, so add the σ to the series DTO (`BitrateSeriesDtos.cs` is the likely place). Localized format. Tests: title text.

## Phase E: separate work (orders 26–28)

- **27, NuGet tasks 3.2, 3.3, 4.1, 4.2:** one branch per task, off `dev`, from `docs/plans/IMPLEMENTATION_PLAN.md`. Check 3.2 first: `PlatformDirectoryService.cs` already takes `IFileSystem`, so it may be done. Start each remaining task with a red test. 3.3 needs Zeroconf confirmed. 4.2 needs `IPNetwork2` confirmed. Details in the detail doc, Issue 27.
- **30, dependency alerts:** list the five Dependabot alerts (package, version, fixed version, runtime or test). Fix criticals first, one package per commit. Stop before any major version bump and ask.
- **31, test-support:** decision needed. Option 1: keep as is and document why in `.claude/project-layout.md`. Option 2: move `test-support/TestTempRedirect.cs` into `src/utils/testing/src/`, add a Utils.Testing reference to every test project, and update `Directory.Build.targets` line 5.

## Decisions recorded (2026-11-05)

- RSSI and deviation flags: match the overlapping event in the flag window; "No event" when none (Issue 16, 20).
- Bit rate Dev baseline: add a camera-wide all-time query (Issue 18).
- Push route: build `/api/v1/notifications/preferences` once, in Issue 10; Issue 8 only fixes subscribe and loading.
- Recipient: remove from the export manifest too (Issue 25). Manifest format changes; older exports will not match.
- Import / Export: new top-level Tools group (Issue 3).
- test-support: move into `src/utils/testing/src/`, add a Utils.Testing reference to every test project (Issue 31).
- Dependabot: review, then fix criticals and moderates one package per commit; stop before major bumps (Issue 30).
- My Notifications email: on/off plus minimum severity, same as push (Issue 10).
- Passphrase (Issue 25): remove it. Exports are no longer passphrase-protected; another protection is needed before this ships (not yet planned).
- Dev backfill (Issues 17 and 18): backfill existing flags from the current all-time baseline, as a one-off job.

## Verification (whole plan)

- Each issue: red then green test run, scoped to its namespace (regression-only tests are marked as such).
- Each touched project builds with `-m:1` to a scratch OutDir.
- Browser pass on a local dev host for each UI issue, on `/signal-history`, `/bitrate-history`, `/settings/*`, `/review/export`, `/tools/import-export`, `/accounts/manual-sync`.
- Full gate before any PR to `dev`.
- Push only after confirmation, in the order above.
