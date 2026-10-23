using System.Web;

namespace VideoForensics.Client.Ui.Shared.Services.Cases;

/// <summary>
/// Helper for managing the 'case' query parameter in the URL.
/// </summary>
public static class CaseUrl
{
    /// <summary>
    /// Merges a case ID into the current URI, updating or removing the 'case' query parameter.
    /// Preserves all other query parameters.
    /// </summary>
    public static string Merge(string? currentUri, Guid? caseId)
    {
        if (string.IsNullOrEmpty(currentUri))
        {
            currentUri = "http://localhost/";
        }

        var uri = new Uri(currentUri, UriKind.Absolute);
        string query = uri.Query;

        // Parse existing query string
        var pairs = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(query))
        {
            // Remove leading '?'
            if (query.StartsWith("?"))
            {
                query = query[1..];
            }

            string[] parts = query.Split('&', StringSplitOptions.RemoveEmptyEntries);
            foreach (string part in parts)
            {
                int eqIndex = part.IndexOf('=');
                if (eqIndex > 0)
                {
                    string key = part[..eqIndex];
                    string value = HttpUtility.UrlDecode(part[(eqIndex + 1)..]);
                    pairs[key] = value;
                }
                else if (eqIndex < 0)
                {
                    // Key with no value
                    pairs[part] = string.Empty;
                }
            }
        }

        // Update or remove 'case' key
        if (caseId.HasValue)
        {
            pairs["case"] = caseId.Value.ToString("D");
        }
        else
        {
            _ = pairs.Remove("case");
        }

        // Rebuild query string
        IEnumerable<string> newParts = pairs.Select(kvp =>
            string.IsNullOrEmpty(kvp.Value)
                ? HttpUtility.UrlEncode(kvp.Key)
                : $"{HttpUtility.UrlEncode(kvp.Key)}={HttpUtility.UrlEncode(kvp.Value)}");
        string newQuery = string.Join("&", newParts);

        // Reconstruct URI
        string baseUri = uri.GetLeftPart(UriPartial.Path);
        return string.IsNullOrEmpty(newQuery) ? baseUri : $"{baseUri}?{newQuery}";
    }

    /// <summary>
    /// Reads the 'case' query parameter from a URI and returns it as a GUID, or null if not found or invalid.
    /// </summary>
    public static Guid? Read(string? uri)
    {
        if (string.IsNullOrEmpty(uri))
        {
            return null;
        }

        try
        {
            var parsedUri = new Uri(uri, UriKind.RelativeOrAbsolute);
            string query = parsedUri.Query;

            if (string.IsNullOrEmpty(query))
            {
                return null;
            }

            // Remove leading '?'
            if (query.StartsWith("?"))
            {
                query = query[1..];
            }

            string[] parts = query.Split('&', StringSplitOptions.RemoveEmptyEntries);
            foreach (string part in parts)
            {
                int eqIndex = part.IndexOf('=');
                if (eqIndex > 0)
                {
                    string key = part[..eqIndex];
                    if (key == "case")
                    {
                        string value = HttpUtility.UrlDecode(part[(eqIndex + 1)..]);
                        if (!string.IsNullOrEmpty(value) && Guid.TryParse(value, out Guid caseId))
                        {
                            return caseId;
                        }
                    }
                }
            }
        }
        catch
        {
            // Invalid URI format - return null
        }

        return null;
    }
}
