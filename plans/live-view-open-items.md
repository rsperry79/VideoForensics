# Live view: open items (follow-up to plans/live-view-telemetry-signalr.md)

**Status (2026-10-09)**: Batches 1-4 of the telemetry plan are merged to `wip` (`1d369820`). Three items remain: the telemetry sampler, the page test, and the full gate. Sample push to clients and telemetry charts depend on the sampler.

## Corrections to the parent plan

- The parent plan says "nothing produces telemetry samples". That is only partly true. The Ring provider already emits `OnReceiverReport` (RTCP loss, jitter) and `OnBitrateSampleBps` on `ILiveViewConnection`. What is missing is a **consumer**: nothing in `LiveViewSessionOrchestrator` subscribes to those events, so no `LiveViewTelemetrySample` rows are written.
- The parent plan names `LiveHubConnection` and `ILiveHubConnection`. Those do not exist. The client observable is `RealtimeHub` plus `ILiveViewSessionSource` (Ui.Shared).

## Phase A: telemetry sampler (server orchestrator)

Goal: each live session turns provider events into scored, persisted, published telemetry.

Touchpoints:
- `src/client/core/VideoForensics.Client.Core/Tools/LiveViewSessionOrchestrator.cs`: on `StartAsync`, subscribe to the connection's `OnReceiverReport` and `OnBitrateSampleBps`. Keep the handlers on `ActiveLiveViewHandle` so `StopAsync` can unsubscribe before disposing. The existing `TelemetryTimer` slot is unused and should be removed or repurposed.
- `src/client/common/VideoForensics.Client.Common/Contracts/ILiveViewTelemetryPublisher.cs`: re-add `PublishSampleAsync(LiveViewTelemetrySample, CancellationToken)`. It was removed in batch 3a because nothing called it.
- `src/data/common/data.common/Contracts/ILiveViewTelemetryRepository.cs`: `AddSampleAsync` already exists. No schema change expected.
- `src/data/common/data.common/Contracts/ICameraBitrateBaselineRepository.cs` and `ILiveViewInterferenceScorer`: use the existing baseline and scorer. With no baseline, `InterferenceScore` stays null.

Behaviour:
- One sample per receiver report. The latest bitrate value is merged in. `FractionLost` (0-255) is converted to a percentage to match the scorer, which expects 0-100. Confirm this conversion against the scorer tests before coding.
- Per sample: score, persist, publish. Each step is isolated. A scorer, persistence or publish failure is logged with the session id and does not close the connection or fail the session.
- A `StopAsync` that runs while a handler is mid-write must not write after the session is closed. Drop late samples for stopped sessions.

Tests (TDD, test first):
- `PersistSample_OnReceiverReport_WritesSample`
- `Sample_BitrateEvent_MergesIntoNextSample`
- `Sample_NoBaseline_InterferenceScoreNull`
- `Sample_FractionLost_ConvertedToPercent`
- `Sample_ScorerThrows_StillPersistsAndKeepsSession`
- `Sample_PublishThrows_StillPersists`
- `Stop_UnsubscribesHandlers_NoWriteAfterStop`

Open decision (A1): cadence. One row per receiver report is the proposal. Receiver reports can be frequent. Throttling is an option if the database or hub load is high.

## Phase B: telemetry to clients

- Server: `LiveViewTelemetryBroadcastService` (WebApp) gets `PublishSampleAsync`. It sends `LiveViewTelemetrySampleDto` to the `live-view:{id}` group under the name `LiveViewTelemetry`. Add the constant to `LiveHubMethods`. Test: `PublishSample_SendsToSessionGroupOnly`.
- Client: `RealtimeHub` binds `LiveViewTelemetry` and exposes `IObservable<LiveViewTelemetrySampleDto>`. `ILiveViewSessionSource` gains a `Telemetry` observable, and `RealtimeLiveViewSessionSource` adapts it.
- Page: show the latest sample (loss, jitter, bitrate, score) as text. Any new text is localized in `LiveView.resx`.

Open decision (B1): are telemetry charts in MAUI in scope now? The parent plan says charts. This plan covers only the latest-values display unless you say otherwise.

## Phase C: page test (bUnit)

Ui.Shared.Tests already references bUnit 2.11.3. Add `LiveViewPageTests`:
- `LiveView_SourceConnected_UsesPushedActiveState` (no polling calls after the push)
- `LiveView_SourceDisconnected_PollsUntilActive`
- `LiveView_StartFails_ShowsLocalizedSessionFailedToConnect`
- `LiveView_Dispose_UnsubscribesFromSession`

Fakes: `ILiveViewSessionService`, `ILiveViewSessionSource`, `IDeviceRepository`, `IJSRuntime`, `NavigationManager`. The waiter tests already cover timing, so these tests check only the wiring.

## Phase D: full gate (needs your confirmation before running)

CLAUDE.md requires confirmation before the full gate. When you confirm, I run it in this order:
1. Confirm `wip` contains `origin/dev`, and sync the work branch with `wip`.
2. `dotnet list VideoForensics.sln package --outdated`, then bump every flagged package in every project that references it. Same version everywhere.
3. `dotnet clean` and `dotnet build` on the whole solution. Fix every warning.
4. `dotnet test` on the whole solution. Fix failures.

Open decision (D1): run the full gate once after Phase C, or after each phase? The proposal is once, after C, to avoid repeating the slow build.

## Order and gates

1. Phase A (Haiku subagent; lite gate; merge and push per the handoff rule).
2. Phase B (same).
3. Phase C (same).
4. Full gate after confirmation.

## Not in this plan

- WebRTC video in MAUI (out of scope per the parent plan).
- Per-device visibility on live-view subscribe. Decided as option 1 (any ReadOnly paired device). Revisit if the access model changes.
