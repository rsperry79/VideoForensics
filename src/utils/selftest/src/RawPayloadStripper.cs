using System.Globalization;
using System.Text.Json;

using Microsoft.Data.Sqlite;

using VideoForensics.Data.Database.Sqlite.DependencyInjection;
using VideoForensics.Providers.Ring.Services;

namespace VideoForensics.Utils.SelfTest
{
    /// <summary>Row counts for one table. No values are ever reported.</summary>
    /// <param name="Scanned">Rows with a non-null MetadataJson.</param>
    /// <param name="Rewritten">Rows whose MetadataJson changed (including those set to null).</param>
    /// <param name="SetToNull">Rewritten rows that had nothing but structured fields and are now null.</param>
    /// <param name="OtherKept">Scanned rows left alone: app markers, text that is not JSON, other shapes.</param>
    internal sealed record StripTableCounts(int Scanned, int Rewritten, int SetToNull, int OtherKept);

    /// <summary>The outcome of <see cref="RawPayloadStripper.Run"/>; <see cref="Refusal"/> is set when nothing was changed.</summary>
    internal sealed record StripRawPayloadResult(bool Success, string? Refusal, string? BackupPath, StripTableCounts Events, StripTableCounts MediaItems);

    /// <summary>
    /// Opt-in maintenance (<c>--strip-raw-payload</c>): reduces the raw Ring event payload still held in
    /// <c>Events.MetadataJson</c> and <c>MediaItems.MetadataJson</c> to its unknown keys (null when none), per
    /// <see cref="RingEventPayloadRemainder"/>. App markers (<c>{"type":"snapshot",...}</c>, the roll-up marker) and anything
    /// that is not a raw event payload are left byte-for-byte. It never runs on its own and is not a migration.
    /// </summary>
    /// <remarks>
    /// Safeguards: it needs an explicit <c>--yes</c>, an existing non-empty dump file with at least one line per row it will
    /// rewrite (the dump is the only copy of the raw payloads afterwards, apart from the backup), and it copies the database
    /// first with the SQLite online backup (consistent even with a WAL file). All updates run in one transaction.
    /// </remarks>
    internal static class RawPayloadStripper
    {
        private static readonly string[] Tables = ["Events", "MediaItems"];

