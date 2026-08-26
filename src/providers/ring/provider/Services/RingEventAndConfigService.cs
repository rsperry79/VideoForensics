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
                    deviceId, startDate, endDate);

                var session = _sessionProvider.GetSession();
                if (session == null)
                {
                    _logger.LogError("Not authenticated: Session is null");
                    return new List<DeviceEvent>().AsReadOnly();
                }

                var events = await GetHistoryEventsAsync(session, startDate, endDate);

                var deviceEvents = events?
                    .Where(e => e.Doorbot?.Id.ToString() == deviceId)
                    .Where(e => eventType == null || e.Kind == eventType)
                    .Select(e => new DeviceEvent(
                        Id: (e.Id?.ToString()) ?? "unknown",
                        DeviceId: deviceId,
                        EventType: e.Kind ?? "unknown",
                        Timestamp: e.CreatedAtDateTime ?? DateTime.MinValue,
                        SnapshotUrl: e.SnapshotUrl
                    ))
                    .ToList() ?? new List<DeviceEvent>();

                _logger.LogInformation("Found {EventCount} events for device {DeviceId}", deviceEvents.Count, deviceId);
                return deviceEvents.AsReadOnly();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching events for device {DeviceId}", deviceId);
                return new List<DeviceEvent>().AsReadOnly();
            }
        }

        public async Task<DeviceConfig?> GetDeviceConfigAsync(string deviceId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Fetching configuration for device {DeviceId}", deviceId);

                var session = _sessionProvider.GetSession();
                if (session == null)
                {
                    _logger.LogError("Not authenticated: Session is null");
                    return null;
                }

                if (!long.TryParse(deviceId, out var doorbotId))
                {
                    _logger.LogWarning("Invalid device ID format: {DeviceId}", deviceId);
                    return null;
                }

                var history = await session.GetDoorbotsHistory(doorbotId);
                if (history?.FirstOrDefault() is not Entities.DoorbotHistoryEvent firstEvent)
                {
                    return null;
                }

                return new DeviceConfig(
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

        private async Task<List<Entities.DoorbotHistoryEvent>> GetHistoryEventsAsync(Session session, DateTime startDate, DateTime endDate)
        {
            await _historyCacheLock.WaitAsync();
            try
            {
                if (_cachedHistoryEvents != null && _cachedHistoryStart == startDate && _cachedHistoryEnd == endDate)
                {
                    return _cachedHistoryEvents;
                }

                var events = await session.GetDoorbotsHistory(startDate, endDate);
                _cachedHistoryEvents = events ?? new List<Entities.DoorbotHistoryEvent>();
                _cachedHistoryStart = startDate;
                _cachedHistoryEnd = endDate;
                return _cachedHistoryEvents;
            }
            finally
            {
                _historyCacheLock.Release();
            }
        }
    }
}
