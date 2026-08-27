using Microsoft.Extensions.Logging;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics
{
    /// <summary>Results from verifying local file integrity or provider reconciliation.</summary>
    public class MediaVerificationResult
    {
        public required Guid MediaItemId { get; set; }
        public required string FileName { get; set; }
        public required string Status { get; set; } // "verified", "failed", "missing"
        public string? FailureReason { get; set; }
    }

    /// <summary>Service for validating evidence files (local integrity + provider reconciliation).</summary>
    public interface IEvidenceValidationService
    {
        /// <summary>
        /// Re-verifies the SHA-256 hash of downloaded files against their stored hashes.
        /// </summary>
        /// <param name="deviceId">Device to verify (null = all devices).</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>List of verification results per device/file.</returns>
        Task<IReadOnlyList<MediaVerificationResult>> VerifyLocalIntegrityAsync(Guid? deviceId, CancellationToken ct);

        /// <summary>
        /// Reconciles stored events against the provider's current record, detecting changes/deletions.
        /// </summary>
        /// <param name="deviceId">Guid device ID from the data layer.</param>
        /// <param name="providerDeviceId">Provider's string device ID (e.g. Ring doorbot ID).</param>
        /// <param name="fromUtc">Start of date range to reconcile.</param>
        /// <param name="toUtc">End of date range to reconcile.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>List of discrepancies found (persisted via IProviderReconciliationService).</returns>
        Task<IReadOnlyList<ReconciliationDiscrepancy>> ReconcileWithProviderAsync(
            Guid deviceId,
            string providerDeviceId,
            DateTime fromUtc,
            DateTime toUtc,
            CancellationToken ct);
    }

    /// <summary>
    /// CLI-layer adapter combining IEventAndConfigService (provider) and IVideoForensicsDataClient (data)
    /// to orchestrate integrity verification and provider reconciliation.
    /// Mirrors the architectural role of VideoDownloadServiceAdapter.
    /// </summary>
    public class EvidenceValidationOrchestrator : IEvidenceValidationService
    {
        private readonly ILogger<EvidenceValidationOrchestrator> _logger;
        private readonly VideoForensics.Providers.Common.Contracts.IEventAndConfigService _eventAndConfigService;
        private readonly VideoForensics.Data.Common.Contracts.IEventRepository _eventRepository;
        private readonly VideoForensics.Data.Common.Contracts.IDeviceRepository _deviceRepository;
        private readonly VideoForensics.Data.Common.Contracts.IIntegrityVerificationService _integrityService;
        private readonly VideoForensics.Data.Common.Contracts.IMediaItemRepository _mediaItemRepository;
        private readonly VideoForensics.Data.Core.Contracts.IProviderReconciliationService _reconciliationService;

        /// <summary>Retry configuration for transient provider API failures.</summary>
        private const int MaxRetries = 3;
        private const int InitialDelayMs = 1000;
        private const int MaxDelayMs = 30000;

        public EvidenceValidationOrchestrator(
            ILogger<EvidenceValidationOrchestrator> logger,
            VideoForensics.Providers.Common.Contracts.IEventAndConfigService eventAndConfigService,
            VideoForensics.Data.Common.Contracts.IEventRepository eventRepository,
            VideoForensics.Data.Common.Contracts.IDeviceRepository deviceRepository,
            VideoForensics.Data.Common.Contracts.IIntegrityVerificationService integrityService,
            VideoForensics.Data.Common.Contracts.IMediaItemRepository mediaItemRepository,
            VideoForensics.Data.Core.Contracts.IProviderReconciliationService reconciliationService)
        {
            _logger = logger;
            _eventAndConfigService = eventAndConfigService;
            _eventRepository = eventRepository;
            _deviceRepository = deviceRepository;
            _integrityService = integrityService;
            _mediaItemRepository = mediaItemRepository;
            _reconciliationService = reconciliationService;
        }

        public async Task<IReadOnlyList<MediaVerificationResult>> VerifyLocalIntegrityAsync(Guid? deviceId, CancellationToken ct)
        {
            try
            {
                var results = new List<MediaVerificationResult>();

                IReadOnlyList<Guid> deviceIds;
                if (deviceId.HasValue)
                {
                    deviceIds = new[] { deviceId.Value };
                }
                else
                {
                    var devices = await _deviceRepository.ListAsync(ct);
                    deviceIds = devices.Select(d => d.Id).ToList();
                    _logger.LogInformation("Starting integrity verification across {DeviceCount} device(s)", deviceIds.Count);
                }

                foreach (var id in deviceIds)
                {
                    var mediaItems = await _mediaItemRepository.GetByDeviceIdAsync(id, ct);
                    foreach (var item in mediaItems.Where(m => !m.IsPurged))
                    {
                        // VerifyAsync itself swallows a missing file as a plain `false` (no
                        // exception, see IntegrityVerificationService.VerifyAsync) - check
                        // existence here first so "file gone" and "hash mismatch" aren't
                        // reported identically to the caller.
                        if (!File.Exists(item.FilePath))
                        {
                            results.Add(new MediaVerificationResult
                            {
                                MediaItemId = item.Id,
                                FileName = item.FileName,
                                Status = "missing",
                                FailureReason = $"File not found at {item.FilePath}"
                            });
                            continue;
                        }

                        var passed = await _integrityService.VerifyAsync(item.Id, ct);
                        results.Add(new MediaVerificationResult
                        {
                            MediaItemId = item.Id,
                            FileName = item.FileName,
                            Status = passed ? "verified" : "failed",
                            FailureReason = passed ? null : "SHA-256 mismatch against stored hash"
                        });
                    }
                    _logger.LogInformation("Verified {Count} media item(s) for device {DeviceId}", mediaItems.Count, id);
                }

                _logger.LogInformation("Local integrity verification completed: {Verified} verified, {Failed} failed, {Missing} missing",
                    results.Count(r => r.Status == "verified"),
                    results.Count(r => r.Status == "failed"),
                    results.Count(r => r.Status == "missing"));

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during local integrity verification");
                throw;
            }
        }

        public async Task<IReadOnlyList<ReconciliationDiscrepancy>> ReconcileWithProviderAsync(
            Guid deviceId,
            string providerDeviceId,
            DateTime fromUtc,
            DateTime toUtc,
            CancellationToken ct)
        {
            try
            {
                _logger.LogInformation(
                    "Starting provider reconciliation for device {DeviceId} ({ProviderDeviceId}) from {FromUtc} to {ToUtc}",
                    deviceId, providerDeviceId, fromUtc, toUtc);

                // Fetch live events from provider with retry
                IReadOnlyList<VideoForensics.Providers.Common.Contracts.DeviceEvent>? liveEvents = null;
                try
                {
                    liveEvents = await RetryWithBackoffAsync(
                        async () => await _eventAndConfigService.GetEventsAsync(providerDeviceId, fromUtc, toUtc, null, ct),
                        $"fetch events for device {providerDeviceId}",
                        ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to fetch events from provider for device {ProviderDeviceId}", providerDeviceId);
                    throw;
                }

                _logger.LogInformation("Provider returned {EventCount} live event(s) for device {ProviderDeviceId}",
                    liveEvents?.Count ?? 0, providerDeviceId);

                // Fetch stored events from database
                var storedEvents = await _eventRepository.ListByDeviceAndDateRangeAsync(deviceId, fromUtc, toUtc, ct);
                _logger.LogInformation("Database has {EventCount} stored event(s) for device {DeviceId}", storedEvents.Count, deviceId);

                // Diff: build discrepancies
                var discrepancies = new List<ReconciliationDiscrepancy>();

                if (liveEvents == null || liveEvents.Count == 0)
                {
                    // No live events from provider
                    foreach (var storedEvent in storedEvents)
                    {
                        discrepancies.Add(new ReconciliationDiscrepancy
                        {
                            Type = DiscrepancyType.MissingFromProvider,
                            ProviderEventId = storedEvent.ProviderEventId
                        });
                    }

                    _logger.LogInformation("Found {Count} stored events with no provider equivalent (MissingFromProvider)",
                        discrepancies.Count);
                }
                else
                {
                    // Check each stored event against live events
                    foreach (var storedEvent in storedEvents)
                    {
                        var liveEvent = liveEvents.FirstOrDefault(e => e.Id == storedEvent.ProviderEventId);

                        if (liveEvent == null)
                        {
                            // Stored event not found in live provider response
                            discrepancies.Add(new ReconciliationDiscrepancy
                            {
                                Type = DiscrepancyType.MissingFromProvider,
                                ProviderEventId = storedEvent.ProviderEventId
                            });
                        }
                        else
                        {
                            // Event exists in both — check for field changes
                            // EventType
                            if (!StringEquals(storedEvent.EventType, liveEvent.EventType))
                            {
                                discrepancies.Add(new ReconciliationDiscrepancy
                                {
                                    Type = DiscrepancyType.MetadataChanged,
                                    ProviderEventId = storedEvent.ProviderEventId,
                                    FieldName = "EventType",
                                    StoredValue = storedEvent.EventType,
                                    ProviderValue = liveEvent.EventType
                                });
                            }

                            // Timestamp (OccurredAtUtc vs Timestamp)
                            if (storedEvent.OccurredAtUtc != liveEvent.Timestamp)
                            {
                                discrepancies.Add(new ReconciliationDiscrepancy
                                {
                                    Type = DiscrepancyType.MetadataChanged,
                                    ProviderEventId = storedEvent.ProviderEventId,
                                    FieldName = "Timestamp",
                                    StoredValue = storedEvent.OccurredAtUtc.ToString("O"),
                                    ProviderValue = liveEvent.Timestamp.ToString("O")
                                });
                            }

                            // SnapshotUrl
                            if (!StringEquals(storedEvent.SnapshotUrl, liveEvent.SnapshotUrl))
                            {
                                discrepancies.Add(new ReconciliationDiscrepancy
                                {
                                    Type = DiscrepancyType.MetadataChanged,
                                    ProviderEventId = storedEvent.ProviderEventId,
                                    FieldName = "SnapshotUrl",
                                    StoredValue = storedEvent.SnapshotUrl,
                                    ProviderValue = liveEvent.SnapshotUrl
                                });
                            }
                        }
                    }

                    // Check for new events on provider (not yet in stored)
                    var storedEventIds = new HashSet<string>(storedEvents.Select(e => e.ProviderEventId));
                    foreach (var liveEvent in liveEvents)
                    {
                        if (!storedEventIds.Contains(liveEvent.Id))
                        {
                            discrepancies.Add(new ReconciliationDiscrepancy
                            {
                                Type = DiscrepancyType.NewEventFoundOnProvider,
                                ProviderEventId = liveEvent.Id
                            });
                        }
                    }

                    _logger.LogInformation(
                        "Reconciliation found {TotalDiscrepancies} discrepancies: " +
                        "{MissingCount} missing, {ChangedCount} changed, {NewCount} new",
                        discrepancies.Count,
                        discrepancies.Count(d => d.Type == DiscrepancyType.MissingFromProvider),
                        discrepancies.Count(d => d.Type == DiscrepancyType.MetadataChanged),
                        discrepancies.Count(d => d.Type == DiscrepancyType.NewEventFoundOnProvider));
                }

                // Record the reconciliation run (persists discrepancies and logs a summary)
                await _reconciliationService.RecordReconciliationRunAsync(deviceId, discrepancies, ct);

                return discrepancies;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during provider reconciliation for device {DeviceId}", deviceId);
                throw;
            }
        }

        /// <summary>Executes an operation with exponential backoff retry on rate-limit errors.</summary>
        private async Task<T> RetryWithBackoffAsync<T>(
            Func<Task<T>> operation,
            string operationName,
            CancellationToken cancellationToken)
        {
            int delayMs = InitialDelayMs;

            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    return await operation();
                }
                catch (Exception ex) when (IsRateLimitError(ex) && attempt < MaxRetries)
                {
                    _logger.LogWarning(ex, "Rate limit on {Operation} (attempt {Attempt}/{Max}). Waiting {DelayMs}ms before retry.",
                        operationName, attempt, MaxRetries, delayMs);

                    await Task.Delay(delayMs, cancellationToken);
                    delayMs = Math.Min(delayMs * 2, MaxDelayMs);
                }
            }

            // Final attempt without catch
            return await operation();
        }

        /// <summary>Detects rate-limit errors from provider API responses.</summary>
        private bool IsRateLimitError(Exception ex)
        {
            var message = ex.Message ?? string.Empty;

            return message.Contains("too many requests", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("rate limit", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("429", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("denied by Ring", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Case-insensitive string equality check, treating null and empty as different.</summary>
        private static bool StringEquals(string? a, string? b)
        {
            if (a == null && b == null)
                return true;
            if (a == null || b == null)
                return false;
            return a.Equals(b, StringComparison.OrdinalIgnoreCase);
        }
    }
}
