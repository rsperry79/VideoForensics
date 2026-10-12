using System.Web;

namespace VideoForensics.Ui.Shared.Services.Cases;

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
        var query = uri.Query;

        // Parse existing query string
        var pairs = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(query))
        {
            // Remove leading '?'
            if (query.StartsWith("?"))
            {
                query = query.Substring(1);
            }

            var parts = query.Split('&', StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var eqIndex = part.IndexOf('=');
                if (eqIndex > 0)
                {
                    var key = part.Substring(0, eqIndex);
                    var value = HttpUtility.UrlDecode(part.Substring(eqIndex + 1));
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
            pairs.Remove("case");
        }

        // Rebuild query string
        var newParts = pairs.Select(kvp =>
            string.IsNullOrEmpty(kvp.Value)
                ? HttpUtility.UrlEncode(kvp.Key)
                : $"{HttpUtility.UrlEncode(kvp.Key)}={HttpUtility.UrlEncode(kvp.Value)}");
        var newQuery = string.Join("&", newParts);

        // Reconstruct URI
        var baseUri = uri.GetLeftPart(UriPartial.Path);
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
            var query = parsedUri.Query;

            if (string.IsNullOrEmpty(query))
            {
                return null;
            }

            // Remove leading '?'
            if (query.StartsWith("?"))
            {
                query = query.Substring(1);
            }

            var parts = query.Split('&', StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var eqIndex = part.IndexOf('=');
                if (eqIndex > 0)
                {
                    var key = part.Substring(0, eqIndex);
                    if (key == "case")
                    {
                        var value = HttpUtility.UrlDecode(part.Substring(eqIndex + 1));
                        if (!string.IsNullOrEmpty(value) && Guid.TryParse(value, out var caseId))
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
