using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using VideoForensics.Hosting.Services;

namespace VideoForensics.Hosting.BackgroundServices
{
    /// <summary>
    /// Drains the account export queue on a background thread, one job at a time, so an export never runs on a
    /// request thread. Server tier only (registered via AddVideoForensicsAccountExport).
    /// </summary>
    internal sealed class AccountExportBackgroundService : BackgroundService
    {
        private readonly AccountExportService _service;
        private readonly ILogger<AccountExportBackgroundService> _logger;

        public AccountExportBackgroundService(AccountExportService service, ILogger<AccountExportBackgroundService> logger)
        {
            _service = service;
            _logger = logger;
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
