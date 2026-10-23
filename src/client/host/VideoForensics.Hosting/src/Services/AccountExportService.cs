using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;

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
        private const int EventPageSize = 500;

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AccountExportService> _logger;
        private readonly TimeProvider _time;
        private readonly Channel<Guid> _queue = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions { SingleReader = true });
        private readonly ConcurrentDictionary<Guid, ExportJobStatus> _jobs = new();
        private readonly ConcurrentDictionary<Guid, Guid> _latestJobByAccount = new();

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
            // Purge deletes only the account's own rows (its credentials, schedule, sync history and links). Locations,
            // devices and events are shared through ProviderAccountLocation and are never deleted with the account.
            var jobId = Guid.NewGuid();
            _jobs[jobId] = new ExportJobStatus(jobId, accountId, ExportJobState.Queued, null, null, purgeAfterExport, _time.GetUtcNow().UtcDateTime, null);
            _latestJobByAccount[accountId] = jobId;
            await _queue.Writer.WriteAsync(jobId, ct);
            return jobId;
        }

        /// <inheritdoc />
        public Task<ExportJobStatus?> GetStatusAsync(Guid jobId, CancellationToken ct)
        {
            return Task.FromResult(_jobs.TryGetValue(jobId, out ExportJobStatus? status) ? status : null);
        }

        /// <inheritdoc />
        public Task<ExportJobStatus?> GetLatestStatusForAccountAsync(Guid accountId, CancellationToken ct)
        {
            return Task.FromResult(_latestJobByAccount.TryGetValue(accountId, out Guid jobId) && _jobs.TryGetValue(jobId, out ExportJobStatus? status) ? status : null);
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

                string stamp = _time.GetUtcNow().UtcDateTime.ToString("yyyyMMdd-HHmmss");
                string finalPath = Path.Combine(folder, $"{stamp}_{SanitizeFileName(providerName)}_{account.Id.ToString("D")[..8]}.json");
                tempPath = finalPath + ".tmp";

                await using (FileStream stream = new(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await WriteDocumentAsync(stream, sp, account, ct);
                }

                // Move last so a partial file never looks complete.
                File.Move(tempPath, finalPath, overwrite: true);
                tempPath = null;

                if (job.PurgeAfterExport)
                {
                    // Purge is irreversible: prove the file on disk is a complete document before deleting anything.
                    try
                    {
                        await VerifyExportFileAsync(finalPath, ct);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _ = Update(jobId, j => j with { State = ExportJobState.Failed, FilePath = finalPath, ErrorMessage = "Export file failed verification; account was not deleted: " + ex.Message, CompletedUtc = _time.GetUtcNow().UtcDateTime });
                        _logger.LogError(ex, "Account export job {JobId} for {ProviderName} account: export file failed verification, account kept", jobId, providerName);
                        return;
                    }

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
            IForensicsConfiguration config = await sp.GetRequiredService<IForensicsConfigurationService>().LoadConfigurationAsync(string.Empty, ct);
            return sp.GetRequiredService<IStorageLocationProvider>().GetEffectiveRoot(StorageCategory.Exports, config.ExportsLocation);
        }

        /// <summary>
        /// Re-reads the written export and parses it to the end. A truncated or malformed file throws, which keeps the
        /// account from being purged on the strength of a bad export.
        /// </summary>
        internal static async Task VerifyExportFileAsync(string path, CancellationToken ct)
        {
            await using FileStream read = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (read.Length == 0)
            {
                throw new InvalidDataException("export file is empty");
            }

            using JsonDocument document = await JsonDocument.ParseAsync(read, cancellationToken: ct);
        }

        internal static string SanitizeFileName(string name)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            string cleaned = new(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
            return string.IsNullOrWhiteSpace(cleaned) ? "account" : cleaned;
        }

        /// <summary>
        /// Streams the export document to <paramref name="stream"/> with a Utf8JsonWriter instead of building it in memory.
        /// Events are read per device in keyset pages starting at the device's earliest event; downloads and media are
        /// still read per device in bulk (no paged read exists for them yet) but are written out per device, not accumulated.
        /// The document shape (property names and order) is unchanged from the previous in-memory build.
        /// </summary>
        private async Task WriteDocumentAsync(Stream stream, IServiceProvider sp, ProviderAccount account, CancellationToken ct)
        {
            // Credentials are deliberately never read: the export contains no secrets or tokens.
            IReadOnlyList<Location> locations = await sp.GetRequiredService<ILocationRepository>().ListByProviderAccountIdAsync(account.Id, ct);
            IReadOnlyList<ProviderSyncRun> syncRuns = await sp.GetRequiredService<IProviderSyncRunRepository>().ListByProviderAccountIdAsync(account.Id, ct);

            IDeviceRepository deviceRepository = sp.GetRequiredService<IDeviceRepository>();
            IEventRepository eventRepository = sp.GetRequiredService<IEventRepository>();
            IMediaItemRepository mediaRepository = sp.GetRequiredService<IMediaItemRepository>();
            IDownloadEventRepository downloadRepository = sp.GetRequiredService<IDownloadEventRepository>();

            // Devices are few; only the per-device event/media/download rows are large.
            var deviceIds = new List<Guid>();
            var devices = new List<object>();
            var seen = new HashSet<Guid>();
            foreach (Location location in locations)
            {
                foreach (Device device in await deviceRepository.GetByLocationIdAsync(location.Id, ct))
                {
                    if (!seen.Add(device.Id))
                    {
                        continue;
                    }

                    deviceIds.Add(device.Id);
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
                }
            }

            await using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
            writer.WriteStartObject();
            writer.WritePropertyName("exportedUtc");
            JsonSerializer.Serialize(writer, _time.GetUtcNow().UtcDateTime, JsonOptions);
            writer.WritePropertyName("account");
            JsonSerializer.Serialize(writer, new
            {
                id = account.Id,
                providerName = account.ProviderName,
                linkedUtc = account.LinkedUtc,
                removedUtc = account.RemovedUtc,
                lastSuccessfulAuthUtc = account.LastSuccessfulAuthUtc,
                isActive = account.IsActive,
                lastErrorMessage = account.LastErrorMessage
            }, JsonOptions);
            writer.WritePropertyName("locations");
            JsonSerializer.Serialize(writer, locations, JsonOptions);
            writer.WritePropertyName("syncRuns");
            JsonSerializer.Serialize(writer, syncRuns, JsonOptions);
            writer.WritePropertyName("devices");
            JsonSerializer.Serialize(writer, devices, JsonOptions);

            writer.WritePropertyName("events");
            writer.WriteStartArray();
            foreach (Guid deviceId in deviceIds)
            {
                ct.ThrowIfCancellationRequested();
                DateTime? earliest = await eventRepository.GetEarliestOccurredAtUtcAsync(deviceId, ct);
                if (earliest == null)
                {
                    continue;
                }

                // Cursor starts at the earliest event with Guid.Empty so every event at that instant is included.
                DateTime? afterAt = earliest;
                Guid? afterId = Guid.Empty;
                while (true)
                {
                    ct.ThrowIfCancellationRequested();
                    IReadOnlyList<Event> page = await eventRepository.ListByDevicePageAsync(deviceId, afterAt, afterId, EventPageSize, ct);
                    if (page.Count == 0)
                    {
                        break;
                    }

                    foreach (Event @event in page)
                    {
                        JsonSerializer.Serialize(writer, @event, JsonOptions);
                    }

                    afterAt = page[^1].OccurredAtUtc;
                    afterId = page[^1].Id;
                    await writer.FlushAsync(ct);
                }
            }

            writer.WriteEndArray();

            writer.WritePropertyName("downloadEvents");
            writer.WriteStartArray();
            foreach (Guid deviceId in deviceIds)
            {
                ct.ThrowIfCancellationRequested();
                foreach (DownloadEvent downloadEvent in await downloadRepository.GetByDeviceIdAsync(deviceId, ct))
                {
                    JsonSerializer.Serialize(writer, downloadEvent, JsonOptions);
                }

                await writer.FlushAsync(ct);
            }

            writer.WriteEndArray();

            writer.WritePropertyName("mediaItems");
            writer.WriteStartArray();
            foreach (Guid deviceId in deviceIds)
            {
                ct.ThrowIfCancellationRequested();
                foreach (MediaItem mediaItem in await mediaRepository.GetByDeviceIdAsync(deviceId, ct))
                {
                    JsonSerializer.Serialize(writer, mediaItem, JsonOptions);
                }

                await writer.FlushAsync(ct);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
            await writer.FlushAsync(ct);
        }
    }
}
