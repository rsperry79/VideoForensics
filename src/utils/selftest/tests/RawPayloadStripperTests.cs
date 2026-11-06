using Microsoft.Data.Sqlite;

using Xunit;

namespace VideoForensics.Utils.SelfTest.Tests
{
    /// <summary>
    /// The opt-in --strip-raw-payload maintenance command: refuses without its safeguards, backs up first, then reduces
    /// raw Ring event payloads in Events/MediaItems.MetadataJson to their unknown keys and leaves app markers untouched.
    /// </summary>
    public sealed class RawPayloadStripper_Tests : IDisposable
    {
        private const string RawKnownOnly = """{"id":1,"created_at":"2026-01-01T08:00:00.000Z","CreatedAtDateTime":"2026-01-01T00:00:00-08:00","answered":false,"events":[],"kind":"motion","doorbot":{"id":1,"description":"Front Door","device_id":null,"type":"x"},"cv_properties":{"person_detected":true,"detection_types":[{"detection_type":"human","verified_timestamps":[1]}],"profiles":null},"riid":null}""";
        private const string RawWithUnknown = """{"id":2,"created_at":"2026-01-01T08:00:00.000Z","kind":"motion","brand_new":{"a":[1,2]}}""";
        private const string SnapshotMarker = """{"type":"snapshot","source":"capture"}""";
        private const string SnapshotWithEventId = """{"deviceId":"86401464","eventId":"snapshot-20261021010331","timestamp":"2026-10-21T01:03:31.5564352Z","type":"snapshot"}""";
        private const string RollupMarker = """{"type":"snapshot-rollup"}""";

        private readonly string _folder = Path.Combine(Path.GetTempPath(), "strip-raw-tests-" + Guid.NewGuid().ToString("N"));
        private readonly string _dump;

