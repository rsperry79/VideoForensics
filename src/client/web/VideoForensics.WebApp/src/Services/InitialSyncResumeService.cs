using Microsoft.Extensions.Options;

using VideoForensics.Client.Hosting.BackgroundServices;

namespace VideoForensics.Client.WebApp.Services
{
    /// <summary>
    /// On server start, re-runs the first sync for accounts that were interrupted before it finished. Nothing is in flight
    /// at startup, so this does not race a first sync started from the add-account endpoint.
    /// </summary>
    public class InitialSyncResumeService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<InitialSyncResumeService> _logger;
        private readonly IOptionsMonitor<ScheduledTasksOptions> _options;

        /// <summary>Creates the hosted service.</summary>
        public InitialSyncResumeService(IServiceScopeFactory scopeFactory, ILogger<InitialSyncResumeService> logger, IOptionsMonitor<ScheduledTasksOptions> options)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _options = options;
        }

        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.CurrentValue.InitialSyncEnabled)
            {
                _logger.LogInformation("First sync is switched off (ScheduledTasks:InitialSyncEnabled); not resuming incomplete first syncs");
                return;
            }

            try
            {
                using IServiceScope scope = _scopeFactory.CreateScope();
                InitialSyncResumer resumer = scope.ServiceProvider.GetRequiredService<InitialSyncResumer>();
                await resumer.ResumeIncompleteAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Shutdown during resume: the next start picks up the remaining accounts.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Resuming incomplete first syncs failed");
            }
        }
    }
}
