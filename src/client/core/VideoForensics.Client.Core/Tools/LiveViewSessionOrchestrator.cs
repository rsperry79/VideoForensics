using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
    /// </summary>
    public class LiveViewSessionOrchestrator : ILiveViewSessionService
    {
        private readonly ILogger<LiveViewSessionOrchestrator> _logger;
        private readonly ILiveViewSessionRepository _sessionRepository;
        private readonly ILiveViewTelemetryRepository _telemetryRepository;
        private readonly ICameraBitrateBaselineRepository _baselineRepository;
        private readonly ILiveViewInterferenceScorer _interferenceScorer;
        private readonly IServiceProvider _serviceProvider;
        private readonly IDeviceRepository _deviceRepository;
        private readonly IForensicsConfiguration _config;
        private readonly IOptions<ForensicsOptions> _options;
        private readonly IProviderApiBudgetGuard _budgetGuard;
        private readonly INotificationDispatcher? _notificationDispatcher;

        /// <summary>
        /// In-memory map of active sessions to their live provider connections and state.
        /// Key: LiveViewSession.Id
        /// Value: handle containing the connection, telemetry state, and cleanup resources.
        /// </summary>
        private readonly ConcurrentDictionary<Guid, ActiveLiveViewHandle> _activeSessions =
            new();

        /// <summary>
        /// Internal record holding live connection state and cleanup resources for a session.
        /// </summary>
        private record ActiveLiveViewHandle(
            ILiveViewConnection Connection,
            IAsyncDisposable? TelemetryTimer);

        public LiveViewSessionOrchestrator(
            ILogger<LiveViewSessionOrchestrator> logger,
            ILiveViewSessionRepository sessionRepository,
            ILiveViewTelemetryRepository telemetryRepository,
            ICameraBitrateBaselineRepository baselineRepository,
            ILiveViewInterferenceScorer interferenceScorer,
            IServiceProvider serviceProvider,
            IDeviceRepository deviceRepository,
            IForensicsConfiguration config,
            IOptions<ForensicsOptions> options,
            IProviderApiBudgetGuard budgetGuard,
            INotificationDispatcher? notificationDispatcher = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
            _telemetryRepository = telemetryRepository ?? throw new ArgumentNullException(nameof(telemetryRepository));
            _baselineRepository = baselineRepository ?? throw new ArgumentNullException(nameof(baselineRepository));
            _interferenceScorer = interferenceScorer ?? throw new ArgumentNullException(nameof(interferenceScorer));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _deviceRepository = deviceRepository ?? throw new ArgumentNullException(nameof(deviceRepository));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _budgetGuard = budgetGuard ?? throw new ArgumentNullException(nameof(budgetGuard));
            _notificationDispatcher = notificationDispatcher;
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

            // Check for existing active session (idempotent)
            var existingSession = await _sessionRepository.GetActiveForDeviceAsync(deviceId, ct);
            if (existingSession != null)
            {
                _logger.LogInformation("Returning existing active live view session {SessionId} for device {DeviceId}", existingSession.Id, deviceId);
                return existingSession;
            }

            // Resolve the device to get its provider device ID
            var device = await _deviceRepository.GetAsync(deviceId, ct)
                ?? throw new KeyNotFoundException($"Device {deviceId} not found.");

            // Determine provider name - for now, we infer from device metadata or use a convention.
            // In production, Device would have a ProviderName property, or we'd look it up via Location.
            // For this implementation, we'll try known provider names or rely on configuration.
            string providerName = "Ring"; // Default to Ring; in production this comes from Device.ProviderName

            // Budget guard: check if provider API calls are within budget
            if (!await _budgetGuard.TryConsumeAsync(providerName, ct))
            {
                throw new InvalidOperationException($"Provider API budget exceeded for {providerName}.");
            }

            // Resolve provider via keyed service resolution
            ILiveViewCapableProvider? provider = _serviceProvider.GetService(typeof(ILiveViewCapableProvider)) as ILiveViewCapableProvider;
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

            session = await _sessionRepository.UpsertSessionAsync(session, ct);
            _logger.LogInformation("Persisted live view session {SessionId} in Starting state for device {DeviceId}", session.Id, deviceId);

            // Store the connection handle in memory
            _activeSessions[session.Id] = new ActiveLiveViewHandle(connection, null);

            // Subscribe to connection state changes: Starting → Active on successful connect
            if (connection != null)
            {
                connection.OnConnectionStateChange += async (state) =>
                {
                    try
                    {
                        if (state == LiveViewConnectionStateDto.Connected)
                        {
                            session.State = LiveViewSessionState.Active;
                            await _sessionRepository.UpsertSessionAsync(session, ct);
                            _logger.LogInformation("Live view session {SessionId} transitioned to Active state", session.Id);
                        }
                        else if (state == LiveViewConnectionStateDto.Failed)
                        {
                            session.State = LiveViewSessionState.Failed;
                            session.EndedAtUtc = DateTime.UtcNow;
                            session.StopReason = "ConnectionFailed";
                            await _sessionRepository.UpsertSessionAsync(session, ct);
                            _logger.LogError("Live view session {SessionId} failed to connect", session.Id);
                            _activeSessions.TryRemove(session.Id, out _);
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
            var session = await _sessionRepository.GetByIdAsync(sessionId, ct)
                ?? throw new KeyNotFoundException($"Session {sessionId} not found.");

            session.LastExtendedAtUtc = DateTime.UtcNow;
            await _sessionRepository.UpsertSessionAsync(session, ct);
            _logger.LogInformation("Extended live view session {SessionId} timeout", sessionId);
        }

        public async Task<LiveViewSession> PromoteToSustainedAsync(Guid sessionId, string reason, CancellationToken ct)
        {
            var session = await _sessionRepository.GetByIdAsync(sessionId, ct)
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

            await _sessionRepository.UpsertSessionAsync(session, ct);
            _logger.LogInformation("Promoted live view session {SessionId} to sustained mode: {Reason}", sessionId, reason);

            // Dispatch notification (isolation pattern: catch and log, don't fail the operation)
            if (_notificationDispatcher != null)
            {
                try
                {
                    await _notificationDispatcher.DispatchAsync(
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
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to dispatch sustained mode notification for session {SessionId} (non-critical)", sessionId);
                }
            }

            return session;
        }

        public async Task<LiveViewSession> DemoteFromSustainedAsync(Guid sessionId, CancellationToken ct)
        {
            var session = await _sessionRepository.GetByIdAsync(sessionId, ct)
                ?? throw new KeyNotFoundException($"Session {sessionId} not found.");

            session.IsSustained = false;
            session.SustainedSinceUtc = null;
            session.State = LiveViewSessionState.Active;

            await _sessionRepository.UpsertSessionAsync(session, ct);
            _logger.LogInformation("Demoted live view session {SessionId} from sustained mode", sessionId);

            return session;
        }

        public async Task StopAsync(Guid sessionId, string stopReason, CancellationToken ct)
        {
            var session = await _sessionRepository.GetByIdAsync(sessionId, ct);
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
                try
                {
                    await handle.Connection.CloseAsync(ct);
                    _logger.LogInformation("Closed live view connection for session {SessionId}", sessionId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error closing connection for session {SessionId}", sessionId);
                }

                // Dispose telemetry timer if present
                if (handle.TelemetryTimer != null)
                {
                    try
                    {
                        await handle.TelemetryTimer.DisposeAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error disposing telemetry timer for session {SessionId}", sessionId);
                    }
                }
            }

            // Persist stopped state
            session.State = LiveViewSessionState.Stopped;
            session.EndedAtUtc = DateTime.UtcNow;
            session.StopReason = stopReason;

            await _sessionRepository.UpsertSessionAsync(session, ct);
            _logger.LogInformation("Stopped live view session {SessionId}: {StopReason}", sessionId, stopReason);
        }

        public async Task<LiveViewSession?> GetActiveSessionAsync(Guid deviceId, CancellationToken ct)
        {
            return await _sessionRepository.GetActiveForDeviceAsync(deviceId, ct);
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
