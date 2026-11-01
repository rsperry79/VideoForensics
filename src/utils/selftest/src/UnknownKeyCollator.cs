using System.Text.Json;

using Microsoft.Data.Sqlite;

using VideoForensics.Data.Database.Sqlite.DependencyInjection;

namespace VideoForensics.Utils.SelfTest
{
    /// <summary>One leaf path of a JSON column: how many rows hold it and which JSON value kinds it held.</summary>
    internal sealed record LeafStats(string Path, int Rows, IReadOnlyList<string> Kinds);

    /// <summary>
    /// One leaf path in the unknown-keys report. <see cref="CoveredByFieldMap"/> is null when the column is not checked
    /// against the field map (only Events and MediaItems are).
    /// </summary>
    internal sealed record UnknownKeyPath(string Column, string Path, int Rows, IReadOnlyList<string> Kinds, bool? CoveredByFieldMap);

    /// <summary>The result for one JSON column. <see cref="Status"/> is "scanned", "ignored by design" or "column missing".</summary>
    internal sealed record UnknownKeyColumnResult(
        string Column,
        string Status,
        bool CheckedAgainstFieldMap,
        int NonNullRows,
        int InvalidJsonRows,
        IReadOnlyList<UnknownKeyPath> Paths);

    internal sealed record UnknownKeyReport(string DbFileName, DateTime GeneratedAtUtc, IReadOnlyList<UnknownKeyColumnResult> Columns);

    /// <summary>
    /// Collects every leaf JSON path of the Ring-data JSON columns, with the number of rows holding each path and the
    /// JSON value kinds seen. It never stores or prints a value: only keys, kinds and counts leave this class.
    /// </summary>
    /// <remarks>
    /// Path format matches <see cref="JsonLeafPaths"/>: object keys joined with '.', array elements as '[]', an empty array
    /// as the leaf 'x[]' and an empty object as the leaf 'x{}'. A root scalar is '$'. A key that itself contains '.' or
    /// '[]' is ambiguous in this format; Ring keys do not contain either.
    /// </remarks>
    internal static class UnknownKeyCollator
    {
        public const string JsonFileName = "unknown-keys.json";
        public const string MarkdownFileName = "unknown-keys.md";

        /// <summary>One JSON column to scan. <paramref name="CheckFieldMap"/> marks the columns whose paths are compared with field-map.json.</summary>
        internal sealed record ColumnSpec(string Table, string Column, bool CheckFieldMap, bool IgnoredByDesign)
        {
            public string Name => $"{Table}.{Column}";
        }

        /// <summary>
        /// The JSON columns in report order. ActionLogEntries.DetailsJson is listed but never scanned: audit details are
        /// intentionally JSON. Events and MediaItems hold the whole raw Ring event payload, which the structured RingEvent*
        /// tables already cover in part, so they are the only columns checked against the field map.
        /// </summary>
        public static readonly IReadOnlyList<ColumnSpec> Columns =
        [
            new("Devices", "SettingsMetadataJson", CheckFieldMap: false, IgnoredByDesign: false),
            new("Devices", "LightSettingsMetadataJson", CheckFieldMap: false, IgnoredByDesign: false),
            new("Devices", "MotionSnoozeJson", CheckFieldMap: false, IgnoredByDesign: false),
            new("Events", "MetadataJson", CheckFieldMap: true, IgnoredByDesign: false),
            new("MediaItems", "MetadataJson", CheckFieldMap: true, IgnoredByDesign: false),
            new("ActionLogEntries", "DetailsJson", CheckFieldMap: false, IgnoredByDesign: true),
            new("DeviceConfigSnapshots", "CustomSettingsJson", CheckFieldMap: false, IgnoredByDesign: false),
            new("RingLocationModeNotYetParticipating", "ValueJson", CheckFieldMap: false, IgnoredByDesign: false),
        ];

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        /// <summary>
        /// The connection string for the read-only scan: the same string the field check uses, plus Mode=ReadOnly so a
        /// write attempt through this connection fails.
        /// </summary>
        public static string ReadOnlyConnectionString(string dbPath)
        {
            return SqliteConnectionStrings.ForFile(dbPath) + ";Mode=ReadOnly";
        }

        /// <summary>
        /// The field-map paths without their endpoint prefix. A field-map key is <c>endpoint:json.path</c>, so the part after
        /// the first ':' is the path. Exact string match only: wildcard entries (<c>*</c>, <c>**</c>) are not expanded.
        /// </summary>
        public static IReadOnlySet<string> FieldMapPaths(FieldMap map)
        {
            var paths = new HashSet<string>(StringComparer.Ordinal);
            foreach (string key in map.Entries.Keys)
            {
                int colon = key.IndexOf(':', StringComparison.Ordinal);
                if (colon >= 0)
                {
                    _ = paths.Add(key[(colon + 1)..]);
                }
            }

            return paths;
        }

