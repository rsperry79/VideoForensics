namespace VideoForensics.Ui.Shared.Services;

using System.Globalization;

/// <summary>Pure logic for the account sync panel: request body shape and range parsing.</summary>
public static class SyncRequestBuilder
{
    public const string CustomRange = "Custom";

    public const string AllRange = "All";

    public const string ConfigSyncType = "Config";

    public const string SinceLastRange = "SinceLast";

    public static readonly string[] SyncTypes = { "All", "Events", "Snapshots", ConfigSyncType };

    public static readonly string[] RangeOptions = { "30", "60", "90", "365", AllRange, CustomRange };

    public const string DateFormat = "yyyy-MM-dd";

    /// <summary>
    /// Builds the camelCase-serialisable body for POST sync-now.
    /// Config syncs have no range: the range arguments are ignored and the body is always SinceLast.
    /// </summary>
    public static object Build(string syncType, string range, string? customStart, string? customEnd)
    {
        if (syncType == ConfigSyncType)
        {
            return new { syncType, rangeKind = SinceLastRange };
        }

        if (range == CustomRange)
        {
            return new { syncType, rangeKind = "Custom", @from = ParseDate(customStart), to = ParseDate(customEnd) };
        }

        if (range == AllRange)
        {
            return new { syncType, rangeKind = "All" };
        }

        int days = int.TryParse(range, NumberStyles.None, CultureInfo.InvariantCulture, out int d) ? d : 30;
        return new { syncType, rangeKind = "Days", days };
    }

    /// <summary>Last-30-days default for the custom range pickers.</summary>
    public static (string Start, string End) DefaultCustomRange(DateTime today) =>
        (today.AddDays(-30).ToString(DateFormat, CultureInfo.InvariantCulture), today.ToString(DateFormat, CultureInfo.InvariantCulture));

    /// <summary>Returns the run's record counts in the given format, or null when the counts are unknown (old runs).</summary>
    public static string? FormatRecords(string format, int? devices, int? locations, int? events, int? configAreas, int? feedsFailed, int? snapshotsSynced = null, string? snapshotsFormat = null)
    {
        string? snapshotsPart = snapshotsSynced is { } s && snapshotsFormat is not null
            ? string.Format(CultureInfo.CurrentCulture, snapshotsFormat, s)
            : null;

        if (devices is null && locations is null && events is null && configAreas is null && feedsFailed is null)
        {
            return snapshotsPart;
        }

        string counts = string.Format(CultureInfo.CurrentCulture, format, devices ?? 0, locations ?? 0, events ?? 0, configAreas ?? 0, feedsFailed ?? 0);
        return snapshotsPart is null ? counts : counts + ", " + snapshotsPart;
    }

    private static DateTime? ParseDate(string? value) =>
        DateTime.TryParseExact(value, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt) ? dt : null;
}
