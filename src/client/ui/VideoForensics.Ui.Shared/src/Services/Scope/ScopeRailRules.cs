using System.Globalization;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Ui.Shared.Services.Scope;

/// <summary>
/// Pure decision rules behind the right-hand scope rail (provider/account/device filters, date range,
/// quick ranges, search, and save-to-case gating). Holds no state and performs no I/O, so the rail
/// component only reads and writes <see cref="ScopeState"/> and <c>CaseState</c> around these calls.
/// </summary>
public static class ScopeRailRules
{
    /// <summary>The date format used by the rail's date pickers and the query string.</summary>
    public const string DateInputFormat = "yyyy-MM-dd";

    /// <summary>The devices visible under the Provider/Account filters. Delegates to <see cref="ScopeDeviceFilter.Filter"/>.</summary>
    public static IReadOnlyList<Device> VisibleDevices(
        IEnumerable<Device> devices,
        IReadOnlyList<DeviceOwnership> ownership,
        string? providerFilter,
        Guid? accountFilter)
    {
        return ScopeDeviceFilter.Filter(devices, ownership, providerFilter, accountFilter);
    }

    /// <summary>
    /// Applies a Provider dropdown pick. A blank value means all providers. If the selected account is not
    /// offered by the newly picked provider, the account filter is cleared.
    /// </summary>
    public static (string? ProviderFilter, Guid? AccountFilter) ApplyProviderPick(
        IEnumerable<ProviderAccount> accounts,
        Guid? currentAccountFilter,
        string? providerValue)
    {
        string? provider = string.IsNullOrWhiteSpace(providerValue) ? null : providerValue;

        Guid? account = currentAccountFilter;
        if (account is not null && !ScopeDeviceFilter.AccountsForProvider(accounts, provider).Any(a => a.Id == account))
        {
            account = null;
        }

        return (provider, account);
    }

    /// <summary>An Account dropdown pick: a GUID value selects that account, anything else means all accounts.</summary>
    public static Guid? ParseAccountPick(string? value)
    {
        return Guid.TryParse(value, out Guid parsed) ? parsed : null;
    }

    /// <summary>
    /// Recomputes the scope after a Provider or Account pick. While a filter is active the scope is pinned to the
    /// visible devices, and an empty result is recorded as <c>MatchesNoDevices</c> so it cannot widen to all devices.
    /// With no filter, only the no-match flag is cleared and the device selection is kept.
    /// </summary>
    public static ForensicScope ApplyFilters(
        ForensicScope current,
        IReadOnlyList<Guid> visibleDeviceIds,
        string? providerFilter,
        Guid? accountFilter)
    {
        if (providerFilter is not null || accountFilter is not null)
        {
            var ids = visibleDeviceIds.ToList();
            return current with { DeviceIds = ids, MatchesNoDevices = ids.Count == 0 };
        }

        return current with { MatchesNoDevices = false };
    }

    /// <summary>
    /// Adds or removes one device from the selection (no duplicates). Unchecking the last device under an
    /// active Provider/Account filter sets <c>MatchesNoDevices</c>, since that means "no device", not "all devices".
    /// </summary>
    public static ForensicScope ToggleDevice(
        ForensicScope current,
        Guid deviceId,
        bool isChecked,
        string? providerFilter,
        Guid? accountFilter)
    {
        IReadOnlyList<Guid> existing = current.DeviceIds;

        List<Guid> updated = isChecked
            ? (existing.Contains(deviceId) ? existing.ToList() : existing.Append(deviceId).ToList())
            : existing.Where(id => id != deviceId).ToList();

        bool filtered = providerFilter is not null || accountFilter is not null;
        return current with { DeviceIds = updated, MatchesNoDevices = filtered && updated.Count == 0 };
    }

    /// <summary>Parses a yyyy-MM-dd date picker value (invariant culture) into a UTC-kind date.</summary>
    public static bool TryParseDateInput(string? text, out DateTime date)
    {
        if (!string.IsNullOrWhiteSpace(text) &&
            DateTime.TryParseExact(text, DateInputFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
        {
            date = DateTime.SpecifyKind(date, DateTimeKind.Utc);
            return true;
        }

        date = default;
        return false;
    }

    /// <summary>Sets the From bound to the picked day (inclusive).</summary>
    public static ForensicScope SetFromDate(ForensicScope current, DateTime pickedDate)
    {
        return current with { FromUtc = pickedDate };
    }

    /// <summary>Sets the To bound from the picked day. The picker shows the inclusive last day; ToUtc is exclusive.</summary>
    public static ForensicScope SetToDate(ForensicScope current, DateTime pickedDate)
    {
        return current with { ToUtc = pickedDate.AddDays(1) };
    }

    /// <summary>The From picker value: the inclusive first day of the window.</summary>
    public static string FormatFromDate(DateTime fromUtc)
    {
        return fromUtc.Date.ToString(DateInputFormat, CultureInfo.InvariantCulture);
    }

    /// <summary>The To picker value: ToUtc is exclusive, so the picker shows the day before it.</summary>
    public static string FormatToDate(DateTime toUtc)
    {
        return toUtc.Date.AddDays(-1).ToString(DateInputFormat, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Sets a whole-day window: from <paramref name="daysBack"/> days before <paramref name="today"/>
    /// through tomorrow (exclusive), so today is fully included.
    /// </summary>
    public static ForensicScope ApplyQuickRange(ForensicScope current, DateTime today, int daysBack)
    {
        return current with { FromUtc = today.AddDays(-daysBack), ToUtc = today.AddDays(1) };
    }

    /// <summary>Search text as stored: blank or whitespace-only becomes null, otherwise the text is kept as typed.</summary>
    public static string? NormalizeSearch(string? draft)
    {
        return string.IsNullOrWhiteSpace(draft) ? null : draft;
    }

    /// <summary>Returns the scope with its search set from the draft text, normalized by <see cref="NormalizeSearch"/>.</summary>
    public static ForensicScope ApplySearch(ForensicScope current, string? draft)
    {
        return current with { Search = NormalizeSearch(draft) };
    }

    /// <summary>
    /// Clears a no-match flag that no Provider/Account filter can explain. Such a flag would block pages with a
    /// notice the operator cannot act on, because the rail does not restore its filter controls from the URL.
    /// </summary>
    public static ForensicScope ClearOrphanedNoMatch(ForensicScope current, string? providerFilter, Guid? accountFilter)
    {
        return current.MatchesNoDevices && providerFilter is null && accountFilter is null
            ? (current with { MatchesNoDevices = false })
            : current;
    }

    /// <summary>
    /// Whether the "save scope to case" button is shown. A no-match filter is not a device list, so it cannot be
    /// saved to a case as one; the operator needs at least Review rights.
    /// </summary>
    public static bool ShowSaveScopeToCase(
        bool hasActiveCase,
        bool scopeDiffersFromCase,
        OperatorRole? role,
        bool matchesNoDevices)
    {
        return hasActiveCase && scopeDiffersFromCase && role.HasValue && role.Value >= OperatorRole.Review && !matchesNoDevices;
    }
}
