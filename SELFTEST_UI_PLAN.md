# Ring Self-Test Tools Page — Implementation Plan

## Goal

Expose `src/selftest`'s Ring API smoke-test tool (`Runner`/`EndpointRegistry`/`IndexDocument` — already
a reusable library in `VideoForensics.Providers.Ring.Utils`, not console-only) as a page under the
existing **Tools** nav group in `VideoForensics.Ui.Shared`, shared by WebApp and MauiApp. The page:

- Uses the already-active Provider/Account (from the existing global `AccountSwitcher`) — no separate
  provider/account picker duplicated on this page.
- Has a right-side settings panel: endpoint selection (grouped read-only vs. destructive/physical),
  destructive-endpoint parameters (siren duration, volume, chime type, DND seconds, location mode,
  ding id, asset uuid, push token), location/doorbot/chime id filters, history limit.
- Shows a clear warning when destructive or physical endpoints are selected, and requires explicit
  confirmation before a run that includes them can start.
- Has a "Run Report" button in the top-right of the page.
- Shows a results panel that stays **hidden** until a run completes, then displays the summary
  (succeeded/failed/total) and per-call results (endpoint, target, duration, success/error, restore
  outcome, schema issues).

All client/server traffic goes through DTOs in `VideoForensics.Api.Contracts` and versioned
`/api/v1/selftest/...` routes, per this repo's client/server split rules (`CLAUDE.md`). MAUI never
references providers/data-layer projects directly — it talks to WebApp's API via a `Remote*`
implementation of a shared interface, exactly like every other client/server feature.

## Why not copy `JammingToolsOrchestrator`'s exact DI pattern

`JammingToolsOrchestrator`/`ConfigToolsOrchestrator` are registered as **concrete classes** only inside
`AddVideoForensicsServerCore`, with `Pages/JammingAnalysis.razor` injecting the concrete type directly.
There is no `Remote*` counterpart or shared interface for either — that only works because WebApp is
the only host that has ever actually loaded that page; it would fail to resolve on MAUI. This is
existing tech debt, not a pattern to copy. Instead, follow the `IVideoDownloadService`/
`RemoteVideoDownloadService` pattern: one interface in `VideoForensics.Client.Common.Contracts`, a
local (server-side, in-process) implementation, and a `Remote*` HTTP implementation — both registered
under the same interface so the Razor page needs zero host-specific code.

## Long-running run: fire-and-forget + poll (no new SignalR hub)

Mirror `Api/DownloadEndpoints.cs` + `Components/DownloadProgressPanel.razor` exactly:

- `POST /api/v1/selftest/run` — starts the run via `Task.Run(...)` server-side, returns `202 Accepted`
  immediately.
- `GET /api/v1/selftest/status` — cheap synchronous status poll (`Idle` / `Running` / `Completed` /
  `Failed`).
- `GET /api/v1/selftest/result` — the completed `IndexDocument`, mapped to a DTO once done (204 while
  still running).
- The Razor page polls `status` on a `PeriodicTimer` (matching `DownloadProgressPanel.razor`'s 500ms
  loop) while `Running`, then fetches `result` once `Completed` and reveals the results panel.

`EndpointRegistry`'s ambient static fields (`CurrentHistoryLimit`, `SirenDurationSeconds`, etc.) and
per-run restore-plan state are **not** safe for concurrent runs — the orchestrator must guard with a
lock so only one self-test run executes at a time server-wide, returning a clear "already running"
error from `POST /run` otherwise.

## Files to add/change

### 1. DTOs — `src/core/api/VideoForensics.Api.Contracts/SelfTestDtos.cs` (new)

- `SelfTestEndpointDto` — `Key, DisplayName, Description, SessionMethod, HttpMethod, ApiPath, Scope, Destructive, Physical`
- `SelfTestRunRequestDto` — `Endpoints: IReadOnlyList<string>` (empty/["all"] = all non-destructive),
  `LocationId: Guid?`, `DoorbotId: long?`, `ChimeId: long?`, `HistoryLimit: int`, `Destructive: bool`,
  `NoPhysical: bool`, `SirenDurationSeconds: int?`, `VolumeLevel: int?`, `ChimeTypeValue: int?`,
  `DndSeconds: int?`, `LocationModeValue: string?`, `DingId: string?`, `AssetUuid: string?`, `PushToken: string?`
