using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using VideoForensics.Client.Hosting.Services;
using VideoForensics.Client.Hosting.Maintenance;

namespace VideoForensics.Client.Hosting.BackgroundServices
{
    /// <summary>
    /// Drains the account export queue on a background thread, one job at a time, so an export never runs on a
    /// request thread. Server tier only (registered via AddVideoForensicsAccountExport).
    /// </summary>
    internal sealed class AccountExportBackgroundService : BackgroundService
    {
        private readonly AccountExportService _service;
        private readonly ILogger<AccountExportBackgroundService> _logger;
        private readonly IMaintenanceGate _maintenance;

        public AccountExportBackgroundService(AccountExportService service, ILogger<AccountExportBackgroundService> logger, IMaintenanceGate? maintenance = null)
        {
            _service = service;
            _logger = logger;
            _maintenance = maintenance ?? new NoOpMaintenanceGate();
        }

        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                await foreach (Guid jobId in _service.Reader.ReadAllAsync(stoppingToken))
                {
                    try
                    {
                        // Why: the job is already dequeued, so it waits for maintenance to end rather than being dropped.
                        using IDisposable lease = await MaintenanceWork.EnterWorkWhenOpenAsync(_maintenance, stoppingToken);
                        await _service.RunJobAsync(jobId, stoppingToken);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _logger.LogError(ex, "Account export job {JobId} crashed", jobId);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown.
            }
        }
    }
}
