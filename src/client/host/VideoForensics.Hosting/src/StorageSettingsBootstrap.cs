using Microsoft.Data.Sqlite;

using VideoForensics.Core.Helpers.Platform;

namespace VideoForensics.Client.Hosting
{
    /// <summary>The storage locations saved in the database. Each is null when it is not saved.</summary>
    public sealed record SavedStorageSettings(string? KeysRoot, string? LogsRoot, string? DefaultRoot);

    /// <summary>
    /// Reads the saved storage locations from the database before any key loads or log file opens. The database is found
    /// through the bootstrap pointer, so this runs after the pointer is known. A missing database, table or value gives
    /// null, so the provider uses its legacy pointer or the default.
    /// </summary>
    public sealed class StorageSettingsBootstrap(IStorageLocationProvider storageLocationProvider)
    {
        /// <summary>Returns the saved storage locations, each null when missing or unreadable.</summary>
        public SavedStorageSettings Read()
        {
            try
            {
                string dbPath = Path.Combine(storageLocationProvider.GetDefaultRoot(StorageCategory.Database), "videoforensics.db");
                if (!File.Exists(dbPath))
                {
                    return new SavedStorageSettings(null, null, null);
                }

                using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
                connection.Open();
                return new SavedStorageSettings(
                    ReadValue(connection, "KeysLocation"),
                    ReadValue(connection, "LogsLocation"),
                    ReadValue(connection, "DefaultStorageRoot"));
            }
            catch
            {
                // A locked, missing or unexpected database must not stop startup. The legacy pointers or defaults are used.
                return new SavedStorageSettings(null, null, null);
            }
        }

        private static string? ReadValue(SqliteConnection connection, string key)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT Value FROM AppSettings WHERE Key = $key LIMIT 1";
            _ = command.Parameters.AddWithValue("$key", key);
            string? value = command.ExecuteScalar() as string;
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
    }
}
