using System.Collections.Concurrent;
using System.Threading.Channels;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Forensics;
using VideoForensics.Providers.Common.Contracts;
using VideoForensics.Providers.Ring.Interfaces;

namespace VideoForensics.Client.Core.Tools
{
    /// <summary>
    /// Orchestrates live-view session lifecycle: starting, extending, promoting to sustained mode, stopping,
    /// and managing in-memory telemetry collection with interference scoring.
    ///
    /// SINGLETON - owns live provider connections in memory. Must be registered as AddSingleton so that
    /// multiple calls to StartAsync against the same device return the same in-memory connection handle.
    /// If registered as Scoped, each scope would own separate connections and state divergence.
    ///
    /// Because this instance outlives any request, it must not capture scoped services (repositories,
    /// the budget guard, the notification dispatcher, providers) in its constructor. Each operation
    /// opens its own DI scope via <see cref="IServiceScopeFactory"/> and resolves what it needs there.
    /// </summary>
    public class LiveViewSessionOrchestrator : ILiveViewSessionService
    {
        private readonly ILogger<LiveViewSessionOrchestrator> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILiveViewInterferenceScorer _interferenceScorer;
        private readonly IForensicsConfiguration _config;
        private readonly ILiveViewTelemetryPublisher _telemetryPublisher;

        /// <summary>
        /// In-memory map of active sessions to their live provider connections and state.
        /// Key: LiveViewSession.Id
        /// Value: handle containing the connection, telemetry state, and cleanup resources.
        /// </summary>
        private readonly ConcurrentDictionary<Guid, ActiveLiveViewHandle> _activeSessions =
            new();

        /// <summary>
        /// Queue depth between the provider thread and a session's telemetry consumer. Receiver reports arrive every
        /// few seconds, so this holds minutes of backlog before any drop happens.
        /// </summary>
        private const int TelemetryQueueCapacity = 256;

        /// <summary>
        /// How long StopAsync waits for already-queued telemetry to be written before it cancels the consumer.
        /// Bounded so that a stalled database can never hold up closing the provider connection.
        /// </summary>
        private static readonly TimeSpan TelemetryDrainTimeout = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Internal record holding live connection state and cleanup resources for a session.
        /// <see cref="Telemetry"/> is null only when the provider returned no connection to subscribe to.
        /// </summary>
        private record ActiveLiveViewHandle(
            ILiveViewConnection Connection,
            TelemetryPipeline? Telemetry);

        /// <summary>
        /// One reading queued for a session's consumer: a receiver report plus the bitrate sample that was
        /// pending when it arrived (null if none).
        /// </summary>
        private sealed record TelemetryReading(RTCPReceiverReportSampleDto Report, long? BitrateBps);

        /// <summary>
        /// Per-session telemetry state. The connection's event handlers are the only code that runs on the
        /// provider's thread, and they never await or do I/O: they enqueue and return.
        ///
        /// Cadence (why one row per receiver report): a receiver report is the only event that carries packet loss
        /// and jitter, so it defines a row. A bitrate sample alone would be a row with no loss or jitter, which the
        /// scorer cannot use. A bitrate sample is therefore held as "pending" and merged into the next written row,
        /// and consumed by that row so a single bitrate value is never repeated on later rows.
        ///
        /// Queue (why a bounded channel with DropWrite): the single consumer writes rows in order and never
        /// concurrently. Bounded so a stalled database cannot grow memory for the life of a session. DropWrite
        /// rather than DropOldest because TryWrite then reports each drop, so drops can be counted and logged.
        /// </summary>
        private sealed class TelemetryPipeline
        {
            private const long NoPendingBitrate = -1;

            private readonly Channel<TelemetryReading> _readings;
            private readonly ILogger _logger;
            private readonly Guid _sessionId;
            private long _pendingBitrateBps = NoPendingBitrate;
            private long _droppedCount;
            private volatile bool _completed;

            public TelemetryPipeline(ILogger logger, Guid sessionId, int capacity)
            {
                _logger = logger;
                _sessionId = sessionId;
                _readings = Channel.CreateBounded<TelemetryReading>(new BoundedChannelOptions(capacity)
                {
                    FullMode = BoundedChannelFullMode.DropWrite,
                    SingleReader = true,
                    SingleWriter = false
                });
            }

