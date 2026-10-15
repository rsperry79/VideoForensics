using System.Web;

namespace VideoForensics.Ui.Shared.Services.Scope;

/// <summary>
/// Represents the global investigation scope: devices, time window, and search query.
/// </summary>
public sealed record ForensicScope(IReadOnlyList<Guid> DeviceIds, DateTime FromUtc, DateTime ToUtc, string? Search)
{
    /// <summary>
    /// Creates a default scope with no devices, last 7 days, and no search.
    /// </summary>
    public static ForensicScope Default(DateTime utcNow)
    {
        var from = utcNow.Date.AddDays(-7);
        var to = utcNow.Date.AddDays(1);
        return new ForensicScope(new List<Guid>(), from, to, null);
    }

    /// <summary>
    /// True if no specific devices are selected (all devices included).
    /// </summary>
    public bool IncludesAllDevices => DeviceIds.Count == 0;

    /// <summary>
    /// Custom equality that properly compares DeviceIds by sequence equality.
    /// </summary>
    public bool Equals(ForensicScope? other)
    {
        if (other is null)
        {
            return false;
        }

        return DeviceIds.SequenceEqual(other.DeviceIds)
            && FromUtc == other.FromUtc
            && ToUtc == other.ToUtc
            && Search == other.Search;
    }

    /// <summary>
    /// Custom hash code that accounts for sequence equality of DeviceIds.
    /// </summary>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var deviceId in DeviceIds)
        {
            hash.Add(deviceId);
        }
        hash.Add(FromUtc);
        hash.Add(ToUtc);
        hash.Add(Search);
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
        var normalized = Normalize(scope);

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
            var deviceStr = string.Join(",", Current.DeviceIds.Select(g => g.ToString("D")));
            parts.Add($"devices={deviceStr}");
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
            var encoded = HttpUtility.UrlEncode(Current.Search);
            parts.Add($"q={encoded}");
        }

        return string.Join("&", parts);
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
            query = query.Substring(1);
        }

        var deviceIds = new List<Guid>();
        DateTime? fromUtc = null;
        DateTime? toUtc = null;
        string? search = null;

        var pairs = query.Split('&');
        foreach (var pair in pairs)
        {
            var eqIndex = pair.IndexOf('=');
            if (eqIndex <= 0)
            {
                continue;
            }

            var key = pair.Substring(0, eqIndex);
            var value = HttpUtility.UrlDecode(pair.Substring(eqIndex + 1));

            if (key == "devices" && !string.IsNullOrWhiteSpace(value))
            {
                var parts = value.Split(',');
                foreach (var part in parts)
                {
                    if (Guid.TryParse(part.Trim(), out var guid))
                    {
                        deviceIds.Add(guid);
                    }
                }
            }
            else if (key == "from" && !string.IsNullOrWhiteSpace(value))
            {
                if (DateTime.TryParseExact(value, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var from))
                {
                    fromUtc = from;
                }
            }
            else if (key == "to" && !string.IsNullOrWhiteSpace(value))
            {
                if (DateTime.TryParseExact(value, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var to))
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
            Search: search ?? Current.Search);

        var oldCurrent = Current;
        Set(newScope);

        // Use custom Equals to check if the scope actually changed
        return !oldCurrent.Equals(Current);
    }

    private static ForensicScope Normalize(ForensicScope scope)
    {
        // Distinct device IDs
        var distinctDevices = scope.DeviceIds.Distinct().ToList();

        // Swap if reversed
        var (from, to) = scope.FromUtc <= scope.ToUtc
            ? (scope.FromUtc, scope.ToUtc)
            : (scope.ToUtc, scope.FromUtc);

        // Trim search to null if blank
        var search = string.IsNullOrWhiteSpace(scope.Search) ? null : scope.Search;

        return new ForensicScope(distinctDevices, from, to, search);
    }
}