        /// <summary>
        /// Builds the report rows for one column. <paramref name="covered"/> null means the column is not checked against the
        /// field map, so every row's flag is null. Not-covered paths sort first, then by path.
        /// </summary>
        public static IReadOnlyList<UnknownKeyPath> ToUnknownPaths(string column, IEnumerable<LeafStats> leaves, IReadOnlySet<string>? covered)
        {
            return leaves
                .Select(l => new UnknownKeyPath(column, l.Path, l.Rows, l.Kinds, covered is null ? null : covered.Contains(l.Path)))
                .OrderBy(p => p.CoveredByFieldMap == true ? 1 : 0)
                .ThenBy(p => p.Path, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// Scans every column of the database read-only. Missing tables or columns give a "column missing" result instead of an
        /// error. A value that is not valid JSON counts as an invalid row and contributes no paths.
        /// </summary>
        public static async Task<UnknownKeyReport> RunAsync(string dbPath, FieldMap map, CancellationToken cancellationToken = default)
        {
            IReadOnlySet<string> covered = FieldMapPaths(map);
            var results = new List<UnknownKeyColumnResult>();

            await using var conn = new SqliteConnection(ReadOnlyConnectionString(dbPath));
            await conn.OpenAsync(cancellationToken);

            foreach (ColumnSpec spec in Columns)
            {
                if (spec.IgnoredByDesign)
                {
                    results.Add(new UnknownKeyColumnResult(spec.Name, "ignored by design", spec.CheckFieldMap, 0, 0, []));
                    continue;
                }

                if (!await ColumnExistsAsync(conn, spec.Table, spec.Column, cancellationToken))
                {
                    results.Add(new UnknownKeyColumnResult(spec.Name, "column missing", spec.CheckFieldMap, 0, 0, []));
                    continue;
                }

                var aggregate = new UnknownKeyAggregator();
                await using (SqliteCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = $"SELECT \"{spec.Column}\" FROM \"{spec.Table}\" WHERE \"{spec.Column}\" IS NOT NULL";
                    await using SqliteDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken);
                    while (await reader.ReadAsync(cancellationToken))
                    {
                        // A non-text value is passed as "", which fails to parse and is counted as invalid JSON.
                        aggregate.AddRow(reader.GetValue(0) as string ?? string.Empty);
                    }
                }

                IReadOnlyList<UnknownKeyPath> paths = ToUnknownPaths(spec.Name, aggregate.Paths, spec.CheckFieldMap ? covered : null);
                results.Add(new UnknownKeyColumnResult(spec.Name, "scanned", spec.CheckFieldMap, aggregate.NonNullRows, aggregate.InvalidJsonRows, paths));
            }

            return new UnknownKeyReport(Path.GetFileName(dbPath), DateTime.UtcNow, results);
        }

        /// <summary>Writes unknown-keys.json (one array of all paths) and unknown-keys.md (grouped by column) into <paramref name="outputDir"/>.</summary>
        public static void WriteFiles(UnknownKeyReport report, string outputDir)
        {
            _ = Directory.CreateDirectory(outputDir);

            List<UnknownKeyPath> allPaths = report.Columns.SelectMany(c => c.Paths).ToList();
            File.WriteAllText(Path.Combine(outputDir, JsonFileName), JsonSerializer.Serialize(allPaths, JsonOptions));
            File.WriteAllText(Path.Combine(outputDir, MarkdownFileName), BuildMarkdown(report));
        }

        /// <summary>One line per column on the console: row count, path count and the field-map counts where they apply.</summary>
        internal static void WriteSummary(UnknownKeyReport report, TextWriter output)
        {
            foreach (UnknownKeyColumnResult column in report.Columns)
            {
                switch (column.Status)
                {
                    case "ignored by design":
                        output.WriteLine($"{column.Column}: ignored by design (audit details are intentionally JSON)");
                        break;
                    case "column missing":
                        output.WriteLine($"{column.Column}: column not in database, skipped");
                        break;
                    default:
                        string coverage = column.CheckedAgainstFieldMap
                            ? $", {column.Paths.Count(p => p.CoveredByFieldMap == true)} covered by field map, {column.Paths.Count(p => p.CoveredByFieldMap != true)} not covered"
                            : string.Empty;
                        output.WriteLine($"{column.Column}: {column.NonNullRows} rows, {column.Paths.Count} paths{coverage}, {column.InvalidJsonRows} invalid JSON");
                        break;
                }
            }
        }

        private static async Task<bool> ColumnExistsAsync(SqliteConnection conn, string table, string column, CancellationToken cancellationToken)
        {
            // PRAGMA table_info returns no rows for a missing table, so an absent table reads as an absent column.
            await using SqliteCommand cmd = conn.CreateCommand();
            cmd.CommandText = $"PRAGMA table_info(\"{table}\")";
            await using SqliteDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string BuildMarkdown(UnknownKeyReport report)
        {
            var sb = new System.Text.StringBuilder();
            _ = sb.AppendLine("# Unknown Ring keys");
            _ = sb.AppendLine();
            _ = sb.AppendLine($"Source: `{report.DbFileName}`, generated {report.GeneratedAtUtc:yyyy-MM-dd HH:mm:ss} UTC.");
            _ = sb.AppendLine("Keys, value kinds and row counts only. No values are written to this file.");
            _ = sb.AppendLine("Covered = the path has an entry in field-map.json (checked for Events and MediaItems only; other columns show n/a).");
            _ = sb.AppendLine();
            _ = sb.AppendLine("## Summary");
            _ = sb.AppendLine();
            _ = sb.AppendLine("| Column | Status | Non-null rows | Paths | Invalid JSON |");
            _ = sb.AppendLine("|---|---|---:|---:|---:|");
            foreach (UnknownKeyColumnResult column in report.Columns)
            {
                _ = sb.AppendLine($"| `{column.Column}` | {column.Status} | {column.NonNullRows} | {column.Paths.Count} | {column.InvalidJsonRows} |");
            }

            foreach (UnknownKeyColumnResult column in report.Columns)
            {
                _ = sb.AppendLine();
                _ = sb.AppendLine($"## {column.Column}");
                _ = sb.AppendLine();

                if (column.Status == "ignored by design")
                {
                    _ = sb.AppendLine("Ignored by design: audit details are intentionally JSON. Not scanned.");
                    continue;
                }

                if (column.Status == "column missing")
                {
                    _ = sb.AppendLine("Column not in the database. Skipped.");
                    continue;
                }

                if (column.Paths.Count == 0)
                {
                    _ = sb.AppendLine("No non-null values.");
                    continue;
                }

                _ = sb.AppendLine("| Path | Rows | Kinds | Covered by field map |");
                _ = sb.AppendLine("|---|---:|---|---|");
                foreach (UnknownKeyPath p in column.Paths)
                {
                    string covered = p.CoveredByFieldMap switch
                    {
                        true => "yes",
                        false => "no",
                        null => "n/a"
                    };
                    _ = sb.AppendLine($"| `{p.Path.Replace("|", "\\|", StringComparison.Ordinal)}` | {p.Rows} | {string.Join(", ", p.Kinds)} | {covered} |");
                }
            }

            return sb.ToString();
        }
    }

    /// <summary>
    /// Folds the rows of one JSON column into per-path counts. Each row adds at most one to a path's count, however many
    /// array elements hold it.
    /// </summary>
    internal sealed class UnknownKeyAggregator
    {
        private sealed class Accumulator
        {
            public int Rows;
            public readonly SortedSet<string> Kinds = new(StringComparer.Ordinal);
        }

        private readonly Dictionary<string, Accumulator> _paths = new(StringComparer.Ordinal);

        /// <summary>Non-null values passed in, valid JSON or not.</summary>
        public int NonNullRows { get; private set; }

        /// <summary>Values that did not parse as JSON. They add no paths.</summary>
        public int InvalidJsonRows { get; private set; }

        /// <summary>The leaf paths seen so far, ordinal by path.</summary>
        public IReadOnlyList<LeafStats> Paths => _paths
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => new LeafStats(kv.Key, kv.Value.Rows, kv.Value.Kinds.ToList()))
            .ToList();

        public void AddRow(string json)
        {
            NonNullRows++;

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(json);
            }
            catch (JsonException)
            {
                // The error text can echo part of the value, so it is not kept or printed.
                InvalidJsonRows++;
                return;
            }

            using (doc)
            {
                var rowLeaves = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
                Walk(doc.RootElement, string.Empty, rowLeaves);

                foreach ((string path, SortedSet<string> kinds) in rowLeaves)
                {
                    if (!_paths.TryGetValue(path, out Accumulator? acc))
                    {
                        acc = new Accumulator();
                        _paths[path] = acc;
                    }

                    acc.Rows++;
                    acc.Kinds.UnionWith(kinds);
                }
            }
        }

