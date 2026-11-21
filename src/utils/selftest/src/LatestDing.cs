using Microsoft.Data.Sqlite;

namespace VideoForensics.Utils.SelfTest
{
    /// <summary>
    /// Reads the newest ready ding stored for one doorbot. The read is read-only and needs no credentials, so the tester
    /// can use a real ding for the ding-id endpoints without a --ding-id argument.
    /// </summary>
    internal static class LatestDing
    {
        /// <summary>Returns the newest ready <c>DingIdString</c> for the doorbot, or null when none is stored.</summary>
        internal static string? Find(string dbPath, long doorbotId)
        {
            using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
            connection.Open();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT DingIdString FROM RingLocationEvents " +
                                  "WHERE DoorbotId = $doorbot AND DingIdString IS NOT NULL AND RecordingStatus = 'ready' " +
                                  "ORDER BY rowid DESC LIMIT 1;";
            _ = command.Parameters.AddWithValue("$doorbot", doorbotId);
            return command.ExecuteScalar() as string;
        }
    }
}
