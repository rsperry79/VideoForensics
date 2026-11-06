namespace VideoForensics.Client.Ui.Shared.Layout
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
            int queryIndex = itemPath.IndexOf('?');
            string itemPathPart = queryIndex >= 0 ? itemPath[..queryIndex] : itemPath;
            string? itemQueryPart = queryIndex >= 0 ? itemPath[(queryIndex + 1)..] : null;

            bool pathMatches = itemPathPart == "/"
                ? currentPath == "/"
                : currentPath.Equals(itemPathPart, StringComparison.OrdinalIgnoreCase)
                || currentPath.StartsWith(itemPathPart + "/", StringComparison.OrdinalIgnoreCase);

            if (!pathMatches || string.IsNullOrEmpty(itemQueryPart))
            {
                return pathMatches;
            }

            Dictionary<string, string> currentParams = ParseQuery(currentQuery);
            foreach (string pair in itemQueryPart.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                int eqIndex = pair.IndexOf('=');
                string key = eqIndex >= 0 ? pair[..eqIndex] : pair;
                string value = eqIndex >= 0 ? pair[(eqIndex + 1)..] : string.Empty;

                if (!currentParams.TryGetValue(key, out string? currentValue) || currentValue != value)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// The most specific item path that matches the current location, or null when none does. The owner is chosen
        /// from the whole catalog, so a page whose own item is hidden is not claimed by a parent item.
        /// </summary>
        /// <param name="itemPaths">Every nav item path in the catalog.</param>
        /// <param name="currentPath">The current URI's path only.</param>
        /// <param name="currentQuery">The current URI's query string. May be null/empty.</param>
        public static string? BestMatch(IEnumerable<string> itemPaths, string currentPath, string? currentQuery)
        {
            return itemPaths
                .Where(path => Matches(currentPath, currentQuery, path))
                .OrderByDescending(Specificity)
                .Select(path => (string?)path)
                .FirstOrDefault();
        }

        /// <summary>
        /// Ranks an item for the active-item choice when several items match the same page. An item that carries a query
        /// string (e.g. <c>/media?tab=Events</c>) outranks a plain item on the same path (<c>/media</c>), so the plain item
        /// does not win a tie by order. Longer paths rank higher than shorter ones.
        /// </summary>
        public static int Specificity(string itemPath)
        {
            int queryIndex = itemPath.IndexOf('?');
            string itemPathPart = queryIndex >= 0 ? itemPath[..queryIndex] : itemPath;
            return (queryIndex >= 0 ? 10_000 : 0) + itemPathPart.Length;
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

            foreach (string pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                int eqIndex = pair.IndexOf('=');
                string key = eqIndex >= 0 ? pair[..eqIndex] : pair;
                string value = eqIndex >= 0 ? pair[(eqIndex + 1)..] : string.Empty;
                result[key] = value;
            }

            return result;
        }
    }
}
