using System.Globalization;
using System.Web;
using VideoForensics.Client.Common.Viewer;
using VideoForensics.Client.Ui.Shared.Services.Scope;
using VideoForensics.Client.Ui.Shared.Services.Time;

namespace VideoForensics.Client.Ui.Shared.Services.Review;

/// <summary>
/// The review page's link state. Each value is null (or the default tab) when the link does not carry it.
/// </summary>
/// <remarks>
/// Equality compares <see cref="Devices"/> by sequence, because the list is a reference type and a record's
/// default equality would treat two equal lists as different.
/// </remarks>
public sealed record ReviewUrlState(
    IReadOnlyList<Guid> Devices,
    DateTime? FromUtc,
    DateTime? ToUtc,
    DateTime? CursorUtc,
    ReviewTab Tab,
    string? Provider,
    Guid? Account,
    Guid? CaseId = null,
    Guid? Location = null)
{
    /// <summary>Compares every field, with <see cref="Devices"/> compared by sequence.</summary>
    public bool Equals(ReviewUrlState? other)
    {
        return other is not null
            && Devices.SequenceEqual(other.Devices)
            && FromUtc == other.FromUtc
            && ToUtc == other.ToUtc
            && CursorUtc == other.CursorUtc
            && Tab == other.Tab
            && Provider == other.Provider
            && Account == other.Account
            && CaseId == other.CaseId
            && Location == other.Location;
    }

    /// <summary>Hash code that agrees with <see cref="Equals(ReviewUrlState)"/>.</summary>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (Guid device in Devices)
        {
            hash.Add(device);
        }

        hash.Add(FromUtc);
        hash.Add(ToUtc);
        hash.Add(CursorUtc);
        hash.Add(Tab);
        hash.Add(Provider);
        hash.Add(Account);
        hash.Add(CaseId);
        hash.Add(Location);
        return hash.ToHashCode();
    }
}

/// <summary>
/// Reads and writes the review page's query string. Devices, from, and to use the same names and formats as
/// the global scope, so a scope link and a review link read the same way; the review page adds cursor (t),
/// tab, provider, account, and location.
/// </summary>
public static class ReviewUrl
{
    private const string CursorFormat = "O";
    private const string ScopeInstantFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";

    /// <summary>
    /// Parses a query string (with or without a leading '?'). Never throws: bad guids, dates, and tabs are ignored,
    /// unknown keys are skipped, and more than <see cref="CameraSync.MaxTiles"/> devices are truncated.
    /// </summary>
    public static ReviewUrlState Parse(string? query)
    {
        var devices = new List<Guid>();
        DateTime? from = null;
        DateTime? to = null;
        DateTime? cursor = null;
        ReviewTab tab = ReviewTab.Timeline;
        string? provider = null;
        Guid? account = null;
        Guid? caseId = null;
        Guid? location = null;

        if (!string.IsNullOrWhiteSpace(query))
        {
            string text = query.StartsWith('?') ? query[1..] : query;
            foreach (string pair in text.Split('&'))
            {
                int eqIndex = pair.IndexOf('=');
                if (eqIndex <= 0)
                {
                    continue;
                }

                string key = pair[..eqIndex];
                string value = HttpUtility.UrlDecode(pair[(eqIndex + 1)..]) ?? string.Empty;

                switch (key)
                {
                    case "devices":
                        devices.AddRange(ParseDevices(value));
                        break;
                    case "from":
                        from = TryParseScopeInstant(value, out DateTime fromValue) ? fromValue : from;
                        break;
                    case "to":
                        to = TryParseScopeInstant(value, out DateTime toValue) ? toValue : to;
                        break;
                    case "t":
                        cursor = TryParseCursor(value, out DateTime cursorValue) ? cursorValue : cursor;
                        break;
                    case "tab":
                        tab = TryParseTab(value, out ReviewTab tabValue) ? tabValue : tab;
                        break;
                    case "provider":
                        provider = string.IsNullOrWhiteSpace(value) ? provider : value;
                        break;
                    case "account":
                        account = Guid.TryParse(value.Trim(), out Guid accountValue) ? accountValue : account;
                        break;
                    case "location":
                        location = Guid.TryParse(value.Trim(), out Guid locationValue) ? locationValue : location;
                        break;
                    case "reviewcase":
                        caseId = Guid.TryParse(value.Trim(), out Guid caseValue) ? caseValue : caseId;
                        break;
                }
            }
        }

        return new ReviewUrlState(
            devices.Distinct().Take(CameraSync.MaxTiles).ToList(),
            from,
            to,
            cursor,
            tab,
            provider,
            account,
            caseId,
            location);
    }

