# Live view telemetry over SignalR (plan phase 3c)

**Status on origin/dev (checked 2026-10-09)**: STILL OPEN (CHANGED in detail). No `RemoteLiveViewSessionService`, no live-view REST endpoints, no live-view DTOs, no telemetry push hub method. MAUI does not register `ILiveViewSessionService` at all.

**Source**: `plans/archive/on-demand-live-view.md` lines 257 (names `src/client/host/VideoForensics.Hosting/Remote/RemoteLiveViewSessionService.cs`) and 259 (scoping note: lifecycle and telemetry only, no WebRTC video relay). `plans/signalr-observable-client.md` was requested as a source but does not exist on origin/dev or in the local `plans/` folder (only `EXECUTION_STATUS.md`, `PRIORITY_ANALYSIS.md`, `SCHEDULED_SYNC_REFACTORED_PLAN.md`, `scheduled-background-sync-tasks-linux.md`, `deferred-items.md`, `maui-local-app-lock-verification.md`). Its design could not be consulted; the design below is derived from existing code.

## Verdict
STILL OPEN. Verified on origin/dev: `git grep RemoteLiveView` finds nothing; `src/client/host/VideoForensics.Hosting/Remote/` has no live-view file; `src/client/web/VideoForensics.WebApp/Api/` has no `LiveViewEndpoints.cs`; `src/core/api/VideoForensics.Api.Contracts/` has no live-view DTOs. `ILiveViewSessionService` is bound only to `LiveViewSessionOrchestrator` (singleton, `VideoForensicsHostingExtensions.cs` line 458, server-core path). `AddVideoForensicsClientApi` registers no live-view service, so `LiveView.razor` (`@inject ILiveViewSessionService`) cannot resolve in MAUI today.

## Scope
Corrections to the archived plan:
- Hosting path is correct, but its endpoint list (`/{sessionId}/stop`, `/device/{id}/active`, `/{sessionId}/telemetry`, `/device/{id}/baseline`) was never built; REST endpoints are new work too.
- `ILiveViewSessionService` (in `Client.Common/Contracts`) exposes `GetConnectionForSessionAsync` returning `ILiveViewConnection` (a provider-side live object). It cannot cross the wire; the Remote class must throw `NotSupportedException` for it (or the interface should be split, see open questions). Only `LiveViewSignalingHub` (WebApp-local browser path) uses it.
- The interface returns the `LiveViewSession` entity (data.common). Wire must use a `LiveViewSessionDto` with `ToDto()`/`ToDomain()`; check `Client.Common` already references data.common (it does, since the interface compiles), so the Remote class does not add a forbidden reference.
- The existing `LiveHub` (`/hubs/live`) is paired-device, ReadOnly policy, send-only broadcast. It is the right channel for telemetry push, but starting/stopping sessions must go through REST under a stronger policy, never through the ReadOnly hub.
- Out of scope (per line 259): WebRTC video for MAUI. MAUI gets session state plus telemetry charts only.

In scope:
1. DTOs: `LiveViewSessionDto`, `StartLiveViewRequestDto`, `StopLiveViewRequestDto`, `LiveViewTelemetrySampleDto`, `CameraBitrateBaselineDto` (optional, baseline chart).
2. REST `/api/v1/live-view/...`: start, extend, promote, demote, stop, active-by-device, telemetry (history).
3. Hub push: server broadcasts `LiveViewTelemetry` (sample DTO) and `LiveViewSessionChanged` (session DTO) on `LiveHub`, scoped per session group.
4. Client: `ILiveHubConnection` gains `LiveViewTelemetryReceived` and `LiveViewSessionChanged` events plus `SubscribeLiveViewAsync(sessionId, ct)`/`UnsubscribeLiveViewAsync`.
5. `RemoteLiveViewSessionService : ILiveViewSessionService` and DI swap in `AddVideoForensicsClientApi`.

