namespace VideoForensics.Client.Ui.Shared.Services.Scope;

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
        int fragmentIndex = currentUri.IndexOf('#');
        string fragment = fragmentIndex >= 0 ? currentUri[fragmentIndex..] : string.Empty;
        string uriWithoutFragment = fragmentIndex >= 0 ? currentUri[..fragmentIndex] : currentUri;

        int queryIndex = uriWithoutFragment.IndexOf('?');
        string path = queryIndex >= 0 ? uriWithoutFragment[..queryIndex] : uriWithoutFragment;
        string existingQuery = queryIndex >= 0 ? uriWithoutFragment[(queryIndex + 1)..] : string.Empty;

        // Parse existing query string, keeping only unrelated keys
        List<string> unrelatedKeys = ParseUnrelatedKeys(existingQuery);

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

        string newQuery = string.Join("&", newQueryParts);

        // Reconstruct the URI
        return string.IsNullOrWhiteSpace(newQuery) ? path + fragment : path + "?" + newQuery + fragment;
    }

    private static List<string> ParseUnrelatedKeys(string query)
    {
        var result = new List<string>();

        if (string.IsNullOrWhiteSpace(query))
        {
            return result;
        }

        string[] pairs = query.Split('&');
        foreach (string pair in pairs)
        {
            int eqIndex = pair.IndexOf('=');
            if (eqIndex <= 0)
            {
                continue;
            }

            string key = pair[..eqIndex];
            if (!ScopeKeys.Contains(key))
            {
                result.Add(pair);
            }
        }

        return result;
    }
}