    /// <summary>
    /// Returns <paramref name="uri"/> without the <c>reviewcase</c> (case view) parameter, keeping every other query
    /// parameter, including <c>case</c>, the app-wide active-case context that <c>CaseUrl</c> manages.
    /// </summary>
    public static string WithoutCaseView(string uri)
    {
        int queryStart = uri.IndexOf('?');
        if (queryStart < 0)
        {
            return uri;
        }

        string path = uri[..queryStart];
        string[] kept = uri[(queryStart + 1)..]
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(part => !part.StartsWith("reviewcase=", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return kept.Length == 0 ? path : path + "?" + string.Join('&', kept);
    }

    /// <summary>
    /// Builds a query string (no leading '?'). Omits values the state does not carry and the default Timeline tab.
    /// Times are written to the second, which is the precision the scope links use.
    /// </summary>
    public static string Build(ReviewUrlState state)
    {
        var parts = new List<string>();

        if (state.Devices.Count > 0)
        {
            parts.Add("devices=" + string.Join(",", state.Devices.Select(device => device.ToString("D"))));
        }

        if (state.FromUtc is DateTime from)
        {
            parts.Add("from=" + FormatScopeInstant(from));
        }

        if (state.ToUtc is DateTime to)
        {
            parts.Add("to=" + FormatScopeInstant(to));
        }

        if (state.CursorUtc is DateTime cursor)
        {
            parts.Add("t=" + UserTimeZone.AsUtc(cursor).ToString(CursorFormat, CultureInfo.InvariantCulture));
        }

        if (state.Tab != ReviewTab.Timeline)
        {
            parts.Add("tab=" + state.Tab.ToString());
        }

        if (!string.IsNullOrWhiteSpace(state.Provider))
        {
            parts.Add("provider=" + HttpUtility.UrlEncode(state.Provider));
        }

        if (state.Account is Guid account)
        {
            parts.Add("account=" + account.ToString("D"));
        }

        if (state.Location is Guid location)
        {
            parts.Add("location=" + location.ToString("D"));
        }

        if (state.CaseId is Guid caseGuid)
        {
            parts.Add("reviewcase=" + caseGuid.ToString("D"));
        }

        return string.Join("&", parts);
    }

    private static IEnumerable<Guid> ParseDevices(string value)
    {
        foreach (string part in value.Split(','))
        {
            if (Guid.TryParse(part.Trim(), out Guid device))
            {
                yield return device;
            }
        }
    }

    private static string FormatScopeInstant(DateTime value)
    {
        return UserTimeZone.AsUtc(value).ToString(ScopeInstantFormat, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Reads a from or to value with the scope's own parser, so a plain date and a UTC instant both read as the scope does.
    /// </summary>
    private static bool TryParseScopeInstant(string value, out DateTime utc)
    {
        if (ScopeState.TryParseScopeDate(value, out DateTime parsed))
        {
            utc = UserTimeZone.AsUtc(parsed);
            return true;
        }

        utc = default;
        return false;
    }

    /// <summary>Reads the cursor as an ISO-8601 UTC instant. Anything else is ignored.</summary>
    private static bool TryParseCursor(string value, out DateTime utc)
    {
        return DateTime.TryParseExact(
            value,
            [CursorFormat, ScopeInstantFormat],
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out utc);
    }

    /// <summary>Reads the tab by name, ignoring case. Numbers and unknown names are not tabs.</summary>
    private static bool TryParseTab(string value, out ReviewTab tab)
    {
        foreach (ReviewTab candidate in Enum.GetValues<ReviewTab>())
        {
            if (string.Equals(candidate.ToString(), value, StringComparison.OrdinalIgnoreCase))
            {
                tab = candidate;
                return true;
            }
        }

        tab = ReviewTab.Timeline;
        return false;
    }
}
