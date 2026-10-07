# Server Log Viewer in the WebApp (SuperAdmin) — replaces the desktop LoggerViewer apps

**STATUS: COMPLETED (M6 deferred) — 2026-10-06**

## Context

The repo has two desktop log viewers: `src/utils/logger/VideoForensics.Utils.LoggerViewer` (WPF, Windows, reads
the named pipe) and `VideoForensics.Utils.LoggerViewer.Maui`. The MAUI one cannot build with the `dotnet` CLI
(`Sdk="Microsoft.Maui.Sdk"` is not resolvable; MAUI has no Linux target, so its `net10.0-linux` TFM and
`Platforms/Linux/Program.cs` were never buildable). The Linux-viewer requirement is dropped. Instead, SuperAdmins
view server logs inside the app itself, which works on Windows and Linux with no extra install, and reaches the
MAUI desktop client for free because the page lives in `VideoForensics.Ui.Shared`.

Existing pieces this builds on:
- Logging is wired at `src/client/web/VideoForensics.WebApp/Program.cs:102` (`AddVideoForensicsLogging`), implemented in
  `src/core/core/core.logging/DependencyInjection/ServiceCollectionExtensions.cs` (named-pipe provider already there).
- Client contract + remote pattern to mirror: `ISecurityEventsService`
  (`src/client/common/VideoForensics.Client.Common/Contracts/`) / `RemoteSecurityEventsService`, registered in the
  WebApp with `AddSelfHttpService<T>` (`Program.cs:337`).
- Authorization: `VideoForensicsPolicies.SuperAdminLocal` (SuperAdmin + Local network tier, so logs are never served
  through the tunnel) and `StepUpEndpointFilter` (`X-StepUp-Token`).
- Nav: admin group in `src/client/ui/VideoForensics.Ui.Shared/Layout/NavGroups.cs` (entries gated with
  `ctx.HasRole(OperatorRole.SuperAdmin)`).

## Scope

In: server-process logs only — bounded in-memory buffer, history query, live tail, SuperAdmin page, removal of both
viewer projects.
Out: persistence of logs to the DB (the log file/Event Log/syslog remain the durable record), logs from other
processes (CLI tools), a local-process log source for the MAUI app (optional follow-up, M6).

## Design

### 1. Capture (core.logging)
- `InMemoryLogBuffer` (`core.logging/Services/`): fixed-capacity ring (default 5000, configurable), each entry gets a
  monotonic `long Sequence`. Thread-safe, lock-based append; snapshot reads. Raises an event/`Channel` fan-out for live
  subscribers (bounded per-subscriber channel, drop-oldest, so a slow client cannot back-pressure logging).
- `InMemoryLogBufferProvider : ILoggerProvider` (`core.logging/Providers/`) feeding the buffer; registered inside
  `AddVideoForensicsLogging` next to the named-pipe provider. Captures timestamp, level, category, formatted message,
  exception text (type + message + stack, truncated to ~4000 chars).
- `LogRedactor`: scrubs `Bearer …`, `X-StepUp-Token`, `password=`, session/step-up token shapes before an entry enters
  the buffer. The CLAUDE.md rule against raw account GUIDs in logs still applies at the call sites; the redactor is a
  second line of defense, not a replacement.

### 2. Contracts (`VideoForensics.Api.Contracts`)
- `LogEntryDto(long Sequence, DateTimeOffset TimestampUtc, string Level, string Category, string Message, string? Exception)`
- `LogQueryDto(string? MinLevel, string? Search, long? AfterSequence, int Limit)` and `LogPageDto(IReadOnlyList<LogEntryDto> Entries, long LatestSequence, bool Truncated)`
- Mapping via `entry.ToDto()` / `dto.ToDomain()` per the repo convention.

### 3. Endpoints (`WebApp/Api/LogEndpoints.cs`)
- `GET /api/v1/logs?minLevel=&search=&afterSequence=&limit=` → `LogPageDto` (limit clamped, default 500, max 2000).
- `GET /api/v1/logs/stream?minLevel=&afterSequence=` → SSE (`TypedResults.ServerSentEvents`), `id:` = sequence so a
  reconnect resumes with no gaps/dupes within the buffer window; heartbeat comment every 15 s; ends on request abort.
- Both mapped behind `VideoForensicsPolicies.SuperAdminLocal`. Step-up: require `StepUpEndpointFilter` on the history
  call and on stream open (the Blazor page calls through the server-side `HttpClient`, so headers are available;
  browser `EventSource` is not used). **Decision to confirm** — see below.
- Each stream open and each history query is written to the security audit log (`SecurityAuditEventTypes`, new
  `LogViewed`), since logs can contain sensitive operational data.

### 4. Client service (names unchanged for the UI)
- Client-side types `LogEntry`, `LogQuery` and `LogPage` live in `Client.Common/Contracts/` next to
  `ILogViewerService`, not the `Api.Contracts` DTOs: `Api.Contracts` references `Client.Common`, so the
  reverse reference would be a cycle (same reason as `SecurityEventSummary`).
- `ILogViewerService`: `GetPageAsync(LogQuery, string stepUpToken, ct)` and
  `StreamAsync(LogQuery, string stepUpToken, ct) : IAsyncEnumerable<LogEntry>`. Both endpoints require a step-up
  token, so it is a required per-call parameter (empty -> `ArgumentException`); a stream reconnect needs a valid
  token again.
