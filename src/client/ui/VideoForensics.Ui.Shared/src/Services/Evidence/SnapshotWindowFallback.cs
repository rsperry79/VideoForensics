using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.Ui.Shared.Services.Evidence;

/// <summary>
/// The roll-up window for a device that has no saved schedule of its own. It uses the account's saved setting, so the
/// Media view matches the loops the account actually builds, instead of a fixed 15 minutes.
/// </summary>
public static class SnapshotWindowFallback
{
    /// <summary>
    /// Returns the one saved window when every account agrees on it. With no saved windows, or accounts that disagree,
    /// no single setting applies, so the entity default is returned.
    /// </summary>
    public static TimeSpan Resolve(IEnumerable<TimeSpan> savedWindows)
    {
        ArgumentNullException.ThrowIfNull(savedWindows);

        var distinct = savedWindows.Distinct().ToList();
        return distinct.Count == 1
            ? distinct[0]
            : TimeSpan.FromMinutes(SyncSchedule.DefaultSnapshotRollupIntervalMinutes);
    }
}
