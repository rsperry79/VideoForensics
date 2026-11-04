using Microsoft.Data.Sqlite;

using VideoForensics.Core.Helpers.Platform;

namespace VideoForensics.Client.Hosting
{
    /// <summary>
    /// Reads the saved storage locations from the database before any key loads. The database is found through the bootstrap
    /// pointer, so this runs after the pointer is known and before the Keys folder is resolved. Missing database, table or
    /// value gives null, so the provider uses its legacy pointer or the default.
    /// </summary>
    public sealed class StorageSettingsBootstrap(IStorageLocationProvider storageLocationProvider)
    {
        /// <summary>Returns the saved Keys folder, or null when it is not saved or cannot be read.</summary>
        public string? ReadKeysRoot()
        {
            try
            {
                string dbPath = Path.Combine(storageLocationProvider.GetDefaultRoot(StorageCategory.Database), "videoforensics.db");
                if (!File.Exists(dbPath))
                {
                    return null;
                }

                using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
                connection.Open();
                using SqliteCommand command = connection.CreateCommand();
                command.CommandText = "SELECT Value FROM AppSettings WHERE Key = 'KeysLocation' LIMIT 1";
                string? value = command.ExecuteScalar() as string;
                return string.IsNullOrWhiteSpace(value) ? null : value;
            }
            catch
            {
                // A locked, missing or unexpected database must not stop startup. The legacy pointer or default is used.
                return null;
            }
        }
    }
}
