using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Core.Helpers.Platform;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Hosting.Services
{
    /// <summary>
    /// Queues account exports and runs them one at a time (drained by
    /// AccountExportBackgroundService). Job status lives in memory only: a restart loses it, while an export file
    /// already written to the exports folder survives. Server tier only. Never exports credentials or tokens; logs
    /// use the provider name and job id, never the account GUID.
    /// </summary>
    internal sealed class AccountExportService : IAccountExportService
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AccountExportService> _logger;
        private readonly TimeProvider _time;
        private readonly Channel<Guid> _queue = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions { SingleReader = true });
        private readonly ConcurrentDictionary<Guid, ExportJobStatus> _jobs = new();

        public AccountExportService(IServiceScopeFactory scopeFactory, ILogger<AccountExportService> logger, TimeProvider time)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _time = time;
        }

        /// <summary>Reader the background service drains.</summary>
        internal ChannelReader<Guid> Reader => _queue.Reader;

        /// <inheritdoc />
        public async Task<Guid> QueueExportAsync(Guid accountId, bool purgeAfterExport, CancellationToken ct, string? stepUpToken = null)
        {
            Guid jobId = Guid.NewGuid();
            _jobs[jobId] = new ExportJobStatus(jobId, accountId, ExportJobState.Queued, null, null, purgeAfterExport, _time.GetUtcNow().UtcDateTime, null);
            await _queue.Writer.WriteAsync(jobId, ct);
            return jobId;
        }

        /// <inheritdoc />
        public Task<ExportJobStatus?> GetStatusAsync(Guid jobId, CancellationToken ct)
        {
            return Task.FromResult(_jobs.TryGetValue(jobId, out ExportJobStatus? status) ? status : null);
        }

        private ExportJobStatus Update(Guid jobId, Func<ExportJobStatus, ExportJobStatus> change)
        {
            return _jobs.AddOrUpdate(jobId, _ => throw new InvalidOperationException("Unknown export job"), (_, current) => change(current));
        }

        /// <summary>Runs one queued job to completion. Internal for direct testing without the background loop.</summary>
        internal async Task RunJobAsync(Guid jobId, CancellationToken ct)
        {
            if (!_jobs.TryGetValue(jobId, out ExportJobStatus? job))
            {
                return;
            }

            _ = Update(jobId, j => j with { State = ExportJobState.Running });
            string providerName = "unknown provider";
            string? tempPath = null;

            try
            {
                using IServiceScope scope = _scopeFactory.CreateScope();
                IServiceProvider sp = scope.ServiceProvider;

                string folder = await ResolveExportsFolderAsync(sp, ct);
                _ = Directory.CreateDirectory(folder);

                IProviderAccountRepository accounts = sp.GetRequiredService<IProviderAccountRepository>();
                ProviderAccount? account = await accounts.GetAsync(job.AccountId, ct);
                if (account == null)
                {
                    Fail(jobId, "Account not found");
                    _logger.LogError("Account export job {JobId} failed: account not found", jobId);
                    return;
                }

                providerName = account.ProviderName;
                _logger.LogInformation("Account export job {JobId} started for {ProviderName} account", jobId, providerName);

                object document = await BuildDocumentAsync(sp, account, ct);

                string stamp = _time.GetUtcNow().UtcDateTime.ToString("yyyyMMdd-HHmmss");
                string finalPath = Path.Combine(folder, $"{stamp}_{SanitizeFileName(providerName)}_{account.Id.ToString("D")[..8]}.json");
                tempPath = finalPath + ".tmp";

                await using (FileStream stream = new(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await JsonSerializer.SerializeAsync(stream, document, JsonOptions, ct);
                }

                // Move last so a partial file never looks complete.
                File.Move(tempPath, finalPath, overwrite: true);
                tempPath = null;

                if (job.PurgeAfterExport)
                {
                    try
                    {
                        await accounts.DeleteAsync(account.Id, ct);
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        // The export file is kept and the account stays retired.
                        _ = Update(jobId, j => j with { State = ExportJobState.Failed, FilePath = finalPath, ErrorMessage = ex.Message, CompletedUtc = _time.GetUtcNow().UtcDateTime });
                        _logger.LogError(ex, "Account export job {JobId} for {ProviderName} account wrote its file but deleting the account failed", jobId, providerName);
                        return;
                    }
                }

                _ = Update(jobId, j => j with { State = ExportJobState.Succeeded, FilePath = finalPath, CompletedUtc = _time.GetUtcNow().UtcDateTime });
                _logger.LogInformation("Account export job {JobId} for {ProviderName} account succeeded", jobId, providerName);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                Fail(jobId, "Cancelled");
                _logger.LogError("Account export job {JobId} for {ProviderName} account was cancelled", jobId, providerName);
            }
            catch (Exception ex)
            {
                Fail(jobId, ex.Message);
                _logger.LogError(ex, "Account export job {JobId} for {ProviderName} account failed", jobId, providerName);
            }
            finally
            {
                if (tempPath != null)
                {
                    try
                    {
                        File.Delete(tempPath);
                    }
                    catch (IOException)
                    {
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }
                }
            }
        }

        private void Fail(Guid jobId, string message)
        {
            _ = Update(jobId, j => j with { State = ExportJobState.Failed, ErrorMessage = message, CompletedUtc = _time.GetUtcNow().UtcDateTime });
        }

        private static async Task<string> ResolveExportsFolderAsync(IServiceProvider sp, CancellationToken ct)
        {
            IForensicsConfiguration config = await sp.GetRequiredService<IForensicsConfigurationService>().LoadConfigurationAsync("", ct);
            return sp.GetRequiredService<IStorageLocationProvider>().GetEffectiveRoot(StorageCategory.Exports, config.ExportsLocation);
        }

        internal static string SanitizeFileName(string name)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            string cleaned = new(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
            return string.IsNullOrWhiteSpace(cleaned) ? "account" : cleaned;
        }

        private async Task<object> BuildDocumentAsync(IServiceProvider sp, ProviderAccount account, CancellationToken ct)
        {
            // Credentials are deliberately never read: the export contains no secrets or tokens.
            IReadOnlyList<Location> locations = await sp.GetRequiredService<ILocationRepository>().ListByProviderAccountIdAsync(account.Id, ct);
            IReadOnlyList<ProviderSyncRun> syncRuns = await sp.GetRequiredService<IProviderSyncRunRepository>().ListByProviderAccountIdAsync(account.Id, ct);

            IDeviceRepository deviceRepository = sp.GetRequiredService<IDeviceRepository>();
            IEventRepository eventRepository = sp.GetRequiredService<IEventRepository>();
            IMediaItemRepository mediaRepository = sp.GetRequiredService<IMediaItemRepository>();
            IDownloadEventRepository downloadRepository = sp.GetRequiredService<IDownloadEventRepository>();

            DateTime from = DateTime.UnixEpoch;
            DateTime to = _time.GetUtcNow().UtcDateTime.AddYears(1);

            var devices = new List<object>();
            var events = new List<Event>();
            var mediaItems = new List<MediaItem>();
            var downloadEvents = new List<DownloadEvent>();
            var seen = new HashSet<Guid>();

            foreach (Location location in locations)
            {
                foreach (Device device in await deviceRepository.GetByLocationIdAsync(location.Id, ct))
                {
                    if (!seen.Add(device.Id))
                    {
                        continue;
                    }

                    devices.Add(new
                    {
                        device.Id,
                        device.LocationId,
                        device.ProviderDeviceId,
                        device.Name,
                        device.Type,
                        device.IsOnline,
                        device.TimeZoneId,
                        device.FirmwareVersion,
                        device.LastSyncedUtc
                    });
                    events.AddRange(await eventRepository.ListByDeviceAndDateRangeAsync(device.Id, from, to, ct));
                    mediaItems.AddRange(await mediaRepository.GetByDeviceIdAsync(device.Id, ct));
                    downloadEvents.AddRange(await downloadRepository.GetByDeviceIdAsync(device.Id, ct));
                }
            }

            return new
            {
                exportedUtc = _time.GetUtcNow().UtcDateTime,
                account = new
                {
                    id = account.Id,
                    providerName = account.ProviderName,
                    linkedUtc = account.LinkedUtc,
                    removedUtc = account.RemovedUtc,
                    lastSuccessfulAuthUtc = account.LastSuccessfulAuthUtc,
                    isActive = account.IsActive,
                    lastErrorMessage = account.LastErrorMessage
                },
                locations,
                syncRuns,
                devices,
                events,
                downloadEvents,
                mediaItems
            };
        }
    }
}