- `RemoteLogViewerService` in `VideoForensics.Hosting/Remote/`: bearer credential like every other `Remote*`
  class, `X-StepUp-Token` attached per request (never on the shared `HttpClient`), maps DTOs with
  `dto.ToDomain()` / `query.ToDto()`. Streaming uses `SseParser` (heartbeat comments skipped); resume via the
  `afterSequence` query value. 401/403 surface as `HttpRequestException` with `StatusCode` set.
  Registered with `AddSelfHttpService<ILogViewerService>` in the WebApp and through `AddVideoForensicsClientApi`
  for MAUI. No `data.*`/provider references are added to any client host (client/server split rules).

### 5. UI (`Ui.Shared`)
- `Pages/ServerLogs.razor` (+ `.resx`, scoped `.razor.css`, inline `@code` like its sibling pages; there is no `Pages/Settings/` folder), route `/settings/logs`; nav entry "Server Logs" in the admin group
  (`ctx.HasRole(OperatorRole.SuperAdmin)`), placed after "Security Audit Log".
- Level filter, text search, pause/resume, auto-scroll, clear (client-side), "download visible as .txt".
  Desktop-first layout per the UI Layout rules; no mobile-specific logic.
- Live tail via `StreamAsync`; initial fill via `GetPageAsync`; bounded client list (e.g. 2000 rows).
- Step-up: the page obtains the token with `WebAuthn.StepUpAsync(SessionState.SessionToken)` and caches it until a
  401/403 from the service forces a new one (re-prompt, then retry or reconnect the stream).
- Implemented (M4): plain Bootstrap table (not Syncfusion/`Virtualize`: append-heavy tail, variable-height expandable rows),
  keyed by sequence, capped at 2000 rows; the stream loop queues entries and a 100 ms throttle renders them in batches.
  Initial fill resumes the stream from `max(LatestSequence, last entry)`. Time, debounce (300 ms) and backoff (1 s doubling to
  30 s) all go through `TimeProvider` (resolved from DI, falling back to `TimeProvider.System`) so tests drive them with a
  manual provider. Scroll-to-bottom and "download visible" use a tiny ES module, `wwwroot/js/server-logs.js`, imported on demand
  (no global `<script>` in the hosts). `WebAuthnClient.StepUpAsync` became `virtual` so the page can be tested with Moq.
  The page has no `[Authorize]` attribute, matching the other admin pages: access control is the nav gate plus the
  `SuperAdminLocal` endpoint policy; a 401/403 from the service shows the access-denied message.

### 6. Removal
- Delete `src/utils/logger/` (both viewer projects) and their sln entries — **hand-edit `VideoForensics.sln`**
  (`dotnet sln add/remove` previously rewrote the whole file and duplicated ~25 solution folders).
- Remove references in CI workflows/installer scripts/docs if any (grep `LoggerViewer`).
- Keep the `NamedPipeLoggerProvider`/`UnixSocketSink` sinks (server-side, unrelated to the viewer UI) unless a
  follow-up decides otherwise.
- Mark `plans/archive/phase-4-logger-viewer.md` as superseded by this plan; move this plan to `plans/archive/` on
  completion and update `plans/EXECUTION_STATUS.md`.

## Tests (TDD — write first, watch fail, then implement)

- `InMemoryLogBufferTests`: capacity eviction keeps newest; sequence monotonic across wrap; `AfterSequence` returns
  only newer; min-level and search filtering; concurrent writers (no lost/duplicated sequence); slow subscriber drops
  oldest without blocking the writer.
- `InMemoryLogBufferProviderTests`: level/category/message/exception captured; exception text truncated.
- `LogRedactorTests`: bearer tokens, step-up tokens, `password=` values redacted; ordinary text untouched.
- `LogEndpointsTests`: 401/403 for non-SuperAdmin and for non-Local tier; step-up missing → 403; query validation
  (limit clamp); SSE emits ids, resumes from `Last-Event-ID`; audit entry written.
- `RemoteLogViewerServiceTests`: DTO mapping, cancellation forwarded, non-success status handling (mock handler).
- `ServerLogsPageTests` (bUnit, same setup style as `SecurityEventsPageTests`): renders rows, filter, pause, stream
  appends, nav entry visible only for SuperAdmin (`NavGroupsTests`).

## Milestones

1. **M1** buffer + provider + redactor + tests; register in `AddVideoForensicsLogging`.
2. **M2** DTOs + `LogEndpoints` + audit event + tests.
3. **M3** `ILogViewerService` + `RemoteLogViewerService` + DI in WebApp and client API + tests.
4. **M4** `ServerLogs.razor` + nav + bUnit tests.
5. **M5** remove viewer projects, hand-edit sln, CI/doc cleanup, archive plans, update `EXECUTION_STATUS.md`.
6. **M6 (optional)** local buffer provider in `VideoForensics.MauiApp` host + a "Local / Server" source toggle on the page.

Branch: `feature/webapp-log-viewer` off `dev`. Implementation dispatched to Haiku subagents with tests first; lite
gate per milestone; full gate (after asking) before the PR.

## Decisions (confirmed)

1. Buffer capacity default: 5000 entries, configurable.
2. Step-up required on every history query and stream open, in addition to SuperAdminLocal.
3. Named-pipe / Unix-socket sinks are kept (server-side, unrelated to the viewer UI).
4. M6 (MAUI local logs) deferred; not part of this plan's first pass.

## Verification

- `dotnet test --filter` scoped to the new test classes plus `NavGroupsTests`.
- Manual (browser pane): sign in as SuperAdmin, open `/settings/logs`, trigger log lines, confirm live tail, filters,
  pause, reconnect-resume; confirm a non-SuperAdmin and a tunnel-origin request cannot reach `/api/v1/logs*`.
- `dotnet build VideoForensics.sln` (non-MAUI) and the CI slnf succeed with the viewer projects gone.
