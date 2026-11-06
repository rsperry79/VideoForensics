using Microsoft.Data.Sqlite;

using VideoForensics.Data.Database.Sqlite.Migrations;

using Xunit;

namespace VideoForensics.Data.Database.Sqlite.Tests.Migrations
{
    /// <summary>Runs the capability backfill against a small database and checks which resolution each device gets.</summary>
    public class BackfillDeviceCapabilitiesResolutionTests
    {
        [Fact]
        public void Backfill_SetsLargestEventResolutionByPixelCount_NotByText()
        {
            using SqliteConnection connection = new("Data Source=:memory:");
            connection.Open();
            Exec(connection, "CREATE TABLE DeviceCapabilities (Id TEXT PRIMARY KEY, DeviceId TEXT NOT NULL, Resolution TEXT NULL);");
            Exec(connection, "CREATE TABLE MediaItems (Id TEXT PRIMARY KEY, DeviceId TEXT NOT NULL, MediaFormat TEXT NOT NULL, Resolution TEXT NULL);");

            const string withEvents = "11111111-1111-1111-1111-111111111111";
            const string snapshotOnly = "22222222-2222-2222-2222-222222222222";
            const string alreadySet = "33333333-3333-3333-3333-333333333333";
            Exec(connection, $"INSERT INTO DeviceCapabilities VALUES ('a1', '{withEvents}', NULL), ('a2', '{snapshotOnly}', NULL), ('a3', '{alreadySet}', '1280x720');");
            Exec(connection,
                "INSERT INTO MediaItems VALUES " +
                $"('m1', '{withEvents}', 'mp4', '640x360'), " +
                $"('m2', '{withEvents}', 'mp4', '1920x1080'), " +
                $"('m3', '{withEvents}', 'mp4', '1280x720'), " +
                $"('m4', '{withEvents}', 'jpg', '3840x2160'), " +
                $"('m5', '{snapshotOnly}', 'jpg', '640x360'), " +
                $"('m6', '{alreadySet}', 'mp4', '1920x1080');");

            Exec(connection, BackfillDeviceCapabilitiesResolution.Sql);

            Assert.Equal("1920x1080", Scalar(connection, $"SELECT Resolution FROM DeviceCapabilities WHERE DeviceId = '{withEvents}'"));
            Assert.Null(Scalar(connection, $"SELECT Resolution FROM DeviceCapabilities WHERE DeviceId = '{snapshotOnly}'"));
            Assert.Equal("1280x720", Scalar(connection, $"SELECT Resolution FROM DeviceCapabilities WHERE DeviceId = '{alreadySet}'"));
        }

        private static void Exec(SqliteConnection connection, string sql)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = sql;
            _ = command.ExecuteNonQuery();
        }

        private static object? Scalar(SqliteConnection connection, string sql)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = sql;
            object? value = command.ExecuteScalar();
            return value is DBNull ? null : value;
        }
    }
}
