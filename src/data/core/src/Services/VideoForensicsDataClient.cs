using Microsoft.Extensions.Logging;

using System.Globalization;

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
        private readonly IDeviceWriteRepository _deviceRepository;
        private readonly IDownloadEventRepository _downloadEventRepository;
        private readonly IEventWriteRepository _eventRepository;
        private readonly IMediaItemWriteRepository _mediaItemRepository;
        private readonly IDeviceHealthRepository _deviceHealthRepository;
        private readonly IDeviceDetailsRepository _deviceDetailsRepository;
        private readonly IDeviceModelRepository _deviceModelRepository;
        private readonly IProviderApiErrorLogRepository _providerApiErrorLogRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IWatermarkService _watermarkService;
        private readonly IActionLogger _actionLogger;
        private readonly ILogger<VideoForensicsDataClient> _logger;
        private readonly IMediaAnalysisService _mediaAnalysis;
        private readonly IEvidenceChangeNotifier _evidenceChangeNotifier;
        private readonly IDeviceHealthChangeNotifier _deviceHealthChangeNotifier;
        private readonly IDingIdLinkRepository _dingIdLinkRepository;
        private readonly IDeviceSettingChangeRepository _deviceSettingChangeRepository;
        private readonly IDetectionEntityProvider _detectionEntityProvider;
        private readonly IActionLogRepository _actionLogRepository;

        public ICredentialRepository Credentials { get; }
        public IIntegrityVerificationService IntegrityVerification { get; }
        public IActionLogRepository ActionLog { get; }

        public VideoForensicsDataClient(
            IUserRepository userRepository,
            IProviderAccountRepository providerAccountRepository,
            ILocationRepository locationRepository,
            IDeviceWriteRepository deviceRepository,
            IDownloadEventRepository downloadEventRepository,
            IEventWriteRepository eventRepository,
            IMediaItemWriteRepository mediaItemRepository,
            IDeviceHealthRepository deviceHealthRepository,
            IDeviceDetailsRepository deviceDetailsRepository,
            IDeviceModelRepository deviceModelRepository,
            IProviderApiErrorLogRepository providerApiErrorLogRepository,
            IUnitOfWork unitOfWork,
            IWatermarkService watermarkService,
            IActionLogger actionLogger,
            ICredentialRepository credentialRepository,
            IIntegrityVerificationService integrityVerification,
            IActionLogRepository actionLogRepository,
            ILogger<VideoForensicsDataClient> logger,
            IMediaAnalysisService mediaAnalysis,
            IDingIdLinkRepository dingIdLinkRepository,
            IDeviceSettingChangeRepository deviceSettingChangeRepository,
            IDetectionEntityProvider detectionEntityProvider,
            IEvidenceChangeNotifier? evidenceChangeNotifier = null,
            IDeviceHealthChangeNotifier? deviceHealthChangeNotifier = null)
        {
            _userRepository = userRepository;
            _providerAccountRepository = providerAccountRepository;
            _locationRepository = locationRepository;
            _deviceRepository = deviceRepository;
            _downloadEventRepository = downloadEventRepository;
            _eventRepository = eventRepository;
            _mediaItemRepository = mediaItemRepository;
            _deviceHealthRepository = deviceHealthRepository;
            _deviceDetailsRepository = deviceDetailsRepository;
            _deviceModelRepository = deviceModelRepository;
            _providerApiErrorLogRepository = providerApiErrorLogRepository;
            _unitOfWork = unitOfWork;
            _watermarkService = watermarkService;
            _actionLogger = actionLogger;
            Credentials = credentialRepository;
            IntegrityVerification = integrityVerification;
            ActionLog = actionLogRepository;
            _logger = logger;
            _mediaAnalysis = mediaAnalysis;
            _dingIdLinkRepository = dingIdLinkRepository;
            _deviceSettingChangeRepository = deviceSettingChangeRepository;
            _detectionEntityProvider = detectionEntityProvider;
            _actionLogRepository = actionLogRepository;
            _evidenceChangeNotifier = evidenceChangeNotifier ?? new NullEvidenceChangeNotifier();
            _deviceHealthChangeNotifier = deviceHealthChangeNotifier ?? new NullDeviceHealthChangeNotifier();
        }

        public async Task<Device> RegisterDeviceAsync(Device device, CancellationToken ct)
        {
            try
            {
                await _deviceRepository.AddAsync(device, ct);
                _logger.LogInformation("Device registered: {DeviceName}", device.Name);
                _ = await _actionLogger.LogAsync("DeviceRegistered", nameof(Device), device.Id, ct: ct);
                return device;
            }
            catch (Exception ex)
            {
                _logger.LogError("Error registering device {DeviceName}: {Message} ({ErrorType})", device.Name, ex.Message, ex.GetType().Name);
                throw;
            }
        }

        public async Task<DeviceHealth> RecordDeviceHealthAsync(DeviceHealth health, CancellationToken ct)
        {
            try
            {
                DeviceHealth recorded = await _deviceHealthRepository.AddAsync(health, ct);
                await _deviceHealthChangeNotifier.DeviceHealthRecordedAsync(recorded.DeviceId, ct);
                return recorded;
            }
            catch (Exception ex)
            {
                _logger.LogError("Error recording device health metric for device {DeviceId}: {Message} ({ErrorType})", health.DeviceId, ex.Message, ex.GetType().Name);
                throw;
            }
        }

        public Task EnsureDeviceModelAsync(string provider, string kind, string family, CancellationToken ct)
        {
            return _deviceModelRepository.EnsureDiscoveredAsync(provider, kind, family, ct);
        }

        public async Task RecordDeviceDetailsAsync(
            Guid deviceId,
            Guid? healthId,
            DeviceCapabilities? capabilities,
            DeviceFeatures? features,
            IReadOnlyList<DeviceAlerts>? alerts,
            CancellationToken ct,
            DeviceSettingsChildren? settings = null)
        {
            try
            {
                await _deviceDetailsRepository.UpsertAsync(deviceId, healthId, capabilities, features, alerts, ct, settings);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error recording device details for device {DeviceId}: {Message} ({ErrorType})", deviceId, ex.Message, ex.GetType().Name);
                throw;
            }
        }

        public async Task UpdateDeviceDetailFieldsAsync(
            Guid deviceId,
            VideoForensics.Core.Providers.Common.Contracts.DeviceDetailFields details,
            CancellationToken ct)
        {
            try
            {
                _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
                {
                    Device? device = await _deviceRepository.GetAsync(deviceId, ct);
                    if (device == null)
                    {
                        _logger.LogDebug("Device detail fields not stored: device {DeviceId} is not in the database yet", deviceId);
                        return null;
                    }

                    var changes = new List<DeviceSettingChange>();
                    ApplyDetailFields(device, details, changes);
                    await _deviceRepository.UpdateAsync(device, ct);
                    if (changes.Count > 0)
                    {
                        // Same unit of work as the Device update: history commits or rolls back with it.
                        await _deviceSettingChangeRepository.AddRangeAsync(changes, ct);
                    }

                    return null;
                }, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error updating detail fields for device {DeviceId}: {Message} ({ErrorType})", deviceId, ex.Message, ex.GetType().Name);
                throw;
            }
        }

        public async Task RecordProviderApiErrorAsync(ProviderApiErrorLog entry, CancellationToken ct)
        {
            try
            {
                await _providerApiErrorLogRepository.RecordAsync(entry, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error recording provider API error log for event {EventId}, device {DeviceId}: {Message} ({ErrorType})",
                    entry.EventId, entry.DeviceId, ex.Message, ex.GetType().Name);
                throw;
            }
        }

        public Task<IReadOnlyList<string>> GetObservedVideoResolutionsAsync(Guid deviceId, CancellationToken ct)
        {
            return _mediaItemRepository.GetDistinctVideoResolutionsAsync(deviceId, ct);
        }

        public async Task<bool> IsMediaAlreadyDownloadedAsync(Guid deviceId, string providerEventId, CancellationToken ct)
        {
            return await _downloadEventRepository.ExistsForProviderEventIdAsync(deviceId, providerEventId, ct);
        }

        public async Task<DownloadEvent?> GetDownloadEventAsync(Guid deviceId, string providerEventId, CancellationToken ct)
        {
            return await _downloadEventRepository.GetByProviderEventIdAsync(deviceId, providerEventId, ct);
        }

        public async Task<string?> GetDownloadedMediaPathAsync(Guid deviceId, string providerEventId, CancellationToken ct)
        {
            DownloadEvent? downloadEvent = await _downloadEventRepository.GetByProviderEventIdAsync(deviceId, providerEventId, ct);
            if (downloadEvent is null)
            {
                return null;
            }

            IReadOnlyList<MediaItem> items = await _mediaItemRepository.GetByDownloadEventIdAsync(downloadEvent.Id, ct);
            return items.FirstOrDefault(i => !i.IsPurged && string.Equals(i.MediaFormat, "mp4", StringComparison.OrdinalIgnoreCase))?.FilePath;
        }

        /// <summary>
        /// Analyses a saved media item and waits for the result, so the next provider call is spaced by local analysis time.
        /// Best-effort: an analysis failure is logged and never fails the recording.
        /// </summary>
        private async Task AnalyzeSavedMediaAsync(Guid mediaItemId, CancellationToken ct)
        {
            try
            {
                await _mediaAnalysis.AnalyzeAsync(mediaItemId, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError("Media analysis failed for media item {MediaItemId}: {Message} ({ErrorType})", mediaItemId, ex.Message, ex.GetType().Name);
            }
        }

        public async Task<DownloadEvent> RecordDownloadEventAsync(
            DownloadEvent evt,
            MediaItem? media,
            CancellationToken ct,
            MediaItemDetection? detection = null,
            List<DetectedPerson>? persons = null,
            List<DetectionTypeOccurrence>? occurrences = null)
        {
            try
            {
                DownloadEvent saved = await _unitOfWork.ExecuteAsync(async context =>
                {
                    // Upsert by (DeviceId, ProviderEventId) rather than blind-inserting - repeated
                    // attempts at the same event (retries after a transient failure, or a permanent
                    // failure recorded so it's not retried forever - see RingMediaDownloadService's
                    // MaxRetries check) would otherwise pile up duplicate rows, and a caller reading
                    // "the" DownloadEvent for an event id would get whichever row happened to sort
                    // first rather than the latest attempt's outcome.
                    DownloadEvent? existing = await _downloadEventRepository.GetByProviderEventIdAsync(evt.DeviceId, evt.ProviderEventId, ct);
                    if (existing != null)
                    {
                        evt.Id = existing.Id;
                        await _downloadEventRepository.UpdateAsync(evt, ct);
                    }
                    else
                    {
                        await _downloadEventRepository.AddAsync(evt, ct);
                    }

                    if (media != null)
                    {
                        // A clip is unique by (DeviceId, RecordedAtUtc). When the file was lost but the row remains, the
                        // re-download must update that row; a blind insert would violate the index and roll back the event.
                        // The range is a single instant, so the list holds one row per media kind at most.
                        // Only a row of the same kind is the same recording: a snapshot or loop at this instant must not be overwritten.
                        MediaItem? sameRecording = (await _mediaItemRepository.GetByDeviceAndDateRangeAsync(
                            media.DeviceId, media.RecordedAtUtc, media.RecordedAtUtc, ct)).FirstOrDefault(m => m.MediaKind == media.MediaKind);
                        if (sameRecording != null)
                        {
                            media.Id = sameRecording.Id;
                            await _mediaItemRepository.UpdateAsync(media, ct);
                        }
                        else
                        {
                            await _mediaItemRepository.AddAsync(media, ct);
                        }
                    }

                    // Persist detection-related entities if provided
                    if (detection != null)
                    {
                        await _detectionEntityProvider.AddMediaItemDetectionAsync(detection, ct);
                    }

                    if (persons != null && persons.Count > 0)
                    {
                        await _detectionEntityProvider.AddDetectedPersonsAsync(persons, ct);
                    }

                    if (occurrences != null && occurrences.Count > 0)
                    {
                        await _detectionEntityProvider.AddDetectionTypeOccurrencesAsync(occurrences, ct);
                    }

                    // Downloads run in the background as the service account, which is not a person.
                    _ = await _actionLogRepository.AppendAsync(
                        Environment.UserName,
                        ActorTypes.ForAccount(Environment.UserName),
                        "MediaDownloaded",
                        nameof(DownloadEvent),
                        evt.Id,
                        null,
                        ct);

                    Device? device = await _deviceRepository.GetAsync(evt.DeviceId, ct);

                    _logger.LogDebug(
                        "Download event recorded for device {DeviceName}, provider event {ProviderEventId}, success={Success}",
                        device?.Name ?? "unknown device",
                        evt.ProviderEventId,
                        evt.Success);

                    return evt;
                }, ct);

                if (media != null)
                {
                    await AnalyzeSavedMediaAsync(media.Id, ct);
                }

                await _evidenceChangeNotifier.EvidenceChangedAsync(evt.DeviceId, ct);
                return saved;
            }
            catch (Exception ex)
            {
                _logger.LogError("Error recording download event for device {DeviceId}, provider event {ProviderEventId}: {Message} ({ErrorType})",
                    evt.DeviceId, evt.ProviderEventId, ex.Message, ex.GetType().Name);
                throw;
            }
        }

        public Task<IReadOnlyList<string>> GetDingIdCandidatesAsync(long doorbotId, CancellationToken ct)
        {
            return _unitOfWork.ReadAsync(context => _dingIdLinkRepository.GetCandidateDingIdsAsync(doorbotId, ct), ct);
        }

        public Task LinkDoorbotHistoryEventAsync(string doorbotHistoryProviderEventId, Guid eventId, CancellationToken ct)
        {
            return _unitOfWork.ExecuteAsync(async context =>
            {
                await _dingIdLinkRepository.LinkDoorbotHistoryEventAsync(doorbotHistoryProviderEventId, eventId, ct);
                return true;
            }, ct);
        }

        public Task<Event?> GetLinkedEventAsync(string doorbotHistoryProviderEventId, CancellationToken ct)
        {
            return _unitOfWork.ExecuteAsync(context => _dingIdLinkRepository.GetLinkedEventAsync(doorbotHistoryProviderEventId, ct), ct);
        }

        public Task RekeyLinkedEventAsync(Guid eventId, string newProviderEventId, CancellationToken ct)
        {
            return _unitOfWork.ExecuteAsync(async context =>
            {
                await _dingIdLinkRepository.RekeyLinkedEventAsync(eventId, newProviderEventId, ct);
                return true;
            }, ct);
        }

        public async Task<Event> UpsertEventAsync(
            Event evt,
            CancellationToken ct,
            EventDetection? detection = null,
            List<EventDetectionZone>? zones = null,
            List<EventSecurityAlert>? alerts = null,
            List<EventDetectedPerson>? persons = null,
            List<EventDetectionTypeOccurrence>? occurrences = null)
        {
            try
            {
                Event saved = await _unitOfWork.ExecuteAsync(async context =>
                {
                    // Upsert the Event record itself
                    Event upserted = await _eventRepository.UpsertAsync(evt, ct);

                    // Persist detection-related entities if provided
                    // Idempotent: this runs several times per event, so detection and its child rows are
                    // upserted/replaced rather than appended.
                    if (detection != null)
                    {
                        detection.EventId = upserted.Id;
                        _ = await _detectionEntityProvider.UpsertEventDetectionAsync(detection, ct);
                        upserted.EventDetectionId = detection.Id;
                    }

                    bool hasAlerts = alerts != null && alerts.Count > 0;
                    bool hasPersons = persons != null && persons.Count > 0;
                    if (hasAlerts || hasPersons)
                    {
                        await _detectionEntityProvider.ClearEventAlertsAndPersonsAsync(upserted.Id, hasAlerts, hasPersons, ct);
                    }

                    if (zones != null && zones.Count > 0)
                    {
                        foreach (EventDetectionZone zone in zones)
                        {
                            zone.EventDetectionId = detection?.Id ?? Guid.Empty;
                        }

                        await _detectionEntityProvider.AddEventDetectionZonesAsync(zones, ct);
                    }

                    if (alerts != null && alerts.Count > 0)
                    {
                        foreach (EventSecurityAlert alert in alerts)
                        {
                            alert.EventId = upserted.Id;
                        }

                        await _detectionEntityProvider.AddEventSecurityAlertsAsync(alerts, ct);
                    }

                    if (persons != null && persons.Count > 0)
                    {
                        foreach (EventDetectedPerson person in persons)
                        {
                            person.EventId = upserted.Id;
                        }

                        await _detectionEntityProvider.AddEventDetectedPersonsAsync(persons, ct);
                    }

                    if (occurrences != null && occurrences.Count > 0)
                    {
                        foreach (EventDetectionTypeOccurrence occurrence in occurrences)
                        {
                            occurrence.EventDetectionId = detection?.Id ?? Guid.Empty;
                        }

                        await _detectionEntityProvider.AddEventDetectionTypeOccurrencesAsync(occurrences, ct);
                    }

                    return upserted;
                }, ct);

                await _evidenceChangeNotifier.EvidenceChangedAsync(evt.DeviceId, ct);
                return saved;
            }
            catch (Exception ex)
            {
                _logger.LogError("Error upserting event for device {DeviceId}, provider event {ProviderEventId}: {Message} ({ErrorType})",
                    evt.DeviceId, evt.ProviderEventId, ex.Message, ex.GetType().Name);
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
                    IReadOnlyList<User> users = await _userRepository.ListAsync(ct);
                    User? existingUser = users.FirstOrDefault(u =>
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
                        await _userRepository.AddAsync(user, ct);
                        _logger.LogDebug("Created new user: {DisplayName}", displayName);
                    }
                    else
                    {
                        user = existingUser;
                        _logger.LogDebug("Found existing user: {DisplayName}", displayName);
                    }

                    // Try to find existing provider account
                    IReadOnlyList<ProviderAccount> accounts = await _providerAccountRepository.ListAsync(ct);
                    ProviderAccount? existingAccount = accounts.FirstOrDefault(a =>
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
                        await _providerAccountRepository.AddAsync(account, ct);
                        _logger.LogDebug("Created new provider account: {Provider} for user {DisplayName}", providerName, user.DisplayName);
                    }
                    else
                    {
                        account = existingAccount;
                        _logger.LogDebug("Found existing provider account: {Provider} for user {DisplayName}", providerName, user.DisplayName);
                    }

                    _ = await _actionLogRepository.AppendAsync(
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
                _logger.LogError("Error ensuring user and account for provider {ProviderName}, user {DisplayName}: {Message} ({ErrorType})",
                    providerName, displayName, ex.Message, ex.GetType().Name);
                throw;
            }
        }

        public async Task<Location> EnsureLocationAsync(
            Guid providerAccountId,
            string providerLocationId,
            string name,
            string? address,
            string? metadataJson = null,
            CancellationToken ct = default)
        {
            try
            {
                return await _unitOfWork.ExecuteAsync(async context =>
                {
                    // Since ProviderLocationId is now globally unique, query globally
                    Location? existingLocation = await _locationRepository.GetByProviderLocationIdAsync(providerLocationId, ct);

                    if (existingLocation == null)
                    {
                        var location = new Location
                        {
                            Id = Guid.NewGuid(),
                            ProviderLocationId = providerLocationId,
                            Name = name,
                            Address = address,
                            LastSyncedUtc = DateTime.UtcNow,
                            SyncStatus = SyncStatus.Synced
                        };
                        await _locationRepository.AddAsync(location, ct);
                        _logger.LogDebug("Created new location: {LocationName}", name);
                        await _locationRepository.LinkToProviderAccountAsync(location.Id, providerAccountId, ct);
                        return location;
                    }
                    else
                    {
                        _logger.LogDebug("Found existing location: {LocationName}", existingLocation.Name);
                        // Locations are globally unique but shared across accounts; ensure this account is linked (idempotent).
                        await _locationRepository.LinkToProviderAccountAsync(existingLocation.Id, providerAccountId, ct);
                        return existingLocation;
                    }
                }, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error ensuring location {LocationName} for provider account {ProviderAccountId}: {Message} ({ErrorType})",
                    name, providerAccountId, ex.Message, ex.GetType().Name);
                throw;
            }
        }

        /// <summary>
        /// Records one change row when a stored scalar differs from the incoming value. A null stored value is never a
        /// change (first population of a column), and null-to-null is equal. Values are invariant-culture text so the
        /// history reads the same regardless of the server's locale.
        /// </summary>
        private static void TrackScalarChange(List<DeviceSettingChange>? changes, Guid deviceId, DateTime changedAtUtc, string fieldName, object? stored, object? incoming)
        {
            if (changes == null || stored == null)
            {
                return;
            }

            string oldText = Convert.ToString(stored, CultureInfo.InvariantCulture)!;
            string? newText = incoming == null ? null : Convert.ToString(incoming, CultureInfo.InvariantCulture);
            if (oldText == newText)
            {
                return;
            }

            changes.Add(new DeviceSettingChange
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                FieldName = fieldName,
                OldValue = oldText,
                NewValue = newText,
                ChangedAtUtc = changedAtUtc
            });
        }

        /// <summary>
        /// Copies provider detail fields onto a device row. A null <paramref name="details"/> is a no-op so a
        /// sync that carries no detail fields never blanks values already on record; a non-null one overwrites
        /// every column, so a field the provider no longer reports is stored as null.
        /// When <paramref name="changes"/> is supplied, each scalar whose stored value actually changes is appended to it
        /// BEFORE the overwrite, so the caller persists the history in the same transaction. Pass null on create paths.
        /// </summary>
        private static void ApplyDetailFields(Device device, VideoForensics.Core.Providers.Common.Contracts.DeviceDetailFields? details, List<DeviceSettingChange>? changes = null)
        {
            if (details == null)
            {
                return;
            }

            DateTime changedAtUtc = DateTime.UtcNow;

            TrackScalarChange(changes, device.Id, changedAtUtc, nameof(Device.RingDeviceId), device.RingDeviceId, details.RingDeviceId);
            device.RingDeviceId = details.RingDeviceId;
            TrackScalarChange(changes, device.Id, changedAtUtc, nameof(Device.RingKind), device.RingKind, details.RingKind);
            device.RingKind = details.RingKind;
            TrackScalarChange(changes, device.Id, changedAtUtc, nameof(Device.FirmwareVersion), device.FirmwareVersion, details.FirmwareVersion);
            device.FirmwareVersion = details.FirmwareVersion;
            TrackScalarChange(changes, device.Id, changedAtUtc, nameof(Device.Latitude), device.Latitude, details.Latitude);
            device.Latitude = details.Latitude;
            TrackScalarChange(changes, device.Id, changedAtUtc, nameof(Device.Longitude), device.Longitude, details.Longitude);
            device.Longitude = details.Longitude;
            TrackScalarChange(changes, device.Id, changedAtUtc, nameof(Device.Address), device.Address, details.Address);
            device.Address = details.Address;
            TrackScalarChange(changes, device.Id, changedAtUtc, nameof(Device.Subscribed), device.Subscribed, details.Subscribed);
            device.Subscribed = details.Subscribed;
            TrackScalarChange(changes, device.Id, changedAtUtc, nameof(Device.SubscribedMotions), device.SubscribedMotions, details.SubscribedMotions);
            device.SubscribedMotions = details.SubscribedMotions;
            TrackScalarChange(changes, device.Id, changedAtUtc, nameof(Device.BatteryLife), device.BatteryLife, details.BatteryLife);
            device.BatteryLife = details.BatteryLife;
            TrackScalarChange(changes, device.Id, changedAtUtc, nameof(Device.ExternalConnection), device.ExternalConnection, details.ExternalConnection);
            device.ExternalConnection = details.ExternalConnection;
            TrackScalarChange(changes, device.Id, changedAtUtc, nameof(Device.Owned), device.Owned, details.Owned);
            device.Owned = details.Owned;
            TrackScalarChange(changes, device.Id, changedAtUtc, nameof(Device.Stolen), device.Stolen, details.Stolen);
            device.Stolen = details.Stolen;
            TrackScalarChange(changes, device.Id, changedAtUtc, nameof(Device.EnableVod), device.EnableVod, details.EnableVod);
            device.EnableVod = details.EnableVod;
            TrackScalarChange(changes, device.Id, changedAtUtc, nameof(Device.ExposureControl), device.ExposureControl, details.ExposureControl);
            device.ExposureControl = details.ExposureControl;
            TrackScalarChange(changes, device.Id, changedAtUtc, nameof(Device.MotionSnoozePresetProfile), device.MotionSnoozePresetProfile, details.MotionSnoozePresetProfile);
            device.MotionSnoozePresetProfile = details.MotionSnoozePresetProfile;
            TrackScalarChange(changes, device.Id, changedAtUtc, nameof(Device.LiveViewPresetProfile), device.LiveViewPresetProfile, details.LiveViewPresetProfile);
            device.LiveViewPresetProfile = details.LiveViewPresetProfile;
            TrackScalarChange(changes, device.Id, changedAtUtc, nameof(Device.PirSensitivity1), device.PirSensitivity1, details.PirSensitivity1);
            device.PirSensitivity1 = details.PirSensitivity1;
            TrackScalarChange(changes, device.Id, changedAtUtc, nameof(Device.VodSuspended), device.VodSuspended, details.VodSuspended);
            device.VodSuspended = details.VodSuspended;
            TrackScalarChange(changes, device.Id, changedAtUtc, nameof(Device.DoorbellVolume), device.DoorbellVolume, details.DoorbellVolume);
            device.DoorbellVolume = details.DoorbellVolume;
            TrackScalarChange(changes, device.Id, changedAtUtc, nameof(Device.VodStatus), device.VodStatus, details.VodStatus);
            device.VodStatus = details.VodStatus;
            TrackScalarChange(changes, device.Id, changedAtUtc, nameof(Device.AdvancedMotionDetectionEnabled), device.AdvancedMotionDetectionEnabled, details.AdvancedMotionDetectionEnabled);
            device.AdvancedMotionDetectionEnabled = details.AdvancedMotionDetectionEnabled;
            TrackScalarChange(changes, device.Id, changedAtUtc, nameof(Device.AdvancedMotionDetectionHumanOnlyMode), device.AdvancedMotionDetectionHumanOnlyMode, details.AdvancedMotionDetectionHumanOnlyMode);
            device.AdvancedMotionDetectionHumanOnlyMode = details.AdvancedMotionDetectionHumanOnlyMode;
            TrackScalarChange(changes, device.Id, changedAtUtc, nameof(Device.EnableAudioRecording), device.EnableAudioRecording, details.EnableAudioRecording);
            device.EnableAudioRecording = details.EnableAudioRecording;
            TrackScalarChange(changes, device.Id, changedAtUtc, nameof(Device.EnableWhiteLeds), device.EnableWhiteLeds, details.EnableWhiteLeds);
            device.EnableWhiteLeds = details.EnableWhiteLeds;
            TrackScalarChange(changes, device.Id, changedAtUtc, nameof(Device.ChimeVolume), device.ChimeVolume, details.ChimeVolume);
            device.ChimeVolume = details.ChimeVolume;
            TrackScalarChange(changes, device.Id, changedAtUtc, nameof(Device.DingAudioUserId), device.DingAudioUserId, details.DingAudioUserId);
            device.DingAudioUserId = details.DingAudioUserId;
            TrackScalarChange(changes, device.Id, changedAtUtc, nameof(Device.DingAudioId), device.DingAudioId, details.DingAudioId);
            device.DingAudioId = details.DingAudioId;
            TrackScalarChange(changes, device.Id, changedAtUtc, nameof(Device.MotionAudioUserId), device.MotionAudioUserId, details.MotionAudioUserId);
            device.MotionAudioUserId = details.MotionAudioUserId;
            TrackScalarChange(changes, device.Id, changedAtUtc, nameof(Device.MotionAudioId), device.MotionAudioId, details.MotionAudioId);
            device.MotionAudioId = details.MotionAudioId;
            TrackScalarChange(changes, device.Id, changedAtUtc, nameof(Device.DoNotDisturbSecondsLeft), device.DoNotDisturbSecondsLeft, details.DoNotDisturbSecondsLeft);
            device.DoNotDisturbSecondsLeft = details.DoNotDisturbSecondsLeft;
            TrackScalarChange(changes, device.Id, changedAtUtc, nameof(Device.LightBrightness), device.LightBrightness, details.LightBrightness);
            device.LightBrightness = details.LightBrightness;
            TrackScalarChange(changes, device.Id, changedAtUtc, nameof(Device.OwnerId), device.OwnerId, details.OwnerId);
            device.OwnerId = details.OwnerId;
            TrackScalarChange(changes, device.Id, changedAtUtc, nameof(Device.OwnerFirstName), device.OwnerFirstName, details.OwnerFirstName);
            device.OwnerFirstName = details.OwnerFirstName;
            TrackScalarChange(changes, device.Id, changedAtUtc, nameof(Device.OwnerLastName), device.OwnerLastName, details.OwnerLastName);
            device.OwnerLastName = details.OwnerLastName;
            TrackScalarChange(changes, device.Id, changedAtUtc, nameof(Device.OwnerEmail), device.OwnerEmail, details.OwnerEmail);
            device.OwnerEmail = details.OwnerEmail;
            TrackScalarChange(changes, device.Id, changedAtUtc, nameof(Device.RingType), device.RingType, details.RingType);
            device.RingType = details.RingType;
            TrackScalarChange(changes, device.Id, changedAtUtc, nameof(Device.RingRingId), device.RingRingId, details.RingRingId);
            device.RingRingId = details.RingRingId;
            TrackScalarChange(changes, device.Id, changedAtUtc, nameof(Device.BatteryLife2), device.BatteryLife2, details.BatteryLife2);
            device.BatteryLife2 = details.BatteryLife2;
            TrackScalarChange(changes, device.Id, changedAtUtc, nameof(Device.BatteryVoltage), device.BatteryVoltage, details.BatteryVoltage);
            device.BatteryVoltage = details.BatteryVoltage;
            TrackScalarChange(changes, device.Id, changedAtUtc, nameof(Device.BatteryVoltage2), device.BatteryVoltage2, details.BatteryVoltage2);
            device.BatteryVoltage2 = details.BatteryVoltage2;
            TrackScalarChange(changes, device.Id, changedAtUtc, nameof(Device.SirenSecondsRemaining), device.SirenSecondsRemaining, details.SirenSecondsRemaining);
            device.SirenSecondsRemaining = details.SirenSecondsRemaining;
            TrackScalarChange(changes, device.Id, changedAtUtc, nameof(Device.NightMode), device.NightMode, details.NightMode);
            device.NightMode = details.NightMode;
            TrackScalarChange(changes, device.Id, changedAtUtc, nameof(Device.MotionSnoozeJson), device.MotionSnoozeJson, details.MotionSnoozeJson);
            device.MotionSnoozeJson = details.MotionSnoozeJson;
            TrackScalarChange(changes, device.Id, changedAtUtc, nameof(Device.SettingsMetadataJson), device.SettingsMetadataJson, details.SettingsMetadataJson);
            device.SettingsMetadataJson = details.SettingsMetadataJson;
            TrackScalarChange(changes, device.Id, changedAtUtc, nameof(Device.LightSettingsMetadataJson), device.LightSettingsMetadataJson, details.LightSettingsMetadataJson);
            device.LightSettingsMetadataJson = details.LightSettingsMetadataJson;
            TrackScalarChange(changes, device.Id, changedAtUtc, nameof(Device.PirSettingsSensitivity1), device.PirSettingsSensitivity1, details.PirSettingsSensitivity1);
            device.PirSettingsSensitivity1 = details.PirSettingsSensitivity1;
            TrackScalarChange(changes, device.Id, changedAtUtc, nameof(Device.PirSettingsSensitivity2), device.PirSettingsSensitivity2, details.PirSettingsSensitivity2);
            device.PirSettingsSensitivity2 = details.PirSettingsSensitivity2;
            TrackScalarChange(changes, device.Id, changedAtUtc, nameof(Device.PirSettingsSensitivity3), device.PirSettingsSensitivity3, details.PirSettingsSensitivity3);
            device.PirSettingsSensitivity3 = details.PirSettingsSensitivity3;
            TrackScalarChange(changes, device.Id, changedAtUtc, nameof(Device.PirSettingsZoneEnable), device.PirSettingsZoneEnable, details.PirSettingsZoneEnable);
            device.PirSettingsZoneEnable = details.PirSettingsZoneEnable;
            TrackScalarChange(changes, device.Id, changedAtUtc, nameof(Device.PirSettingsZoneMask), device.PirSettingsZoneMask, details.PirSettingsZoneMask);
            device.PirSettingsZoneMask = details.PirSettingsZoneMask;
            TrackScalarChange(changes, device.Id, changedAtUtc, nameof(Device.NightModeOn), device.NightModeOn, details.NightModeOn);
            device.NightModeOn = details.NightModeOn;
            TrackScalarChange(changes, device.Id, changedAtUtc, nameof(Device.MotionDetectionEnabled), device.MotionDetectionEnabled, details.MotionDetectionEnabled);
            device.MotionDetectionEnabled = details.MotionDetectionEnabled;
            TrackScalarChange(changes, device.Id, changedAtUtc, nameof(Device.SettingsChimeType), device.SettingsChimeType, details.SettingsChimeType);
            device.SettingsChimeType = details.SettingsChimeType;
            TrackScalarChange(changes, device.Id, changedAtUtc, nameof(Device.SettingsChimeEnabled), device.SettingsChimeEnabled, details.SettingsChimeEnabled);
            device.SettingsChimeEnabled = details.SettingsChimeEnabled;
            TrackScalarChange(changes, device.Id, changedAtUtc, nameof(Device.SettingsChimeDuration), device.SettingsChimeDuration, details.SettingsChimeDuration);
            device.SettingsChimeDuration = details.SettingsChimeDuration;
            TrackScalarChange(changes, device.Id, changedAtUtc, nameof(Device.DeactivatedAtUtc), device.DeactivatedAtUtc, details.DeactivatedAtUtc);
            device.DeactivatedAtUtc = details.DeactivatedAtUtc;
            TrackScalarChange(changes, device.Id, changedAtUtc, nameof(Device.SharedAtUtc), device.SharedAtUtc, details.SharedAtUtc);
            device.SharedAtUtc = details.SharedAtUtc;
        }

        public async Task<Device> EnsureDeviceAsync(
            Guid locationId,
            string providerDeviceId,
            string name,
            string type,
            bool? isOnline,
            CancellationToken ct = default,
            string? timeZoneId = null,
            VideoForensics.Core.Providers.Common.Contracts.DeviceDetailFields? details = null)
        {
            try
            {
                return await _unitOfWork.ExecuteAsync(async context =>
                {
                    // Try the common case first: device already under this same location.
                    IReadOnlyList<Device> devices = await _deviceRepository.GetByLocationIdAsync(locationId, ct);
                    Device? existingDevice = devices.FirstOrDefault(d =>
                        d.ProviderDeviceId == providerDeviceId);

                    // ProviderDeviceId is the device's true identity (Ring's own device id, globally
                    // unique on the account) - a location-scoped lookup alone misses a device that
                    // was previously recorded under a different (e.g. synthetic placeholder) location,
                    // which would otherwise create a duplicate row instead of relocating the original.
                    if (existingDevice == null)
                    {
                        IReadOnlyList<Device> allDevices = await _deviceRepository.ListAsync(ct);
                        existingDevice = allDevices?.FirstOrDefault(d => d.ProviderDeviceId == providerDeviceId);
                    }

                    if (existingDevice == null)
                    {
                        var device = new Device
                        {
                            Id = Guid.NewGuid(),
                            LocationId = locationId,
                            ProviderDeviceId = providerDeviceId,
                            Name = name,
                            Type = type,
                            IsOnline = isOnline,
                            TimeZoneId = timeZoneId,
                            LastSyncedUtc = DateTime.UtcNow,
                            SyncStatus = SyncStatus.Synced
                        };
                        ApplyDetailFields(device, details);
                        await _deviceRepository.AddAsync(device, ct);
                        _logger.LogInformation("Created new device: {DeviceName}", name);
                        return device;
                    }
                    else
                    {
                        if (existingDevice.LocationId != locationId)
                        {
                            // Check if device already exists at target location (race condition check)
                            Device? inTargetLocation = await _deviceRepository
                                .GetByProviderDeviceIdAsync(locationId, providerDeviceId, ct);

                            if (inTargetLocation != null)
                            {
                                return inTargetLocation;  // Already exists there, use it
                            }

                            // Create NEW Device record instead of relocating
                            // Devices are identified by (LocationId, ProviderDeviceId) pair.
                            // If device moves to different location, it's a NEW device record.
                            // This preserves forensic history and location-based queries.
                            // Events at old location still reference old device record.
                            var newDevice = new Device
                            {
                                Id = Guid.NewGuid(),
                                LocationId = locationId,  // NEW location
                                ProviderDeviceId = providerDeviceId,
                                Name = name,
                                Type = type,
                                IsOnline = isOnline,
                                TimeZoneId = timeZoneId,
                                LastSyncedUtc = DateTime.UtcNow,
                                SyncStatus = SyncStatus.Synced,
                                // Local operator setting, not a Ring value: carry it over so a move does not re-enable capture.
                                SnapshotCaptureEnabled = existingDevice.SnapshotCaptureEnabled,
                                HealthCaptureEnabled = existingDevice.HealthCaptureEnabled
                            };
                            ApplyDetailFields(newDevice, details);

                            await _deviceRepository.AddAsync(newDevice, ct);
                            _logger.LogInformation(
                                "Device {DeviceName} ({ProviderDeviceId}) moved to new location; creating new record (NewDeviceId: {NewDeviceId}). " +
                                "Old device {OldDeviceId} at location {OldLocationId} retained for history.",
                                name, providerDeviceId, newDevice.Id, existingDevice.Id, existingDevice.LocationId);

                            return newDevice;
                        }

                        // Update device properties if it exists in the same location
                        // Callers fall back to the provider device ID as the name when a name lookup
                        // fails; don't let that placeholder overwrite a real stored name.
                        bool incomingNameIsId = name == providerDeviceId || name == existingDevice.Id.ToString();
                        bool storedNameIsReal = !string.IsNullOrWhiteSpace(existingDevice.Name)
                            && existingDevice.Name != providerDeviceId
                            && existingDevice.Name != existingDevice.Id.ToString();
                        if (!(incomingNameIsId && storedNameIsReal))
                        {
                            existingDevice.Name = name;
                        }

                        existingDevice.Type = type;
                        existingDevice.IsOnline = isOnline;
                        // Only overwrite the stored time zone when this sync actually carries one, so a
                        // later response without time_zone never blanks a value already on record.
                        if (!string.IsNullOrWhiteSpace(timeZoneId))
                        {
                            existingDevice.TimeZoneId = timeZoneId;
                        }

                        // Same guard as TimeZoneId: a sync that carries no response hash keeps the stored one.

                        var changes = new List<DeviceSettingChange>();
                        ApplyDetailFields(existingDevice, details, changes);
                        await _deviceRepository.UpdateAsync(existingDevice, ct);
                        if (changes.Count > 0)
                        {
                            // Same unit of work as the Device update: history commits or rolls back with it.
                            await _deviceSettingChangeRepository.AddRangeAsync(changes, ct);
                        }

                        _logger.LogDebug("Found and updated existing device: {DeviceName}", existingDevice.Name);
                        return existingDevice;
                    }
                }, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error ensuring device {DeviceName} for location {LocationId}: {Message} ({ErrorType})",
                    name, locationId, ex.Message, ex.GetType().Name);
                throw;
            }
        }

        public async Task UpdateDeviceWatermarkAsync(Guid deviceId, DateTime latestSuccessfulPullTime, CancellationToken ct)
        {
            try
            {
                _ = await _unitOfWork.ExecuteAsync(async context =>
                {
                    Device? device = await _deviceRepository.GetAsync(deviceId, ct);
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
                            await _deviceRepository.UpdateAsync(device, ct);
                            _logger.LogDebug("Advanced watermark for device {DeviceName} to {Timestamp}",
                                device.Name, latestSuccessfulPullTime);
                        }
                        else
                        {
                            _logger.LogDebug(
                                "Skipped watermark update for device {DeviceName}: {Timestamp} is not newer than current watermark {Current}",
                                device.Name, latestSuccessfulPullTime, device.LastSuccessfulPullAtUtc.Value);
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
                _logger.LogError("Error updating device watermark for device {DeviceId}: {Message} ({ErrorType})", deviceId, ex.Message, ex.GetType().Name);
                throw;
            }
        }

        public async Task<DateTime> GetAccountDownloadWatermarkAsync(Guid providerAccountId, CancellationToken ct)
        {
            try
            {
                ProviderAccount? account = await _providerAccountRepository.GetAsync(providerAccountId, ct);
                if (account?.LastDownloadTimeUtc.HasValue == true)
                {
                    _logger.LogDebug("Account {AccountName} last download at {Timestamp}", account.ProviderName, account.LastDownloadTimeUtc.Value);
                    return account.LastDownloadTimeUtc.Value;
                }

                DateTime defaultStart = DateTime.UtcNow.AddDays(-181);
                _logger.LogDebug("Account {AccountId} has no download history; defaulting to 181 days ago: {DefaultStart}", providerAccountId, defaultStart);
                return defaultStart;
            }
            catch (Exception ex)
            {
                _logger.LogError("Error retrieving account download watermark for account {AccountId}: {Message} ({ErrorType})", providerAccountId, ex.Message, ex.GetType().Name);
                throw;
            }
        }

        public async Task UpdateAccountDownloadWatermarkAsync(Guid providerAccountId, DateTime latestDownloadTime, CancellationToken ct)
        {
            try
            {
                _ = await _unitOfWork.ExecuteAsync(async context =>
                {
                    ProviderAccount? account = await _providerAccountRepository.GetAsync(providerAccountId, ct);
                    if (account != null)
                    {
                        if (account.LastDownloadTimeUtc == null || latestDownloadTime > account.LastDownloadTimeUtc.Value)
                        {
                            account.LastDownloadTimeUtc = latestDownloadTime;
                            await _providerAccountRepository.UpdateAsync(account, ct);
                            _logger.LogDebug("Advanced account download watermark for account {AccountName} to {Timestamp}",
                                account.ProviderName, latestDownloadTime);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Provider account {AccountId} not found when attempting to update download watermark", providerAccountId);
                    }

                    return true;
                }, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error updating account download watermark for account {AccountId}: {Message} ({ErrorType})", providerAccountId, ex.Message, ex.GetType().Name);
                throw;
            }
        }
    }
}
