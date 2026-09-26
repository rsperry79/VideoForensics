using Microsoft.Data.Sqlite;

using Moq;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Providers.Common.Helpers.Platform;

using Xunit;

namespace VideoForensics.Hosting.Tests
{
    public class NetworkTierConfigReaderTests
    {
        [Fact]
        public void ReadConfiguredTier_NoDbFileAtConfiguredPath_ReturnsLocal()
        {
            // Arrange: point the resolver at a temp directory that genuinely has no
            // videoforensics.db in it - simulates first run / fresh install.
            string tempDir = Path.Combine(Path.GetTempPath(), "vf-nettier-tests-" + Guid.NewGuid());
            Directory.CreateDirectory(tempDir);

            try
            {
                var storageProviderMock = new Mock<IStorageLocationProvider>();
                storageProviderMock.Setup(p => p.GetDefaultRoot(StorageCategory.Database)).Returns(tempDir);

                var reader = new NetworkTierConfigReader(storageProviderMock.Object);

                NetworkTier result = reader.ReadConfiguredTier();

                Assert.Equal(NetworkTier.Local, result);
            }
            finally
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }

        [Fact]
        public void ReadConfiguredTier_UsesStorageLocationProviderPath_NotHardcodedDefault()
        {
            // Regression test for the bug: the old code hardcoded %ProgramData%\VideoForensics -
            // this proves the reader actually consults IStorageLocationProvider's configured path
            // (which reflects a custom install-time override) instead of any hardcoded default.
            string tempDir = Path.Combine(Path.GetTempPath(), "vf-nettier-tests-" + Guid.NewGuid());
            Directory.CreateDirectory(tempDir);
            string dbPath = Path.Combine(tempDir, "videoforensics.db");

            try
            {
                CreateDatabaseWithSetting(dbPath, "ConfiguredNetworkTier", "Network");

                var storageProviderMock = new Mock<IStorageLocationProvider>();
                storageProviderMock.Setup(p => p.GetDefaultRoot(StorageCategory.Database)).Returns(tempDir);

                var reader = new NetworkTierConfigReader(storageProviderMock.Object);

                NetworkTier result = reader.ReadConfiguredTier();

                Assert.Equal(NetworkTier.Network, result);
            }
            finally
            {
                SqliteConnection.ClearAllPools();
                Directory.Delete(tempDir, recursive: true);
            }
        }

        [Fact]
        public void ReadConfiguredTier_UnparseableValue_ReturnsLocal()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "vf-nettier-tests-" + Guid.NewGuid());
            Directory.CreateDirectory(tempDir);
            string dbPath = Path.Combine(tempDir, "videoforensics.db");

            try
            {
                CreateDatabaseWithSetting(dbPath, "ConfiguredNetworkTier", "NotARealTier");

                var storageProviderMock = new Mock<IStorageLocationProvider>();
                storageProviderMock.Setup(p => p.GetDefaultRoot(StorageCategory.Database)).Returns(tempDir);

                var reader = new NetworkTierConfigReader(storageProviderMock.Object);

                NetworkTier result = reader.ReadConfiguredTier();

                Assert.Equal(NetworkTier.Local, result);
            }
            finally
            {
                SqliteConnection.ClearAllPools();
                Directory.Delete(tempDir, recursive: true);
            }
        }

        [Fact]
        public void ReadConfiguredTier_NoMatchingKey_ReturnsLocal()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "vf-nettier-tests-" + Guid.NewGuid());
            Directory.CreateDirectory(tempDir);
            string dbPath = Path.Combine(tempDir, "videoforensics.db");

            try
            {
                CreateDatabaseWithSetting(dbPath, "SomeOtherKey", "Network");

                var storageProviderMock = new Mock<IStorageLocationProvider>();
                storageProviderMock.Setup(p => p.GetDefaultRoot(StorageCategory.Database)).Returns(tempDir);

                var reader = new NetworkTierConfigReader(storageProviderMock.Object);

                NetworkTier result = reader.ReadConfiguredTier();

                Assert.Equal(NetworkTier.Local, result);
            }
            finally
            {
                SqliteConnection.ClearAllPools();
                Directory.Delete(tempDir, recursive: true);
            }
        }

        private static void CreateDatabaseWithSetting(string dbPath, string key, string value)
        {
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();

            using SqliteCommand createTable = connection.CreateCommand();
            createTable.CommandText = "CREATE TABLE AppSettings (Key TEXT PRIMARY KEY, Value TEXT)";
            createTable.ExecuteNonQuery();

            using SqliteCommand insert = connection.CreateCommand();
            insert.CommandText = "INSERT INTO AppSettings (Key, Value) VALUES ($key, $value)";
            insert.Parameters.AddWithValue("$key", key);
            insert.Parameters.AddWithValue("$value", value);
            insert.ExecuteNonQuery();
        }
    }
}
