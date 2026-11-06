using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

using System.Globalization;
using System.Text.Json;

using VideoForensics.Providers.Ring.Services;

namespace VideoForensics.Utils.SelfTest
{
    /// <summary>Why a response leaf was not compared with a database value.</summary>
    internal enum NotEvaluatedReason
    {
        /// <summary>The target is <c>Rows:Table</c>: child rows are keyed or ordered, so there is no single column to read.</summary>
        Rows,

        /// <summary>The target is <c>NotData</c>.</summary>
        NotData,

        /// <summary>The leaf is null in the response, so there is nothing to compare.</summary>
        NullInResponse,

        /// <summary>An empty array or object.</summary>
        EmptyContainer,

        /// <summary>The path has no map entry (the coverage check reports it).</summary>
        Unmapped,

        /// <summary>No sync code fetches this endpoint, so nothing writes it.</summary>
        NotSynced
    }

    /// <summary>One saved (or fixture) response of an endpoint, and which device it belongs to.</summary>
    /// <param name="Endpoint">The endpoint name used in the field map.</param>
    /// <param name="Name">Label for messages (file name).</param>
    /// <param name="Root">The parsed response.</param>
    /// <param name="DeviceScope">Provider device id when the whole response belongs to one device (device-settings); otherwise null.</param>
    /// <param name="ScopeFromTopLevelId">True for the device list: each element of a top-level array is scoped to its own <c>id</c>.</param>
    /// <param name="Synced">False when no sync code fetches the endpoint.</param>
    internal sealed record FixtureDocument(string Endpoint, string Name, JsonElement Root, string? DeviceScope = null, bool ScopeFromTopLevelId = false, bool Synced = true);

    /// <summary>A leaf that did not match, or whose row is missing.</summary>
    internal sealed record ValueFinding(string Endpoint, string Path, string Target, string Detail);

    /// <summary>Counts of one endpoint's leaves.</summary>
    internal sealed class EndpointValueCounts
    {
        public int Matched { get; set; }

        /// <summary>Matched after a documented conversion (number as text, epoch to date, 0/1 as bool); part of <see cref="Matched"/>.</summary>
        public int MatchedAfterConversion { get; set; }

        public int Mismatched { get; set; }
        public int MissingRow { get; set; }

        /// <summary>The group's value came from another endpoint (the later write wins), so this leaf is not stored; not a failure.</summary>
        public int Shadowed { get; set; }

        public Dictionary<NotEvaluatedReason, int> NotEvaluated { get; } = [];

        public int NotEvaluatedTotal => NotEvaluated.Values.Sum();

        public void AddNotEvaluated(NotEvaluatedReason reason)
        {
            NotEvaluated[reason] = NotEvaluated.GetValueOrDefault(reason) + 1;
        }
    }

    /// <summary>The outcome of comparing response leaves with database values.</summary>
    internal sealed class FieldValueReport
    {
        public SortedDictionary<string, EndpointValueCounts> ByEndpoint { get; } = new(StringComparer.Ordinal);
        public List<ValueFinding> Mismatches { get; } = [];
        public List<ValueFinding> MissingRows { get; } = [];
        public List<ValueFinding> Shadowed { get; } = [];

        /// <summary>Groups compared against every row of the table because the table (or the response) has no device scope.</summary>
        public int UnscopedGroups { get; set; }

        /// <summary>Groups compared against the rows of one device only.</summary>
        public int ScopedGroups { get; set; }

        /// <summary>Tables compared against every row although the response leaf belongs to one device (no foreign key chain to Devices).</summary>
        public SortedSet<string> DeviceDataInUnscopedTables { get; } = new(StringComparer.Ordinal);

        public int Matched => ByEndpoint.Values.Sum(c => c.Matched);
        public int FailureCount => Mismatches.Count + MissingRows.Count;

        public EndpointValueCounts For(string endpoint)
        {
            if (!ByEndpoint.TryGetValue(endpoint, out EndpointValueCounts? counts))
            {
                ByEndpoint[endpoint] = counts = new EndpointValueCounts();
            }

            return counts;
        }

