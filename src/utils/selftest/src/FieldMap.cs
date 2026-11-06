using System.Text.Json;
using System.Text.RegularExpressions;

namespace VideoForensics.Utils.SelfTest
{
    internal enum FieldTargetKind
    {
        /// <summary>A typed column in the EF model (<c>Table.Column</c>).</summary>
        Column,

        /// <summary>Keyed or array data stored as child rows of a table (<c>Rows:Table</c>).</summary>
        Rows,

        /// <summary>Not data that is stored (an error body, an empty container); <c>NotData:reason</c>.</summary>
        NotData
    }

    /// <summary>Where one response path is stored: a typed column, child rows, or nowhere (with the reason).</summary>
    internal sealed record FieldTarget(FieldTargetKind Kind, string? Table, string? Column, string? Reason)
    {
        /// <summary>The map-file spelling of the target.</summary>
        public string Text => Kind switch
        {
            FieldTargetKind.Column => $"{Table}.{Column}",
            FieldTargetKind.Rows => $"Rows:{Table}",
            _ => $"NotData:{Reason}"
        };
    }

    /// <summary>
    /// The explicit response-path to storage map (<c>field-map.json</c>). Keys are <c>endpoint:json.path</c> with array
    /// indexes normalized to <c>[]</c> (an empty object is <c>{}</c>). A path segment may be <c>*</c> (any one name) and the
    /// last segment may be <c>**</c> (the whole subtree). The most specific matching entry wins; an exact entry beats a wildcard.
    /// </summary>
    internal sealed class FieldMap
    {
        private const string EmbeddedName = "field-map.json";

        private readonly Dictionary<string, FieldTarget> _entries = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<Pattern>> _patterns = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<Pattern>> _wildcards = new(StringComparer.Ordinal);

        private sealed class Pattern(string[] segments, bool subtree, Regex?[] regexes, FieldTarget target)
        {
            public string[] Segments { get; } = segments;

            /// <summary>True when the last segment was <c>**</c>; <see cref="Segments"/> then holds only the prefix.</summary>
            public bool Subtree { get; } = subtree;

            public Regex?[] Regexes { get; } = regexes;
            public FieldTarget Target { get; } = target;
            public int Literals => Segments.Count(s => !s.Contains('*', StringComparison.Ordinal));
        }

        private FieldMap()
        {
        }

        public IReadOnlyDictionary<string, FieldTarget> Entries => _entries;

        /// <summary>Loads the map embedded in this assembly.</summary>
        public static FieldMap LoadEmbedded()
        {
            using Stream stream = typeof(FieldMap).Assembly.GetManifestResourceStream(EmbeddedName)
                ?? throw new InvalidOperationException($"Embedded resource {EmbeddedName} is missing.");
            using var reader = new StreamReader(stream);
            return Parse(reader.ReadToEnd());
        }

        /// <summary>Parses a map file. Throws <see cref="FormatException"/> for a bad key, a bad target or a duplicate key.</summary>
        public static FieldMap Parse(string json)
        {
            var map = new FieldMap();
            using JsonDocument doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new FormatException("The field map must be a JSON object.");
            }

            foreach (JsonProperty p in doc.RootElement.EnumerateObject())
            {
                if (p.Value.ValueKind != JsonValueKind.String)
                {
                    throw new FormatException($"Field map entry '{p.Name}' must be a string.");
                }

                map.Add(p.Name, ParseTarget(p.Name, p.Value.GetString() ?? string.Empty));
            }

            return map;
        }

        /// <summary>The most specific entry for the path, or null when the path is unmapped.</summary>
        public FieldTarget? Resolve(string endpoint, string path)
        {
            if (_entries.TryGetValue(Key(endpoint, path), out FieldTarget? exact))
            {
                return exact;
            }

            if (!_wildcards.TryGetValue(endpoint, out List<Pattern>? patterns))
            {
                return null;
            }

            string[] segments = Split(path);
            Pattern? best = null;
            foreach (Pattern pattern in patterns)
            {
                if (Matches(pattern, segments) && (best is null || pattern.Literals > best.Literals
                    || (pattern.Literals == best.Literals && best.Subtree && !pattern.Subtree)))
                {
                    best = pattern;
                }
            }

            return best?.Target;
        }

