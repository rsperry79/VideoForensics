using VideoForensics.Client.Ui.Shared.Services.Evidence;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>A device with no saved roll-up window uses the account's saved setting, not a hardcoded 15 minutes.</summary>
public class SnapshotWindowFallbackTests
{
    [Fact]
    public void SingleSavedWindow_IsUsedForUnmappedDevices()
    {
        TimeSpan window = SnapshotWindowFallback.Resolve([TimeSpan.FromMinutes(30)]);

        Assert.Equal(TimeSpan.FromMinutes(30), window);
    }

    [Fact]
    public void NoSavedWindows_FallsBackToTheEntityDefault()
    {
        TimeSpan window = SnapshotWindowFallback.Resolve([]);

        Assert.Equal(TimeSpan.FromMinutes(SyncSchedule.DefaultSnapshotRollupIntervalMinutes), window);
    }

    [Fact]
    public void RepeatedSavedWindow_CountsAsOne()
    {
        TimeSpan window = SnapshotWindowFallback.Resolve([TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5)]);

        Assert.Equal(TimeSpan.FromMinutes(5), window);
    }

    [Fact]
    public void AccountsWithDifferentSettings_FallBackToTheEntityDefault()
    {
        // Two accounts with different settings cannot pick one window for an unmapped device, so the default stands.
        TimeSpan window = SnapshotWindowFallback.Resolve([TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(30)]);

        Assert.Equal(TimeSpan.FromMinutes(SyncSchedule.DefaultSnapshotRollupIntervalMinutes), window);
    }
}
