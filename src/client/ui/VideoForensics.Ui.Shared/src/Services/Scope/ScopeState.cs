using System.Web;

namespace VideoForensics.Client.Ui.Shared.Services.Scope;

/// <summary>
/// Represents the global investigation scope: devices, time window, and search query.
/// </summary>
/// <param name="MatchesNoDevices">
/// True when a provider/account filter leaves no device. An empty <see cref="DeviceIds"/> means "all devices",
/// so this flag is what keeps an empty filter result from widening to every device.
/// </param>
public sealed record ForensicScope(
    IReadOnlyList<Guid> DeviceIds,
    DateTime FromUtc,
    DateTime ToUtc,
    string? Search,
    bool MatchesNoDevices = false)
{
    /// <summary>
    /// Creates a default scope with no devices, last 7 days, and no search.
    /// </summary>
    public static ForensicScope Default(DateTime utcNow)
    {
        DateTime from = utcNow.Date.AddDays(-7);
        DateTime to = utcNow.Date.AddDays(1);
        return new ForensicScope([], from, to, null);
    }

    /// <summary>
    /// True if no specific devices are selected (all devices included).
    /// </summary>
    public bool IncludesAllDevices => DeviceIds.Count == 0 && !MatchesNoDevices;

    /// <summary>
    /// Custom equality that properly compares DeviceIds by sequence equality.
    /// </summary>
    public bool Equals(ForensicScope? other)
    {
        return other is not null && DeviceIds.SequenceEqual(other.DeviceIds)
            && FromUtc == other.FromUtc
            && ToUtc == other.ToUtc
            && Search == other.Search
            && MatchesNoDevices == other.MatchesNoDevices;
    }

    /// <summary>
    /// Custom hash code that accounts for sequence equality of DeviceIds.
    /// </summary>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (Guid deviceId in DeviceIds)
        {
            hash.Add(deviceId);
        }

        hash.Add(FromUtc);
        hash.Add(ToUtc);
        hash.Add(Search);
        hash.Add(MatchesNoDevices);
        return hash.ToHashCode();
    }
}

/// <summary>
/// Circuit-scoped state for the global forensic investigation scope.
/// Tracks devices, time window, and search; raises events when the scope changes.
/// </summary>
public class ScopeState
{
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// The current forensic scope.
    /// </summary>
    public ForensicScope Current { get; private set; }

    /// <summary>
    /// Raised when Set is called and changes the scope.
    /// </summary>
    public event Action? OnChange;

    public ScopeState(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
        Current = ForensicScope.Default(_timeProvider.GetUtcNow().DateTime);
    }

    /// <summary>
    /// Update the scope, normalizing device IDs, dates, and search text.
    /// Raises OnChange only if the scope actually changed.
    /// </summary>
    public void Set(ForensicScope scope)
    {
        ForensicScope normalized = Normalize(scope);

        if (normalized == Current)
        {
            return;
        }

        Current = normalized;
        OnChange?.Invoke();
    }

    /// <summary>
    /// Convert the current scope to a query string (devices=..., from=..., to=..., q=...).
    /// Omits keys that have default values. Returns empty string when fully default.
    /// </summary>
    public string ToQueryString()
    {
        var parts = new List<string>();

        // Devices
        if (Current.DeviceIds.Count > 0)
        {
            string deviceStr = string.Join(",", Current.DeviceIds.Select(g => g.ToString("D")));
            parts.Add($"devices={deviceStr}");
        }
        else if (Current.MatchesNoDevices)
        {
            // An empty devices list means "all"; "none" keeps a no-match filter from widening on reload.
            parts.Add("devices=none");
        }

        // From date
        var defaultScope = ForensicScope.Default(_timeProvider.GetUtcNow().DateTime);
        if (Current.FromUtc != defaultScope.FromUtc)
        {
            parts.Add($"from={Current.FromUtc:yyyy-MM-dd}");
        }

        // To date
        if (Current.ToUtc != defaultScope.ToUtc)
        {
            parts.Add($"to={Current.ToUtc:yyyy-MM-dd}");
        }

        // Search
        if (!string.IsNullOrWhiteSpace(Current.Search))
        {
            string encoded = HttpUtility.UrlEncode(Current.Search);
            parts.Add($"q={encoded}");
        }

        return string.Join("&", parts);
    }