            /// <summary>The queue the consumer reads from.</summary>
            public ChannelReader<TelemetryReading> Reader => _readings.Reader;

            /// <summary>The consumer task. Set once by the orchestrator right after construction.</summary>
            public Task Consumer { get; set; } = Task.CompletedTask;

            /// <summary>Cancels in-flight writes only when the drain times out during stop.</summary>
            public CancellationTokenSource Cancellation { get; } = new();

            /// <summary>Provider-thread entry point for receiver reports. Never blocks.</summary>
            public void OnReceiverReport(RTCPReceiverReportSampleDto report)
            {
                if (_completed)
                {
                    return;
                }

                long bitrate = Interlocked.Exchange(ref _pendingBitrateBps, NoPendingBitrate);
                var reading = new TelemetryReading(report, bitrate == NoPendingBitrate ? null : bitrate);

                if (!_readings.Writer.TryWrite(reading))
                {
                    long dropped = Interlocked.Increment(ref _droppedCount);
                    _logger.LogWarning(
                        "Dropped live view telemetry sample for session {SessionId}: sampler queue full ({DroppedCount} dropped so far)",
                        _sessionId,
                        dropped);
                }
            }

            /// <summary>Provider-thread entry point for bitrate samples. Only records the value; writes no row.</summary>
            public void OnBitrateSampleBps(long bitsPerSecond)
            {
                Interlocked.Exchange(ref _pendingBitrateBps, Math.Max(0, bitsPerSecond));
            }

            /// <summary>Stops accepting new readings; the consumer still drains what is already queued.</summary>
            public void Complete()
            {
                _completed = true;
                _ = _readings.Writer.TryComplete();
            }
        }

        /// <summary>
        /// Converts an RTCP fraction-lost value (an 8-bit fixed-point fraction of 256) to a percentage 0-100,
        /// which is the unit the interference scorer expects.
        /// </summary>
        private static byte ToPercentFractionLost(byte fractionLost)
        {
            return (byte)Math.Round(fractionLost * 100.0 / 256.0);
        }

        public LiveViewSessionOrchestrator(
            ILogger<LiveViewSessionOrchestrator> logger,
            IServiceScopeFactory scopeFactory,
            ILiveViewInterferenceScorer interferenceScorer,
            IForensicsConfiguration config,
            ILiveViewTelemetryPublisher telemetryPublisher)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _interferenceScorer = interferenceScorer ?? throw new ArgumentNullException(nameof(interferenceScorer));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _telemetryPublisher = telemetryPublisher ?? throw new ArgumentNullException(nameof(telemetryPublisher));
        }

