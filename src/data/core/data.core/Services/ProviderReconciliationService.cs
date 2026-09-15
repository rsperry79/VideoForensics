using Microsoft.Extensions.Logging;

using System.Text.Json;

using VideoForensics.Core.Logging.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Contracts;

namespace VideoForensics.Data.Core.Services
{
    /// <summary>Service for recording provider reconciliation findings.</summary>
    internal class ProviderReconciliationService : IProviderReconciliationService
    {
        private readonly IProviderReconciliationRepository _reconciliationRepository;
        private readonly IEventRepository _eventRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IActionLogger _actionLogger;
        private readonly ILogger<ProviderReconciliationService> _logger;

        public ProviderReconciliationService(
            IProviderReconciliationRepository reconciliationRepository,
            IEventRepository eventRepository,
            IUnitOfWork unitOfWork,
            IActionLogger actionLogger,
            ILogger<ProviderReconciliationService> logger)
        {
            _reconciliationRepository = reconciliationRepository;
            _eventRepository = eventRepository;
            _unitOfWork = unitOfWork;
            _actionLogger = actionLogger;
            _logger = logger;
        }

        public async Task RecordReconciliationRunAsync(
            Guid deviceId,
            IReadOnlyList<ReconciliationDiscrepancy> discrepancies,
            CancellationToken ct)
        {
            try
            {
                _ = await _unitOfWork.ExecuteAsync(async context =>
                {
                    DateTime runAtUtc = DateTime.UtcNow;

                    // Convert discrepancies to ProviderReconciliationRecord entities
                    var records = discrepancies.Select(d => new ProviderReconciliationRecord
                    {
                        Id = Guid.NewGuid(),
                        DeviceId = deviceId,
                        RanAtUtc = runAtUtc,
                        ProviderEventId = d.ProviderEventId,
                        DiscrepancyType = d.Type,
                        FieldName = d.FieldName,
                        StoredValue = d.StoredValue,
                        ProviderValue = d.ProviderValue
                    }).ToList();

                    // Append all records
                    foreach (ProviderReconciliationRecord? record in records)
                    {
                        _ = await context.ProviderReconciliation.AppendAsync(record, ct);
                    }

                    // Count discrepancies by type
                    int discrepancyCount = records.Count;
                    int missingCount = records.Count(r => r.DiscrepancyType == DiscrepancyType.MissingFromProvider);
                    int changedCount = records.Count(r => r.DiscrepancyType == DiscrepancyType.MetadataChanged);
                    int newCount = records.Count(r => r.DiscrepancyType == DiscrepancyType.NewEventFoundOnProvider);

                    // Log a summary entry
                    var summary = new
                    {
                        DeviceId = deviceId,
                        RunAtUtc = runAtUtc,
                        TotalDiscrepancies = discrepancyCount,
                        MissingFromProvider = missingCount,
                        MetadataChanged = changedCount,
                        NewEventFoundOnProvider = newCount
                    };

                    _ = await context.ActionLog.AppendAsync(
                        Environment.UserName,
                        ActorType.Human,
                        "ProviderReconciliationRun",
                        nameof(Device),
                        deviceId,
                        JsonSerializer.Serialize(summary),
                        ct);

                    _logger.LogInformation(
                        "Reconciliation run completed for device {DeviceId}: {TotalDiscrepancies} discrepancies found " +
                        "(missing={Missing}, changed={Changed}, new={New})",
                        deviceId, discrepancyCount, missingCount, changedCount, newCount);

                    return true;
                }, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording reconciliation run for device {DeviceId}", deviceId);
                throw;
            }
        }

        public async Task<IReadOnlyList<ProviderReconciliationRecord>> GetHistoryAsync(Guid deviceId, CancellationToken ct)
        {
            try
            {
                IReadOnlyList<ProviderReconciliationRecord> history = await _reconciliationRepository.GetHistoryForDeviceAsync(deviceId, ct);
                _logger.LogInformation("Retrieved {RecordCount} reconciliation records for device {DeviceId}",
                    history.Count, deviceId);
                return history;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving reconciliation history for device {DeviceId}", deviceId);
                throw;
            }
        }

        public async Task<AutoFixResult> AutoFixDiscrepanciesAsync(
            Guid deviceId,
            IReadOnlyList<ReconciliationDiscrepancy> discrepancies,
            Func<string, DateTime, DateTime, CancellationToken, Task<IReadOnlyList<Event>>> fetchEventsFunc,
            CancellationToken ct)
        {
            var result = new AutoFixResult();

            try
            {
                _ = await _unitOfWork.ExecuteAsync(async context =>
                {
                    DateTime fixedAtUtc = DateTime.UtcNow;

                    // Process NewEventFoundOnProvider discrepancies
                    var newEventDiscrepancies = discrepancies
                        .Where(d => d.Type == DiscrepancyType.NewEventFoundOnProvider)
                        .ToList();

                    foreach (ReconciliationDiscrepancy? discrepancy in newEventDiscrepancies)
                    {
                        try
                        {
                            // Fetch full event details from provider
                            IReadOnlyList<Event> providerEvents = await fetchEventsFunc(discrepancy.ProviderEventId, DateTime.MinValue, DateTime.MaxValue, ct);

                            if (providerEvents.Count == 0)
                            {
                                result.Failed++;
                                result.ErrorDetails.Add($"No events fetched for provider event {discrepancy.ProviderEventId}");
                                _logger.LogWarning("No events fetched from provider for event {ProviderEventId}", discrepancy.ProviderEventId);
                                continue;
                            }

                            Event providerEvent = providerEvents[0];

                            // Create Event entity
                            var newEvent = new Event
                            {
                                Id = Guid.NewGuid(),
                                DeviceId = deviceId,
                                ProviderEventId = discrepancy.ProviderEventId,
                                EventType = providerEvent.EventType,
                                OccurredAtUtc = providerEvent.OccurredAtUtc,
                                SnapshotUrl = providerEvent.SnapshotUrl,
                                MetadataJson = providerEvent.MetadataJson,
                                DiscoveredAtUtc = fixedAtUtc,
                                ApiSourceHash = providerEvent.ApiSourceHash,
                                EventIntegrityHash = providerEvent.EventIntegrityHash
                            };

                            // Store via repository
                            _ = await _eventRepository.CreateAsync(newEvent, ct);
                            result.NewEventsInserted++;

                            _logger.LogInformation("Auto-fixed new event {ProviderEventId} for device {DeviceId}",
                                discrepancy.ProviderEventId, deviceId);

                            // Log to action log
                            _ = await context.ActionLog.AppendAsync(
                                "System",
                                ActorType.System,
                                "AutoFixNewEvent",
                                nameof(Event),
                                newEvent.Id,
                                JsonSerializer.Serialize(new { discrepancy.ProviderEventId }),
                                ct);
                        }
                        catch (Exception ex)
                        {
                            result.Failed++;
                            result.ErrorDetails.Add($"Error fixing new event {discrepancy.ProviderEventId}: {ex.Message}");
                            _logger.LogError(ex, "Error auto-fixing new event {ProviderEventId} for device {DeviceId}",
                                discrepancy.ProviderEventId, deviceId);
                        }
                    }

                    // Process MetadataChanged discrepancies
                    var metadataDiscrepancies = discrepancies
                        .Where(d => d.Type == DiscrepancyType.MetadataChanged)
                        .ToList();

                    foreach (ReconciliationDiscrepancy? discrepancy in metadataDiscrepancies)
                    {
                        try
                        {
                            // Fetch the stored event
                            Event? storedEvent = await _eventRepository.GetByProviderEventIdAsync(deviceId, discrepancy.ProviderEventId, ct);

                            if (storedEvent == null)
                            {
                                result.Failed++;
                                result.ErrorDetails.Add($"Stored event not found for provider event {discrepancy.ProviderEventId}");
                                _logger.LogWarning("Stored event not found for provider event {ProviderEventId}", discrepancy.ProviderEventId);
                                continue;
                            }

                            // Update only the changed fields
                            string fieldName = discrepancy.FieldName ?? "Unknown";
                            if (fieldName.Equals("EventType", StringComparison.OrdinalIgnoreCase))
                            {
                                storedEvent.EventType = discrepancy.ProviderValue ?? storedEvent.EventType;
                            }
                            else if (fieldName.Equals("OccurredAtUtc", StringComparison.OrdinalIgnoreCase))
                            {
                                if (DateTime.TryParse(discrepancy.ProviderValue, out DateTime parsedDateTime))
                                {
                                    storedEvent.OccurredAtUtc = parsedDateTime;
                                }
                            }
                            else if (fieldName.Equals("SnapshotUrl", StringComparison.OrdinalIgnoreCase))
                            {
                                storedEvent.SnapshotUrl = discrepancy.ProviderValue;
                            }

                            // Update via repository
                            await _eventRepository.UpdateAsync(storedEvent, ct);
                            result.MetadataUpdated++;

                            _logger.LogInformation("Auto-fixed metadata for event {ProviderEventId} field {FieldName} for device {DeviceId}",
                                discrepancy.ProviderEventId, fieldName, deviceId);

                            // Log to action log
                            _ = await context.ActionLog.AppendAsync(
                                "System",
                                ActorType.System,
                                "AutoFixMetadata",
                                nameof(Event),
                                storedEvent.Id,
                                JsonSerializer.Serialize(new { discrepancy.ProviderEventId, FieldName = fieldName, OldValue = discrepancy.StoredValue, NewValue = discrepancy.ProviderValue }),
                                ct);
                        }
                        catch (Exception ex)
                        {
                            result.Failed++;
                            result.ErrorDetails.Add($"Error fixing metadata for {discrepancy.ProviderEventId}: {ex.Message}");
                            _logger.LogError(ex, "Error auto-fixing metadata for event {ProviderEventId} for device {DeviceId}",
                                discrepancy.ProviderEventId, deviceId);
                        }
                    }

                    return true;
                }, ct);

                _logger.LogInformation(
                    "Auto-fix completed for device {DeviceId}: {NewEventsInserted} new events, {MetadataUpdated} metadata updates, {Failed} failures",
                    deviceId, result.NewEventsInserted, result.MetadataUpdated, result.Failed);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during auto-fix reconciliation for device {DeviceId}", deviceId);
                throw;
            }
        }
    }
}
