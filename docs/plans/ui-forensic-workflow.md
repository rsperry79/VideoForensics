# UI Forensic Workflow Plan

Branches: phases 1–3 `claude/ui-media-auth` (PR #48, merged to dev); phase 4 `claude/ui-cases`; later phases each on their own branch. All PRs target `dev`; `main` only takes PRs from `dev` (#49). Status is updated at the end of every phase.

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
| **Sources** | Provider accounts: status/re-auth, sync, locations & devices, API tester (raw responses shown, never stored), error log | Accounts, AccountDetails, AddAccountWizard, AccountSyncSchedule, QueryApi, RingSelfTest, DeviceConfig |
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

### Phase 3 — Evidence Timeline / Gallery / media viewer ✅ Done

- ✅ `GET /api/v1/media-items` accepts `from`/`to`; `GET /api/v1/media-items/{id}`;
  `RemoteMediaItemRepository` implements date-range and by-id reads for MAUI.
- ✅ `EvidenceLoader` merges events with every stored media file in scope (no duplicates, purged
  skipped, per-device failures isolated); `EvidenceTimeline` groups by day → device, prev/next.
- ✅ `MediaViewer`: image zoom (1–8×, wheel, keys) and drag-pan; video speed, frame-stepping from
  the media's frame rate, play/pause; keyboard navigation; header shows device, UTC time, SHA-256.
- ✅ `/evidence` page (first nav item): Timeline / Grid / Gallery views over the scope, viewer with
  inspector docked, fresh ticket per opened item with stale-response guard, `view=`/`item=`
  deep links.
- Deferred: "grab still with hash" (needs a server write path — revisit with Cases in phase 4);
  Dashboard/Events/`EventDetailsDialog` removal moves to phase 6 with the nav restructure.

### Phase 4 — Cases ✅ Done (`claude/ui-cases`)

- ✅ `ForensicCase` / `CaseDevice` / `CaseItem` schema (migration `AddForensicCases`, generated from the
  model, drift check empty): unique case number, device-scope junction, soft-removed pins with a
  SHA-256 snapshot, filtered unique indexes (one active pin per target) and a CHECK constraint.
- ✅ `CaseRepository` with chain-of-custody ActionLog entries for every mutation; closed cases are
  read-only.
- ✅ `/api/v1/cases` (read: any operator; create/edit/scope/pin: Review; close/reopen: Admin; 400/401/
  404/409 semantics) and `RemoteCaseRepository` for MAUI.
- ✅ `CaseState` + `CaseUrl`: active case in the `case=` query key, applies/saves the case scope.
- ✅ Scope rail case picker + "save scope to case"; inspector "Pin to {case}" with required reason.
- ✅ Pages: `/cases` (status filter), `/cases/new` (optionally from current scope), `/cases/{id}`
  (details edit, pinned items with hash integrity Match/Mismatch/Missing, remove with reason,
  Admin close/reopen, "Work on this case" → Evidence). New "Cases" nav group.

### Phase 5 — Raw API data in the tester ✅ Done (`claude/ui-api-tester`)

Decision: raw provider API traffic is **displayed, never persisted** (storing payloads would bloat
the database; parsed provider data is already stored in structured tables), and it is shown **only in
the API tester** (Ring Self-Test) — no app-wide buffer or explorer.

- During a self-test run, capture the `ApiRawLogger` calls made by each tested endpoint (scoped to that
  call) and return them on the result: `SelfTestCallDto.RawCalls` (method, redacted URL, status,
  timestamp, response body truncated at 256 KB with original length + SHA-256; tokens/secrets redacted).
- Ring Self-Test page: results in a ForensicGrid; selecting a call opens the inspector with the parsed
  result in Fields and the raw responses in Raw (DumpView), plus schema issues.

### Phase 6 — Navigation restructure ✅ Done (`claude/ui-nav`)

- ✅ Nav is Evidence, Cases, Analyze, Sources, Admin (Lockout Policy added); a reflection test asserts
  every nav/user-menu path is a real route.
- ✅ Operator's own login (change password, passkeys, sign out, device sign-in) in a `UserMenuButton`
  on desktop and mobile, separate from provider logins.
- ✅ "/" lands on Evidence; Dashboard and Workflow pages removed (/workflow redirects). Events stays as
  "Event Grid" (its hold/export/verify actions have no Evidence equivalent yet).
- ✅ `/analyze` merges reports, signal anomalies, access control and jamming as tabs on the global
  scope; old routes redirect; `NavPathMatcher` highlights query-string nav items.
- ✅ Mobile layout: filters sheet with the ScopeRail (+ custom-scope indicator) and an inspector
  drawer (resolves the phase 2 mobile follow-up).
- ✅ Security Events page (from #45, previously in an uncompiled directory) rehomed and wired to
  `ISecurityEventsService` (separate PR).

### Phase 7 — Device-by-time view + snapshot stream ✅ Done (`claude/ui-device-time`)

- ✅ Evidence `view=devicetime`: device × time grid (`DeviceTimeGrid`) — hourly buckets up to 7 days,
  wider ranges use multi-hour buckets (≤168 columns); cells show event and media counts; empty runs
  between a device's first and last activity are highlighted as gaps. Clicking a cell narrows the
  scope to that device + bucket and switches to Timeline.
- ✅ `SnapshotStrip`: per-device time-ordered ticketed thumbnails with a scrubber (range + arrow
  keys) and an inline-SVG WiFi RSSI plot with the scrub position marked; selecting opens the viewer.
- ✅ RSSI data path: `IDeviceHealthRepository.GetHistoryAsync(from, to)`,
  `GET /api/v1/devices/{id}/health?from=&to=` (auth required), `DeviceHealthDto.ToDomain()`,
  `RemoteDeviceHealthRepository` for MAUI.

### Phase 8 — Evidence actions 🔄 In progress (`claude/ui-evidence-actions`)

- Grab still with hash: capture the current video frame from the media viewer as derived evidence
  (source media id, frame time, SHA-256 of both, operator, chain-of-custody entry; original untouched),
  optionally pinned to the active case. Resolves the phase 3 deferral.
- Move the Event Grid's legal hold, export and integrity-verify actions into Evidence (grid context
  menu / inspector), then retire the Events page and `EventDetailsDialog` (old route redirects).

## Follow-ups

- ✅ Session network tier (`claude/session-network-tier`): each web session records its real tier from
  the browser's initial request (prerender → persisted state; unknown → Internet; never loosens).
  Self-HTTP calls (security services, Security* pages via `ISelfApiHttpClientFactory`, WebAuthnClient
  login) carry a signed 2-minute `X-VF-Session-Tier` header, honoured only on loopback; the auth
  handler requires it bound to the operator, pre-auth login accepts an unbound one
  (`RequestTierResolver`).

## Environment notes

- The repo `nuget.config` lists only the GitHub Packages feed, which the cloud proxy blocks;
  restore with `--ignore-failed-sources` (all needed packages come from nuget.org).
- MAUI cannot be built on Linux; MAUI edits are limited to DI registrations and are unverified here.
- PRs require the full gate (CLAUDE.md) and user confirmation before running it.
