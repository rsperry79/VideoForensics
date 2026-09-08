using Microsoft.Extensions.Logging;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.Core.Services
{
    /// <summary>
    /// Orchestrates bulk validation and reconciliation across all devices,
    /// automatically fixing discrepancies discovered during provider reconciliation.
    /// </summary>
    public class BulkValidationService
    {
        private readonly IEvidenceValidationService _evidenceValidationService;
        private readonly IDeviceRepository _deviceRepository;
        private readonly ILogger<BulkValidationService> _logger;

        public BulkValidationService(
            IEvidenceValidationService evidenceValidationService,
            IDeviceRepository deviceRepository,
            ILogger<BulkValidationService> logger)
        {
            _evidenceValidationService = evidenceValidationService;
            _deviceRepository = deviceRepository;
            _logger = logger;
        }

        /// <summary>
        /// Runs full validation (provider reconciliation) across all devices with auto-fix.
        /// </summary>
        /// <param name="fromUtcOverride">Optional custom start date; defaults to 90 days ago.</param>
        /// <param name="toUtcOverride">Optional custom end date; defaults to today.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Aggregated validation result with summary statistics.</returns>
        public async Task<BulkValidationResult> RunFullValidationAsync(
            DateTime? fromUtcOverride = null,
            DateTime? toUtcOverride = null,
            CancellationToken ct = default)
        {
            var startTime = DateTime.UtcNow;
            var result = new BulkValidationResult { RanAtUtc = startTime };

            try
            {
                // Determine date range
                var fromUtc = fromUtcOverride ?? DateTime.Today.AddDays(-90);
                var toUtc = toUtcOverride ?? DateTime.Today;

                _logger.LogInformation(
                    "Starting bulk validation run: {FromUtc} to {ToUtc}",
                    fromUtc, toUtc);

                // Fetch all devices
                IReadOnlyList<Device> devices = await _deviceRepository.ListAsync(ct);
                _logger.LogInformation("Found {DeviceCount} device(s) to validate", devices.Count);

                // Validate each device
                foreach (Device device in devices)
                {
                    if (string.IsNullOrWhiteSpace(device.ProviderDeviceId))
                    {
                        _logger.LogDebug("Skipping device {DeviceId} ({DeviceName}) - no ProviderDeviceId", device.Id, device.Name);
                        continue;
                    }

                    try
                    {
                        _logger.LogInformation(
                            "Validating device {DeviceId} ({DeviceName}) with provider ID {ProviderDeviceId}",
                            device.Id, device.Name, device.ProviderDeviceId);

                        var discrepancies = await _evidenceValidationService.ReconcileWithProviderAsync(
                            device.Id,
                            device.ProviderDeviceId,
                            fromUtc,
                            toUtc,
                            ct);

                        result.DevicesValidated++;
                        result.TotalDiscrepancies += discrepancies.Count;

                        // Count auto-fix results from the discrepancies
                        // Note: The actual auto-fix happens inside ReconcileWithProviderAsync,
                        // so we count the types of discrepancies that were fixed.
                        var newEventCount = discrepancies.Count(d => d.Type == DiscrepancyType.NewEventFoundOnProvider);
                        var metadataChangedCount = discrepancies.Count(d => d.Type == DiscrepancyType.MetadataChanged);

                        result.NewEventsInserted += newEventCount;
                        result.MetadataUpdated += metadataChangedCount;

                        _logger.LogInformation(
                            "Device {DeviceId} ({DeviceName}): validated with {DiscrepancyCount} discrepancy(ies)",
                            device.Id, device.Name, discrepancies.Count);
                    }
                    catch (Exception ex)
                    {
                        result.FailedDevices++;
                        var errorMsg = $"Device {device.Id} ({device.Name}): {ex.Message}";
                        result.ErrorsByDevice.Add(errorMsg);

                        _logger.LogError(ex,
                            "Error validating device {DeviceId} ({DeviceName})",
                            device.Id, device.Name);
                    }
                }

                result.ElapsedTime = DateTime.UtcNow - startTime;

                _logger.LogInformation(
                    "Bulk validation completed: {DevicesValidated} devices, {TotalDiscrepancies} discrepancies, " +
                    "{NewEventsInserted} new events inserted, {MetadataUpdated} metadata updated, " +
                    "{FailedDevices} failed. Elapsed: {ElapsedMs}ms",
                    result.DevicesValidated, result.TotalDiscrepancies, result.NewEventsInserted,
                    result.MetadataUpdated, result.FailedDevices, result.ElapsedTime.TotalMilliseconds);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during bulk validation");
                throw;
            }
        }
    }

    /// <summary>Summary results from a bulk validation run.</summary>
    public class BulkValidationResult
    {
        /// <summary>Number of devices successfully validated.</summary>
        public int DevicesValidated { get; set; }

        /// <summary>Total discrepancies found across all devices.</summary>
        public int TotalDiscrepancies { get; set; }

        /// <summary>Number of new events inserted during auto-fix.</summary>
        public int NewEventsInserted { get; set; }

        /// <summary>Number of metadata fields updated during auto-fix.</summary>
        public int MetadataUpdated { get; set; }

        /// <summary>Number of devices that failed validation.</summary>
        public int FailedDevices { get; set; }

        /// <summary>Error details per device (device ID/name + error message).</summary>
        public List<string> ErrorsByDevice { get; set; } = new();

        /// <summary>UTC timestamp when the validation run started.</summary>
        public DateTime RanAtUtc { get; set; }

        /// <summary>Total elapsed time for the entire validation run.</summary>
        public TimeSpan ElapsedTime { get; set; }
    }
}
