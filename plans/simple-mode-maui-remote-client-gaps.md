# Simple mode: MAUI remote-client gaps

**Status on origin/dev (checked 2026-10-09)**: CHANGED. The jamming endpoint, DTOs, Remote repository and DI registration all landed in #184 (commit 39b51aa). One narrow gap remains: the parameterless-range `GetHistoryAsync(deviceId, ct)` on `RemoteDeviceHealthRepository` still throws `NotSupportedException`.

**Source**: `plans/archive/simple-mode-embedded-mcp-chat.md` line 14: "MAUI remote-client mode has no jamming endpoint or Remote repository, so the Simple home shows device events only (with a note) there; the Analyze page has the same pre-existing gap."

## Verdict

The cited gap is stale. `src/client/web/VideoForensics.WebApp/Api/JammingEndpoints.cs` serves `/api/v1/jamming/*` (GET incidents, incidents/{id}, stats, stats/{deviceId}; PUT incidents; POST stats/{deviceId}/recompute). `src/core/api/VideoForensics.Api.Contracts/JammingDtos.cs`, `Remote/RemoteJammingRepository.cs` and `AddHttpClient<IJammingRepository, RemoteJammingRepository>` (VideoForensicsHostingExtensions.cs ~line 635) exist, with `RemoteJammingRepositoryTests`, `JammingDtoMappingTests` and `JammingEndpointTests` covering them. `SimpleHome.razor` resolves `IJammingRepository` via `Services.GetService`, so in MAUI it now receives the Remote implementation. The only leftover is a code comment in VideoForensicsHostingExtensions.cs (~line 659-662): `JammingToolsOrchestrator.AnalyzeJammingAsync` (auto-detection from RSSI history) reports a failure in client mode because `RemoteDeviceHealthRepository.GetHistoryAsync(Guid, CancellationToken)` throws `NotSupportedException`. The ranged overload `GetHistoryAsync(deviceId, fromUtc, toUtc, ct)` is implemented and backed by `GET /api/v1/.../devices/{id}/health` (MediaApiEndpoints.cs `GetDeviceHealthHistoryAsync`). Simple home reads incidents/stats only, so it is unaffected; this residual affects the Analyze page's auto-detect button in MAUI.

## Scope

In: make the unranged `GetHistoryAsync(deviceId, ct)` work remotely (or make the orchestrator use the ranged overload), update the stale comment, and verify the Simple home "device events only" note is no longer shown in remote mode. Out: new endpoints, new DTOs, new repositories (all exist); other unsupported `RemoteDeviceHealthRepository` methods.

## Code and file touchpoints

| File | Change | Why |
|---|---|---|
| src/client/host/VideoForensics.Hosting/Remote/RemoteDeviceHealthRepository.cs | Implement `GetHistoryAsync(Guid, CancellationToken)` by delegating to the ranged overload with a bounded default window (or an unbounded server query), forwarding `ct` | Removes the NotSupportedException that breaks jamming auto-detect in MAUI |
| src/client/core/VideoForensics.Client.Core/Tools/JammingToolsOrchestrator.cs | Alternative option: call the ranged overload directly, so no repository change is needed | Smaller blast radius if the unranged semantics (all history) are too heavy over HTTP |
| src/client/host/VideoForensics.Hosting/VideoForensicsHostingExtensions.cs | Delete the "does not support yet" comment near the `JammingToolsOrchestrator` registration | Comment becomes false |
| src/client/host/VideoForensics.Hosting.Tests/RemoteJammingRepositoryTests.cs | Reference only, as the style pattern | Existing handler-mock pattern |
| src/client/ui/VideoForensics.Ui.Shared/Layout/Simple/SimpleHome.razor | Verify only; no change expected | Confirm the jamming-missing note is gated on a null repository, not on mode |
| src/client/host/VideoForensics.Hosting.Tests (a RemoteDeviceHealthRepository test file; check whether one exists, otherwise NEW `RemoteDeviceHealthRepositoryTests.cs`) | Add tests | Cover the new path |
| src/client/ui/VideoForensics.Ui.Shared/Resources (SimpleHomeOlderNote and any "device events only" key) | Check only; remove unused key if the note is dead | Localization hygiene |

## Test plan

TDD, tests first, project `src/client/host/VideoForensics.Hosting.Tests`:
- `RemoteDeviceHealthRepository_GetHistoryUnranged_ReturnsMappedHealth()` - expected failure first: throws NotSupportedException.
- `RemoteDeviceHealthRepository_GetHistoryUnranged_ForwardsCancellationToken()` - fails the same way.
- `RemoteDeviceHealthRepository_GetHistoryUnranged_SendsBearerAuthorized()` only if auth is applied per-class in this repo; otherwise skip.
- If the orchestrator route is chosen instead: `JammingToolsOrchestrator_AnalyzeJamming_UsesRangedHistory()` in `src/client/core/VideoForensics.Client.Core.tests/JammingToolsOrchestratorTests.cs`.
Run scoped with `dotnet test --filter "FullyQualifiedName~RemoteDeviceHealthRepository"` only.

## Risks and open questions

- Unbounded "all history" over HTTP may be large; prefer a bounded default window matching what the detector needs. Confirm the window with `ForensicsOptions`.
- Confirm that the server health endpoint enforces the same authorization as the other `/api/v1` routes and does not leak devices outside the caller's scope.
- Unverified in a running MAUI client; the Simple home claim should be checked once manually.
- Possibly a no-op item: if the owner considers jamming auto-detect in MAUI out of scope, close this plan as done by #184.

## Priority recommendation

Low: the headline gap is already closed by #184, no new exposed surface is needed, and only a narrow MAUI Analyze auto-detect path remains. Effort S.

## Implementation dispatch

One Sonnet subagent: write the failing tests first, confirm they fail, implement the repository change plus comment cleanup, then confirm they pass; I verify via lite gate on Hosting and its Tests project.