        /// <summary>One line per endpoint plus totals; safe to print (no response values).</summary>
        public string Summary()
        {
            var lines = new List<string> { "endpoint | matched (after conversion) | mismatched | missing row | shadowed | not evaluated" };
            foreach ((string endpoint, EndpointValueCounts c) in ByEndpoint)
            {
                string reasons = string.Join(", ", c.NotEvaluated.OrderBy(r => r.Key).Select(r => $"{r.Key}={r.Value}"));
                lines.Add($"{endpoint} | {c.Matched} ({c.MatchedAfterConversion}) | {c.Mismatched} | {c.MissingRow} | {c.Shadowed} | {c.NotEvaluatedTotal}{(reasons.Length == 0 ? string.Empty : $" [{reasons}]")}");
            }

            lines.Add($"scoped groups {ScopedGroups}, unscoped groups {UnscopedGroups}");
            if (DeviceDataInUnscopedTables.Count > 0)
            {
                lines.Add($"device data compared across all rows of: {string.Join(", ", DeviceDataInUnscopedTables)}");
            }

            return string.Join(Environment.NewLine, lines);
        }
    }

    /// <summary>A value read from the database, already converted to a CLR value: null, bool, long, double, string, DateTime (UTC) or Guid.</summary>
    internal readonly record struct DbCell(object? Value);

    /// <summary>Reads the stored values of a column, optionally for one device's rows only.</summary>
    internal interface IDbValueSource
    {
        /// <summary>True when the table exists and the column is in it.</summary>
        bool HasColumn(string table, string column);

        /// <summary>True when rows of the table can be tied to a device (it is, or reaches, the Devices table).</summary>
        bool IsDeviceScoped(string table);

        /// <summary>The column's values, for the rows of device <paramref name="deviceId"/> when given, otherwise for every row.</summary>
        IReadOnlyList<DbCell> Values(string table, string column, string? deviceId);
    }

    /// <summary>
    /// Compares the value of every mapped response leaf with the database column the field map names. For each map target
    /// (table, column) and device, the response values must all be found among the stored values of that column (each stored
    /// value matches one response value), so a column that is empty, wrong or overwritten shows up. Array elements are matched
    /// as a multiset because child rows carry no response index. Rows: entries and NotData are counted, not compared.
    /// </summary>
    internal sealed class FieldMapValueVerifier : IFieldValueVerifier
    {
        private readonly IDbValueSource _db;

        public FieldMapValueVerifier(IDbValueSource db)
        {
            _db = db;
        }

        private sealed record Instance(string Endpoint, string Path, string Concrete, string Table, string Column, string? Device, Expected Value);

        /// <inheritdoc />
        public IReadOnlyList<FieldValueMismatch> Verify(FieldMap map, string outputDir)
        {
            FieldValueReport report = Evaluate(map, ReadDocuments(outputDir));
            return [.. report.Mismatches.Concat(report.MissingRows).Select(f => new FieldValueMismatch($"{f.Endpoint}:{f.Path}", f.Target, f.Detail))];
        }

        /// <summary>
        /// Reads the saved responses of a selftester run. A device-settings file belongs to the device id in its name
        /// (<c>endpoint_deviceid_timestamp.json</c>); the device list is scoped per element. Failed calls are skipped.
        /// </summary>
        public static List<FixtureDocument> ReadDocuments(string outputDir)
        {
            Dictionary<string, string> failed = FieldCoverageAnalyzer.ReadFailedFiles(outputDir);
            var docs = new List<FixtureDocument>();
            foreach (string file in Directory.EnumerateFiles(outputDir, "*.json").OrderBy(f => f, StringComparer.Ordinal))
            {
                string name = Path.GetFileName(file);
                int underscore = name.IndexOf('_', StringComparison.Ordinal);
                if (underscore <= 0 || name.StartsWith("db-completeness", StringComparison.Ordinal) || failed.ContainsKey(name))
                {
                    continue;
                }

                string endpoint = name[..underscore];
                string[] parts = name[..^".json".Length].Split('_');
                string? device = endpoint == "device-settings" && parts.Length >= 3 ? parts[1] : null;
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(file));
                    docs.Add(new FixtureDocument(endpoint, name, doc.RootElement.Clone(), device, ScopeFromTopLevelId: endpoint == "devices"));
                }
                catch (JsonException)
                {
                    // An unreadable file is reported by the coverage check.
                }
            }

