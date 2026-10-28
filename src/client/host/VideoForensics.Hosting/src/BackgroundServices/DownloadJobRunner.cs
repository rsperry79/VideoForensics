using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using VideoForensics.Client.Hosting.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.Hosting.BackgroundServices
{
    /// <summary>
    /// Runs queued download jobs one at a time. At startup, jobs left Running by a previous process are requeued,
    /// because they have no live runner.
    /// </summary>
    public class DownloadJobRunner : BackgroundService
    {
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<DownloadJobRunner> _logger;

        /// <summary>Initializes a new instance of the DownloadJobRunner.</summary>
        public DownloadJobRunner(IServiceScopeFactory scopes, ILogger<DownloadJobRunner> logger)
        {
            _scopes = scopes;
            _logger = logger;
        }

        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using (IServiceScope startup = _scopes.CreateScope())
            {
                _ = await startup.ServiceProvider.GetRequiredService<IDownloadJobRepository>().RequeueRunningAsync(stoppingToken);
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                bool ran = false;
                try
                {
                    using IServiceScope scope = _scopes.CreateScope();
                    ran = await RunNextJobAsync(
                        scope.ServiceProvider.GetRequiredService<IDownloadJobRepository>(),
                        scope.ServiceProvider.GetRequiredService<IDownloadJobExecutor>(),
                        stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError("Download job runner loop failed: {ErrorType}: {ErrorMessage}", ex.GetType().Name, ex.Message);
                }

                if (!ran)
                {
                    try
                    {
                        await Task.Delay(PollInterval, stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Claims the oldest queued job and runs it. Returns false when nothing was queued. Failures are recorded on
        /// the job, not thrown, so one bad job does not stop the runner.
        /// </summary>
        public async Task<bool> RunNextJobAsync(IDownloadJobRepository repository, IDownloadJobExecutor executor, CancellationToken ct)
        {
            DownloadJob? job = await repository.ClaimNextQueuedAsync(ct);
            if (job is null)
            {
                return false;
            }

            _logger.LogInformation("Starting {Kind} download job {JobId} for account {AccountId}", job.Kind, job.Id, job.ProviderAccountId);

            try
            {
                DownloadJobOutcome outcome = await executor.ExecuteAsync(job, ct);
                if (outcome.ErrorMessage is not null)
                {
                    await repository.FailAsync(job.Id, outcome.ErrorMessage, CancellationToken.None);
                    _logger.LogError("{Kind} download job {JobId} failed: {ErrorMessage}", job.Kind, job.Id, outcome.ErrorMessage);
                }
                else
                {
                    _ = await repository.CompleteAsync(job.Id, outcome.FilesDownloaded, outcome.FilesSkipped, outcome.FilesFailed, CancellationToken.None);
                    _logger.LogInformation("{Kind} download job {JobId} completed: {Downloaded} downloaded, {Skipped} skipped, {Failed} failed",
                        job.Kind, job.Id, outcome.FilesDownloaded, outcome.FilesSkipped, outcome.FilesFailed);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Shutdown: leave the job Running. The next startup requeues it.
                throw;
            }
            catch (Exception ex)
            {
                await repository.FailAsync(job.Id, ex.Message, CancellationToken.None);
                _logger.LogError("{Kind} download job {JobId} failed: {ErrorType}: {ErrorMessage}", job.Kind, job.Id, ex.GetType().Name, ex.Message);
            }

            return true;
        }
    }
}