        /// <summary>
        /// Publishes a session state change. Failures are logged and swallowed: a broken real-time transport
        /// must never fail or roll back a lifecycle operation that has already been persisted.
        /// </summary>
        private async Task PublishSessionChangedSafelyAsync(LiveViewSession session, CancellationToken ct)
        {
            try
            {
                await _telemetryPublisher.PublishSessionChangedAsync(session, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish state change for live view session {SessionId} (non-critical)", session.Id);
            }
        }

        /// <summary>
        /// Subscribes the sampler to a connection's receiver reports and bitrate samples, and starts the single
        /// consumer that persists and publishes them for this session. The handlers run on the provider's thread
        /// and only enqueue. The consumer uses its own token, not the request token, because it outlives StartAsync.
        /// </summary>
        private TelemetryPipeline StartTelemetryPipeline(ILiveViewConnection connection, Guid sessionId, Guid deviceId)
        {
            var pipeline = new TelemetryPipeline(_logger, sessionId, TelemetryQueueCapacity);
            pipeline.Consumer = ConsumeTelemetryAsync(pipeline.Reader, sessionId, deviceId, pipeline.Cancellation.Token);

            connection.OnReceiverReport += pipeline.OnReceiverReport;
            connection.OnBitrateSampleBps += pipeline.OnBitrateSampleBps;
            return pipeline;
        }

        /// <summary>
        /// Stops the sampler from receiving events and marks its queue complete. Does not wait; the consumer
        /// finishes whatever is already queued.
        /// </summary>
        private static void DetachTelemetry(ILiveViewConnection connection, TelemetryPipeline pipeline)
        {
            connection.OnReceiverReport -= pipeline.OnReceiverReport;
            connection.OnBitrateSampleBps -= pipeline.OnBitrateSampleBps;
            pipeline.Complete();
        }

        /// <summary>
        /// Detaches the sampler, then waits up to <see cref="TelemetryDrainTimeout"/> for queued rows to be written.
        /// On timeout the consumer is cancelled so no write starts after stop. The wait deliberately ignores the
        /// caller's token, so a cancelled stop still reaches the connection close that follows.
        /// </summary>
        private async Task DrainTelemetryAsync(ILiveViewConnection connection, TelemetryPipeline pipeline, Guid sessionId)
        {
            DetachTelemetry(connection, pipeline);

            try
            {
                await pipeline.Consumer.WaitAsync(TelemetryDrainTimeout, CancellationToken.None);
                _logger.LogInformation("Telemetry sampler drained for live view session {SessionId}", sessionId);
            }
            catch (TimeoutException)
            {
                _logger.LogWarning(
                    "Telemetry sampler for live view session {SessionId} did not drain within {TimeoutSeconds}s; cancelling remaining writes",
                    sessionId,
                    TelemetryDrainTimeout.TotalSeconds);
                pipeline.Cancellation.Cancel();
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Telemetry sampler for live view session {SessionId} ended with an error during stop", sessionId);
            }

            pipeline.Cancellation.Dispose();
        }

        /// <summary>
        /// The only writer for a session's telemetry: reads readings in queue order and persists them one at a time.
        /// Per-sample failures are handled inside <see cref="PersistTelemetrySampleAsync"/>, so this loop continues
        /// after any bad sample. Only the stop-time cancellation ends it.
        /// </summary>
        private async Task ConsumeTelemetryAsync(ChannelReader<TelemetryReading> reader, Guid sessionId, Guid deviceId, CancellationToken ct)
        {
            try
            {
                await foreach (TelemetryReading reading in reader.ReadAllAsync(ct))
                {
                    await PersistTelemetrySampleAsync(sessionId, deviceId, reading, ct);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                _logger.LogWarning("Telemetry sampler for live view session {SessionId} was cancelled; remaining samples discarded", sessionId);
            }
        }

        /// <summary>
        /// Builds, scores, persists, and publishes one sample in its own DI scope. Any failure is logged with the
        /// session id and swallowed, so the session and connection are left untouched. Cancellation propagates.
        /// </summary>
        private async Task PersistTelemetrySampleAsync(Guid sessionId, Guid deviceId, TelemetryReading reading, CancellationToken ct)
        {
            try
            {
                using IServiceScope scope = _scopeFactory.CreateScope();
                ILiveViewTelemetryRepository telemetryRepository = scope.ServiceProvider.GetRequiredService<ILiveViewTelemetryRepository>();

                var sample = new LiveViewTelemetrySample
                {
                    Id = Guid.NewGuid(),
                    SessionId = sessionId,
                    CapturedAtUtc = reading.Report.ReceivedAtUtc,
                    FractionLost = ToPercentFractionLost(reading.Report.FractionLost),
                    CumulativePacketsLost = reading.Report.PacketsLost,
                    JitterTicks = reading.Report.Jitter,
                    BitrateBps = reading.BitrateBps
                };

                sample.InterferenceScore = await ScoreSampleSafelyAsync(scope, deviceId, sessionId, sample, ct);

                await telemetryRepository.AddSampleAsync(sample, ct);
                await PublishSampleSafelyAsync(sessionId, sample, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist telemetry sample for live view session {SessionId} (sampler continues)", sessionId);
            }
        }

        /// <summary>
        /// Looks up the sample's hour/weekend bucket baseline (falling back to the device-global baseline when the
        /// bucket is missing) and scores it. Returns null, meaning "cannot score yet", if anything fails, so the
        /// sample is still persisted without a score.
        /// </summary>
        private async Task<double?> ScoreSampleSafelyAsync(IServiceScope scope, Guid deviceId, Guid sessionId, LiveViewTelemetrySample sample, CancellationToken ct)
        {
            try
            {
                ICameraBitrateBaselineRepository baselineRepository = scope.ServiceProvider.GetRequiredService<ICameraBitrateBaselineRepository>();

                // Buckets use the UTC hour and weekend flag, matching how CameraBitrateBaselineRepository builds them
                DateTime capturedAt = sample.CapturedAtUtc;
                bool isWeekend = capturedAt.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
                CameraBitrateBaseline? bucket = await baselineRepository.GetBucketAsync(deviceId, capturedAt.Hour, isWeekend, ct);

                // The device-global baseline is only needed when the bucket has no data
                CameraBitrateBaseline? deviceGlobal = bucket == null
                    ? await baselineRepository.GetDeviceGlobalAsync(deviceId, ct)
                    : null;

                return _interferenceScorer.ScoreSample(sample, bucket, deviceGlobal);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to score telemetry sample for live view session {SessionId}; persisting unscored (non-critical)", sessionId);
                return null;
            }
        }

        /// <summary>
        /// Publishes a persisted sample. A failure is logged and swallowed, since the row is already saved.
        /// </summary>
        private async Task PublishSampleSafelyAsync(Guid sessionId, LiveViewTelemetrySample sample, CancellationToken ct)
        {
            try
            {
                await _telemetryPublisher.PublishSampleAsync(sample, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish telemetry sample for live view session {SessionId} (non-critical)", sessionId);
            }
        }

        public async Task<LiveViewSession> StartAsync(
            Guid deviceId,
            LiveViewTriggerReason reason,
            Guid? operatorId,
            CancellationToken ct)
        {
            // Configuration check: live view must be enabled
            if (!_config.EnableLiveView)
            {
                throw new InvalidOperationException("Live view is disabled in configuration.");
            }

            using IServiceScope scope = _scopeFactory.CreateScope();
            ILiveViewSessionRepository sessionRepository = scope.ServiceProvider.GetRequiredService<ILiveViewSessionRepository>();
            IDeviceRepository deviceRepository = scope.ServiceProvider.GetRequiredService<IDeviceRepository>();
            IProviderApiBudgetGuard budgetGuard = scope.ServiceProvider.GetRequiredService<IProviderApiBudgetGuard>();

            // Check for existing active session (idempotent)
            var existingSession = await sessionRepository.GetActiveForDeviceAsync(deviceId, ct);
            if (existingSession != null)
            {
                _logger.LogInformation("Returning existing active live view session {SessionId} for device {DeviceId}", existingSession.Id, deviceId);
                return existingSession;
            }

            // Resolve the device to get its provider device ID
            var device = await deviceRepository.GetAsync(deviceId, ct)
                ?? throw new KeyNotFoundException($"Device {deviceId} not found.");

            // Determine provider name - for now, we infer from device metadata or use a convention.
            // In production, Device would have a ProviderName property, or we'd look it up via Location.
            // For this implementation, we'll try known provider names or rely on configuration.
            string providerName = "Ring"; // Default to Ring; in production this comes from Device.ProviderName

            // Budget guard: check if provider API calls are within budget
            if (!await budgetGuard.TryConsumeAsync(providerName, ct))
            {
                throw new InvalidOperationException($"Provider API budget exceeded for {providerName}.");
            }

            // Resolve provider from the same scope that resolved the repositories
            ILiveViewCapableProvider? provider = scope.ServiceProvider.GetService(typeof(ILiveViewCapableProvider)) as ILiveViewCapableProvider;
            if (provider == null)
            {
                throw new NotSupportedException($"Live view not supported for provider {providerName}. No capable provider found.");
            }

            // Start the provider connection
            _logger.LogInformation("Starting live view connection for device {DeviceId} ({ProviderDeviceId})", deviceId, device.ProviderDeviceId);
            var connection = await provider.StartLiveViewAsync(device.ProviderDeviceId, ct);

            // Create and persist the session in Starting state
            var session = new LiveViewSession
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                TriggerReason = reason,
                State = LiveViewSessionState.Starting,
                StartedAtUtc = DateTime.UtcNow,
                LastExtendedAtUtc = DateTime.UtcNow,
                OperatorId = operatorId,
                IsSustained = false
            };

            session = await sessionRepository.UpsertSessionAsync(session, ct);
            _logger.LogInformation("Persisted live view session {SessionId} in Starting state for device {DeviceId}", session.Id, deviceId);
            await PublishSessionChangedSafelyAsync(session, ct);

            // Start the telemetry sampler before storing the handle, so every handle in the map has a live consumer
            TelemetryPipeline? telemetry = connection != null
                ? StartTelemetryPipeline(connection, session.Id, deviceId)
                : null;

            // Store the connection handle in memory
            _activeSessions[session.Id] = new ActiveLiveViewHandle(connection, telemetry);

            // Subscribe to connection state changes: Starting → Active on successful connect.
            // The callback fires long after this scope is disposed, so it opens its own scope per event.
            if (connection != null)
            {
                connection.OnConnectionStateChange += async (state) =>
                {
                    try
                    {
                        using IServiceScope callbackScope = _scopeFactory.CreateScope();
                        ILiveViewSessionRepository callbackRepository = callbackScope.ServiceProvider.GetRequiredService<ILiveViewSessionRepository>();

                        if (state == LiveViewConnectionStateDto.Connected)
                        {
                            session.State = LiveViewSessionState.Active;
                            await callbackRepository.UpsertSessionAsync(session, ct);
                            _logger.LogInformation("Live view session {SessionId} transitioned to Active state", session.Id);
                        }
                        else if (state == LiveViewConnectionStateDto.Failed)
                        {
                            session.State = LiveViewSessionState.Failed;
                            session.EndedAtUtc = DateTime.UtcNow;
                            session.StopReason = "ConnectionFailed";
                            await callbackRepository.UpsertSessionAsync(session, ct);
                            _logger.LogError("Live view session {SessionId} failed to connect", session.Id);
                            if (_activeSessions.TryRemove(session.Id, out var failedHandle) && failedHandle.Telemetry != null)
                            {
                                // Stop feeding the sampler; its consumer drains anything already queued and exits.
                                DetachTelemetry(failedHandle.Connection, failedHandle.Telemetry);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error handling connection state change for session {SessionId}", session.Id);
                    }
                };
            }

            return session;
        }

        public async Task ExtendAsync(Guid sessionId, CancellationToken ct)
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            ILiveViewSessionRepository sessionRepository = scope.ServiceProvider.GetRequiredService<ILiveViewSessionRepository>();

            var session = await sessionRepository.GetByIdAsync(sessionId, ct)
                ?? throw new KeyNotFoundException($"Session {sessionId} not found.");

            session.LastExtendedAtUtc = DateTime.UtcNow;
            await sessionRepository.UpsertSessionAsync(session, ct);
            _logger.LogInformation("Extended live view session {SessionId} timeout", sessionId);
        }

        public async Task<LiveViewSession> PromoteToSustainedAsync(Guid sessionId, string reason, CancellationToken ct)
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            ILiveViewSessionRepository sessionRepository = scope.ServiceProvider.GetRequiredService<ILiveViewSessionRepository>();

            var session = await sessionRepository.GetByIdAsync(sessionId, ct)
                ?? throw new KeyNotFoundException($"Session {sessionId} not found.");

            // Idempotent: if already sustained, return as-is
            if (session.IsSustained)
            {
                _logger.LogInformation("Session {SessionId} already in sustained mode", sessionId);
                return session;
            }

            session.IsSustained = true;
            session.SustainedSinceUtc = DateTime.UtcNow;
            session.PromotionReason = reason;
            session.State = LiveViewSessionState.Sustained;

            await sessionRepository.UpsertSessionAsync(session, ct);
            _logger.LogInformation("Promoted live view session {SessionId} to sustained mode: {Reason}", sessionId, reason);
            await PublishSessionChangedSafelyAsync(session, ct);

            // Dispatch notification (isolation pattern: catch and log, don't fail the operation).
            // Resolved here rather than injected: INotificationDispatcher is scoped.
            try
            {
                var notificationDispatcher = scope.ServiceProvider.GetService<INotificationDispatcher>();
                if (notificationDispatcher != null)
                {
                    await notificationDispatcher.DispatchAsync(
                        new NotificationEvent(
                            EventType: "LiveViewSustainedModeEntered",
                            TimestampUtc: DateTime.UtcNow,
                            OperatorId: session.OperatorId,
                            PairedDeviceId: session.DeviceId,
                            SourceIp: null,
                            Details: $"Live view session {sessionId} promoted to sustained mode: {reason}",
                            Audience: NotificationAudience.AdminsOnly,
                            Severity: NoticeSeverity.Alert),
                        ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to dispatch sustained mode notification for session {SessionId} (non-critical)", sessionId);
            }

            return session;
        }

        public async Task<LiveViewSession> DemoteFromSustainedAsync(Guid sessionId, CancellationToken ct)
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            ILiveViewSessionRepository sessionRepository = scope.ServiceProvider.GetRequiredService<ILiveViewSessionRepository>();

            var session = await sessionRepository.GetByIdAsync(sessionId, ct)
                ?? throw new KeyNotFoundException($"Session {sessionId} not found.");

            session.IsSustained = false;
            session.SustainedSinceUtc = null;
            session.State = LiveViewSessionState.Active;

            await sessionRepository.UpsertSessionAsync(session, ct);
            _logger.LogInformation("Demoted live view session {SessionId} from sustained mode", sessionId);
            await PublishSessionChangedSafelyAsync(session, ct);

            return session;
        }

        public async Task StopAsync(Guid sessionId, string stopReason, CancellationToken ct)
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            ILiveViewSessionRepository sessionRepository = scope.ServiceProvider.GetRequiredService<ILiveViewSessionRepository>();

            var session = await sessionRepository.GetByIdAsync(sessionId, ct);
            if (session == null)
            {
                throw new KeyNotFoundException($"Session {sessionId} not found.");
            }

            // Idempotent: if already stopped, no-op
            if (session.State == LiveViewSessionState.Stopped)
            {
                _logger.LogInformation("Session {SessionId} already stopped", sessionId);
                return;
            }

            // Close the underlying connection if it's in memory
            if (_activeSessions.TryRemove(sessionId, out var handle))
            {
                // Drain the sampler before closing, so no sample is written after the connection is gone
                if (handle.Telemetry != null)
                {
                    await DrainTelemetryAsync(handle.Connection, handle.Telemetry, sessionId);
                }

                try
                {
                    await handle.Connection.CloseAsync(ct);
                    _logger.LogInformation("Closed live view connection for session {SessionId}", sessionId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error closing connection for session {SessionId}", sessionId);
                }
            }

            // Persist stopped state
            session.State = LiveViewSessionState.Stopped;
            session.EndedAtUtc = DateTime.UtcNow;
            session.StopReason = stopReason;

            await sessionRepository.UpsertSessionAsync(session, ct);
            _logger.LogInformation("Stopped live view session {SessionId}: {StopReason}", sessionId, stopReason);
            await PublishSessionChangedSafelyAsync(session, ct);
        }

        public async Task<LiveViewSession?> GetActiveSessionAsync(Guid deviceId, CancellationToken ct)
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            ILiveViewSessionRepository sessionRepository = scope.ServiceProvider.GetRequiredService<ILiveViewSessionRepository>();

            return await sessionRepository.GetActiveForDeviceAsync(deviceId, ct);
        }

        /// <summary>
        /// Gets the live provider connection for an active session without re-establishing it.
        /// Used by browser bridges to attach media processors or forward packets to other peers.
        /// </summary>
        /// <param name="sessionId">The session ID to look up.</param>
        /// <param name="ct">Cancellation token (unused for this in-memory lookup).</param>
        /// <returns>The active connection, or null if the session does not exist or is not in memory.</returns>
        public async Task<ILiveViewConnection?> GetConnectionForSessionAsync(Guid sessionId, CancellationToken ct)
        {
            if (_activeSessions.TryGetValue(sessionId, out var handle))
            {
                return handle.Connection;
            }

            return await Task.FromResult<ILiveViewConnection?>(null);
        }
    }
}
