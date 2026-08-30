using Microsoft.Extensions.Logging;
using VideoForensics.Core.Logging.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Contracts;

namespace VideoForensics.Data.Core.Services
{
    /// <summary>Top-level facade for accessing the VideoForensics data layer.</summary>
    internal class VideoForensicsDataClient : IVideoForensicsDataClient
    {
        private readonly IUserRepository _userRepository;
        private readonly IProviderAccountRepository _providerAccountRepository;
        private readonly ILocationRepository _locationRepository;
        private readonly IDeviceRepository _deviceRepository;
        private readonly IDownloadEventRepository _downloadEventRepository;
        private readonly IEventRepository _eventRepository;
        private readonly IMediaItemRepository _mediaItemRepository;
        private readonly IDeviceHealthSnapshotRepository _deviceHealthSnapshotRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IWatermarkService _watermarkService;
        private readonly IActionLogger _actionLogger;
        private readonly ICredentialRepository _credentialRepository;
        private readonly IIntegrityVerificationService _integrityVerification;
        private readonly IActionLogRepository _actionLogRepository;
        private readonly ILogger<VideoForensicsDataClient> _logger;

        public ICredentialRepository Credentials => _credentialRepository;
        public IIntegrityVerificationService IntegrityVerification => _integrityVerification;
        public IActionLogRepository ActionLog => _actionLogRepository;

        public VideoForensicsDataClient(
            IUserRepository userRepository,
            IProviderAccountRepository providerAccountRepository,
            ILocationRepository locationRepository,
            IDeviceRepository deviceRepository,
            IDownloadEventRepository downloadEventRepository,
            IEventRepository eventRepository,
            IMediaItemRepository mediaItemRepository,
            IDeviceHealthSnapshotRepository deviceHealthSnapshotRepository,
            IUnitOfWork unitOfWork,
            IWatermarkService watermarkService,
            IActionLogger actionLogger,
            ICredentialRepository credentialRepository,
            IIntegrityVerificationService integrityVerification,
            IActionLogRepository actionLogRepository,
            ILogger<VideoForensicsDataClient> logger)
        {
            _userRepository = userRepository;
            _providerAccountRepository = providerAccountRepository;
            _locationRepository = locationRepository;
            _deviceRepository = deviceRepository;
            _downloadEventRepository = downloadEventRepository;
            _eventRepository = eventRepository;
            _mediaItemRepository = mediaItemRepository;
            _deviceHealthSnapshotRepository = deviceHealthSnapshotRepository;
            _unitOfWork = unitOfWork;
            _watermarkService = watermarkService;
            _actionLogger = actionLogger;
            _credentialRepository = credentialRepository;
            _integrityVerification = integrityVerification;
            _actionLogRepository = actionLogRepository;
            _logger = logger;
        }

        public async Task<Device> RegisterDeviceAsync(Device device, CancellationToken ct)
        {
            try
            {
                await _deviceRepository.AddAsync(device, ct);
                _logger.LogInformation("Device registered: {DeviceName} ({DeviceId})", device.Name, device.Id);
                await _actionLogger.LogAsync("DeviceRegistered", nameof(Device), device.Id, ct: ct);
                return device;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering device {DeviceName}", device.Name);
                throw;
            }
        }

