# UI Forensic Workflow Plan

Branch: `claude/ui-media-auth` · Status is updated at the end of every phase.

## Goal

Turn the UI into a natural forensic workflow: one scope (case → devices → time window) that every
page reads, drill-down on every table row into its parsed fields, raw provider JSON and provenance
(LinqPad-style), in-app viewing of snapshots and event video, and fewer, better-organised pages.

## Decisions

- **App login is separate from provider logins.** Operator account (change password, passkeys,
  sign out) lives in a top-bar user menu; security administration lives under Admin. Ring/provider
  accounts live only under Sources.
- **Cases are a first-class entity.** `Case` / `CaseScope` / `CaseItem`; the scope rail becomes
  case-scoped; custody, validation, export and reports belong to a case.
- **In-app media viewing is required** for snapshots and events (web and MAUI).

## Target information architecture

| Area | Contents | Replaces |
|---|---|---|
| **Cases** | Case list, create case, case overview (scope, item counts, custody, exports) | — |
| **Evidence** | Timeline / Grid / Gallery; media viewer; device-by-time view; Collect action | Dashboard, Events, CollectVideos, CollectSnapshots |
| **Analyze** | Reports, anomalies, jamming, access control — one page, runs on case scope | ForensicReports, SignalAnomalies, JammingAnalysis, AccessControl |
| **Sources** | Provider accounts: status/re-auth, sync, locations & devices, API Explorer (raw), error log | Accounts, AccountDetails, AddAccountWizard, AccountSyncSchedule, QueryApi, RingSelfTest, DeviceConfig |
| **Admin ⚙** | Operators, paired client devices, security, network, storage, notifications, update, infrastructure | Security*/Settings* pages |

Workflow page is removed — the nav order is the workflow.

## Phases

### Phase 1 — Media auth + viewing tickets ✅ Done

- `IMediaAccessTicketService`: 10-minute Data-Protection tickets scoped to one media item + operator.
- `/api/v1` media routes require bearer auth; `GET /api/v1/media/{id}/content` also accepts
  `?ticket=` (img/video tags can't send headers), re-checks the operator is active/approved, and
  records each initial view in the access audit log (continuation Range requests skipped; audit
  failure → 500).
- `POST /api/v1/media/tickets` (batch, max 200).
- `IMediaContentUrlProvider`: `RemoteMediaContentUrlProvider` (MAUI, absolute URLs) and
  `LocalMediaContentUrlProvider` (WebApp, attributes tickets to the validated session principal).
- Events page and event dialog load media via ticketed URLs (previously a broken unversioned route).
- New `VideoForensics.Ui.Shared.Tests` project (xUnit v3 + bUnit).

### Phase 2 — Shared building blocks ✅ Done

- ✅ `DumpView` — LinqPad-style raw data viewer (tree, tabular arrays, embedded-JSON expansion,
  search with auto-expand, copy JSON).
- ✅ Inspector panel (Fields / Raw / Related / Provenance) in the right panel; clears on navigation.
- ✅ `ForensicGrid<TItem>` — standard grid (paging, sort, checkbox filter, column chooser, search,
  CSV export); row select opens the inspector.
- ✅ `ScopeState` + left `ScopeRail` (devices, date range, search, quick ranges), synced to the URL query string.
- ✅ Events migrated: reads the global scope (multi-device fan-out via `EventRowLoader`), ForensicGrid with
  context menu, inspector shows event + media entities, raw provider JSON and provenance.
- ✅ Query API migrated: ForensicGrid + inspector per result type, collapsible whole-response DumpView,
  device-events window from the scope, row selection fills location/device ids, config shown via DumpView.

### Phase 3 — Evidence Timeline / Gallery / media viewer 🔄 Next

Timeline (grouped by day → device), Grid and Gallery views over the scope; full-pane media viewer
(frame-step, speed, zoom/pan, prev/next, keyboard, grab still with hash) with inspector docked;
replaces Dashboard, Events and `EventDetailsDialog`.

### Phase 4 — Cases ⏳

Entities + migration, `/api/v1/cases` (`ToDto()`/`ToDomain()`), `Remote*` client classes, Cases
pages; scope rail becomes case-scoped ("Save to case"); "Add to case" from the inspector/viewer.

### Phase 5 — Raw API traffic capture ⏳

Persist `ApiRawLogger` traffic to an `ApiCallLog` table (redacted request/response bodies, status,
duration, account, run id); `GET /api/v1/api-calls/{id}`; `ApiCallId` on self-test/query results;
merge Query API + Ring Self-Test into a Sources → API Explorer with raw drill-down.

### Phase 6 — Navigation restructure ⏳

Rewrite `NavGroups.cs` to the five areas, merge Analyze pages, redirect old routes, top-bar user
menu for operator login, move Admin items, delete Workflow page.

### Phase 7 — Device-by-time view + snapshot stream ⏳

Device × hour grid of event/snapshot markers (gap spotting for jamming/anomaly work) and a
per-device scrubbable snapshot strip alongside RSSI.

## Environment notes

- The repo `nuget.config` lists only the GitHub Packages feed, which the cloud proxy blocks;
  restore with `--ignore-failed-sources` (all needed packages come from nuget.org).
- MAUI cannot be built on Linux; MAUI edits are limited to DI registrations and are unverified here.
- PRs require the full gate (CLAUDE.md) and user confirmation before running it.
