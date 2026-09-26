using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Providers.Common.Contracts;

namespace VideoForensics.Hosting.BackgroundServices
{
    /// <summary>
    /// Periodically checks for idle or overly-long-sustained live-view sessions and stops them.
    ///
    /// For Active sessions: if LastExtendedAtUtc exceeds the configured idle timeout,
    /// stops the session with reason "IdleTimeout".
    ///
    /// For Sustained sessions: if configured with a positive SustainedModeMaxDurationMinutes and
    /// the session has been sustained for longer than that duration, stops it with reason "SafetyValve"
    /// and dispatches a NotificationEvent for audit purposes.
    ///
    /// One session's stop failure does not block the next session's processing (per-item isolation,
    /// matching DeviceHealthSyncService's pattern).
    /// </summary>
    public class LiveViewIdleTimeoutService : BackgroundService
    {
        private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(30);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IForensicsConfiguration _config;
        private readonly ILogger<LiveViewIdleTimeoutService> _logger;

        public LiveViewIdleTimeoutService(
            IServiceScopeFactory scopeFactory,
            IForensicsConfiguration config,
            ILogger<LiveViewIdleTimeoutService> logger)
        {
            _scopeFactory = scopeFactory;
            _config = config;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(CheckInterval);

            do
            {
                await RunOneTickAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }

        /// <summary>Internal for direct testability (VideoForensics.Hosting.Tests) without driving the whole BackgroundService lifecycle/timer.</summary>
        internal async Task RunOneTickAsync(CancellationToken ct)
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            IServiceProvider sp = scope.ServiceProvider;

            ILiveViewSessionRepository sessionRepository = sp.GetRequiredService<ILiveViewSessionRepository>();
            ILiveViewSessionService sessionService = sp.GetRequiredService<ILiveViewSessionService>();
            INotificationDispatcher? notificationDispatcher = sp.GetService<INotificationDispatcher>();

            IReadOnlyList<LiveViewSession> activeSessions;
            try
            {
                activeSessions = await sessionRepository.ListActiveAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Live view idle timeout tick: failed to list active sessions; skipping this tick");
                return;
            }

            if (activeSessions.Count == 0)
            {
                return;
            }

            foreach (LiveViewSession session in activeSessions)
            {
                try
                {
                    // Check idle timeout for Active (non-Sustained) sessions
                    if (session.State == LiveViewSessionState.Active && !session.IsSustained)
                    {
                        if (DateTime.UtcNow - session.LastExtendedAtUtc > TimeSpan.FromMinutes(_config.LiveViewIdleTimeoutMinutes))
                        {
                            await sessionService.StopAsync(session.Id, "IdleTimeout", ct);
                            _logger.LogInformation(
                                "Live view idle timeout tick: stopped active session {SessionId} after exceeding {IdleTimeoutMinutes} minute idle timeout",
                                session.Id, _config.LiveViewIdleTimeoutMinutes);
                            continue;
                        }
                    }

                    // Check safety valve for Sustained sessions
                    if (session.State == LiveViewSessionState.Sustained && session.IsSustained && session.SustainedSinceUtc.HasValue)
                    {
                        if (_config.SustainedModeMaxDurationMinutes > 0 &&
                            DateTime.UtcNow - session.SustainedSinceUtc.Value > TimeSpan.FromMinutes(_config.SustainedModeMaxDurationMinutes))
                        {
                            await sessionService.StopAsync(session.Id, "SafetyValve", ct);
                            _logger.LogInformation(
                                "Live view safety valve tick: stopped sustained session {SessionId} after exceeding {MaxDurationMinutes} minute safety valve",
                                session.Id, _config.SustainedModeMaxDurationMinutes);

                            // Dispatch a notification for audit purposes
                            if (notificationDispatcher != null)
                            {
                                try
                                {
                                    var notificationEvent = new NotificationEvent(
                                        EventType: "LiveViewSessionSafetyValveTriggered",
                                        TimestampUtc: DateTime.UtcNow,
                                        OperatorId: session.OperatorId,
                                        PairedDeviceId: session.DeviceId,
                                        SourceIp: null,
                                        Details: $"Sustained live view session {session.Id} force-closed after exceeding the {_config.SustainedModeMaxDurationMinutes} minute safety valve",
                                        Audience: NotificationAudience.AdminsOnly,
                                        Severity: NoticeSeverity.Alert);

                                    await notificationDispatcher.DispatchAsync(notificationEvent, ct);
                                }
                                catch (Exception ex)
                                {
                                    // One notification failure must not stop processing other sessions.
                                    _logger.LogWarning(ex, "Live view safety valve tick: failed to dispatch notification for session {SessionId} (non-critical)", session.Id);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // One session's failure must not stop the next session's processing.
                    _logger.LogWarning(ex, "Live view idle timeout tick: failed to process session {SessionId} (non-critical)", session.Id);
                }
            }
        }
    }
}