        public async Task<DeviceHealthSnapshot> RecordDeviceHealthSnapshotAsync(DeviceHealthSnapshot snapshot, CancellationToken ct)
        {
            try
            {
                return await _deviceHealthSnapshotRepository.AppendSnapshotAsync(snapshot, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording device health snapshot for device {DeviceId}", snapshot.DeviceId);
                throw;
            }
        }

        public async Task<bool> IsMediaAlreadyDownloadedAsync(Guid deviceId, string providerEventId, CancellationToken ct)
        {
            return await _downloadEventRepository.ExistsForProviderEventIdAsync(deviceId, providerEventId, ct);
        }

        public async Task<DownloadEvent> RecordDownloadEventAsync(DownloadEvent evt, MediaItem? media, CancellationToken ct)
        {
            try
            {
                return await _unitOfWork.ExecuteAsync(async context =>
                {
                    await context.DownloadEvents.AddAsync(evt, ct);
                    if (media != null)
                    {
                        await context.MediaItems.AddAsync(media, ct);
                    }

                    await context.ActionLog.AppendAsync(
                        Environment.UserName,
                        ActorType.Human,
                        "MediaDownloaded",
                        nameof(DownloadEvent),
                        evt.Id,
                        null,
                        ct);

                    _logger.LogInformation(
                        "Download event recorded for device {DeviceId}, provider event {ProviderEventId}, success={Success}",
                        evt.DeviceId,
                        evt.ProviderEventId,
                        evt.Success);

                    return evt;
                }, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording download event for device {DeviceId}, provider event {ProviderEventId}",
                    evt.DeviceId, evt.ProviderEventId);
                throw;
            }
        }

        public async Task<Event> UpsertEventAsync(Event evt, CancellationToken ct)
        {
            try
            {
                return await _eventRepository.UpsertAsync(evt, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error upserting event for device {DeviceId}, provider event {ProviderEventId}",
                    evt.DeviceId, evt.ProviderEventId);
                throw;
            }
        }

        public Task<DateTime> GetWatermarkAsync(Guid deviceId, DateTime requestedStartDate, bool force, CancellationToken ct)
        {
            return _watermarkService.ResolveStartDateAsync(deviceId, requestedStartDate, force, ct);
        }

        public async Task<(User User, ProviderAccount Account)> EnsureUserAndAccountAsync(
            string providerName,
            string providerUserKey,
            string displayName,
            string? email,
            CancellationToken ct)
        {
            try
            {
                return await _unitOfWork.ExecuteAsync(async context =>
                {
                    // Try to find existing user
                    var users = await context.Users.ListAsync(ct);
                    var existingUser = users.FirstOrDefault(u =>
                        u.ProviderUserKey == providerUserKey &&
                        u.DisplayName == displayName);

                    User user;
                    if (existingUser == null)
                    {
                        user = new User
                        {
                            Id = Guid.NewGuid(),
                            ProviderUserKey = providerUserKey,
                            DisplayName = displayName,
                            Email = email,
                            CreatedUtc = DateTime.UtcNow
                        };
                        await context.Users.AddAsync(user, ct);
                        _logger.LogInformation("Created new user: {DisplayName} ({UserId})", displayName, user.Id);
                    }
                    else
                    {
                        user = existingUser;
                        _logger.LogInformation("Found existing user: {DisplayName} ({UserId})", displayName, user.Id);
                    }

                    // Try to find existing provider account
                    var accounts = await context.ProviderAccounts.ListAsync(ct);
                    var existingAccount = accounts.FirstOrDefault(a =>
                        a.UserId == user.Id &&
                        a.ProviderName == providerName);

                    ProviderAccount account;
                    if (existingAccount == null)
                    {
                        account = new ProviderAccount
                        {
                            Id = Guid.NewGuid(),
                            UserId = user.Id,
                            ProviderName = providerName,
                            LinkedUtc = DateTime.UtcNow,
                            IsActive = true
                        };
                        await context.ProviderAccounts.AddAsync(account, ct);
                        _logger.LogInformation("Created new provider account: {Provider} for user {UserId}", providerName, user.Id);
                    }
                    else
                    {
                        account = existingAccount;
                        _logger.LogInformation("Found existing provider account: {Provider} for user {UserId}", providerName, user.Id);
                    }

                    await context.ActionLog.AppendAsync(
                        Environment.UserName,
                        ActorType.Human,
                        existingUser == null ? "UserCreated" : "AccountLinked",
                        nameof(ProviderAccount),
                        account.Id,
                        null,
                        ct);

                    return (user, account);
                }, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ensuring user and account for provider {ProviderName}, user key {ProviderUserKey}",
                    providerName, providerUserKey);
                throw;
            }
        }

        public async Task<Location> EnsureLocationAsync(
            Guid providerAccountId,
            string providerLocationId,
            string name,
            string? address,
            CancellationToken ct)
        {
            try
            {
                return await _unitOfWork.ExecuteAsync(async context =>
                {
                    // Try to find existing location
                    var locations = await context.Locations.GetByProviderAccountIdAsync(providerAccountId, ct);
                    var existingLocation = locations.FirstOrDefault(l =>
                        l.ProviderLocationId == providerLocationId);

                    if (existingLocation == null)
                    {
                        var location = new Location
                        {
                            Id = Guid.NewGuid(),
                            ProviderAccountId = providerAccountId,
                            ProviderLocationId = providerLocationId,
                            Name = name,
                            Address = address
                        };
                        await context.Locations.AddAsync(location, ct);
                        _logger.LogInformation("Created new location: {LocationName} ({LocationId})", name, location.Id);
                        return location;
                    }
                    else
                    {
                        _logger.LogInformation("Found existing location: {LocationName} ({LocationId})", existingLocation.Name, existingLocation.Id);
                        return existingLocation;
                    }
                }, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ensuring location {LocationName} for provider account {ProviderAccountId}",
                    name, providerAccountId);
                throw;
            }
        }

        public async Task<Device> EnsureDeviceAsync(
            Guid locationId,
            string providerDeviceId,
            string name,
            string type,
            bool isOnline,
            CancellationToken ct)
        {
            try
            {
                return await _unitOfWork.ExecuteAsync(async context =>
                {
                    // Try to find existing device
                    var devices = await context.Devices.GetByLocationIdAsync(locationId, ct);
                    var existingDevice = devices.FirstOrDefault(d =>
                        d.ProviderDeviceId == providerDeviceId);

                    if (existingDevice == null)
                    {
                        var device = new Device
                        {
                            Id = Guid.NewGuid(),
                            LocationId = locationId,
                            ProviderDeviceId = providerDeviceId,
                            Name = name,
                            Type = type,
                            IsOnline = isOnline
                        };
                        await context.Devices.AddAsync(device, ct);
                        _logger.LogInformation("Created new device: {DeviceName} ({DeviceId})", name, device.Id);
                        return device;
                    }
                    else
                    {
                        // Update device properties if it exists
                        existingDevice.Name = name;
                        existingDevice.Type = type;
                        existingDevice.IsOnline = isOnline;
                        await context.Devices.UpdateAsync(existingDevice, ct);
                        _logger.LogInformation("Found and updated existing device: {DeviceName} ({DeviceId})", existingDevice.Name, existingDevice.Id);
                        return existingDevice;
                    }
                }, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ensuring device {DeviceName} for location {LocationId}",
                    name, locationId);
                throw;
            }
        }

        public async Task UpdateDeviceWatermarkAsync(Guid deviceId, DateTime latestSuccessfulPullTime, CancellationToken ct)
        {
            try
            {
                await _unitOfWork.ExecuteAsync(async context =>
                {
                    var device = await context.Devices.GetAsync(deviceId, ct);
                    if (device != null)
                    {
                        // Events download concurrently (see RingMediaDownloadService's
                        // Parallel.ForEachAsync), so calls here race and can complete in an order
                        // unrelated to event timestamps. Only ever advance the watermark - never
                        // let a later-finishing-but-earlier-timestamped event regress it and cause
                        // the next incremental pull to re-scan (and potentially re-download) events
                        // already recorded.
                        if (device.LastSuccessfulPullAtUtc == null || latestSuccessfulPullTime > device.LastSuccessfulPullAtUtc.Value)
                        {
                            device.LastSuccessfulPullAtUtc = latestSuccessfulPullTime;
                            await context.Devices.UpdateAsync(device, ct);
                            _logger.LogInformation("Advanced watermark for device {DeviceId} to {Timestamp}",
                                deviceId, latestSuccessfulPullTime);
                        }
                        else
                        {
                            _logger.LogDebug(
                                "Skipped watermark update for device {DeviceId}: {Timestamp} is not newer than current watermark {Current}",
                                deviceId, latestSuccessfulPullTime, device.LastSuccessfulPullAtUtc.Value);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Device {DeviceId} not found when attempting to update watermark", deviceId);
                    }
                    return true;
                }, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating device watermark for device {DeviceId}", deviceId);
                throw;
            }
        }
    }
}