## Code and file touchpoints
| File | Change | Why |
|---|---|---|
| `src/core/api/VideoForensics.Api.Contracts/LiveViewDtos.cs` | NEW: session, request, telemetry sample DTOs | Wire types; no entities on the wire |
| `src/core/api/VideoForensics.Api.Contracts/` (existing mapping location, follow `ReportDtos.cs` pattern) | NEW mapping ext `ToDto()`/`ToDomain()` for session and sample | Project mapping convention |
| `src/client/web/VideoForensics.WebApp/Api/LiveViewEndpoints.cs` | NEW: `/api/v1/live-view` group, `.RequireRateLimiting("media")`, stronger-than-ReadOnly policy for mutations | Server side of Remote client |
| `src/client/web/VideoForensics.WebApp/Program.cs` | Map endpoints (near line 473), register telemetry broadcaster | Wiring |
| `src/client/web/VideoForensics.WebApp/Hubs/LiveHub.cs` | Add `SubscribeLiveView(Guid sessionId)` / `Unsubscribe` group methods with authorization check | Per-session telemetry fan-out; avoid broadcasting to all devices |
| `src/client/web/VideoForensics.WebApp/Hubs/LiveViewTelemetryBroadcastService.cs` | NEW: pushes samples/state changes to hub group (model on `DownloadProgressBroadcastService.cs`) | Source of push |
| `src/client/core/VideoForensics.Client.Core/Tools/LiveViewSessionOrchestrator.cs` | Raise an event or call a publisher interface when a sample is persisted or state changes | Hook for broadcast (no SignalR types in Client.Core) |
| `src/client/common/VideoForensics.Client.Common/Contracts/ILiveViewSessionService.cs` | Possibly add `GetTelemetryAsync` and `GetBitrateBaselineAsync`; decide on splitting `GetConnectionForSessionAsync` | Interface needs telemetry read for the UI, and a wire-safe surface |
| `src/client/common/VideoForensics.Client.Common/Contracts/ILiveViewTelemetryPublisher.cs` | NEW (name tentative): publisher abstraction | Decouples orchestrator from hub |
| `src/client/host/VideoForensics.Hosting/Remote/RemoteLiveViewSessionService.cs` | NEW: HTTP implementation, forwards `CancellationToken`, bearer via `PairedDeviceAuthHandler` default | Core of the item |
| `src/client/host/VideoForensics.Hosting/LiveHubConnection.cs` | Add live-view events, subscribe methods, `On<>` handlers | Client push receive |
| `src/client/host/VideoForensics.Hosting/VideoForensicsHostingExtensions.cs` | In `AddVideoForensicsClientApi`: `AddHttpClient<ILiveViewSessionService, Remote.RemoteLiveViewSessionService>(...)` | DI swap; client path currently has no registration |
| `src/client/ui/VideoForensics.Ui.Shared/Pages/LiveView.razor` | Use hub events for state and telemetry instead of the 30-attempt poll (lines ~152-161) when available; localize any new text via `.resx` | Observable client; CLAUDE.md localization rule since the page is touched |
| `src/client/host/VideoForensics.Hosting.Tests/RemoteLiveViewSessionServiceTests.cs` | NEW | Mirror `RemoteReportGenerationServiceTests.cs` |
| `src/client/web/VideoForensics.WebApp.Tests` (verify the project name before writing; mirror the existing endpoint test project) | NEW `LiveViewEndpointsTests`, `LiveHub` subscribe tests | Server coverage |
| `src/client/core/VideoForensics.Client.Core.tests/LiveViewSessionOrchestratorTests.cs` | Add publish-on-sample / publish-on-state tests | Changed behavior |

## Test plan
TDD, tests first, each expected to fail first for the right reason (missing type or no call to the publisher/endpoint).
1. `RemoteLiveViewSessionServiceTests` (Hosting.Tests; fails to compile until class exists, then fails on assertions): `StartAsync_ValidResponse_ReturnsDomainSession`, `StartAsync_ServerError_Throws`, `GetActiveSessionAsync_NotFound_ReturnsNull`, `StopAsync_ForwardsCancellationToken`, `ExtendAsync_UnknownSession_ThrowsKeyNotFound`, `GetConnectionForSessionAsync_Always_ThrowsNotSupported`, `AllCalls_UseV1Routes`.
2. DTO mapping round-trip tests (`LiveViewSessionDto_RoundTrip_PreservesAllFields`).
3. `LiveViewEndpointsTests`: success, not found, validation failure, unauthorized and ReadOnly-role forbidden per mutating route.
4. `LiveHub` tests: `SubscribeLiveView_UnknownSession_Rejected`, `SubscribeLiveView_Valid_AddsToGroup`; broadcaster test: `PublishSample_SendsToSessionGroupOnly`.
5. `LiveViewSessionOrchestratorTests`: `PersistSample_PublishesTelemetry`, `Promote_PublishesSessionChanged`.
6. `LiveHubConnection` event tests where practical (handler raises `LiveViewTelemetryReceived`).
7. DI test: `AddVideoForensicsClientApi_ResolvesILiveViewSessionService_AsRemote` and that no server-core assembly is required.
Run scoped with `dotnet test --filter`; no full suite locally.

## Risks and open questions
- Missing `plans/signalr-observable-client.md`: confirm whether it exists on another branch or was never written; the intended "observable client" abstraction (e.g. `IObservable`/event shape) is unknown.
- Authorization: `LiveHub` is ReadOnly. Starting live view triggers camera access, so mutating routes need Operator/Admin policy (verify actual policy names in `VideoForensicsPolicies`). Per-session group join must verify the caller may see that device.
- Privacy/audit: live-view start/stop should write security events with operator id; MAUI must pass the paired operator, not trust a client-supplied `operatorId`.
- `GetConnectionForSessionAsync` is not remotable; splitting the interface (e.g. `ILiveViewConnectionProvider`) is cleaner but touches `LiveViewSignalingHub` and the orchestrator DI.
- Hub is WebSockets-only and auth-refresh relies on `PairedSessionState`; reconnect must re-subscribe to session groups.
- Singleton `ILiveHubConnection` is only started when MAUI calls `StartAsync`; ordering with `LiveView` page lifetime needs defined behavior.
- Does MAUI actually surface the LiveView page (no registration today)? Confirm product intent before investing in UI changes.
- Idle-timeout service extends sessions; telemetry sampling rate versus push rate may need throttling.

## Priority recommendation
**Low-Medium (Medium if MAUI live view is a near-term goal).** Product value is real for mobile operators but video is out of scope, so MAUI would get state and charts only; security exposure is meaningful (remote camera control and session-scoped hub groups need correct authorization); effort is substantial across server, hub, client and tests. **Effort: L.**

## Implementation dispatch
Per CLAUDE.md, delegate to Sonnet-class subagents (the project memory says `model: "haiku"`; follow the user's current instruction), tests first, in four sequential batches: (1) DTOs, mappings and `RemoteLiveViewSessionService` plus tests; (2) `LiveViewEndpoints` plus authorization tests; (3) publisher abstraction, orchestrator hook, `LiveHub` subscribe and broadcaster plus tests; (4) `LiveHubConnection` events, DI swap, and `LiveView.razor` observable wiring with localization. Lite gate after each batch.