- `SelfTestRunResponseDto` — `Accepted: bool`, `Error: string?` (e.g. "already running")
- `SelfTestStatusDto` — `Status: SelfTestRunStatus` (enum: `Idle, Running, Completed, Failed`),
  `StartedAtUtc: DateTime?`, `CompletedAtUtc: DateTime?`, `Error: string?`
- `SelfTestResultDto` — `ToolVersion, GeneratedAtUtc, CredentialSource, Summary: SelfTestSummaryDto {TotalCalls,Succeeded,Failed}, Calls: IReadOnlyList<SelfTestCallDto>`
- `SelfTestCallDto` — `Endpoint, DisplayName, SessionMethod, Destructive, Physical, Target: string?, StartedAtUtc, DurationMs, Success, Error: string?, RestoreAttempted, RestoreSuccess: bool?, RestoreError: string?, RestoreSkippedReason: string?, SchemaIssues: IReadOnlyList<string>`
- `SelfTestDtoMapping` static class: `ToDto()` extensions off `EndpointDescriptor`, `IndexDocument`, `CallRecord` (in `VideoForensics.Providers.Ring.Utils` — this file needs a reference to that project, or the mapping lives server-side next to the orchestrator instead; see note in task 2).

### 2. Server orchestrator + interface — `src/client/core/VideoForensics.Client.Core/Tools/RingSelfTestOrchestrator.cs` (new) + `IRingSelfTestService` interface

- Add `<ProjectReference Include="../../../providers/ring/utils/VideoForensics.Providers.Ring.Utils.csproj" />` to `VideoForensics.Client.Core.csproj`.
- `RingSelfTestOrchestrator` (constructor takes `ILogger<RingSelfTestOrchestrator>`, `ISessionProvider`): holds run state (`SelfTestRunStatus`, last `IndexDocument?`, started/completed timestamps, error) behind a lock; `Task<bool> TryStartRunAsync(RunOptions options, string outputDir)` (returns false if already running — fire the actual `Runner.RunAsync` in `Task.Run`), `SelfTestStatusInfo GetStatus()`, `IndexDocument? GetResult()`. Uses `ISessionProvider.GetSession()` for the already-authenticated session — do **not** re-derive credentials.
- Interface `IRingSelfTestService` in `VideoForensics.Client.Common.Contracts` (new file): `Task<IReadOnlyList<SelfTestEndpointDto>> ListEndpointsAsync(CancellationToken ct)`, `Task<SelfTestRunResponseDto> StartRunAsync(SelfTestRunRequestDto request, CancellationToken ct)`, `Task<SelfTestStatusDto> GetStatusAsync(CancellationToken ct)`, `Task<SelfTestResultDto?> GetResultAsync(CancellationToken ct)`.
- Local implementation `LocalRingSelfTestService` (in `Client.Core/Tools/` alongside the orchestrator) implements `IRingSelfTestService` by calling straight into `RingSelfTestOrchestrator` and `EndpointRegistry.All` (for `ListEndpointsAsync`), doing the DTO mapping itself (put `SelfTestDtoMapping` here instead of in `Api.Contracts`, since `Api.Contracts` should not reference `Ring.Utils` — keep the DTO project provider-agnostic, matching every other DTO file).
- Register in `VideoForensicsHostingExtensions.AddVideoForensicsServerCore` (only for `activeProviderName == "Ring"`, alongside the other Ring services): `services.AddScoped<RingSelfTestOrchestrator>()` as effectively-singleton-per-process state — actually register as **Singleton** (the run state must survive across scoped requests; same reasoning as `ISessionProvider`), and `services.AddScoped<IRingSelfTestService, LocalRingSelfTestService>()`.

### 3. WebApp API endpoints — `src/client/web/VideoForensics.WebApp/Api/SelfTestEndpoints.cs` (new)