        public RawPayloadStripper_Tests()
        {
            _ = Directory.CreateDirectory(_folder);
            _dump = Path.Combine(_folder, "dump.ndjson");
            File.WriteAllText(_dump, "{\"table\":\"Events\",\"id\":\"1\",\"metadata\":{}}\n{\"table\":\"Events\",\"id\":\"2\",\"metadata\":{}}\n{\"table\":\"MediaItems\",\"id\":\"3\",\"metadata\":{}}\n{\"table\":\"MediaItems\",\"id\":\"4\",\"metadata\":{}}\n{\"table\":\"MediaItems\",\"id\":\"5\",\"metadata\":{}}\n{\"table\":\"MediaItems\",\"id\":\"6\",\"metadata\":{}}\n{\"table\":\"MediaItems\",\"id\":\"7\",\"metadata\":{}}\n{\"table\":\"MediaItems\",\"id\":\"8\",\"metadata\":{}}\n");
        }

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(_folder))
            {
                Directory.Delete(_folder, recursive: true);
            }
        }

        private string CreateDb()
        {
            string path = Path.Combine(_folder, "vf.db");
            using var conn = new SqliteConnection($"Data Source={path}");
            conn.Open();
            Exec(conn, "CREATE TABLE Events (Id INTEGER PRIMARY KEY, Other TEXT, MetadataJson TEXT)");
            Exec(conn, "CREATE TABLE MediaItems (Id INTEGER PRIMARY KEY, Other TEXT, MetadataJson TEXT)");
            Insert(conn, "Events", 1, RawKnownOnly);
            Insert(conn, "Events", 2, RawWithUnknown);
            Insert(conn, "Events", 3, null);
            Insert(conn, "MediaItems", 1, RawKnownOnly);
            Insert(conn, "MediaItems", 2, SnapshotMarker);
            Insert(conn, "MediaItems", 3, SnapshotWithEventId);
            Insert(conn, "MediaItems", 4, RollupMarker);
            Insert(conn, "MediaItems", 5, "{not json");
            Insert(conn, "MediaItems", 6, null);
            return path;
        }

        private static void Exec(SqliteConnection conn, string sql)
        {
            using SqliteCommand cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            _ = cmd.ExecuteNonQuery();
        }

        private static void Insert(SqliteConnection conn, string table, int id, string? json)
        {
            using SqliteCommand cmd = conn.CreateCommand();
            cmd.CommandText = $"INSERT INTO {table} (Id, Other, MetadataJson) VALUES ($id, 'keep', $json)";
            _ = cmd.Parameters.AddWithValue("$id", id);
            _ = cmd.Parameters.AddWithValue("$json", (object?)json ?? DBNull.Value);
            _ = cmd.ExecuteNonQuery();
        }

        private static string? Read(string dbPath, string table, int id)
        {
            using var conn = new SqliteConnection($"Data Source={dbPath};Pooling=false");
            conn.Open();
            using SqliteCommand cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT MetadataJson FROM {table} WHERE Id = $id";
            _ = cmd.Parameters.AddWithValue("$id", id);
            object? value = cmd.ExecuteScalar();
            return value is null or DBNull ? null : (string)value;
        }

        private string[] Backups()
        {
            return Directory.GetFiles(_folder, "vf*pre-strip*");
        }

        [Fact]
        public void Parse_StripFlags_AreRead()
        {
            (CliOptions? options, string? error) = CliOptions.Parse(["--strip-raw-payload", "--db-path", "a.db", "--dump-file", "d.ndjson", "--yes"]);

            Assert.Null(error);
            Assert.True(options!.StripRawPayload);
            Assert.True(options.Yes);
            Assert.Equal("a.db", options.DbPath);
            Assert.Equal("d.ndjson", options.DumpFile);
        }

        [Fact]
        public void Parse_StripWithoutYes_LeavesYesFalse()
        {
            (CliOptions? options, _) = CliOptions.Parse(["--strip-raw-payload", "--db-path", "a.db", "--dump-file", "d.ndjson"]);

            Assert.False(options!.Yes);
        }

        [Fact]
        public void Run_WithoutYes_RefusesAndChangesNothing()
        {
            string db = CreateDb();

            StripRawPayloadResult result = RawPayloadStripper.Run(db, _dump, yes: false);

            Assert.False(result.Success);
            Assert.Contains("--yes", result.Refusal, StringComparison.Ordinal);
            Assert.Empty(Backups());
            Assert.Equal(RawKnownOnly, Read(db, "Events", 1));
        }

        [Fact]
        public void Run_DumpFileMissing_Refuses()
        {
            string db = CreateDb();

            StripRawPayloadResult result = RawPayloadStripper.Run(db, Path.Combine(_folder, "nope.ndjson"), yes: true);

            Assert.False(result.Success);
            Assert.Contains("dump", result.Refusal, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(Backups());
            Assert.Equal(RawKnownOnly, Read(db, "Events", 1));
        }

        [Fact]
        public void Run_DumpFileNotGivenOrEmpty_Refuses()
        {
            string db = CreateDb();
            string empty = Path.Combine(_folder, "empty.ndjson");
            File.WriteAllText(empty, string.Empty);

            Assert.False(RawPayloadStripper.Run(db, null, yes: true).Success);
            StripRawPayloadResult result = RawPayloadStripper.Run(db, empty, yes: true);

            Assert.False(result.Success);
            Assert.Empty(Backups());
            Assert.Equal(RawKnownOnly, Read(db, "Events", 1));
        }

        [Fact]
        public void Run_DumpHasFewerLinesThanRowsToRewrite_Refuses()
        {
            string db = CreateDb();
            string tiny = Path.Combine(_folder, "tiny.ndjson");
            File.WriteAllText(tiny, "{\"table\":\"Events\"}\n");

            StripRawPayloadResult result = RawPayloadStripper.Run(db, tiny, yes: true);

            Assert.False(result.Success);
            Assert.Empty(Backups());
            Assert.Equal(RawKnownOnly, Read(db, "Events", 1));
        }

        [Fact]
        public void Run_DatabaseMissingOrNotGiven_Refuses()
        {
            Assert.False(RawPayloadStripper.Run(Path.Combine(_folder, "missing.db"), _dump, yes: true).Success);
            Assert.False(RawPayloadStripper.Run(null, _dump, yes: true).Success);
            Assert.Empty(Backups());
        }

        [Fact]
        public void Run_TableWithoutMetadataColumn_RefusesAndChangesNothing()
        {
            string path = Path.Combine(_folder, "bad.db");
            using (var conn = new SqliteConnection($"Data Source={path};Pooling=false"))
            {
                conn.Open();
                Exec(conn, "CREATE TABLE Events (Id INTEGER PRIMARY KEY)");
            }

            StripRawPayloadResult result = RawPayloadStripper.Run(path, _dump, yes: true);

            Assert.False(result.Success);
            Assert.Empty(Directory.GetFiles(_folder, "bad*pre-strip*"));
        }

        [Fact]
        public void Run_WithAllSafeguards_ReducesRawEventsAndLeavesEverythingElse()
        {
            string db = CreateDb();

            StripRawPayloadResult result = RawPayloadStripper.Run(db, _dump, yes: true);

            Assert.True(result.Success, result.Refusal);
            Assert.Null(Read(db, "Events", 1));
            Assert.Equal("""{"brand_new":{"a":[1,2]}}""", Read(db, "Events", 2));
            Assert.Null(Read(db, "Events", 3));
            Assert.Null(Read(db, "MediaItems", 1));
            Assert.Equal(SnapshotMarker, Read(db, "MediaItems", 2));
            Assert.Equal(SnapshotWithEventId, Read(db, "MediaItems", 3));
            Assert.Equal(RollupMarker, Read(db, "MediaItems", 4));
            Assert.Equal("{not json", Read(db, "MediaItems", 5));
            Assert.Null(Read(db, "MediaItems", 6));

            // Only MetadataJson changes.
            using var conn = new SqliteConnection($"Data Source={db};Pooling=false");
            conn.Open();
            using SqliteCommand cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM Events WHERE Other = 'keep'";
            Assert.Equal(3L, cmd.ExecuteScalar());
        }

        [Fact]
        public void Run_CountsAreReportedPerTable()
        {
            string db = CreateDb();

            StripRawPayloadResult result = RawPayloadStripper.Run(db, _dump, yes: true);

            Assert.Equal(new StripTableCounts(Scanned: 2, Rewritten: 2, SetToNull: 1, OtherKept: 0), result.Events);
            Assert.Equal(new StripTableCounts(Scanned: 5, Rewritten: 1, SetToNull: 1, OtherKept: 4), result.MediaItems);
        }

        [Fact]
        public void Run_BacksUpTheDatabaseBeforeChangingIt()
        {
            string db = CreateDb();
            var now = new DateTime(2026, 11, 1, 12, 30, 45, DateTimeKind.Utc);

            StripRawPayloadResult result = RawPayloadStripper.Run(db, _dump, yes: true, () => now);

            Assert.True(result.Success, result.Refusal);
            Assert.NotNull(result.BackupPath);
            Assert.True(File.Exists(result.BackupPath));
            Assert.Contains("20261101T123045", Path.GetFileName(result.BackupPath), StringComparison.Ordinal);
            Assert.Equal(RawKnownOnly, Read(result.BackupPath!, "Events", 1));
            Assert.Equal(RawWithUnknown, Read(result.BackupPath!, "Events", 2));
            Assert.Null(Read(db, "Events", 1));
        }

        [Fact]
        public void Run_SecondRun_RewritesNothing()
        {
            string db = CreateDb();
            _ = RawPayloadStripper.Run(db, _dump, yes: true);

            StripRawPayloadResult second = RawPayloadStripper.Run(db, _dump, yes: true);

            Assert.True(second.Success, second.Refusal);
            Assert.Equal(0, second.Events.Rewritten);
            Assert.Equal(0, second.MediaItems.Rewritten);
        }
    }
}
