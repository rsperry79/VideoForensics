using Microsoft.Extensions.Logging;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Providers.Common.Contracts;

namespace VideoForensics.Hosting.Services
{
    /// <summary>Service for pulling events and configuration from provider accounts automatically.</summary>
    public interface IEventPullService
    {
        /// <summary>Pulls events and configuration for all devices in a provider account.</summary>
        /// <param name="accountId">The provider account ID.</param>
        /// <param name="fromTimestampUtc">Start timestamp for event retrieval. If null, defaults to DateTime.MinValue for first-time pulls.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task PullAccountEventsAsync(Guid accountId, DateTime? fromTimestampUtc, CancellationToken cancellationToken);
    }

    /// <summary>Pulls events and configuration from provider accounts automatically.</summary>
    public class EventPullService : IEventPullService
    {
        private readonly IEventAndConfigService _eventAndConfigService;
        private readonly IDeviceRepository _deviceRepository;
        private readonly ILocationRepository _locationRepository;
        private readonly IProviderAccountRepository _providerAccountRepository;
        private readonly ILogger<EventPullService> _logger;

        /// <summary>Creates a new instance of the EventPullService.</summary>
        public EventPullService(
            IEventAndConfigService eventAndConfigService,
            IDeviceRepository deviceRepository,
            ILocationRepository locationRepository,
            IProviderAccountRepository providerAccountRepository,
            ILogger<EventPullService>? logger = null)
        {
            _eventAndConfigService = eventAndConfigService;
            _deviceRepository = deviceRepository;
            _locationRepository = locationRepository;
            _providerAccountRepository = providerAccountRepository;
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<EventPullService>.Instance;
        }

        public async Task PullAccountEventsAsync(Guid accountId, DateTime? fromTimestampUtc, CancellationToken cancellationToken)
        {
            try
            {
                var account = await _providerAccountRepository.GetAsync(accountId, cancellationToken);
                if (account == null)
                {
                    _logger.LogWarning("Provider account not found");
                    return;
                }

#pragma warning disable CS0618
                var locations = await _locationRepository.GetByProviderAccountIdAsync(accountId, cancellationToken);
#pragma warning restore CS0618
                if (locations.Count == 0)
                {
                    _logger.LogInformation("No locations found for account");
                    account.LastSuccessfulAuthUtc = DateTime.UtcNow;
                    account.LastErrorMessage = null;
                    await _providerAccountRepository.UpdateAsync(account, cancellationToken);
                    return;
                }

                var allDevices = new List<VideoForensics.Data.Common.Entities.Device>();
                foreach (var location in locations)
                {
                    var devices = await _deviceRepository.GetByLocationIdAsync(location.Id, cancellationToken);
                    allDevices.AddRange(devices);
                }

                var startTime = fromTimestampUtc ?? DateTime.MinValue;
                var endTime = DateTime.UtcNow;
                var deviceErrors = new List<string>();

                foreach (var device in allDevices)
                {
                    try
                    {
                        _logger.LogDebug("Pulling events for device {DeviceId}", device.ProviderDeviceId);

                        await _eventAndConfigService.GetEventsAsync(
                            device.ProviderDeviceId,
                            startTime,
                            endTime,
                            eventType: null,
                            cancellationToken
                        );

                        await _eventAndConfigService.GetDeviceConfigAsync(device.ProviderDeviceId, cancellationToken);

                        device.LastSuccessfulPullAtUtc = DateTime.UtcNow;
                        device.LastPullAttemptAtUtc = DateTime.UtcNow;
                        await _deviceRepository.UpdateAsync(device, cancellationToken);

                        _logger.LogDebug("Successfully pulled events for device");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to pull events for device");
                        device.LastPullAttemptAtUtc = DateTime.UtcNow;
                        await _deviceRepository.UpdateAsync(device, cancellationToken);
                        deviceErrors.Add($"Device {device.Name}: {ex.Message}");
                    }
                }

                if (deviceErrors.Count > 0)
                {
                    account.LastErrorMessage = TruncateErrorMessage(string.Join("; ", deviceErrors));
                    account.LastErrorUtc = DateTime.UtcNow;
                }
                else
                {
                    account.LastSuccessfulAuthUtc = DateTime.UtcNow;
                    account.LastErrorMessage = null;
                    account.LastErrorUtc = null;
                }

                await _providerAccountRepository.UpdateAsync(account, cancellationToken);

                _logger.LogInformation("Successfully pulled events for account");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to pull events for account");

                var account = await _providerAccountRepository.GetAsync(accountId, cancellationToken);
                if (account != null)
                {
                    account.LastErrorMessage = TruncateErrorMessage(ex.Message);
                    account.LastErrorUtc = DateTime.UtcNow;
                    await _providerAccountRepository.UpdateAsync(account, cancellationToken);
                }
            }
        }

        private static string TruncateErrorMessage(string message, int maxLength = 2000)
        {
            return message.Length > maxLength ? message.Substring(0, maxLength) : message;
        }
    }
}