            return docs;
        }

        public FieldValueReport Evaluate(FieldMap map, IEnumerable<FixtureDocument> documents)
        {
            var report = new FieldValueReport();
            var instances = new List<Instance>();
            foreach (FixtureDocument doc in documents)
            {
                var walker = new Walker(map, doc, report, instances);
                walker.Walk(doc.Root, string.Empty, string.Empty, doc.DeviceScope);
            }

            foreach (IGrouping<(string Table, string Column, string? Device), Instance> group in instances.GroupBy(i => (i.Table, i.Column, i.Device)))
            {
                CompareGroup(report, group.Key.Table, group.Key.Column, group.Key.Device, [.. group]);
            }

            return report;
        }

        private void CompareGroup(FieldValueReport report, string table, string column, string? device, List<Instance> group)
        {
            if (!_db.HasColumn(table, column))
            {
                foreach (Instance i in group)
                {
                    report.For(i.Endpoint).MissingRow++;
                    report.MissingRows.Add(new ValueFinding(i.Endpoint, i.Concrete, $"{table}.{column}", "table or column not in the database"));
                }

                return;
            }

            bool scoped = device is not null && _db.IsDeviceScoped(table);
            if (scoped)
            {
                report.ScopedGroups++;
            }
            else
            {
                report.UnscopedGroups++;
                if (device is not null)
                {
                    _ = report.DeviceDataInUnscopedTables.Add(table);
                }
            }

            IReadOnlyList<DbCell> cells = _db.Values(table, column, scoped ? device : null);
            if (cells.Count == 0)
            {
                foreach (Instance i in group)
                {
                    report.For(i.Endpoint).MissingRow++;
                    report.MissingRows.Add(new ValueFinding(i.Endpoint, i.Concrete, $"{table}.{column}", scoped ? "no row stored for this device" : "no row stored"));
                }

                return;
            }

            bool[] used = new bool[cells.Count];
            var matched = new List<Instance>();
            var unmatched = new List<Instance>();
            foreach (Instance i in group)
            {
                int hit = -1;
                bool converted = false;
                for (int c = 0; c < cells.Count && hit < 0; c++)
                {
                    if (!used[c] && ValueComparer.Equal(i.Value, cells[c], out bool viaConversion))
                    {
                        hit = c;
                        converted = viaConversion;
                    }
                }

                if (hit < 0)
                {
                    unmatched.Add(i);
                    continue;
                }

                used[hit] = true;
                matched.Add(i);
                EndpointValueCounts counts = report.For(i.Endpoint);
                counts.Matched++;
                if (converted)
                {
                    counts.MatchedAfterConversion++;
                }
            }

            foreach (Instance i in unmatched)
            {
                EndpointValueCounts counts = report.For(i.Endpoint);
                if (matched.Any(m => m.Endpoint != i.Endpoint))
                {
                    counts.Shadowed++;
                    report.Shadowed.Add(new ValueFinding(i.Endpoint, i.Concrete, $"{table}.{column}", "another endpoint's value is stored in this column"));
                    continue;
                }

                counts.Mismatched++;
                string left = string.Join(", ", cells.Where((_, idx) => !used[idx]).Take(3).Select(c => ValueComparer.Describe(c)));
                report.Mismatches.Add(new ValueFinding(i.Endpoint, i.Concrete, $"{table}.{column}",
                    $"expected {i.Value.Describe()}; stored values left: {(left.Length == 0 ? "none" : left)}"));
            }
        }

        /// <summary>Walks one response, counting the leaves that are not compared and collecting the ones that are.</summary>
        private sealed class Walker(FieldMap map, FixtureDocument doc, FieldValueReport report, List<Instance> instances)
        {
            public void Walk(JsonElement element, string path, string concrete, string? device)
            {
                switch (element.ValueKind)
                {
                    case JsonValueKind.Object:
                        bool any = false;
                        foreach (JsonProperty p in element.EnumerateObject())
                        {
                            any = true;
                            Walk(p.Value, path.Length == 0 ? p.Name : $"{path}.{p.Name}", concrete.Length == 0 ? p.Name : $"{concrete}.{p.Name}", device);
                        }

                        if (!any)
                        {
                            report.For(doc.Endpoint).AddNotEvaluated(NotEvaluatedReason.EmptyContainer);
                        }

                        break;
                    case JsonValueKind.Array:
                        int index = 0;
                        foreach (JsonElement item in element.EnumerateArray())
                        {
                            string? childDevice = device;
                            if (childDevice is null && doc.ScopeFromTopLevelId && path.Length > 0 && path.IndexOfAny(['.', '[']) < 0
                                && item.ValueKind == JsonValueKind.Object && item.TryGetProperty("id", out JsonElement id))
                            {
                                childDevice = id.ToString();
                            }

                            Walk(item, path + "[]", $"{concrete}[{index++}]", childDevice);
                        }

                        if (index == 0)
                        {
                            report.For(doc.Endpoint).AddNotEvaluated(NotEvaluatedReason.EmptyContainer);
                        }

                        break;
                    case JsonValueKind.Null:
                    case JsonValueKind.Undefined:
                        report.For(doc.Endpoint).AddNotEvaluated(NotEvaluatedReason.NullInResponse);
                        break;
                    default:
                        Leaf(element, path, concrete, device);
                        break;
                }
            }

            private void Leaf(JsonElement value, string path, string concrete, string? device)
            {
                EndpointValueCounts counts = report.For(doc.Endpoint);
                if (!doc.Synced)
                {
                    counts.AddNotEvaluated(NotEvaluatedReason.NotSynced);
                    return;
                }

                FieldTarget? target = map.Resolve(doc.Endpoint, path.Length == 0 ? "$" : path);
                if (target is null)
                {
                    counts.AddNotEvaluated(NotEvaluatedReason.Unmapped);
                    return;
                }

                if (target.Kind == FieldTargetKind.Rows)
                {
                    counts.AddNotEvaluated(NotEvaluatedReason.Rows);
                    return;
                }

                if (target.Kind == FieldTargetKind.NotData)
                {
                    counts.AddNotEvaluated(NotEvaluatedReason.NotData);
                    return;
                }

                var expected = Expected.FromJson(value);
                instances.Add(new Instance(doc.Endpoint, path, concrete, target.Table!, target.Column!, device, expected));

                // Columns the app derives from a raw value (RingHealthUnits) are checked beside the raw one.
                foreach (DerivedColumn derived in DerivedColumns.For(target.Table!, target.Column!))
                {
                    Expected? converted = derived.Convert(value);
                    if (converted is not null)
                    {
                        instances.Add(new Instance(doc.Endpoint, path, concrete + " (derived)", target.Table!, derived.Column, device, converted.Value));
                    }
                }
            }
        }

        /// <summary>The columns the app fills from a raw Ring value by a fixed conversion.</summary>
        private sealed record DerivedColumn(string Column, Func<JsonElement, Expected?> Convert);

        private static class DerivedColumns
        {
            private static readonly Dictionary<(string, string), DerivedColumn[]> Registry = new()
            {
                [("DeviceReportedHealths", "StatusTime")] =
                [
                    new DerivedColumn("StatusAtUtc", v => v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out long ticks) && RingHealthUnits.StatusTimeToUtc(ticks) is { } at
                        ? Expected.FromDate(at) : null)
                ],
                [("DeviceReportedHealths", "BatteryTemp")] =
                [
                    new DerivedColumn("BatteryTempCelsius", v => v.ValueKind == JsonValueKind.Number && RingHealthUnits.BatteryTempToCelsius(v.GetDouble()) is { } c
                        ? Expected.FromNumber((double)c) : null)
                ]
            };

            public static IEnumerable<DerivedColumn> For(string table, string column)
            {
                return Registry.TryGetValue((table, column), out DerivedColumn[]? found) ? found : [];
            }
        }
    }

    /// <summary>A response value in a form that can be compared with a stored one.</summary>
    internal readonly record struct Expected(JsonValueKind Kind, string? Text, long Integer, double Number, bool IsDate, DateTime Date)
    {
        public static Expected FromJson(JsonElement e)
        {
            return e.ValueKind switch
            {
                JsonValueKind.True => new Expected(JsonValueKind.True, null, 1, 1, false, default),
                JsonValueKind.False => new Expected(JsonValueKind.False, null, 0, 0, false, default),
                JsonValueKind.Number => e.TryGetInt64(out long l)
                    ? new Expected(JsonValueKind.Number, e.GetRawText(), l, l, false, default)
                    : new Expected(JsonValueKind.Number, e.GetRawText(), 0, e.GetDouble(), false, default),
                _ => new Expected(JsonValueKind.String, e.GetString(), 0, 0, false, default)
            };
        }

        public static Expected FromDate(DateTime utc)
        {
            return new Expected(JsonValueKind.String, null, 0, 0, true, utc);
        }

        public static Expected FromNumber(double value)
        {
            return new Expected(JsonValueKind.Number, value.ToString("R", CultureInfo.InvariantCulture), (long)value, value, false, default);
        }

        public bool IsIntegral => Kind == JsonValueKind.Number && !Text!.Contains('.', StringComparison.Ordinal) && !Text.Contains('e', StringComparison.OrdinalIgnoreCase);

        /// <summary>The kind, and the value when it is not personal-like (numbers, booleans and short plain strings).</summary>
        public string Describe()
        {
            return Kind switch
            {
                JsonValueKind.True or JsonValueKind.False => $"bool {Kind.ToString().ToLowerInvariant()}",
                JsonValueKind.Number => $"number {Text}",
                _ when IsDate => "date",
                _ => ValueComparer.IsSafeText(Text) ? $"string '{Text}'" : "string"
            };
        }
    }

    /// <summary>Type-aware equality between a response value and a stored value.</summary>
    internal static class ValueComparer
    {
        private const double RelativeTolerance = 1e-5;

        /// <summary>True when the stored value equals the response value. <paramref name="viaConversion"/> is set when a documented conversion was needed.</summary>
        public static bool Equal(Expected expected, DbCell cell, out bool viaConversion)
        {
            viaConversion = false;
            object? actual = cell.Value;
            if (actual is null)
            {
                return false;
            }

            switch (expected.Kind)
            {
                case JsonValueKind.True:
                case JsonValueKind.False:
                    bool want = expected.Kind == JsonValueKind.True;
                    if (actual is bool b)
                    {
                        return b == want;
                    }

                    if (actual is long bl && bl is 0 or 1)
                    {
                        viaConversion = true;
                        return bl == 1 == want;
                    }

                    if (actual is string bs && bool.TryParse(bs, out bool parsed))
                    {
                        viaConversion = true;
                        return parsed == want;
                    }

                    return false;

                case JsonValueKind.Number:
                    return NumberEqual(expected, actual, out viaConversion);

                default:
                    return StringEqual(expected, actual, out viaConversion);
            }
        }

        private static bool NumberEqual(Expected expected, object actual, out bool viaConversion)
        {
            viaConversion = false;
            switch (actual)
            {
                case long l:
                    return expected.IsIntegral ? l == expected.Integer : Math.Abs(l - expected.Number) <= Tolerance(expected.Number);
                case double d:
                    return Math.Abs(d - expected.Number) <= Tolerance(expected.Number);
                case bool b:
                    viaConversion = true;
                    return expected.IsIntegral && expected.Integer is 0 or 1 && expected.Integer == 1 == b;
                case string s:
                    viaConversion = true;
                    return string.Equals(s, expected.Text, StringComparison.Ordinal)
                        || (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double sd) && Math.Abs(sd - expected.Number) <= Tolerance(expected.Number));
                case DateTime dt:
                    // Epoch milliseconds, then seconds.
                    viaConversion = true;
                    return expected.IsIntegral && (SameInstant(dt, DateTimeOffset.FromUnixTimeMilliseconds(Clamp(expected.Integer)).UtcDateTime)
                        || SameInstant(dt, DateTimeOffset.FromUnixTimeSeconds(Clamp(expected.Integer)).UtcDateTime));
                default:
                    return false;
            }
        }

        private static bool StringEqual(Expected expected, object actual, out bool viaConversion)
        {
            viaConversion = false;
            if (expected.IsDate)
            {
                return actual is DateTime d && SameInstant(d, expected.Date);
            }

            string text = expected.Text ?? string.Empty;
            switch (actual)
            {
                case string s:
                    return string.Equals(s, text, StringComparison.Ordinal);
                case DateTime dt:
                    return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset parsed) && SameInstant(dt, parsed.UtcDateTime);
                case Guid g:
                    return Guid.TryParse(text, out Guid wanted) && wanted == g;
                case long l:
                    viaConversion = true;
                    // Zone states: Ring sends the words, the app stores 1 and 0 (ZoneStateConverter).
                    return text.Equals("enabled", StringComparison.OrdinalIgnoreCase) ? l == 1
                        : text.Equals("disabled", StringComparison.OrdinalIgnoreCase) ? l == 0
                        : long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long tl) && tl == l;
                case double d:
                    viaConversion = true;
                    return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double td) && Math.Abs(td - d) <= Tolerance(td);
                case bool b:
                    viaConversion = true;
                    return bool.TryParse(text, out bool tb) && tb == b;
                default:
                    return false;
            }
        }

        private static bool SameInstant(DateTime a, DateTime b)
        {
            // JSON dates carry at most millisecond precision; allow one tick of rounding.
            return Math.Abs((a.ToUniversalTime() - b.ToUniversalTime()).Ticks) <= 10;
        }

        private static long Clamp(long value)
        {
            return Math.Clamp(value, -62135596800L, 253402300799L);
        }

        private static double Tolerance(double value)
        {
            return RelativeTolerance * Math.Max(1d, Math.Abs(value));
        }

        /// <summary>A stored value as text for messages: its kind, and the value when it is not personal-like.</summary>
        public static string Describe(DbCell cell)
        {
            return cell.Value switch
            {
                null => "null",
                bool b => $"bool {b.ToString().ToLowerInvariant()}",
                long l => $"integer {l}",
                double d => $"real {d.ToString("R", CultureInfo.InvariantCulture)}",
                DateTime => "date",
                Guid => "guid",
                string s => IsSafeText(s) ? $"text '{s}'" : "text",
                _ => cell.Value.GetType().Name
            };
        }

        /// <summary>True for short plain text (no '@', not a long digit run); anything else is described by type only.</summary>
        public static bool IsSafeText(string? text)
        {
            return text is { Length: <= 40 } && !text.Contains('@', StringComparison.Ordinal)
                && text.All(c => char.IsLetterOrDigit(c) || c is '_' or '-' or '.' or ' ' or ':')
                && text.Count(char.IsDigit) < 7;
        }
    }

    /// <summary>
    /// Reads stored values with plain SQL over a SQLite file. Column CLR types come from the EF model, so booleans, dates and
    /// GUIDs are converted the way EF stores them. A table is tied to a device by following its foreign keys to Devices.
    /// </summary>
    internal sealed class SqliteDbValueSource : IDbValueSource
    {
        private sealed record TablePlan(Dictionary<string, Type> Columns, string? ScopeJoins, string? ScopeExpression);
        private sealed record TableData(List<Dictionary<string, DbCell>> Rows, List<string?> Devices);

        private readonly SqliteConnection _connection;
        private readonly Dictionary<string, TablePlan> _plans = new(StringComparer.Ordinal);
        private readonly Dictionary<string, TableData> _loaded = new(StringComparer.Ordinal);

        public SqliteDbValueSource(SqliteConnection connection, IModel model)
        {
            _connection = connection;
            foreach (IGrouping<string, IEntityType> entities in model.GetEntityTypes().Where(e => e.GetTableName() is not null).GroupBy(e => e.GetTableName()!, StringComparer.Ordinal))
            {
                var columns = new Dictionary<string, Type>(StringComparer.Ordinal);
                foreach (IEntityType entity in entities)
                {
                    var id = StoreObjectIdentifier.Table(entities.Key, entity.GetSchema());
                    foreach (IProperty property in entity.GetProperties())
                    {
                        string? column = property.GetColumnName(id);
                        if (column is not null)
                        {
                            Type type = property.GetTypeMapping().Converter?.ProviderClrType ?? property.ClrType;
                            columns[column] = Nullable.GetUnderlyingType(type) ?? type;
                        }
                    }
                }

                (string? joins, string? expression) = ScopePlan(entities.First());
                _plans[entities.Key] = new TablePlan(columns, joins, expression);
            }
        }

        public bool HasColumn(string table, string column)
        {
            return _plans.TryGetValue(table, out TablePlan? plan) && plan.Columns.ContainsKey(column);
        }

        public bool IsDeviceScoped(string table)
        {
            return _plans.TryGetValue(table, out TablePlan? plan) && plan.ScopeExpression is not null;
        }

        public IReadOnlyList<DbCell> Values(string table, string column, string? deviceId)
        {
            TableData data = Load(table);
            var cells = new List<DbCell>();
            for (int i = 0; i < data.Rows.Count; i++)
            {
                if (deviceId is null || string.Equals(data.Devices[i], deviceId, StringComparison.Ordinal))
                {
                    cells.Add(data.Rows[i].GetValueOrDefault(column));
                }
            }

            return cells;
        }

        /// <summary>
        /// The shortest chain from the entity to Devices (at most four hops), as SQL joins and the provider id expression. A hop is a
        /// declared foreign key, or a Guid property named <c>DeviceId</c> (most device tables carry it without a declared relationship).
        /// </summary>
        private static (string? Joins, string? Expression) ScopePlan(IEntityType start)
        {
            if (start.ClrType.Name == "Device")
            {
                return (string.Empty, "t.\"ProviderDeviceId\"");
            }

            var queue = new Queue<(IEntityType Entity, string Alias, string Joins, int Depth)>();
            queue.Enqueue((start, "t", string.Empty, 0));
            var seen = new HashSet<IEntityType> { start };
            while (queue.Count > 0)
            {
                (IEntityType entity, string alias, string joins, int depth) = queue.Dequeue();
                IProperty? deviceId = entity.FindProperty("DeviceId");
                if (deviceId is not null && deviceId.ClrType == typeof(Guid))
                {
                    string next = $"d{depth}";
                    return ($"{joins} LEFT JOIN \"Devices\" {next} ON {alias}.\"{deviceId.GetColumnName()}\" = {next}.\"Id\"", $"{next}.\"ProviderDeviceId\"");
                }

                if (depth >= 4)
                {
                    continue;
                }

                foreach (IForeignKey fk in entity.GetForeignKeys())
                {
                    IEntityType principal = fk.PrincipalEntityType;
                    if (fk.Properties.Count != 1 || !seen.Add(principal) || principal.GetTableName() is null)
                    {
                        continue;
                    }

                    string next = $"p{depth + 1}";
                    string join = $"{joins} LEFT JOIN \"{principal.GetTableName()}\" {next} ON {alias}.\"{fk.Properties[0].GetColumnName()}\" = {next}.\"{fk.PrincipalKey.Properties[0].GetColumnName()}\"";
                    if (principal.ClrType.Name == "Device")
                    {
                        return (join, $"{next}.\"ProviderDeviceId\"");
                    }

                    queue.Enqueue((principal, next, join, depth + 1));
                }
            }

            return (null, null);
        }

        private TableData Load(string table)
        {
            if (_loaded.TryGetValue(table, out TableData? cached))
            {
                return cached;
            }

            TablePlan plan = _plans[table];
            using SqliteCommand command = _connection.CreateCommand();
            string scope = plan.ScopeExpression is null ? "NULL" : plan.ScopeExpression;
            command.CommandText = $"SELECT t.*, {scope} AS \"__device\" FROM \"{table}\" t{plan.ScopeJoins}";
            var rows = new List<Dictionary<string, DbCell>>();
            var devices = new List<string?>();
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                var row = new Dictionary<string, DbCell>(StringComparer.Ordinal);
                string? device = null;
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    string name = reader.GetName(i);
                    object raw = reader.GetValue(i);
                    if (name == "__device")
                    {
                        device = raw is DBNull ? null : System.Convert.ToString(raw, CultureInfo.InvariantCulture);
                    }
                    else
                    {
                        row[name] = new DbCell(ToCell(raw, plan.Columns.GetValueOrDefault(name)));
                    }
                }

                rows.Add(row);
                devices.Add(device);
            }

            return _loaded[table] = new TableData(rows, devices);
        }

        private static object? ToCell(object raw, Type? clr)
        {
            return raw is DBNull
                ? null
                : clr == typeof(bool) && raw is long l
                ? l != 0
                : (clr == typeof(DateTime) || clr == typeof(DateTimeOffset)) && raw is string text
                && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset when)
                ? when.UtcDateTime
                : clr == typeof(Guid) && raw is string g && Guid.TryParse(g, out Guid guid) ? guid : raw is byte[]? "(blob)" : raw;
        }
    }
}