        private static void Walk(JsonElement element, string path, Dictionary<string, SortedSet<string>> into)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    bool anyProperty = false;
                    foreach (JsonProperty property in element.EnumerateObject())
                    {
                        anyProperty = true;
                        Walk(property.Value, path.Length == 0 ? property.Name : $"{path}.{property.Name}", into);
                    }

                    if (!anyProperty)
                    {
                        Mark(into, path + "{}", "object");
                    }

                    break;
                case JsonValueKind.Array:
                    string elementPath = path + "[]";
                    bool anyItem = false;
                    foreach (JsonElement item in element.EnumerateArray())
                    {
                        anyItem = true;
                        Walk(item, elementPath, into);
                    }

                    if (!anyItem)
                    {
                        Mark(into, elementPath, "array");
                    }

                    break;
                default:
                    Mark(into, path.Length == 0 ? "$" : path, KindName(element.ValueKind));
                    break;
            }
        }

        private static string KindName(JsonValueKind kind)
        {
            return kind switch
            {
                JsonValueKind.String => "string",
                JsonValueKind.Number => "number",
                JsonValueKind.True or JsonValueKind.False => "bool",
                _ => "null"
            };
        }

        private static void Mark(Dictionary<string, SortedSet<string>> into, string path, string kind)
        {
            if (!into.TryGetValue(path, out SortedSet<string>? kinds))
            {
                kinds = new SortedSet<string>(StringComparer.Ordinal);
                into[path] = kinds;
            }

            _ = kinds.Add(kind);
        }
    }
}
