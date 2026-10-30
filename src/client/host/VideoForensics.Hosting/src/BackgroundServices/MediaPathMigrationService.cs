using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.Hosting.BackgroundServices
{
    /// <summary>
    /// One pass at startup that rewrites media rows holding a full path (written before paths were relative to the media
    /// root) to the relative form. A row whose path is not under the media root is left as it is: it still resolves, because
    /// a rooted path passes through the resolver unchanged. Safe to run on every start; a converted row is skipped.
    ///
    /// Registered in server-tier hosts only, beside the other media backfills.
    /// </summary>
    public class MediaPathMigrationService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<MediaPathMigrationService> _logger;

        /// <summary>Creates the service.</summary>
        public MediaPathMigrationService(IServiceScopeFactory scopeFactory, ILogger<MediaPathMigrationService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await RunPassAsync(stoppingToken);
        }

        /// <summary>Runs the pass in its own scope. A failure is logged and not rethrown, so startup is unaffected.</summary>
        public async Task RunPassAsync(CancellationToken ct)
        {
            try
            {
                using IServiceScope scope = _scopeFactory.CreateScope();
                IMediaItemRepository items = scope.ServiceProvider.GetRequiredService<IMediaItemRepository>();
                IMediaPathResolver paths = scope.ServiceProvider.GetRequiredService<IMediaPathResolver>();

                IReadOnlyList<MediaItem> all = await items.ListAsync(ct);
                int converted = 0;
                foreach (MediaItem item in all.Where(m => Path.IsPathRooted(m.FilePath) || (m.PreviewPath is not null && Path.IsPathRooted(m.PreviewPath))))
                {
                    ct.ThrowIfCancellationRequested();

                    string filePath = await paths.ToStoredAsync(item.FilePath, ct);
                    string? previewPath = item.PreviewPath is null ? null : await paths.ToStoredAsync(item.PreviewPath, ct);
                    if (filePath == item.FilePath && previewPath == item.PreviewPath)
                    {
                        continue;
                    }

                    item.FilePath = filePath;
                    item.PreviewPath = previewPath;
                    await items.UpdateAsync(item, ct);
                    converted++;
                }

                _logger.LogInformation("Media path migration: {Converted} of {Total} row(s) converted to paths relative to the media root", converted, all.Count);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError("Media path migration failed: {Message} ({ExceptionType})", ex.Message, ex.GetType().Name);
            }
        }
    }
}
