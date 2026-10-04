using Microsoft.Extensions.Logging;

using VideoForensics.Providers.Common.Contracts;

namespace VideoForensics.Providers.Ring.Services
{
    public class RingEventAndConfigService : IEventAndConfigService
    {
        private readonly ILogger _logger;
        private readonly ISessionProvider _sessionProvider;

        // GetDoorbotsHistory returns the FULL account history (all devices), not just one device's.
        // Cache it so a caller looping over devices for the same date range doesn't refetch per device
        // (see the identical bug fixed in RingMediaDownloadService).
        private readonly SemaphoreSlim _historyCacheLock = new(1, 1);
        private DateTime? _cachedHistoryStart;
        private DateTime? _cachedHistoryEnd;
        private List<Entities.DoorbotHistoryEvent>? _cachedHistoryEvents;

        public RingEventAndConfigService(ILogger logger, ISessionProvider sessionProvider)
        {
            _logger = logger;
            _sessionProvider = sessionProvider ?? throw new ArgumentNullException(nameof(sessionProvider));
        }

        public async Task<IReadOnlyList<DeviceEvent>> GetEventsAsync(string deviceId, DateTime startDate, DateTime endDate, string? eventType = null, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Fetching events for device {DeviceId} from {StartDate} to {EndDate}",
                    SanitizeForLog(deviceId), startDate, endDate);

                Session? session = _sessionProvider.GetSession();
                if (session == null)
                {
                    _logger.LogError("Not authenticated: Session is null");
                    return new List<DeviceEvent>().AsReadOnly();
                }

                List<Entities.DoorbotHistoryEvent> events = await GetHistoryEventsAsync(session, startDate, endDate);

                List<DeviceEvent> deviceEvents = events?
                    .Where(e => e.Doorbot?.Id.ToString() == deviceId)
                    .Where(e => eventType == null || e.Kind == eventType)
                    .Select(e => new DeviceEvent(
                        Id: (e.Id?.ToString()) ?? "unknown",
                        DeviceId: deviceId,
                        EventType: e.Kind ?? "unknown",
                        Timestamp: e.CreatedAtDateTime ?? DateTime.MinValue,
                        SnapshotUrl: e.SnapshotUrl
                    ))
                    .ToList() ?? [];

