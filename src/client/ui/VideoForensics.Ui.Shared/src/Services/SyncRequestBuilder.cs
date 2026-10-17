namespace VideoForensics.Ui.Shared.Services;

using System.Globalization;
using VideoForensics.Core.Api.Contracts;

/// <summary>Pure logic for the account sync panel: the sync-now body, which types take a range, and the record counts text.</summary>
public static class SyncRequestBuilder
{
    /// <summary>Range choice that sends the custom start and end days.</summary>
    public const string CustomRange = "Custom";

    /// <summary>Range choice for the provider's retained history.</summary>
    public const string AllRange = "All";

    /// <summary>Range choice for the window since the last successful sync.</summary>
    public const string SinceLastRange = "SinceLast";

    /// <summary>Sync type that pulls events and fetches snapshots in one run.</summary>
    public const string AllType = "All";

    /// <summary>Sync type that pulls events only.</summary>
    public const string EventsType = "Events";

    /// <summary>True when the sync type reads event history, so the range controls apply. Config and Snapshots ignore the window.</summary>
    public static bool UsesRange(string syncType) =>
        syncType is EventsType or AllType;

    /// <summary>
    /// Builds the body for POST sync-now. A custom range runs from the start day through the end day inclusive, local time.
    /// Types that ignore the window send the default range.
    /// </summary>
    public static SyncNowRequest Build(string syncType, string range, DateOnly customStart, DateOnly customEnd)
    {
        if (!UsesRange(syncType))
        {
            return new SyncNowRequest(syncType, SinceLastRange, null, null, null);
        }

        if (range == CustomRange)
        {
            return new SyncNowRequest(syncType, CustomRange, null, LocalDayStartUtc(customStart), LocalDayStartUtc(customEnd.AddDays(1)));
        }

        if (range == AllRange)
        {
            return new SyncNowRequest(syncType, AllRange, null, null, null);
        }

        if (int.TryParse(range, NumberStyles.None, CultureInfo.InvariantCulture, out int days))
        {
            return new SyncNowRequest(syncType, "Days", days, null, null);
        }

        return new SyncNowRequest(syncType, SinceLastRange, null, null, null);
    }

    /// <summary>
    /// Returns the run's record counts in the given format, with the snapshot count appended when the run fetched snapshots.
    /// Null counts and a null snapshot count mean nothing was recorded (old runs): the result is null.
    /// </summary>
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

    private static DateTime LocalDayStartUtc(DateOnly day) =>
        DateTime.SpecifyKind(day.ToDateTime(TimeOnly.MinValue), DateTimeKind.Local).ToUniversalTime();
}
