# On-Demand Live View with Jamming-Triggered Sustained Mode

## Context

Ring already has raw WebRTC live-view connection code (`Session.StartLiveView` → `RingLiveViewSession`) but nothing orchestrates it: no idle-timeout, no persisted session state, no way to keep a session open indefinitely, and no link to jamming detection. Separately, jamming detection today (`JammingToolsOrchestrator`) only runs on-demand over historical `DeviceHealth` rows collected on a fixed 15-minute poll — far too coarse to catch a jamming burst in time to react.

The goal: when jamming is suspected, automatically open a live view of the affected camera and keep it open (a normal live view times out after a short idle period; a jamming-triggered one should stay open under "sustained mode" until the threat passes or an operator/safety-valve closes it). To judge whether interference is actually happening — both to decide when to escalate polling and to judge signal quality *while a live view is open* — we need something to compare current readings against: a baseline. RSSI already has a same-day rolling baseline (`JammingToolsOrchestrator`'s median-of-recent-readings). Bitrate needs the same treatment, but it's harder: bitrate degrades under jamming, but it also varies naturally by time of day (night IR mode, low-light encoding, traffic patterns) — so the baseline has to be time-bucketed (hour-of-day × weekday/weekend), not a single global number, or every night-time live view would look "jammed."

Decisions made with the user before finalizing this plan:
- **Bitrate baseline data source**: scheduled short calibration sessions (a new low-frequency background job that opens brief live-view sessions purely to sample bitrate across all time buckets), not just opportunistic organic-session data — organic sessions are too rare/skewed to fill in every bucket.
- **Sustained-mode ceiling**: configurable safety valve, non-zero default (e.g. 240 min) — protects against an indefinitely-open session from a false positive, but is a deliberate opt-out (set to 0) if truly unlimited is wanted.
- **Non-Ring providers**: register a no-op `ILiveViewCapableProvider` stub for every provider now, so the UI/API always has a consistent "not supported" result instead of the capability just not existing for non-Ring devices.
- **Step-up re-auth**: NOT required on manual live-view start — treated as a monitoring action, not a sensitive/destructive one.

## Data Model

### `LiveViewSession` — `src/data/common/data.common/Entities/LiveViewSession.cs`
```csharp
public class LiveViewSession
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public LiveViewTriggerReason TriggerReason { get; set; }   // Manual, JammingSuspected, JammingConfirmed, Calibration
    public LiveViewSessionState State { get; set; }            // Starting, Active, Sustained, Stopping, Stopped, Failed
    public DateTime StartedAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }
    public DateTime LastExtendedAtUtc { get; set; }
    public bool IsSustained { get; set; }
    public DateTime? SustainedSinceUtc { get; set; }
    public string? PromotionReason { get; set; }
    public Guid? OperatorId { get; set; }                      // null when system-triggered
    public string? StopReason { get; set; }                    // IdleTimeout, ManualStop, SafetyValve, ConnectionFailed, Demoted, CalibrationComplete
    public string? ProviderSessionRef { get; set; }             // opaque Ring dialogId/doorbotId correlation
}

public enum LiveViewTriggerReason { Manual, JammingSuspected, JammingConfirmed, Calibration }
public enum LiveViewSessionState { Starting, Active, Sustained, Stopping, Stopped, Failed }
```

### `LiveViewTelemetrySample` — `src/data/common/data.common/Entities/LiveViewTelemetrySample.cs`
```csharp
public class LiveViewTelemetrySample
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }             // FK -> LiveViewSession.Id, cascade delete (real FK here, unlike jamming's loose join-by-DeviceId — telemetry is meaningless without its parent session)
    public DateTime CapturedAtUtc { get; set; }
    public byte? FractionLost { get; set; }          // RTCPReceiverReport.ReceptionReports[].FractionLost
    public int? CumulativePacketsLost { get; set; }
    public uint? JitterTicks { get; set; }            // raw RTP timestamp units; convert to ms at read time using negotiated clock rate
    public long? BitrateBps { get; set; }             // derived from OnRtpPacketReceived byte-count over a rolling window
    public double? InterferenceScore { get; set; }    // composite 0-1ish score vs. the current time-bucket baseline (see Interference Scoring below); null if no baseline existed yet for this sample
}
```

### `CameraBitrateBaseline` — `src/data/common/data.common/Entities/CameraBitrateBaseline.cs`
The comparison point for gauging interference: what "normal" looks like for this camera at this time of day/week.
```csharp
public class CameraBitrateBaseline
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public int HourOfDay { get; set; }               // 0-23
    public bool IsWeekend { get; set; }
    public int SampleCount { get; set; }
    public long MedianBitrateBps { get; set; }
    public double StdDevBitrateBps { get; set; }
    public double MedianFractionLost { get; set; }
    public double MedianJitterTicks { get; set; }
    public DateTime LastRecomputedAtUtc { get; set; }
}
```
48 buckets per camera (24 hours × weekday/weekend). Recomputed incrementally as new telemetry (from calibration *or* organic sessions) lands in that bucket.

### Contracts
- `src/data/common/data.common/Contracts/ILiveViewSessionRepository.cs` — `UpsertSessionAsync`, `GetByIdAsync`, `GetActiveForDeviceAsync` (State in Starting/Active/Sustained), `ListActiveAsync` (for the idle reaper), `ListAsync(deviceId?, fromUtc?, toUtc?, ct)`.
- `src/data/common/data.common/Contracts/ILiveViewTelemetryRepository.cs` — `AddSampleAsync` (append-only), `GetSamplesAsync(sessionId, ct)`, `PruneOlderThanAsync(age, ct)`.
- `src/data/common/data.common/Contracts/ICameraBitrateBaselineRepository.cs` — `GetBucketAsync(deviceId, hourOfDay, isWeekend, ct)`, `GetDeviceGlobalAsync(deviceId, ct)` (fallback average across all buckets, for cold-start), `RecomputeBucketAsync(deviceId, hourOfDay, isWeekend, ct)` (same "aggregate from detail rows" shape as `JammingRepository.RecomputeStatsAsync`), `ListBucketsNeedingCalibrationAsync(minSamples, staleness, ct)` (drives the calibration job).

### EF Configurations, Repositories, Migration
Follow `JammingIncidentRecordConfiguration.cs` / `JammingRepository.cs` exactly (see that file for the concrete pattern: `IDbContextFactory<VideoForensicsDbContext>`, `await using db = await _factory.CreateDbContextAsync(ct)` per call, manual upsert-by-Id). `LiveViewTelemetrySampleConfiguration` gets a real FK + `OnDelete(DeleteBehavior.Cascade)` to `LiveViewSession`, plus `HasIndex(SessionId, CapturedAtUtc)`. `CameraBitrateBaselineConfiguration` gets `HasIndex(DeviceId, HourOfDay, IsWeekend)` (unique) for the bucket lookup.

Add `DbSet<LiveViewSession>`, `DbSet<LiveViewTelemetrySample>`, `DbSet<CameraBitrateBaseline>` to `VideoForensicsDbContext`. New migration `AddLiveViewSessionsAndBitrateBaseline` via `dotnet ef migrations add`, mirroring the explicit-SQLite-column-types style in `20260911174830_InitialCreate.cs`. Register the three new repositories in `src/data/database/data.database/DependencyInjection/ServiceCollectionExtensions.cs` next to `IJammingRepository`.

## Interference Scoring (the comparison mechanism)

New pure-logic helper — `src/core/forensics/Implementations/LiveViewInterferenceScorer.cs` (interface `ILiveViewInterferenceScorer` in `src/core/forensics/Interfaces/`, matching `ISignalAnomalyDetector`'s home):
```csharp
public interface ILiveViewInterferenceScorer
{
    /// Compares a live telemetry sample against the camera's time-bucketed baseline.
    /// Returns null if no baseline exists yet for this bucket AND no device-global fallback exists either
    /// (brand new camera, no calibration data at all) - caller should not treat null as "no interference."
    double? ScoreSample(LiveViewTelemetrySample sample, CameraBitrateBaseline? bucketBaseline, CameraBitrateBaseline? deviceGlobalFallback);
}
```
Composite score (0-1ish, clamped) from three z-score-like deviations, weighted:
- Bitrate collapse: `(baseline.MedianBitrateBps - sample.BitrateBps) / baseline.StdDevBitrateBps` (positive = degraded)
- Packet loss vs. baseline median `FractionLost`
- Jitter vs. baseline median `JitterTicks`

Cold-start fallback order: bucket baseline (`SampleCount >= MinCalibrationSamplesPerBucket`) → device-global baseline (avg across all buckets) → `null` (skip scoring, log at debug; the RSSI-based jamming path in Phase 4 still works independently of bitrate scoring).

This score is what feeds `LiveViewTelemetrySample.InterferenceScore` and drives auto-promotion to sustained mode in the orchestrator (Phase 3).

## Phase 2 — Ring RTCP/bitrate telemetry capture

Modify `src/providers/ring/core/Streaming/RingLiveViewSession.cs`:
- Subscribe to `_pc.OnReceiveReport` (confirmed present on SIPSorcery 10.0.16's `RTCPeerConnection`) alongside existing subscriptions in the constructor. Extract `RTCPReceiverReport.ReceptionReports` (guard nulls — audio/video are separate `SDPMediaTypesEnum` streams). Expose via a new lightweight record `RTCPReceiverReportSample(SDPMediaTypesEnum MediaType, byte FractionLost, int PacketsLost, uint Jitter, DateTime ReceivedAtUtc)` and event `event Action<RTCPReceiverReportSample> OnReceiverReport`.
- Add a self-contained rolling-bitrate tracker (internal `System.Threading.Timer`, 1-2s interval, started in constructor, disposed in `Dispose()`/`CloseAsync()`) summing byte counts from the existing `OnRtpPacketReceived` event over a rolling window, exposed as `event Action<long> OnBitrateSampleBps`.

Tests (`RingLiveViewSessionTests`, extend existing test file if present):
- `OnReceiveReport_FiresWithReceptionReports_RaisesOnReceiverReportEvent`
- `OnReceiveReport_NullReceptionReports_DoesNotThrow`
- `BitrateTracking_RtpPacketsReceived_RaisesOnBitrateSampleBpsPeriodically`
- `Dispose_StopsInternalBitrateTimer`

## Phase 3 — `ILiveViewSessionService` orchestration core

New optional-capability interface — `src/core/providers/providers-common/Contracts/ILiveViewCapableProvider.cs`:
```csharp
public interface ILiveViewCapableProvider
{
    Task<ILiveViewConnection> StartLiveViewAsync(string providerDeviceId, CancellationToken ct);
}

public interface ILiveViewConnection : IAsyncDisposable
{
    event Action<RTCPReceiverReportSampleDto>? OnReceiverReport;   // provider-agnostic DTO, no SIPSorcery types leak above providers/ring
    event Action<long>? OnBitrateSampleBps;
    event Action<LiveViewConnectionStateDto>? OnConnectionStateChange;
    Task CloseAsync(CancellationToken ct);
}
```
Ring adapter: `src/providers/ring/provider/Services/RingLiveViewConnection.cs` (wraps `RingLiveViewSession`, translates events to the DTOs above) and `src/providers/ring/provider/Services/RingLiveViewProvider.cs` implementing `ILiveViewCapableProvider`, following `RingMediaDownloadService`'s pattern of pulling `Session` via `ISessionProvider.GetSession()` rather than holding it directly.

No-op stubs for every other provider (per the decision above) — `src/providers/wyze/provider/Services/WyzeLiveViewProvider.cs` etc., each implementing `ILiveViewCapableProvider.StartLiveViewAsync` to throw a clear `NotSupportedException`/return a "not supported" result type, registered in DI alongside the real Ring one so every provider bundle has a consistent entry.

New orchestration interface — `src/client/common/VideoForensics.Client.Common/Contracts/ILiveViewSessionService.cs` (client/common so it's consumable both server-side directly and via `Remote*` on MAUI, same layering as `IReportGenerationService`):
```csharp
public interface ILiveViewSessionService
{
    Task<LiveViewSession> StartAsync(Guid deviceId, LiveViewTriggerReason reason, Guid? operatorId, CancellationToken ct);
    Task ExtendAsync(Guid sessionId, CancellationToken ct);
    Task<LiveViewSession> PromoteToSustainedAsync(Guid sessionId, string reason, CancellationToken ct);
    Task<LiveViewSession> DemoteFromSustainedAsync(Guid sessionId, CancellationToken ct);
    Task StopAsync(Guid sessionId, string stopReason, CancellationToken ct);
    Task<LiveViewSession?> GetActiveSessionAsync(Guid deviceId, CancellationToken ct);
}
```

Implementation — `src/client/core/VideoForensics.Client.Core/Tools/LiveViewSessionOrchestrator.cs` (server-tier only; owns the real `ILiveViewConnection` instances in memory, so it must be registered `AddSingleton`, not scoped — a MAUI client only ever talks to it through `Remote*`):
- Constructor: `ILiveViewSessionRepository`, `ILiveViewTelemetryRepository`, `ICameraBitrateBaselineRepository`, `ILiveViewInterferenceScorer`, `IEnumerable<ILiveViewCapableProvider>` (keyed lookup by provider name), `IDeviceRepository`, `IForensicsConfiguration`, `IProviderApiBudgetGuard`, `INotificationDispatcher?`, `ILogger`.
- In-memory `ConcurrentDictionary<Guid, ActiveLiveViewHandle>` mapping `LiveViewSession.Id` → `{ ILiveViewConnection, telemetry timer }`.
- `StartAsync`: budget-guard check, resolve provider by device's provider name, call `StartLiveViewAsync`, persist `LiveViewSession` (Starting → Active once connected), wire `OnReceiverReport`/`OnBitrateSampleBps` into a periodic sample writer (interval from config) that: builds a `LiveViewTelemetrySample`, looks up the current hour/weekend bucket's `CameraBitrateBaseline` (+ device-global fallback), calls `ILiveViewInterferenceScorer.ScoreSample`, persists the sample with its score, and feeds a rolling accumulator for auto-promote.
- Auto-promote: if not already sustained and `InterferenceScore` crosses `LiveViewAutoPromoteInterferenceScoreThreshold` for `LiveViewAutoPromoteConsecutiveSamples` samples in a row, calls `PromoteToSustainedAsync` internally with reason "Sustained bitrate/RTCP degradation vs. baseline during live view."
- Calibration sessions (`TriggerReason.Calibration`): fixed short duration (`CalibrationSessionDurationSeconds`), auto-`StopAsync(..., "CalibrationComplete")` on a timer, no notification dispatch, and after stopping triggers `ICameraBitrateBaselineRepository.RecomputeBucketAsync` for the bucket that was sampled.
- `PromoteToSustainedAsync`/`DemoteFromSustainedAsync`/`StopAsync`: straightforward state transitions + persistence, dispatching `NotificationEvent`s (`"LiveViewSustainedModeEntered"`, etc.) via `INotificationDispatcher` with the same isolated try/catch pattern `JammingToolsOrchestrator` already uses.

Tests (`LiveViewSessionOrchestratorTests`, Moq every dependency):
- `StartAsync_NoActiveSession_CreatesAndPersistsStartingSession`
- `StartAsync_ExistingActiveSession_ReturnsExistingWithoutStartingSecondConnection`
- `StartAsync_BudgetExceeded_DoesNotCallProvider`
- `StartAsync_NoProviderRegisteredForDevice_ReturnsNotSupported`
- `ExtendAsync_ActiveSession_UpdatesLastExtendedAtUtc`
- `PromoteToSustainedAsync_ActiveSession_SetsIsSustainedAndDispatchesNotification`
- `PromoteToSustainedAsync_NotificationDispatchThrows_StillPromotes`
- `StopAsync_ActiveSession_ClosesConnectionAndPersistsStoppedState`
- `StopAsync_AlreadyStopped_IsIdempotent`
- `TelemetryCapture_ReceiverReportEvent_PersistsSampleWithInterferenceScore`
- `TelemetryCapture_NoBaselineForBucketOrDevice_PersistsSampleWithNullScore`
- `AutoPromote_SustainedDegradationAcrossThreshold_PromotesToSustained`
- `AutoPromote_TransientSingleBadSample_DoesNotPromote`
- `CalibrationSession_CompletesAfterDuration_RecomputesBucketBaseline`
- `GetActiveSessionAsync_DeviceWithNoSession_ReturnsNull`

`LiveViewInterferenceScorerTests` (pure logic, no mocks needed):
- `ScoreSample_BitrateAtBaseline_ReturnsLowScore`
- `ScoreSample_BitrateCollapsed_ReturnsHighScore`
- `ScoreSample_NoBucketBaseline_FallsBackToDeviceGlobal`
- `ScoreSample_NoBaselineAtAll_ReturnsNull`
- `ScoreSample_HighLossAndJitterLowBitrate_CombinesIntoHigherScoreThanAnyOneFactor`

## Phase 4 — Idle-timeout reaper, tiered polling, and bitrate calibration (background services)

### `LiveViewIdleTimeoutService` — `src/client/host/VideoForensics.Hosting/BackgroundServices/LiveViewIdleTimeoutService.cs`
`BackgroundService`, short `PeriodicTimer` (`LiveViewIdleTimeoutCheckIntervalSeconds`, e.g. 30s). Each tick: `ListActiveAsync` (Active-state only — Sustained is exempt from idle timeout), stop any session past `LiveViewIdleTimeoutMinutes` since `LastExtendedAtUtc`. Separately, for Sustained sessions, enforce the safety valve: if `SustainedModeMaxDurationMinutes > 0` and elapsed time since `SustainedSinceUtc` exceeds it, `StopAsync(..., "SafetyValve")` and dispatch an urgent notification. Registered via `AddHostedService<LiveViewIdleTimeoutService>()`, server-tier hosts only (same rule as `DeviceHealthSyncService`).

### Tiered polling — modify `DeviceHealthSyncService.cs`
New collaborator `src/client/host/VideoForensics.Hosting/BackgroundServices/ElevatedPollingWindowTracker.cs` — in-memory per-process `ConcurrentDictionary<Guid, DateTime>` tracking which devices are in an elevated-polling window (`EnterElevated`, `IsElevated`, `Clear`, `ElevatedDeviceIds`). Registered `AddSingleton`.

`DeviceHealthSyncService` changes:
- On each base 15-min tick, after persisting a `DeviceHealth` row, run a cheap inline check: compare the new reading against a rolling per-device RSSI baseline (small recent-history median, same threshold `DegradationThresholdDb` already used in `JammingToolsOrchestrator`). A soft hit → `ElevatedPollingWindowTracker.EnterElevated(deviceId, ElevatedPollingWindowMinutes)`.
- A second, faster `PeriodicTimer` (`ElevatedPollingIntervalSeconds`, 30-60s) re-polls (still through `IProviderHealthSource.FetchHealthAsync`, still budget-guarded) and, for devices currently elevated, runs the existing `JammingToolsOrchestrator.AnalyzeJammingAsync` (reused, not reimplemented).
- Add `public int NewlyDetectedCount { get; set; }` to `JammingAnalysisReport` (in `JammingToolsOrchestrator.cs`) — small additive change so the caller can detect "a new incident was confirmed this run" without parsing the `Message` string.
- If `NewlyDetectedCount > 0`: call `ILiveViewSessionService.StartAsync(deviceId, JammingConfirmed, null, ct)` if no active session exists for that device, else `PromoteToSustainedAsync`.
- Window elapses without confirmation → `Clear(deviceId)`, reverts to base-interval-only polling.

### Bitrate calibration — new `src/client/host/VideoForensics.Hosting/BackgroundServices/CameraBitrateCalibrationService.cs`
`BackgroundService`, `PeriodicTimer` at `CalibrationCheckIntervalMinutes` (e.g. 15 min). Each tick: for the *current* hour/weekend bucket, call `ICameraBitrateBaselineRepository.ListBucketsNeedingCalibrationAsync(MinCalibrationSamplesPerBucket, CalibrationStalenessDays, ct)` to find devices whose current-hour bucket is under-sampled or stale. For each: skip if that device already has an active `LiveViewSession` (never interrupt a real manual/jamming session for calibration); otherwise `ILiveViewSessionService.StartAsync(deviceId, Calibration, null, ct)`, which self-terminates after `CalibrationSessionDurationSeconds` per the orchestrator logic in Phase 3. Gated by `EnableBitrateCalibration` (default true) since it's extra provider API usage some deployments may want off. Registered `AddHostedService<CameraBitrateCalibrationService>()`, server-tier only, budget-guarded like every other provider-calling background service.

Tests:
- `DeviceHealthSyncServiceTests`: `RunOneTickAsync_SingleDegradedReading_EntersElevatedPollingWindow`, `RunOneTickAsync_ReadingWithinBaseline_DoesNotEnterElevatedWindow`, `RunElevatedTickAsync_DeviceInElevatedWindow_PollsAndRunsJammingAnalysis`, `RunElevatedTickAsync_JammingConfirmed_NoActiveSession_StartsSession`, `RunElevatedTickAsync_JammingConfirmed_ActiveSessionExists_PromotesToSustained`, `RunElevatedTickAsync_WindowElapsedWithoutConfirmation_RevertsToBaseInterval`, `RunElevatedTickAsync_BudgetExceeded_SkipsPollWithoutCallingProvider`.
- `LiveViewIdleTimeoutServiceTests`: `RunOneTickAsync_ActiveSessionPastIdleTimeout_StopsSession`, `RunOneTickAsync_ActiveSessionWithinTimeout_DoesNotStop`, `RunOneTickAsync_SustainedSession_IgnoredByIdleCheck`, `RunOneTickAsync_SustainedSessionPastSafetyValve_StopsWithSafetyValveReason`, `RunOneTickAsync_SafetyValveDisabledZero_NeverStopsSustainedSession`.
- `ElevatedPollingWindowTrackerTests`: `EnterElevated_ThenIsElevated_ReturnsTrueWithinWindow`, `IsElevated_AfterWindowExpires_ReturnsFalse`, `Clear_RemovesDeviceFromElevatedSet`.
- `CameraBitrateCalibrationServiceTests`: `RunOneTickAsync_UndersampledBucket_StartsCalibrationSession`, `RunOneTickAsync_BucketAlreadySufficient_SkipsDevice`, `RunOneTickAsync_DeviceHasActiveSession_SkipsCalibration`, `RunOneTickAsync_CalibrationDisabled_DoesNothing`, `RunOneTickAsync_BudgetExceeded_SkipsWithoutCallingProvider`.

## Phase 5 — Config knobs

`IForensicsConfiguration`/`ForensicsConfiguration` (DB-persisted, user-toggleable — add alongside `EnableHealthSync`, round-trip through `ForensicsConfigurationService.LoadFromDatabaseAsync`, `ConfigDtos.cs`, and the config API layer that already exposes `EnableHealthSync` — locate the exact endpoint/orchestrator via Grep before implementing):
```
bool EnableLiveView { get; set; }                          // default true
int LiveViewIdleTimeoutMinutes { get; set; }                // default 5
int LiveViewTelemetrySampleIntervalSeconds { get; set; }    // default 2
int SustainedModeMaxDurationMinutes { get; set; }           // default 240; 0 = unlimited
int LiveViewTelemetryRetentionDays { get; set; }            // default 30
int ElevatedPollingWindowMinutes { get; set; }              // default 10
int ElevatedPollingIntervalSeconds { get; set; }             // default 45
bool EnableBitrateCalibration { get; set; }                  // default true
int CalibrationCheckIntervalMinutes { get; set; }            // default 15
int CalibrationSessionDurationSeconds { get; set; }          // default 12
int MinCalibrationSamplesPerBucket { get; set; }              // default 5
int CalibrationStalenessDays { get; set; }                    // default 14 (recalibrate even a "full" bucket after this long - environment changes)
```

`ForensicsOptions` (IOptions-bound, algorithm tuning, not user-facing):
```
public double LiveViewAutoPromoteInterferenceScoreThreshold { get; set; } = 0.6;
public int LiveViewAutoPromoteConsecutiveSamples { get; set; } = 3;
```

## Phase 6 — DI wiring (`VideoForensicsHostingExtensions.cs`)

- `AddScoped<ILiveViewCapableProvider, RingLiveViewProvider>()` (match whatever registration shape `RingHealthSource`/`IProviderHealthSource` uses — check before writing, may need `AddKeyedScoped` for multi-account).
- No-op stub registrations for every other provider's `ILiveViewCapableProvider`.
- `AddSingleton<ILiveViewSessionService, LiveViewSessionOrchestrator>()` — singleton (owns in-memory connections), server-tier only.
- `AddSingleton<ElevatedPollingWindowTracker>()`.
- `AddHostedService<LiveViewIdleTimeoutService>()`, `AddHostedService<CameraBitrateCalibrationService>()`.
- MAUI/client-tier: `AddHttpClient<ILiveViewSessionService, RemoteLiveViewSessionService>(c => c.BaseAddress = serverAddress)`, mirroring `IReportGenerationService`'s registration.

## Phase 7 — API layer

DTOs — `src/core/api/VideoForensics.Api.Contracts/LiveViewDtos.cs`: `LiveViewSessionDto`, `LiveViewTelemetrySampleDto`, `CameraBitrateBaselineDto`, `StartLiveViewRequest(Guid DeviceId)`, `ToDto()`/`ToDomain()` mappers following `ReportDtos.cs`.

Endpoints — `src/client/web/VideoForensics.WebApp/Api/LiveViewEndpoints.cs`, `MapGroup("/api/v1/liveview").RequireAuthorization()`:
```
POST /{deviceId}/start          -> StartAsync(Manual)   [no step-up, per decision]
POST /{sessionId}/extend        -> ExtendAsync
POST /{sessionId}/promote       -> PromoteToSustainedAsync (manual operator override)
POST /{sessionId}/demote        -> DemoteFromSustainedAsync
POST /{sessionId}/stop          -> StopAsync
GET  /device/{deviceId}/active  -> GetActiveSessionAsync
GET  /{sessionId}/telemetry     -> ILiveViewTelemetryRepository.GetSamplesAsync
GET  /device/{deviceId}/baseline -> ICameraBitrateBaselineRepository (all 48 buckets, for a UI baseline chart)
```
Same try/catch → `Results.BadRequest` pattern and `.RequireRateLimiting("media")` as `ReportEndpoints.cs`.

Remote client — `src/client/host/VideoForensics.Hosting/Remote/RemoteLiveViewSessionService.cs`, implementing `ILiveViewSessionService`, same `GetAsync`/`PostAsJsonAsync` + `EnsureSuccessStatusCode()` + `ReadFromJsonAsync<TDto>` + `.ToDomain()` shape as `RemoteReportGenerationService.cs`.

**Open scoping note carried from research**: these endpoints manage session lifecycle/telemetry only, not the actual browser-side WebRTC video rendering (a separate SDP/ICE relay to a viewer's browser would be a larger follow-up design). Confirm before Phase 8 whether v1 needs an embedded video viewer or just session state + telemetry/interference charts.

Tests: `LiveViewEndpointsTests` (mirror `ReportEndpointsTests.cs`'s integration-test setup — one per route, success/not-found/validation-failure), `RemoteLiveViewSessionServiceTests` (mirror `RemoteReportGenerationServiceTests.cs`'s HTTP-mocking approach).

## Phase 8 — UI and MCP

- `src/client/ui/VideoForensics.Ui.Shared/Pages/LiveView.razor` — follows `JammingAnalysis.razor`'s shape: `PageRightPanelContent` with camera picker + Start/Extend/Stop/Promote controls, main area showing session state, elapsed time, and a live-updating telemetry/interference-score strip (bitrate vs. baseline, loss %, jitter) via `SfGrid`/chart, matching existing Syncfusion usage. No embedded video player unless the open scoping note above is resolved otherwise.
- `src/client/web/VideoForensics.WebApp/Mcp/Tools/LiveViewTools.cs` — `[McpServerToolType]` extending `ForensicsToolBase`, mirrors `JammingTools.cs`: `StartLiveView`, `StopLiveView`, `ExtendLiveView`, `PromoteLiveViewToSustained`, `GetActiveLiveViewSession`, `GetLiveViewTelemetry`, `GetCameraBitrateBaseline`.

Tests: `LiveViewToolsTests` (Moq `ILiveViewSessionService`, one per tool method), Blazor component tests for `LiveView.razor` if `JammingAnalysis.razor` has a bUnit test file to mirror (check via Glob first).

## Suggested dispatch order (TDD-first, one file/service per Haiku subagent, per this repo's workflow)

1. `LiveViewSession` / `LiveViewTelemetrySample` / `CameraBitrateBaseline` entities + EF configs + migration.
2. `ILiveViewSessionRepository` / `ILiveViewTelemetryRepository` / `ICameraBitrateBaselineRepository` + impls + tests (pure data layer, depends only on step 1).
3. `RingLiveViewSession` RTCP/bitrate additions + tests (independent, parallel-dispatchable with step 2).
4. `ILiveViewInterferenceScorer` + tests (pure logic, independent, parallel-dispatchable).
5. `ILiveViewCapableProvider`/`ILiveViewConnection` contracts + `RingLiveViewProvider`/`RingLiveViewConnection` + no-op stubs for other providers (depends on 3).
6. `ILiveViewSessionService` contract + `LiveViewSessionOrchestrator` + tests (depends on 2, 4, 5).
7. Config knobs (Phase 5) — alongside step 6.
8. `JammingAnalysisReport.NewlyDetectedCount` + `ElevatedPollingWindowTracker` + `DeviceHealthSyncService` tiered-polling changes + `LiveViewIdleTimeoutService` + `CameraBitrateCalibrationService` + tests (depends on 6).
9. DI wiring (Phase 6) — depends on everything above compiling.
10. DTOs + endpoints + Remote client + tests (Phase 7) — depends on 6, 7, 9.
11. UI + MCP tools + tests (Phase 8) — depends on 10.

## Verification

After each phase's lite gate (`dotnet build` on touched `.csproj`s + `dotnet test` on their sibling `tests/` projects), an end-to-end check once Phase 8 lands:
1. Manually trigger `POST /api/v1/liveview/{deviceId}/start` (or the MCP tool) against a real or test-double Ring session and confirm a `LiveViewSession` row appears `Active`, telemetry samples accumulate, and `GetVideoStream`-style RTP flow is visible in logs.
2. Let a session sit idle past `LiveViewIdleTimeoutMinutes` and confirm `LiveViewIdleTimeoutService` stops it.
3. Force a jamming-confirmed condition (seed degraded `DeviceHealth` rows in the test DB) and confirm the elevated-polling path in `DeviceHealthSyncService` starts/promotes a live view automatically, with a `LiveViewSustainedModeEntered` notification dispatched.
4. Confirm `CameraBitrateCalibrationService` fills in `CameraBitrateBaseline` buckets over a few ticks and that `InterferenceScore` on subsequent telemetry samples reflects deviation from those buckets, not a flat threshold.
5. Full gate (`dotnet clean`+`dotnet build`, `dotnet test` solution-wide) before opening the PR, per this repo's standing rule — ask the user to confirm before running it.
