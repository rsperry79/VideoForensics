using Microsoft.Data.Sqlite;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Providers.Common.Helpers.Platform;

namespace VideoForensics.Hosting
{
    /// <summary>
    /// Reads the persisted network-tier setting directly from SQLite via a lightweight ADO.NET
    /// connection, bypassing the normal EF/DI stack. Exists solely for
    /// <c>Program.cs</c>'s pre-host-build Kestrel bind decision (see the comment above its call
    /// site there): which interfaces Kestrel binds to must be decided before
    /// <c>WebApplicationBuilder</c> exists, so this can't wait for the normal configuration
    /// pipeline the way every other setting in the app does.
    /// </summary>
    public interface INetworkTierConfigReader
    {
        /// <summary>
        /// Reads the currently-configured <see cref="NetworkTier"/> from the database, or
        /// <see cref="NetworkTier.Local"/> if the database doesn't exist yet, has no
        /// configured value, or any error occurs while reading it.
        /// </summary>
        NetworkTier ReadConfiguredTier();
    }

    public class NetworkTierConfigReader : INetworkTierConfigReader
    {
        private readonly IStorageLocationProvider _storageLocationProvider;

        public NetworkTierConfigReader(IStorageLocationProvider storageLocationProvider)
        {
            _storageLocationProvider = storageLocationProvider;
        }

        public NetworkTier ReadConfiguredTier()
        {
            try
            {
                // Resolve via IStorageLocationProvider - the ONE place in the codebase that already
                // knows how to honor a custom Database location (Windows registry override or the
                // Linux VIDEOFORENSICS_DATABASE_PATH env var), instead of hardcoding the default
                // %ProgramData%/VideoForensics path and silently ignoring any configured override.
                string dbPath = Path.Combine(_storageLocationProvider.GetDefaultRoot(StorageCategory.Database), "videoforensics.db");
                if (!File.Exists(dbPath))
                {
                    return NetworkTier.Local;
                }

                using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
                connection.Open();
                using SqliteCommand command = connection.CreateCommand();
                command.CommandText = "SELECT Value FROM AppSettings WHERE Key = 'ConfiguredNetworkTier' LIMIT 1";
                string? value = command.ExecuteScalar() as string;
                return Enum.TryParse<NetworkTier>(value, out NetworkTier tier) ? tier : NetworkTier.Local;
            }
            catch
            {
                // Any failure here (DB locked by another process, table not created yet, corrupt row) falls
                // back to the safest default rather than risking an unintended wide-open bind.
                return NetworkTier.Local;
            }
        }
    }
}
