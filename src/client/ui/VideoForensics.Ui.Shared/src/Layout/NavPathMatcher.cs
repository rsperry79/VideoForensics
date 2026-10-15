namespace VideoForensics.Ui.Shared.Layout
{
    /// <summary>
    /// Pure, testable logic for deciding whether a nav item's target path is the "active" one for
    /// highlighting in the left rail / mobile drawer / top tab bar.
    /// </summary>
    /// <remarks>
    /// A nav item's <c>Path</c> is usually a plain path (e.g. <c>/cases</c>), matched by exact
    /// equality or by prefix (<c>/cases/123</c> also matches <c>/cases</c>). Some items (e.g. the
    /// Analyze sub-tabs) instead carry a query string, e.g. <c>/analyze?analysis=anomalies</c> -
    /// for those, the path portion must match AND every key=value pair in the item's query string
    /// must be present with the same value in the current URI's query string. Extra keys in the
    /// current query (e.g. a scope's <c>from=</c>/<c>to=</c>/<c>devices=</c>) are ignored, so the
    /// item stays highlighted while the scope rail changes the date range.
    /// </remarks>
    public static class NavPathMatcher
    {
        /// <summary>
        /// Returns true if <paramref name="itemPath"/> (a nav item's target, optionally with a
        /// query string) is the active match for the current location.
        /// </summary>
        /// <param name="currentPath">The current URI's path only (no query string), e.g. <c>/analyze</c>.</param>
        /// <param name="currentQuery">The current URI's query string, with or without a leading '?'. May be null/empty.</param>
        /// <param name="itemPath">The nav item's <c>Path</c>, e.g. <c>/cases</c> or <c>/analyze?analysis=jamming</c>.</param>
        public static bool Matches(string currentPath, string? currentQuery, string itemPath)
        {
            var queryIndex = itemPath.IndexOf('?');
            var itemPathPart = queryIndex >= 0 ? itemPath[..queryIndex] : itemPath;
            var itemQueryPart = queryIndex >= 0 ? itemPath[(queryIndex + 1)..] : null;

            bool pathMatches = itemPathPart == "/"
                ? currentPath == "/"
                : currentPath.Equals(itemPathPart, StringComparison.OrdinalIgnoreCase)
                || currentPath.StartsWith(itemPathPart + "/", StringComparison.OrdinalIgnoreCase);

            if (!pathMatches || string.IsNullOrEmpty(itemQueryPart))
            {
                return pathMatches;
            }

            var currentParams = ParseQuery(currentQuery);
            foreach (var pair in itemQueryPart.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var eqIndex = pair.IndexOf('=');
                var key = eqIndex >= 0 ? pair[..eqIndex] : pair;
                var value = eqIndex >= 0 ? pair[(eqIndex + 1)..] : string.Empty;

                if (!currentParams.TryGetValue(key, out var currentValue) || currentValue != value)
                {
                    return false;
                }
            }

            return true;
        }

        private static Dictionary<string, string> ParseQuery(string? query)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(query))
            {
                return result;
            }

            if (query.StartsWith('?'))
            {
                query = query[1..];
            }

            foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var eqIndex = pair.IndexOf('=');
                var key = eqIndex >= 0 ? pair[..eqIndex] : pair;
                var value = eqIndex >= 0 ? pair[(eqIndex + 1)..] : string.Empty;
                result[key] = value;
            }

            return result;
        }
    }
}
