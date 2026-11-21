using Microsoft.Data.Sqlite;

using Xunit;

namespace VideoForensics.Utils.SelfTest.Tests
{
    /// <summary>
    /// The ding-id endpoints need a real ding for the doorbot under test. The tester takes the newest ready ding stored for
    /// that doorbot, read-only, so a run needs no manual --ding-id.
    /// </summary>
    public sealed class LatestDingTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "vf-latest-ding-" + Guid.NewGuid().ToString("N"));
        private readonly string _db;

        public LatestDingTests()
        {
            _ = Directory.CreateDirectory(_dir);
            _db = Path.Combine(_dir, "test.db");
            using var connection = new SqliteConnection($"Data Source={_db}");
            connection.Open();
            using SqliteCommand create = connection.CreateCommand();
            create.CommandText = "CREATE TABLE RingLocationEvents (Id TEXT PRIMARY KEY, DoorbotId INTEGER, DingIdString TEXT, RecordingStatus TEXT);";
            _ = create.ExecuteNonQuery();
            Insert(connection, "a", 561158930, "1001", "ready");
            Insert(connection, "b", 561158930, "1002", "ready");
            Insert(connection, "c", 561158930, "1003", "failed");
            Insert(connection, "d", 86401464, "2001", "ready");
            Insert(connection, "e", 561158930, null, "ready");
        }

        private static void Insert(SqliteConnection connection, string id, long doorbot, string? ding, string status)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "INSERT INTO RingLocationEvents (Id, DoorbotId, DingIdString, RecordingStatus) VALUES ($id, $d, $ding, $status);";
            _ = command.Parameters.AddWithValue("$id", id);
            _ = command.Parameters.AddWithValue("$d", doorbot);
            _ = command.Parameters.AddWithValue("$ding", (object?)ding ?? DBNull.Value);
            _ = command.Parameters.AddWithValue("$status", status);
            _ = command.ExecuteNonQuery();
        }

        [Fact]
        public void Find_ReturnsNewestReadyDingForTheDoorbot()
        {
            Assert.Equal("1002", LatestDing.Find(_db, 561158930));
        }

        [Fact]
        public void Find_IgnoresOtherDoorbots()
        {
            Assert.Equal("2001", LatestDing.Find(_db, 86401464));
        }

        [Fact]
        public void Find_ReturnsNullWhenTheDoorbotHasNoReadyDing()
        {
            Assert.Null(LatestDing.Find(_db, 999));
        }

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(_dir, recursive: true);
        }
    }
}
