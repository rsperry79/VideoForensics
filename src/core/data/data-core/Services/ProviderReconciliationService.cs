using System.Text.Json;
using Microsoft.Extensions.Logging;
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
        private readonly IUnitOfWork _unitOfWork;
        private readonly IActionLogger _actionLogger;
        private readonly ILogger<ProviderReconciliationService> _logger;

        public ProviderReconciliationService(
            IProviderReconciliationRepository reconciliationRepository,
            IUnitOfWork unitOfWork,
            IActionLogger actionLogger,
            ILogger<ProviderReconciliationService> logger)
        {
            _reconciliationRepository = reconciliationRepository;
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
                await _unitOfWork.ExecuteAsync(async context =>
                {
                    var runAtUtc = DateTime.UtcNow;

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
                    foreach (var record in records)
                    {
                        await context.ProviderReconciliation.AppendAsync(record, ct);
                    }

                    // Count discrepancies by type
                    var discrepancyCount = records.Count;
                    var missingCount = records.Count(r => r.DiscrepancyType == DiscrepancyType.MissingFromProvider);
                    var changedCount = records.Count(r => r.DiscrepancyType == DiscrepancyType.MetadataChanged);
                    var newCount = records.Count(r => r.DiscrepancyType == DiscrepancyType.NewEventFoundOnProvider);

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

                    await context.ActionLog.AppendAsync(
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
                var history = await _reconciliationRepository.GetHistoryForDeviceAsync(deviceId, ct);
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
    }
}
