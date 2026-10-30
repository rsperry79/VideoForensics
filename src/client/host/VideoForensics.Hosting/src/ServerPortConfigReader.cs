using Microsoft.Data.Sqlite;

using VideoForensics.Core.Helpers.Platform;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.Hosting
{
    /// <summary>The HTTP and HTTPS ports saved on the Network Access page. 0 means no port is saved for that listener.</summary>
    public sealed record SavedServerPorts(int HttpPort, int HttpsPort);

    /// <summary>
    /// Reads the saved listen ports straight from SQLite, the same way <see cref="NetworkTierConfigReader"/> reads the
    /// tier. Program.cs needs them before Kestrel binds, so this cannot wait for the normal configuration pipeline.
    /// </summary>
    public class ServerPortConfigReader
    {
        private readonly IStorageLocationProvider _storageLocationProvider;

        public ServerPortConfigReader(IStorageLocationProvider storageLocationProvider)
        {
            _storageLocationProvider = storageLocationProvider;
        }

        public SavedServerPorts ReadSavedPorts()
        {
            try
            {
                string dbPath = Path.Combine(_storageLocationProvider.GetDefaultRoot(StorageCategory.Database), "videoforensics.db");
                if (!File.Exists(dbPath))
                {
                    return new SavedServerPorts(0, 0);
                }

                using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
                connection.Open();
                using SqliteCommand command = connection.CreateCommand();
                command.CommandText = "SELECT Key, Value FROM AppSettings WHERE Key IN ('ServerHttpPort', 'ServerHttpsPort')";
                int httpPort = 0;
                int httpsPort = 0;
                using SqliteDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    string key = reader.GetString(0);
                    if (!int.TryParse(reader.GetString(1), out int port) || port is < 1 or > 65535)
                    {
                        continue;
                    }

                    if (key == "ServerHttpPort")
                    {
                        httpPort = port;
                    }
                    else if (key == "ServerHttpsPort")
                    {
                        httpsPort = port;
                    }
                }

                return new SavedServerPorts(httpPort, httpsPort);
            }
            catch
            {
                // Any failure falls back to the urls configuration, the same safe default the tier reader uses.
                return new SavedServerPorts(0, 0);
            }
        }
    }
}