        /// <summary>
        /// True when some entry describes a path below <paramref name="path"/> (a child of the object, an element of the
        /// array it names, or the scalar array <c>path[]</c> itself). Used to treat a null or empty container as mapped once the shape inside it is mapped.
        /// </summary>
        public bool HasEntryBelow(string endpoint, string path)
        {
            if (!_patterns.TryGetValue(endpoint, out List<Pattern>? patterns))
            {
                return false;
            }

            string[] segments = path.Length == 0 ? [] : Split(path);
            foreach (Pattern pattern in patterns)
            {
                int length = pattern.Segments.Length + (pattern.Subtree ? 1 : 0);
                if (length <= segments.Length)
                {
                    // Same depth counts only when the entry is the scalar array of that container (entry "tags[]" below path "tags").
                    if (length != segments.Length || pattern.Subtree || segments.Length == 0
                        || StripMarker(pattern.Segments[^1]) == pattern.Segments[^1])
                    {
                        continue;
                    }
                }

                bool below = true;
                for (int i = 0; i < segments.Length && below; i++)
                {
                    below = SegmentMatches(pattern, i, segments[i])
                        || (i == segments.Length - 1 && SegmentMatches(pattern, i, segments[i], stripMarkers: true));
                }

                if (below)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>True when the endpoint has at least one entry that stores data (a column or child rows).</summary>
        public bool HasStoredEntries(string endpoint)
        {
            return _patterns.TryGetValue(endpoint, out List<Pattern>? patterns)
                && patterns.Any(p => p.Target.Kind != FieldTargetKind.NotData);
        }

        private static string Key(string endpoint, string path)
        {
            return $"{endpoint}:{path}";
        }

        private static string[] Split(string path)
        {
            return path.Split('.');
        }

        private static string StripMarker(string segment)
        {
            while (segment.EndsWith("[]", StringComparison.Ordinal) || segment.EndsWith("{}", StringComparison.Ordinal))
            {
                segment = segment[..^2];
            }

            return segment;
        }

        private static bool SegmentMatches(Pattern pattern, int index, string segment, bool stripMarkers = false)
        {
            string text = pattern.Segments[index];
            if (stripMarkers)
            {
                text = StripMarker(text);
                segment = StripMarker(segment);
            }

            if (!text.Contains('*', StringComparison.Ordinal))
            {
                return string.Equals(text, segment, StringComparison.Ordinal);
            }

            return stripMarkers ? Regex.IsMatch(segment, WildcardToRegex(text)) : pattern.Regexes[index]!.IsMatch(segment);
        }

        private static bool Matches(Pattern pattern, string[] segments)
        {
            int prefix = pattern.Segments.Length;
            if (pattern.Subtree)
            {
                if (segments.Length < prefix)
                {
                    return false;
                }

                for (int i = 0; i < prefix; i++)
                {
                    bool last = i == prefix - 1 && segments.Length == prefix;
                    if (!SegmentMatches(pattern, i, segments[i]) && !(last && SegmentMatches(pattern, i, segments[i], stripMarkers: true)))
                    {
                        return false;
                    }
                }

                return true;
            }

            if (segments.Length != prefix)
            {
                return false;
            }

            for (int i = 0; i < prefix; i++)
            {
                if (!SegmentMatches(pattern, i, segments[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static string WildcardToRegex(string segment)
        {
            return "^" + Regex.Escape(segment).Replace("\\*", "[^.\\[\\]{}]+", StringComparison.Ordinal) + "$";
        }

        private void Add(string key, FieldTarget target)
        {
            int colon = key.IndexOf(':', StringComparison.Ordinal);
            if (colon <= 0 || colon == key.Length - 1)
            {
                throw new FormatException($"Field map key '{key}' must be 'endpoint:path'.");
            }

            string endpoint = key[..colon];
            string path = key[(colon + 1)..];
            if (!_entries.TryAdd(key, target))
            {
                throw new FormatException($"Field map key '{key}' is duplicated.");
            }

            string[] segments = Split(path);
            if (segments.Take(segments.Length - 1).Any(s => s == "**"))
            {
                throw new FormatException($"Field map key '{key}': '**' is only allowed as the last segment.");
            }

            bool subtree = segments[^1] == "**";
            string[] prefix = subtree ? segments[..^1] : segments;
            Regex?[] regexes = prefix
                .Select(s => s.Contains('*', StringComparison.Ordinal) ? new Regex(WildcardToRegex(s), RegexOptions.CultureInvariant) : null)
                .ToArray();
            var pattern = new Pattern(prefix, subtree, regexes, target);

            if (!_patterns.TryGetValue(endpoint, out List<Pattern>? all))
            {
                _patterns[endpoint] = all = [];
            }

            all.Add(pattern);
            if (subtree || regexes.Any(r => r is not null))
            {
                if (!_wildcards.TryGetValue(endpoint, out List<Pattern>? wild))
                {
                    _wildcards[endpoint] = wild = [];
                }

                wild.Add(pattern);
            }
        }

        private static FieldTarget ParseTarget(string key, string value)
        {
            if (value.StartsWith("NotData:", StringComparison.Ordinal))
            {
                string reason = value["NotData:".Length..].Trim();
                return reason.Length == 0
                    ? throw new FormatException($"Field map entry '{key}': NotData needs a reason.")
                    : new FieldTarget(FieldTargetKind.NotData, null, null, reason);
            }

            if (value.StartsWith("Rows:", StringComparison.Ordinal))
            {
                string table = value["Rows:".Length..].Trim();
                return IsIdentifier(table)
                    ? new FieldTarget(FieldTargetKind.Rows, table, null, null)
                    : throw new FormatException($"Field map entry '{key}': Rows needs a table name.");
            }

            string[] parts = value.Split('.');
            return parts.Length == 2 && IsIdentifier(parts[0]) && IsIdentifier(parts[1])
                ? new FieldTarget(FieldTargetKind.Column, parts[0], parts[1], null)
                : throw new FormatException($"Field map entry '{key}': '{value}' is not Table.Column, Rows:Table or NotData:reason.");
        }

        private static bool IsIdentifier(string text)
        {
            return text.Length > 0 && text.All(c => char.IsLetterOrDigit(c) || c == '_');
        }
    }
}