Mirror `DownloadEndpoints.cs`/`EvidenceEndpoints.cs` exactly: `MapGroup("/api/v1/selftest")`,
`RequireAuthorization()` baseline for all four routes. On `POST /run`, if `request.Destructive` is
true, additionally require `VideoForensicsPolicies.SuperAdminLocal` + `StepUpEndpointFilter` (matching
`EvidenceEndpoints.cs`'s asymmetric narrowing/widening pattern) — check the role from
`HttpContext.User` inside the handler (can't conditionally apply `RequireAuthorization` per-request
body, so gate in the handler body and return `403` with a clear message if the caller only has the
base policy but requested `Destructive: true`). Register `app.MapSelfTestEndpoints()` from `Program.cs`
next to the other `Map*Endpoints()` calls.

### 4. Remote client — `src/client/host/VideoForensics.Hosting/Remote/RemoteRingSelfTestService.cs` (new)

Typed `HttpClient` implementation of `IRingSelfTestService`, POSTing/GETting the four routes above with
`PairedDeviceAuthHandler` already attached (matching every other `Remote*` class — see
`RemoteVideoDownloadService.cs`/`RemoteProviderAuthService.cs` for the exact shape). Register in
`AddVideoForensicsClientApi` (`VideoForensicsHostingExtensions.cs`, alongside the other typed-client
registrations).

### 5. UI page — `src/client/ui/VideoForensics.Ui.Shared/Pages/Tools/RingSelfTest.razor` (new)

- `@page "/tools/ring-selftest"`, `@inject IRingSelfTestService SelfTestService`.
- Add `new("Ring Self-Test", "/tools/ring-selftest")` to the `"tools"` `NavGroup` in
  `Layout/NavGroups.cs`.
- Top-right "Run Report" button in the page header (disabled while a run is in progress; disabled
  until the destructive/physical confirmation checkbox is checked if any selected endpoint is
  destructive/physical).
- `<PageRightPanelContent>` (matching `JammingAnalysis.razor`/`QueryApi.razor`): endpoint checklist
  grouped "Read-only" vs. "Destructive / Physical" (loaded once via `ListEndpointsAsync`), a **warning
  banner** ("These actions affect real devices/hardware and cannot always be undone") shown whenever
  any checked endpoint has `Destructive` or `Physical` true, a required "I understand" confirmation
  checkbox gating the Run button in that case, and the settings fields (location/doorbot/chime id,
  history limit, and the destructive-only fields shown conditionally when a destructive endpoint
  needing them is selected).
- Results panel: `hidden` (or simply not rendered) until `SelfTestStatusDto.Status == Completed`;
  polls via a 500ms `PeriodicTimer` while `Running` (same pattern as `DownloadProgressPanel.razor`).
  Shows summary counts, then a table/grid of calls (endpoint, target, duration, success/fail badge,
  error text, restore outcome, schema issues) — reuse `SfGrid` styling consistent with
  `Pages/Accounts.razor` if a grid component is already standard here.

### 6. Tests

- `src/client/VideoForensics.Client.Core.tests/RingSelfTestOrchestratorTests.cs` — xUnit + Moq,
  matching `JammingToolsOrchestratorTests.cs`'s style: single-run-at-a-time guard rejects a second
  concurrent start, status transitions Idle→Running→Completed, `GetResult()` returns null before
  completion.
- DTO mapping tests alongside `LocalRingSelfTestService` (or a small `SelfTestDtoMappingTests.cs`) —
  verify `IndexDocument`/`CallRecord`/`EndpointDescriptor` map to their DTOs without loss for the
  fields the UI displays.
- New `VideoForensics.WebApp.Tests` project does not exist yet; scope endpoint testing to
  direct-handler-invocation tests if time allows, otherwise defer full endpoint integration tests as
  explicit follow-up (call this out rather than silently skipping).

## Execution order (Haiku subagents, per CLAUDE.md's delegation rule)

1. **Agent A** — DTOs (`SelfTestDtos.cs`) + `IRingSelfTestService` interface. Foundational; everything
   else depends on it.
2. **Agent B** (after A) — `RingSelfTestOrchestrator` + `LocalRingSelfTestService` + `Client.Core.csproj`
   reference + DI registration in `AddVideoForensicsServerCore`.
3. **Agent C** (after A, parallel with B/D) — WebApp API endpoints (`SelfTestEndpoints.cs`) +
   `Program.cs` registration. Depends only on the DTOs from A (calls into B's service via DI, but the
   file itself can be written against the interface before B lands, then wired/tested after).
4. **Agent D** (after A, parallel with B/C) — `RemoteRingSelfTestService` + `AddVideoForensicsClientApi`
   registration.
5. **Agent E** (after A) — Razor page + nav entry. Depends on the interface/DTOs from A, not on the
   concrete B/C/D implementations to *write* against, but needs them to actually run end-to-end.
6. **Agent F** (after B) — Orchestrator + DTO mapping tests.
7. Final integration pass (main session): wire B+C+D+E together, full clean rebuild, run selftest UI
   manually against the live WebApp (`preview_start`/browser tools) to confirm the run→poll→results
   flow actually works, fix any seams the agents' isolated work didn't cover.
