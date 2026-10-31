using Microsoft.Data.Sqlite;

using VideoForensics.Utils.SelfTest;

using Xunit;

namespace VideoForensics.Utils.SelfTest.Tests
{
    public sealed class UnknownKeyCollator_Tests : IDisposable
    {
        private static readonly string[] FullSchema =
        [
            "CREATE TABLE Devices (Id INTEGER PRIMARY KEY, SettingsMetadataJson TEXT, LightSettingsMetadataJson TEXT, MotionSnoozeJson TEXT, MetadataJson TEXT)",
            "CREATE TABLE DeviceCapabilities (Id INTEGER PRIMARY KEY, MetadataJson TEXT)",
            "CREATE TABLE Events (Id INTEGER PRIMARY KEY, MetadataJson TEXT)",
            "CREATE TABLE MediaItems (Id INTEGER PRIMARY KEY, MetadataJson TEXT)",
            "CREATE TABLE LocationMetadata (Id INTEGER PRIMARY KEY, MetadataJson TEXT)",
            "CREATE TABLE RingAccounts (Id INTEGER PRIMARY KEY, MetadataJson TEXT)",
            "CREATE TABLE ActionLogEntries (Id INTEGER PRIMARY KEY, DetailsJson TEXT)",
            "CREATE TABLE DeviceConfigSnapshots (Id INTEGER PRIMARY KEY, CustomSettingsJson TEXT)",
            "CREATE TABLE RingLocationModeNotYetParticipating (Id INTEGER PRIMARY KEY, ValueJson TEXT)",
        ];

        private readonly string _folder = Path.Combine(Path.GetTempPath(), "unknown-keys-tests-" + Guid.NewGuid().ToString("N"));

