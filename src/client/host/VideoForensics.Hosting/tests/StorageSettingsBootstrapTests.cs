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
        public void Read_SavedValues_ReturnsAllThree()
        {
            _ = Directory.CreateDirectory(_dbRoot);
            using (var connection = new SqliteConnection("Data Source=" + Path.Combine(_dbRoot, "videoforensics.db")))
            {
                connection.Open();
                using SqliteCommand create = connection.CreateCommand();
                create.CommandText = "CREATE TABLE AppSettings (Key TEXT PRIMARY KEY, Value TEXT)";
                _ = create.ExecuteNonQuery();
                using SqliteCommand insert = connection.CreateCommand();
                insert.CommandText = "INSERT INTO AppSettings (Key, Value) VALUES ('KeysLocation', 'G:/Saved/Keys'), ('LogsLocation', 'G:/Saved/Logs'), ('DefaultStorageRoot', 'G:/Saved/Root')";
                _ = insert.ExecuteNonQuery();
            }

            DatabaseLocationPointerStore.Current = new FixedPointerStore(_dbRoot);
            var bootstrap = new StorageSettingsBootstrap(new StorageLocationProvider());

            SavedStorageSettings saved = bootstrap.Read();
            Assert.Equal("G:/Saved/Keys", saved.KeysRoot);
            Assert.Equal("G:/Saved/Logs", saved.LogsRoot);
            Assert.Equal("G:/Saved/Root", saved.DefaultRoot);
        }

        [Fact]
        public void Read_NoDatabase_ReturnsNulls()
        {
            DatabaseLocationPointerStore.Current = new FixedPointerStore(_dbRoot);
            var bootstrap = new StorageSettingsBootstrap(new StorageLocationProvider());

            SavedStorageSettings saved = bootstrap.Read();
            Assert.Null(saved.KeysRoot);
            Assert.Null(saved.LogsRoot);
            Assert.Null(saved.DefaultRoot);
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