                _logger.LogInformation("Found {EventCount} events for device {DeviceId}", deviceEvents.Count, SanitizeForLog(deviceId));
                return deviceEvents.AsReadOnly();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching events for device {DeviceId}", SanitizeForLog(deviceId));
                return new List<DeviceEvent>().AsReadOnly();
            }
        }

        public async Task<DeviceConfig?> GetDeviceConfigAsync(string deviceId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Fetching configuration for device {DeviceId}", deviceId);

                Session? session = _sessionProvider.GetSession();
                if (session == null)
                {
                    _logger.LogError("Not authenticated: Session is null");
                    return null;
                }

                if (!long.TryParse(deviceId, out long doorbotId))
                {
                    _logger.LogWarning("Invalid device ID format: {DeviceId}", deviceId);
                    return null;
                }

                List<Entities.DoorbotHistoryEvent> history = await session.GetDoorbotsHistory(doorbotId);
                return history?.FirstOrDefault() is not Entities.DoorbotHistoryEvent firstEvent
                    ? null
                    : new DeviceConfig(
                    DeviceId: deviceId,
                    MotionDetectionEnabled: true,
                    MotionSensitivity: 75,
                    RecordingMode: "motion"
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching configuration for device {DeviceId}", deviceId);
                return null;
            }
        }

        public async Task<bool> UpdateDeviceConfigAsync(string deviceId, DeviceConfig config, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Updating configuration for device {DeviceId}", deviceId);

                _logger.LogWarning("Device configuration update not fully implemented for Ring provider");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating configuration for device {DeviceId}", deviceId);
                return false;
            }
        }

        // ===== Account-Aware Overloads (Phase 1 Refactoring) =====
        // These methods enable concurrent processing of multiple Ring accounts without race conditions
        // by accepting an explicit providerAccountId and passing it to SessionProvider.GetSession(Guid)

        /// <summary>
        /// Gets events for a device in a specific provider account (account-aware overload).
        /// </summary>
        public async Task<IReadOnlyList<DeviceEvent>> GetEventsAsync(Guid providerAccountId, string deviceId, DateTime startDate, DateTime endDate, string? eventType = null, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Fetching events for device {DeviceId} from {StartDate} to {EndDate} for account {AccountId}",
                    SanitizeForLog(deviceId), startDate, endDate, providerAccountId);

                Session? session = _sessionProvider.GetSession(providerAccountId);
                if (session == null)
                {
                    _logger.LogError("Not authenticated for account {AccountId}: Session is null", providerAccountId);
                    return new List<DeviceEvent>().AsReadOnly();
                }

                List<Entities.DoorbotHistoryEvent> events = await GetHistoryEventsAsync(session, startDate, endDate);

                List<DeviceEvent> deviceEvents = events?
                    .Where(e => e.Doorbot?.Id.ToString() == deviceId)
                    .Where(e => eventType == null || e.Kind == eventType)
                    .Select(e => new DeviceEvent(
                        Id: (e.Id?.ToString()) ?? "unknown",
                        DeviceId: deviceId,
                        EventType: e.Kind ?? "unknown",
                        Timestamp: e.CreatedAtDateTime ?? DateTime.MinValue,
                        SnapshotUrl: e.SnapshotUrl
                    ))
                    .ToList() ?? [];

                _logger.LogInformation("Found {EventCount} events for device {DeviceId} for account {AccountId}", deviceEvents.Count, SanitizeForLog(deviceId), providerAccountId);
                return deviceEvents.AsReadOnly();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching events for device {DeviceId} for account {AccountId}", SanitizeForLog(deviceId), providerAccountId);
                return new List<DeviceEvent>().AsReadOnly();
            }
        }

        /// <summary>
        /// Gets device configuration for a specific provider account (account-aware overload).
        /// </summary>
        public async Task<DeviceConfig?> GetDeviceConfigAsync(Guid providerAccountId, string deviceId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Fetching configuration for device {DeviceId} for account {AccountId}", deviceId, providerAccountId);

                Session? session = _sessionProvider.GetSession(providerAccountId);
                if (session == null)
                {
                    _logger.LogError("Not authenticated for account {AccountId}: Session is null", providerAccountId);
                    return null;
                }

                if (!long.TryParse(deviceId, out long doorbotId))
                {
                    _logger.LogWarning("Invalid device ID format: {DeviceId}", deviceId);
                    return null;
                }

                List<Entities.DoorbotHistoryEvent> history = await session.GetDoorbotsHistory(doorbotId);
                return history?.FirstOrDefault() is not Entities.DoorbotHistoryEvent firstEvent
                    ? null
                    : new DeviceConfig(
                    DeviceId: deviceId,
                    MotionDetectionEnabled: true,
                    MotionSensitivity: 75,
                    RecordingMode: "motion"
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching configuration for device {DeviceId} for account {AccountId}", deviceId, providerAccountId);
                return null;
            }
        }

        /// <summary>
        /// Updates device configuration for a specific provider account (account-aware overload).
        /// </summary>
        public async Task<bool> UpdateDeviceConfigAsync(Guid providerAccountId, string deviceId, DeviceConfig config, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Updating configuration for device {DeviceId} for account {AccountId}", deviceId, providerAccountId);

                _logger.LogWarning("Device configuration update not fully implemented for Ring provider");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating configuration for device {DeviceId} for account {AccountId}", deviceId, providerAccountId);
                return false;
            }
        }

        private async Task<List<Entities.DoorbotHistoryEvent>> GetHistoryEventsAsync(Session session, DateTime startDate, DateTime endDate)
        {
            await _historyCacheLock.WaitAsync();
            try
            {
                if (_cachedHistoryEvents != null && _cachedHistoryStart == startDate && _cachedHistoryEnd == endDate)
                {
                    return _cachedHistoryEvents;
                }

                List<Entities.DoorbotHistoryEvent> events = await session.GetDoorbotsHistory(startDate, endDate);
                _cachedHistoryEvents = events ?? [];
                _cachedHistoryStart = startDate;
                _cachedHistoryEnd = endDate;
                return _cachedHistoryEvents;
            }
            finally
            {
                _ = _historyCacheLock.Release();
            }
        }

        /// <summary>Sanitizes a string for logging by replacing newline characters to prevent log forging.</summary>
        private static string SanitizeForLog(string value) =>
            value.Replace('\r', '_').Replace('\n', '_');
    }
}