        public UnknownKeyCollator_Tests()
        {
            _ = Directory.CreateDirectory(_folder);
        }

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(_folder))
            {
                Directory.Delete(_folder, recursive: true);
            }
        }

        private string CreateDb(IEnumerable<string> schema, params string[] statements)
        {
            string path = Path.Combine(_folder, "vf-" + Guid.NewGuid().ToString("N") + ".db");
            using var conn = new SqliteConnection($"Data Source={path}");
            conn.Open();
            foreach (string sql in schema.Concat(statements))
            {
                using SqliteCommand cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                _ = cmd.ExecuteNonQuery();
            }

            return path;
        }

        [Fact]
        public void UnknownKeyAggregator_LeafPaths_ArraysEmptiesAndNullsNormalized()
        {
            var agg = new UnknownKeyAggregator();
            agg.AddRow("""{"a":1,"b":null,"c":[{"d":"x"},{"d":2}],"e":[],"f":{},"g":{"h":[true]}}""");

            string[] expected = ["a", "b", "c[].d", "e[]", "f{}", "g.h[]"];
            Assert.Equal(expected, agg.Paths.Select(p => p.Path).OrderBy(p => p, StringComparer.Ordinal));

            Dictionary<string, LeafStats> byPath = agg.Paths.ToDictionary(p => p.Path, StringComparer.Ordinal);
            Assert.Equal(new[] { "number" }, byPath["a"].Kinds);
            Assert.Equal(new[] { "null" }, byPath["b"].Kinds);
            Assert.Equal(new[] { "number", "string" }, byPath["c[].d"].Kinds);
            Assert.Equal(new[] { "array" }, byPath["e[]"].Kinds);
            Assert.Equal(new[] { "object" }, byPath["f{}"].Kinds);
            Assert.Equal(new[] { "bool" }, byPath["g.h[]"].Kinds);
        }

        [Fact]
        public void UnknownKeyAggregator_RowCounts_CountsRowsNotOccurrences()
        {
            var agg = new UnknownKeyAggregator();
            agg.AddRow("""{"k":1,"list":[{"id":1},{"id":2},{"id":3}]}""");
            agg.AddRow("""{"k":"s"}""");
            agg.AddRow("""{"list":[]}""");
            agg.AddRow("not json {");

            Dictionary<string, LeafStats> byPath = agg.Paths.ToDictionary(p => p.Path, StringComparer.Ordinal);
            Assert.Equal(4, agg.NonNullRows);
            Assert.Equal(1, agg.InvalidJsonRows);
            Assert.Equal(2, byPath["k"].Rows);
            Assert.Equal(1, byPath["list[].id"].Rows);
            Assert.Equal(1, byPath["list[]"].Rows);
        }

        [Fact]
        public void UnknownKeyAggregator_KindsAggregate_UnionsAcrossRows()
        {
            var agg = new UnknownKeyAggregator();
            agg.AddRow("""{"k":"s"}""");
            agg.AddRow("""{"k":1}""");
            agg.AddRow("""{"k":true}""");
            agg.AddRow("""{"k":false}""");
            agg.AddRow("""{"k":null}""");
            agg.AddRow("""{"k":[]}""");
            agg.AddRow("""{"k":{}}""");

            // Empty containers are their own leaves (k[] and k{}), so only the five scalar and null rows reach "k".
            LeafStats k = Assert.Single(agg.Paths, p => p.Path == "k");
            Assert.Equal(5, k.Rows);
            Assert.Equal(new[] { "bool", "null", "number", "string" }, k.Kinds);
            Assert.Equal(new[] { "array" }, Assert.Single(agg.Paths, p => p.Path == "k[]").Kinds);
            Assert.Equal(new[] { "object" }, Assert.Single(agg.Paths, p => p.Path == "k{}").Kinds);
        }

        [Fact]
        public void UnknownKeyCollator_CoveredFlag_MatchesFieldMapPathsWithoutEndpoint()
        {
            FieldMap map = FieldMap.Parse("""{"ep-one:doorbot.id":"Devices.Id","ep-two:media.type[]":"Rows:MediaItems"}""");
            IReadOnlySet<string> covered = UnknownKeyCollator.FieldMapPaths(map);
            Assert.Contains("doorbot.id", covered);
            Assert.Contains("media.type[]", covered);
            Assert.DoesNotContain("ep-one:doorbot.id", covered);

            var agg = new UnknownKeyAggregator();
            agg.AddRow("""{"doorbot":{"id":1,"name":"n"},"media":{"type":["a"]}}""");

            IReadOnlyList<UnknownKeyPath> paths = UnknownKeyCollator.ToUnknownPaths("Events.MetadataJson", agg.Paths, covered);
            Dictionary<string, UnknownKeyPath> byPath = paths.ToDictionary(p => p.Path, StringComparer.Ordinal);
            Assert.True(byPath["doorbot.id"].CoveredByFieldMap == true);
            Assert.True(byPath["doorbot.name"].CoveredByFieldMap == false);
            Assert.True(byPath["media.type[]"].CoveredByFieldMap == true);

            // Unknown (not covered) paths sort first within a column.
            Assert.Equal("doorbot.name", paths[0].Path);
        }

        [Fact]
        public void UnknownKeyCollator_CoveredFlagNotChecked_IsNull()
        {
            var agg = new UnknownKeyAggregator();
            agg.AddRow("""{"x":1}""");

            UnknownKeyPath path = Assert.Single(UnknownKeyCollator.ToUnknownPaths("Devices.MetadataJson", agg.Paths, covered: null));
            Assert.Null(path.CoveredByFieldMap);
        }

        [Fact]
        public async Task UnknownKeyCollator_Output_NoValueTextInFiles()
        {
            const string sentinel = "SENTINEL-8c1f-value";
            string db = CreateDb(
                FullSchema,
                $"INSERT INTO Events (MetadataJson) VALUES ('{{\"doorbot\":{{\"id\":1,\"description\":\"{sentinel}\"}}}}')",
                $"INSERT INTO Devices (SettingsMetadataJson) VALUES ('{{\"secretNote\":\"{sentinel}\"}}')");

            UnknownKeyReport report = await UnknownKeyCollator.RunAsync(db, FieldMap.Parse("{}"));
            string outDir = Path.Combine(_folder, "out");
            UnknownKeyCollator.WriteFiles(report, outDir);

            string json = await File.ReadAllTextAsync(Path.Combine(outDir, "unknown-keys.json"));
            string md = await File.ReadAllTextAsync(Path.Combine(outDir, "unknown-keys.md"));

            Assert.DoesNotContain(sentinel, json, StringComparison.Ordinal);
            Assert.DoesNotContain(sentinel, md, StringComparison.Ordinal);
            Assert.Contains("doorbot.description", json, StringComparison.Ordinal);
            Assert.Contains("secretNote", md, StringComparison.Ordinal);
        }

        [Fact]
        public void UnknownKeyCollator_Database_OpensReadOnly()
        {
            string db = CreateDb(FullSchema, "INSERT INTO Devices (SettingsMetadataJson) VALUES ('{\"a\":1}')");

            string connectionString = UnknownKeyCollator.ReadOnlyConnectionString(db);
            Assert.Contains("Mode=ReadOnly", connectionString, StringComparison.OrdinalIgnoreCase);

            using var conn = new SqliteConnection(connectionString);
            conn.Open();
            using SqliteCommand write = conn.CreateCommand();
            write.CommandText = "UPDATE Devices SET SettingsMetadataJson = '{\"a\":2}'";
            _ = Assert.Throws<SqliteException>(() => write.ExecuteNonQuery());
        }

        [Fact]
        public async Task UnknownKeyCollator_MissingTableAndIgnoredColumn_StatusesWithoutError()
        {
            string db = CreateDb(
                ["CREATE TABLE Devices (Id INTEGER PRIMARY KEY, SettingsMetadataJson TEXT, LightSettingsMetadataJson TEXT, MotionSnoozeJson TEXT, MetadataJson TEXT)"],
                "INSERT INTO Devices (SettingsMetadataJson) VALUES ('{\"a\":1}')");

            UnknownKeyReport report = await UnknownKeyCollator.RunAsync(db, FieldMap.Parse("{}"));

            UnknownKeyColumnResult devices = report.Columns.Single(c => c.Column == "Devices.SettingsMetadataJson");
            Assert.Equal("scanned", devices.Status);
            Assert.Equal(1, devices.NonNullRows);

            UnknownKeyColumnResult missing = report.Columns.Single(c => c.Column == "DeviceCapabilities.MetadataJson");
            Assert.Equal("column missing", missing.Status);
            Assert.Empty(missing.Paths);

            UnknownKeyColumnResult ignored = report.Columns.Single(c => c.Column == "ActionLogEntries.DetailsJson");
            Assert.Equal("ignored by design", ignored.Status);
            Assert.Empty(ignored.Paths);
        }
    }
}
