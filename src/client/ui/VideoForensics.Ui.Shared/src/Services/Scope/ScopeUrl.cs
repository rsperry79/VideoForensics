namespace VideoForensics.Ui.Shared.Services.Scope;

/// <summary>
/// Helper for merging scope query strings with existing URIs, preserving unrelated query keys and fragments.
/// </summary>
public static class ScopeUrl
{
    private static readonly string[] ScopeKeys = { "devices", "from", "to", "q" };

    /// <summary>
    /// Merge a scope query string into the current URI, keeping unrelated query keys.
    /// Removes scope keys when scopeQuery is empty.
    /// </summary>
    /// <param name="currentUri">The current full or relative URI.</param>
    /// <param name="scopeQuery">The scope query string (without leading '?').</param>
    /// <returns>The merged URI with scope keys updated and unrelated keys preserved.</returns>
    public static string Merge(string currentUri, string scopeQuery)
    {
        if (string.IsNullOrEmpty(currentUri))
        {
            return scopeQuery;
        }

        // Parse the URI to extract path, query, and fragment
        var fragmentIndex = currentUri.IndexOf('#');
        var fragment = fragmentIndex >= 0 ? currentUri.Substring(fragmentIndex) : "";
        var uriWithoutFragment = fragmentIndex >= 0 ? currentUri.Substring(0, fragmentIndex) : currentUri;

        var queryIndex = uriWithoutFragment.IndexOf('?');
        var path = queryIndex >= 0 ? uriWithoutFragment.Substring(0, queryIndex) : uriWithoutFragment;
        var existingQuery = queryIndex >= 0 ? uriWithoutFragment.Substring(queryIndex + 1) : "";

        // Parse existing query string, keeping only unrelated keys
        var unrelatedKeys = ParseUnrelatedKeys(existingQuery);

        // Build new query string: scope keys first, then unrelated keys
        var newQueryParts = new List<string>();

        if (!string.IsNullOrWhiteSpace(scopeQuery))
        {
            newQueryParts.Add(scopeQuery);
        }

        if (unrelatedKeys.Count > 0)
        {
            newQueryParts.AddRange(unrelatedKeys);
        }

        var newQuery = string.Join("&", newQueryParts);

        // Reconstruct the URI
        if (string.IsNullOrWhiteSpace(newQuery))
        {
            return path + fragment;
        }

        return path + "?" + newQuery + fragment;
    }

    private static List<string> ParseUnrelatedKeys(string query)
    {
        var result = new List<string>();

        if (string.IsNullOrWhiteSpace(query))
        {
            return result;
        }

        var pairs = query.Split('&');
        foreach (var pair in pairs)
        {
            var eqIndex = pair.IndexOf('=');
            if (eqIndex <= 0)
            {
                continue;
            }

            var key = pair.Substring(0, eqIndex);
            if (!ScopeKeys.Contains(key))
            {
                result.Add(pair);
            }
        }

        return result;
    }
}