    /// <summary>
    /// Reads a scope bound from a link. A plain date (yyyy-MM-dd) is the day as the forms write it. A UTC instant
    /// (yyyy-MM-ddTHH:mm:ssZ) is what a notice's review link carries, so the scope can open on an hour of the day.
    /// </summary>
    private static bool TryParseScopeDate(string value, out DateTime date)
    {
        if (DateTime.TryParseExact(value, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out date))
        {
            return true;
        }

        return DateTime.TryParseExact(
            value,
            "yyyy-MM-dd'T'HH:mm:ss'Z'",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
            out date);
    }

    /// <summary>
    /// Parse and apply a query string (with or without leading '?').
    /// Ignores unknown keys and invalid values.
    /// Returns true if the scope changed, false if it stayed the same.
    /// </summary>
    public bool ApplyQueryString(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return false;
        }

        // Remove leading '?' if present
        if (query.StartsWith("?"))
        {
            query = query[1..];
        }

        var deviceIds = new List<Guid>();
        bool matchesNoDevices = false;
        DateTime? fromUtc = null;
        DateTime? toUtc = null;
        string? search = null;

        string[] pairs = query.Split('&');
        foreach (string pair in pairs)
        {
            int eqIndex = pair.IndexOf('=');
            if (eqIndex <= 0)
            {
                continue;
            }

            string key = pair[..eqIndex];
            string value = HttpUtility.UrlDecode(pair[(eqIndex + 1)..]);

            if (key == "devices" && value == "none")
            {
                matchesNoDevices = true;
            }
            else if (key == "devices" && !string.IsNullOrWhiteSpace(value))
            {
                string[] parts = value.Split(',');
                foreach (string part in parts)
                {
                    if (Guid.TryParse(part.Trim(), out Guid guid))
                    {
                        deviceIds.Add(guid);
                    }
                }
            }
            else if (key == "from" && !string.IsNullOrWhiteSpace(value))
            {
                if (TryParseScopeDate(value, out DateTime from))
                {
                    fromUtc = from;
                }
            }
            else if (key == "to" && !string.IsNullOrWhiteSpace(value))
            {
                if (TryParseScopeDate(value, out DateTime to))
                {
                    toUtc = to;
                }
            }
            else if (key == "q" && !string.IsNullOrWhiteSpace(value))
            {
                search = value;
            }
        }

        // Build a new scope with parsed values, falling back to current for unparsed keys
        var newScope = new ForensicScope(
            DeviceIds: deviceIds,
            FromUtc: fromUtc ?? Current.FromUtc,
            ToUtc: toUtc ?? Current.ToUtc,
            Search: search ?? Current.Search,
            MatchesNoDevices: matchesNoDevices);

        ForensicScope oldCurrent = Current;
        Set(newScope);

        // Use custom Equals to check if the scope actually changed
        return !oldCurrent.Equals(Current);
    }

    private static ForensicScope Normalize(ForensicScope scope)
    {
        // Distinct device IDs
        var distinctDevices = scope.DeviceIds.Distinct().ToList();

        // Swap if reversed
        (DateTime from, DateTime to) = scope.FromUtc <= scope.ToUtc
            ? (scope.FromUtc, scope.ToUtc)
            : (scope.ToUtc, scope.FromUtc);

        // Trim search to null if blank
        string? search = string.IsNullOrWhiteSpace(scope.Search) ? null : scope.Search;

        // The no-match flag only means something while no device is listed; a listed device wins.
        bool matchesNoDevices = scope.MatchesNoDevices && distinctDevices.Count == 0;

        return new ForensicScope(distinctDevices, from, to, search, matchesNoDevices);
    }
}
