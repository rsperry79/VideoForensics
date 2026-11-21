using VideoForensics.Client.Common.Contracts;
using VideoForensics.Core.Helpers.Platform;

namespace VideoForensics.Client.WebApp.Services
{
    /// <summary>
    /// Writes backup exports to the Backup folder saved in the database (QueryExportLocation), or the storage default
    /// when none is saved. The folder is created on demand, so it exists before the export writes to it.
    /// </summary>
    public sealed class ConfiguredBackupStagingLocator : IBackupStagingLocator
    {
        private readonly IForensicsConfigurationService _configService;
        private readonly IStorageLocationProvider _storageLocations;

        public ConfiguredBackupStagingLocator(IForensicsConfigurationService configService, IStorageLocationProvider storageLocations)
        {
            _configService = configService;
            _storageLocations = storageLocations;
        }

        /// <inheritdoc />
        public async Task<string> GetExportDirectoryAsync(CancellationToken ct)
        {
            IForensicsConfiguration config = await _configService.LoadConfigurationAsync(string.Empty, ct);
            string root = _storageLocations.GetEffectiveRoot(StorageCategory.Backup, config.QueryExportLocation);
            _ = Directory.CreateDirectory(root);
            return root;
        }
    }
}