        public static StripRawPayloadResult Run(string? dbPath, string? dumpFile, bool yes, Func<DateTime>? utcNow = null)
        {
            if (!yes)
            {
                return Refuse("Refusing to run without --yes. This rewrites Events.MetadataJson and MediaItems.MetadataJson.");
            }

            if (string.IsNullOrWhiteSpace(dbPath) || !File.Exists(dbPath))
            {
                return Refuse("Database file not found. Pass --db-path <file>.");
            }

            if (string.IsNullOrWhiteSpace(dumpFile) || !File.Exists(dumpFile) || new FileInfo(dumpFile).Length == 0)
            {
                return Refuse("Refusing to run: --dump-file must name an existing, non-empty dump of the current raw payloads.");
            }

            try
            {
                return RunCore(dbPath, dumpFile, utcNow ?? (() => DateTime.UtcNow));
            }
            catch (Exception ex) when (ex is SqliteException or IOException or UnauthorizedAccessException)
            {
                return Refuse($"Stopped, nothing was committed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        /// <summary>One line: counts per table, then the backup path. No JSON values.</summary>
        public static void WriteSummary(StripRawPayloadResult result, TextWriter output)
        {
            if (!result.Success)
            {
                output.WriteLine(result.Refusal);
                return;
            }

            output.WriteLine($"Events.MetadataJson: scanned {result.Events.Scanned}, rewritten {result.Events.Rewritten} ({result.Events.SetToNull} now null), left as is {result.Events.OtherKept}");
            output.WriteLine($"MediaItems.MetadataJson: scanned {result.MediaItems.Scanned}, rewritten {result.MediaItems.Rewritten} ({result.MediaItems.SetToNull} now null), left as is {result.MediaItems.OtherKept}");
            output.WriteLine($"Backup: {result.BackupPath}");
        }

        private static StripRawPayloadResult Refuse(string reason)
        {
            return new StripRawPayloadResult(false, reason, null, Empty, Empty);
        }

        private static readonly StripTableCounts Empty = new(0, 0, 0, 0);

        private static StripRawPayloadResult RunCore(string dbPath, string dumpFile, Func<DateTime> utcNow)
        {
            // Pooling off: the connections must release the file when the command ends (a pooled handle would keep it open).
            string connectionString = SqliteConnectionStrings.ForFile(dbPath).Replace("Pooling=true", "Pooling=false", StringComparison.Ordinal);
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            foreach (string table in Tables)
            {
                if (!ColumnExists(connection, table, "MetadataJson"))
                {
                    return Refuse($"Table {table} has no MetadataJson column; this is not a VideoForensics database. Nothing was changed.");
                }
            }

            // Plan first (read-only) so the dump check and the backup happen before anything is written.
            var plans = new Dictionary<string, List<(long RowId, string? NewJson)>>(StringComparer.Ordinal);
            var counts = new Dictionary<string, StripTableCounts>(StringComparer.Ordinal);
            int rowsToRewrite = 0;
            foreach (string table in Tables)
            {
                (List<(long, string?)> changes, StripTableCounts tableCounts) = Plan(connection, table);
                plans[table] = changes;
                counts[table] = tableCounts;
                rowsToRewrite += changes.Count;
            }

            int dumpLines = File.ReadLines(dumpFile).Count(l => !string.IsNullOrWhiteSpace(l));
            if (dumpLines < rowsToRewrite)
            {
                return Refuse($"Refusing to run: the dump file has {dumpLines} lines but {rowsToRewrite} rows would be rewritten. Nothing was changed.");
            }

            string backupPath = BackupPathFor(dbPath, utcNow());
            using (var backup = new SqliteConnection($"Data Source={backupPath};Pooling=false"))
            {
                backup.Open();
                connection.BackupDatabase(backup);
            }

            using SqliteTransaction transaction = connection.BeginTransaction();
            foreach (string table in Tables)
            {
                foreach ((long rowId, string? newJson) in plans[table])
                {
                    using SqliteCommand update = connection.CreateCommand();
                    update.Transaction = transaction;
                    update.CommandText = $"UPDATE \"{table}\" SET \"MetadataJson\" = $json WHERE rowid = $rowid";
                    _ = update.Parameters.AddWithValue("$json", (object?)newJson ?? DBNull.Value);
                    _ = update.Parameters.AddWithValue("$rowid", rowId);
                    _ = update.ExecuteNonQuery();
                }
            }

            transaction.Commit();
            return new StripRawPayloadResult(true, null, backupPath, counts["Events"], counts["MediaItems"]);
        }

        private static (List<(long RowId, string? NewJson)> Changes, StripTableCounts Counts) Plan(SqliteConnection connection, string table)
        {
            var changes = new List<(long, string?)>();
            int scanned = 0;
            int setToNull = 0;
            int otherKept = 0;

            using SqliteCommand select = connection.CreateCommand();
            select.CommandText = $"SELECT rowid, \"MetadataJson\" FROM \"{table}\" WHERE \"MetadataJson\" IS NOT NULL";
            using SqliteDataReader reader = select.ExecuteReader();
            while (reader.Read())
            {
                scanned++;
                long rowId = reader.GetInt64(0);
                string json = reader.GetValue(1) as string ?? string.Empty;

                if (!TryParseRawEvent(json))
                {
                    otherKept++;
                    continue;
                }

                string? remainder = RingEventPayloadRemainder.Compute(json);
                if (string.Equals(remainder, json, StringComparison.Ordinal))
                {
                    otherKept++;
                    continue;
                }

                changes.Add((rowId, remainder));
                if (remainder is null)
                {
                    setToNull++;
                }
            }

            return (changes, new StripTableCounts(scanned, changes.Count, setToNull, otherKept));
        }

        private static bool TryParseRawEvent(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(json);
                return RingEventPayloadRemainder.IsRawEventPayload(document.RootElement);
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static bool ColumnExists(SqliteConnection connection, string table, string column)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info(\"{table}\")";
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string BackupPathFor(string dbPath, DateTime utcNow)
        {
            string directory = Path.GetDirectoryName(Path.GetFullPath(dbPath)) ?? ".";
            string name = Path.GetFileNameWithoutExtension(dbPath);
            string extension = Path.GetExtension(dbPath);
            string stamp = utcNow.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
            string candidate = Path.Combine(directory, $"{name}.pre-strip-{stamp}{extension}");
            for (int i = 2; File.Exists(candidate); i++)
            {
                candidate = Path.Combine(directory, $"{name}.pre-strip-{stamp}-{i}{extension}");
            }

            return candidate;
        }
    }
}
