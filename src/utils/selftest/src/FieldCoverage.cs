using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace VideoForensics.Utils.SelfTest
{
    /// <summary>What one response path held across the sampled responses.</summary>
    [Flags]
    internal enum LeafKind
    {
        None = 0,
        Scalar = 1,
        Null = 2,
        EmptyArray = 4,
        EmptyObject = 8
    }

    /// <summary>
    /// Enumerates the leaf paths of a JSON response. Array indexes are normalized to <c>[]</c>, so every element of an
    /// array contributes to the same paths. An array of scalars is the single leaf <c>name[]</c>. An empty array is the
    /// leaf <c>name[]</c> and an empty object is the leaf <c>name{}</c>, so containers that hold nothing are not lost.
    /// </summary>
    internal static class JsonLeafPaths
    {
        /// <summary>Adds the leaf paths of <paramref name="root"/> to <paramref name="into"/>, OR-ing the kinds of a repeated path.</summary>
        public static void Collect(JsonElement root, IDictionary<string, LeafKind> into)
        {
            Walk(root, string.Empty, into);
        }

        private static void Walk(JsonElement element, string path, IDictionary<string, LeafKind> into)
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
                        Mark(into, path + "{}", LeafKind.EmptyObject);
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
                        Mark(into, elementPath, LeafKind.EmptyArray);
                    }

                    break;
                case JsonValueKind.Null:
                case JsonValueKind.Undefined:
                    Mark(into, path.Length == 0 ? "$" : path, LeafKind.Null);
                    break;
                default:
                    Mark(into, path.Length == 0 ? "$" : path, LeafKind.Scalar);
                    break;
            }
        }

        private static void Mark(IDictionary<string, LeafKind> into, string path, LeafKind kind)
        {
            into[path] = into.TryGetValue(path, out LeafKind existing) ? existing | kind : kind;
        }
    }

    /// <summary>
    /// The leaf paths of every sampled response of one endpoint. A null or empty marker that another sample fills in
    /// (the same path holding an object, array elements or a value) says nothing about the shape, so it is dropped.
    /// </summary>
    internal sealed class EndpointLeafSet
    {
        private readonly Dictionary<string, LeafKind> _raw = new(StringComparer.Ordinal);

        public void Add(JsonElement root)
        {
            JsonLeafPaths.Collect(root, _raw);
        }

        /// <summary>The leaf paths after the superseded null and empty markers are removed.</summary>
        public IReadOnlyDictionary<string, LeafKind> Leaves
        {
            get
            {
                // Container names that some sample shows holding a real value (a scalar, or a null leaf inside them).
                var populated = new HashSet<string>(StringComparer.Ordinal);
                foreach ((string path, LeafKind kind) in _raw)
                {
                    if ((kind & (LeafKind.Scalar | LeafKind.Null)) == 0)
                    {
                        continue;
                    }

                    for (int i = 0; i < path.Length; i++)
                    {
                        bool marker = i + 1 < path.Length && ((path[i] == '[' && path[i + 1] == ']') || (path[i] == '{' && path[i + 1] == '}'));
                        if (path[i] == '.' || marker)
                        {
                            _ = populated.Add(path[..i]);
                        }
                    }
                }

                var result = new Dictionary<string, LeafKind>(StringComparer.Ordinal);
                foreach ((string path, LeafKind kind) in _raw)
                {
                    LeafKind kept = kind;
                    if (kept.HasFlag(LeafKind.Null) && populated.Contains(path))
                    {
                        kept &= ~LeafKind.Null;
                    }

                    if (kept.HasFlag(LeafKind.EmptyArray) && (kept != LeafKind.EmptyArray || populated.Contains(path)))
                    {
                        kept &= ~LeafKind.EmptyArray;
                    }

                    if (kept.HasFlag(LeafKind.EmptyObject) && populated.Contains(path[..^2]))
                    {
                        kept &= ~LeafKind.EmptyObject;
                    }

                    if (kept != LeafKind.None)
                    {
                        result[path] = kept;
                    }
                }

                return result;
            }
        }
    }

    /// <summary>The tables and columns of the EF model, by store name.</summary>
    internal sealed class DbSchema
    {
        private readonly IReadOnlyDictionary<string, IReadOnlySet<string>> _tables;

        public DbSchema(IReadOnlyDictionary<string, IReadOnlySet<string>> tableColumns)
        {
            _tables = tableColumns;
        }

        public static DbSchema FromModel(IModel model)
        {
            var tables = new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal);
            foreach (IEntityType entity in model.GetEntityTypes())
            {
                string? table = entity.GetTableName();
                if (table is null)
                {
                    continue;
                }

                var id = StoreObjectIdentifier.Table(table, entity.GetSchema());
                if (!tables.TryGetValue(table, out IReadOnlySet<string>? existing))
                {
                    tables[table] = existing = new HashSet<string>(StringComparer.Ordinal);
                }

                foreach (IProperty property in entity.GetProperties())
                {
                    string? column = property.GetColumnName(id);
                    if (column is not null)
                    {
                        _ = ((HashSet<string>)existing).Add(column);
                    }
                }
            }

            return new DbSchema(tables);
        }

        public bool HasTable(string table)
        {
            return _tables.ContainsKey(table);
        }

        public bool HasColumn(string table, string column)
        {
            return _tables.TryGetValue(table, out IReadOnlySet<string>? columns) && columns.Contains(column);
        }
    }

    /// <summary>A map entry whose target is not in the EF model (renamed or dropped) or is a *Json column.</summary>
    internal sealed record StaleMapEntry(string Key, string Target, string Reason);

    /// <summary>A response file the field check did not read, and why.</summary>
    internal sealed record SkippedResponseFile(string File, string Reason);

    /// <summary>A mapped field whose stored value differs from the response value.</summary>
    internal sealed record FieldValueMismatch(string Key, string Expected, string Actual);

    /// <summary>
    /// Extension point for the later step that compares every mapped value with the database rows. Not implemented yet:
    /// <see cref="FieldCoverageAnalyzer.Analyze"/> only calls a verifier when one is supplied, and none exists today.
    /// </summary>
    internal interface IFieldValueVerifier
    {
        IReadOnlyList<FieldValueMismatch> Verify(FieldMap map, string outputDir);
    }

    /// <summary>The result of checking one run's saved responses against the field map.</summary>
    internal sealed class FieldCoverageReport
    {
        /// <summary>Response paths with no map entry, per endpoint: the true to-do list.</summary>
        public Dictionary<string, List<string>> UnmappedByEndpoint { get; set; } = [];

        /// <summary>Map entries whose table or column is not in the EF model, or that point at a *Json column.</summary>
        public List<StaleMapEntry> StaleEntries { get; set; } = [];

        /// <summary>Endpoints that returned data but have no map entry that stores anything.</summary>
        public List<string> EndpointsWithoutMapEntries { get; set; } = [];

        /// <summary>Paths that were null in every sample, so their type is unverified (mapped or not).</summary>
        public Dictionary<string, List<string>> NullInSampleByEndpoint { get; set; } = [];

        public List<SkippedResponseFile> SkippedFiles { get; set; } = [];

        /// <summary>Always empty until a <see cref="IFieldValueVerifier"/> exists.</summary>
        public List<FieldValueMismatch> ValueMismatches { get; set; } = [];

        public int UnmappedPathCount => UnmappedByEndpoint.Values.Sum(v => v.Count);
        public int StaleEntryCount => StaleEntries.Count;
        public int NullInSamplePathCount => NullInSampleByEndpoint.Values.Sum(v => v.Count);
    }

    /// <summary>
    /// Compares the leaf paths of the saved endpoint responses with the explicit field map. Matching is by full path, never
    /// by leaf name, and a value kept only in a *Json column is not stored. Failed calls (per index.json) are not read.
    /// </summary>
    internal static class FieldCoverageAnalyzer
    {
        public static FieldCoverageReport Analyze(string outputDir, FieldMap map, DbSchema schema, IFieldValueVerifier? verifier = null)
        {
            var report = new FieldCoverageReport { StaleEntries = FindStaleEntries(map, schema) };
            Dictionary<string, string> failed = ReadFailedFiles(outputDir);

            var byEndpoint = new SortedDictionary<string, EndpointLeafSet>(StringComparer.Ordinal);
            foreach (string file in Directory.EnumerateFiles(outputDir, "*.json").OrderBy(f => f, StringComparer.Ordinal))
            {
                string name = Path.GetFileName(file);
                int underscore = name.IndexOf('_', StringComparison.Ordinal);
                if (underscore <= 0 || name.StartsWith("db-completeness", StringComparison.Ordinal))
                {
                    continue;
                }

                if (failed.TryGetValue(name, out string? why))
                {
                    report.SkippedFiles.Add(new SkippedResponseFile(name, why));
                    continue;
                }

                try
                {
                    using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(file));
                    string endpoint = name[..underscore];
                    if (!byEndpoint.TryGetValue(endpoint, out EndpointLeafSet? set))
                    {
                        byEndpoint[endpoint] = set = new EndpointLeafSet();
                    }

                    set.Add(doc.RootElement);
                }
                catch (JsonException)
                {
                    report.SkippedFiles.Add(new SkippedResponseFile(name, "not valid JSON"));
                }
            }

            foreach ((string endpoint, EndpointLeafSet set) in byEndpoint)
            {
                IReadOnlyDictionary<string, LeafKind> leaves = set.Leaves;
                var unmapped = new List<string>();
                var nulls = new List<string>();
                foreach ((string path, LeafKind kind) in leaves.OrderBy(l => l.Key, StringComparer.Ordinal))
                {
                    if (kind == LeafKind.Null)
                    {
                        nulls.Add(path);
                    }

                    if (!IsMapped(map, endpoint, path, kind))
                    {
                        unmapped.Add(path);
                    }
                }

                if (unmapped.Count > 0)
                {
                    report.UnmappedByEndpoint[endpoint] = unmapped;
                }

                if (nulls.Count > 0)
                {
                    report.NullInSampleByEndpoint[endpoint] = nulls;
                }

                // Data is a scalar the map does not declare NotData (an error body or similar is not data).
                bool returnedData = leaves.Any(l => l.Value.HasFlag(LeafKind.Scalar)
                    && map.Resolve(endpoint, l.Key) is not { Kind: FieldTargetKind.NotData });
                if (returnedData && !map.HasStoredEntries(endpoint))
                {
                    report.EndpointsWithoutMapEntries.Add(endpoint);
                }
            }

            if (verifier is not null)
            {
                report.ValueMismatches = [.. verifier.Verify(map, outputDir)];
            }

            return report;
        }

        private static bool IsMapped(FieldMap map, string endpoint, string path, LeafKind kind)
        {
            if (map.Resolve(endpoint, path) is not null)
            {
                return true;
            }

            // A null or empty container is covered once the shape inside it is mapped (its type is flagged separately).
            if ((kind & LeafKind.Scalar) != 0)
            {
                return false;
            }

            string container = path.EndsWith("[]", StringComparison.Ordinal) || path.EndsWith("{}", StringComparison.Ordinal) ? path[..^2] : path;
            return map.HasEntryBelow(endpoint, container);
        }

        private static List<StaleMapEntry> FindStaleEntries(FieldMap map, DbSchema schema)
        {
            var stale = new List<StaleMapEntry>();
            foreach ((string key, FieldTarget target) in map.Entries)
            {
                string? reason = target.Kind switch
                {
                    FieldTargetKind.Column when target.Column!.EndsWith("Json", StringComparison.Ordinal) => "maps to a *Json column, which does not count as stored",
                    FieldTargetKind.Column when !schema.HasTable(target.Table!) => $"table {target.Table} is not in the EF model",
                    FieldTargetKind.Column when !schema.HasColumn(target.Table!, target.Column!) => $"column {target.Column} is not in table {target.Table}",
                    FieldTargetKind.Rows when !schema.HasTable(target.Table!) => $"table {target.Table} is not in the EF model",
                    _ => null
                };

                if (reason is not null)
                {
                    stale.Add(new StaleMapEntry(key, target.Text, reason));
                }
            }

            return stale;
        }

        /// <summary>Result files of failed calls (and bodies with an error status), by file name, with the reason.</summary>
        internal static Dictionary<string, string> ReadFailedFiles(string outputDir)
        {
            var failed = new Dictionary<string, string>(StringComparer.Ordinal);
            string indexPath = Path.Combine(outputDir, "index.json");
            if (!File.Exists(indexPath))
            {
                return failed;
            }

            try
            {
                using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(indexPath));
                if (!doc.RootElement.TryGetProperty("Calls", out JsonElement calls) || calls.ValueKind != JsonValueKind.Array)
                {
                    return failed;
                }

                foreach (JsonElement call in calls.EnumerateArray())
                {
                    bool callFailed = call.TryGetProperty("Success", out JsonElement success) && success.ValueKind == JsonValueKind.False;
                    if (callFailed && call.TryGetProperty("ResultFile", out JsonElement result) && result.ValueKind == JsonValueKind.String)
                    {
                        failed[result.GetString()!] = "call failed in index.json";
                    }

                    if (!call.TryGetProperty("HttpCalls", out JsonElement http) || http.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    foreach (JsonElement exchange in http.EnumerateArray())
                    {
                        if (!exchange.TryGetProperty("BodyFile", out JsonElement body) || body.ValueKind != JsonValueKind.String)
                        {
                            continue;
                        }

                        bool errorStatus = exchange.TryGetProperty("StatusCode", out JsonElement status)
                            && status.ValueKind == JsonValueKind.Number && status.TryGetInt32(out int code) && code >= 400;
                        if (callFailed || errorStatus)
                        {
                            failed[body.GetString()!] = callFailed ? "call failed in index.json" : "error status in index.json";
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // An unreadable index means no call is known to have failed; every response file is read.
            }

            return failed;
        }
    }
}
