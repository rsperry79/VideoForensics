using Microsoft.Data.Sqlite;

using VideoForensics.Core.Helpers.Platform;

using Xunit;

namespace VideoForensics.Client.Hosting.Tests
{
    public class StorageSettingsBootstrap_Tests : IDisposable
    {
        private readonly IDatabaseLocationPointerStore _previous = DatabaseLocationPointerStore.Current;
        private readonly string _dbRoot = Path.Combine(Path.GetTempPath(), "vf-bootstrap-" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            DatabaseLocationPointerStore.Current = _previous;
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(_dbRoot))
            {
                Directory.Delete(_dbRoot, recursive: true);
            }
        }

        [Fact]
        public void ReadKeysRoot_SavedValue_ReturnsIt()
        {
            _ = Directory.CreateDirectory(_dbRoot);
            using (var connection = new SqliteConnection("Data Source=" + Path.Combine(_dbRoot, "videoforensics.db")))
            {
                connection.Open();
                using SqliteCommand create = connection.CreateCommand();
                create.CommandText = "CREATE TABLE AppSettings (Key TEXT PRIMARY KEY, Value TEXT)";
                _ = create.ExecuteNonQuery();
                using SqliteCommand insert = connection.CreateCommand();
                insert.CommandText = "INSERT INTO AppSettings (Key, Value) VALUES ('KeysLocation', 'G:/Saved/Keys')";
                _ = insert.ExecuteNonQuery();
            }

            DatabaseLocationPointerStore.Current = new FixedPointerStore(_dbRoot);
            var bootstrap = new StorageSettingsBootstrap(new StorageLocationProvider());

            Assert.Equal("G:/Saved/Keys", bootstrap.ReadKeysRoot());
        }

        [Fact]
        public void ReadKeysRoot_NoDatabase_ReturnsNull()
        {
            DatabaseLocationPointerStore.Current = new FixedPointerStore(_dbRoot);
            var bootstrap = new StorageSettingsBootstrap(new StorageLocationProvider());

            Assert.Null(bootstrap.ReadKeysRoot());
        }

        private sealed class FixedPointerStore(string root) : IDatabaseLocationPointerStore
        {
            public string? Read() => root;

            public void Write(string databaseRoot)
            {
            }
        }
    }
}
