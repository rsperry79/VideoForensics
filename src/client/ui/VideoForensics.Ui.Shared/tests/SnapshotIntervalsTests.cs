using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>
/// The snapshot spacing the capture service runs at, in seconds. The stored intervals are in minutes. A jamming window narrows
/// the spacing; the stored floor only raises it. Times use UTC, so the window's day and minute are fixed.
/// </summary>
public class SnapshotIntervalsTests
{
    // 2026-10-24 is a Saturday (DayOfWeek 6).
    private static readonly DateTime Noon = new(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Afternoon = new(2026, 10, 24, 14, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Effective_IgnoresJammingWindowsWhenAdvancedJammingIsOff()
    {
        SyncSchedule schedule = Schedule(rssiMinutes: 2, advanced: false, windows: [Window(startMinute: 600, endMinute: 780, minutes: 5)]);

        Assert.Equal(120, SnapshotIntervals.Effective(schedule, Noon, TimeZoneInfo.Utc));
    }

    [Fact]
    public void Effective_NarrowsToTheSmallestActiveWindow()
    {
        SyncSchedule schedule = Schedule(
            rssiMinutes: 2,
            advanced: true,
            windows:
            [
                Window(startMinute: 600, endMinute: 780, minutes: 30),
                Window(startMinute: 660, endMinute: 720, minutes: 5),
            ]);

        // 11:30 is inside both windows; the end minute is exclusive, so 12:00 would be outside the 5 minute window.
        Assert.Equal(300, SnapshotIntervals.Effective(schedule, new DateTime(2026, 10, 24, 11, 30, 0, DateTimeKind.Utc), TimeZoneInfo.Utc));
    }

    [Fact]
    public void Effective_UsesTheRssiIntervalOutsideEveryWindow()
    {
        SyncSchedule schedule = Schedule(rssiMinutes: 2, advanced: true, windows: [Window(startMinute: 600, endMinute: 780, minutes: 5)]);

        Assert.Equal(120, SnapshotIntervals.Effective(schedule, Afternoon, TimeZoneInfo.Utc));
    }

    [Fact]
    public void CaptureSpacing_RaisesTheEffectiveIntervalToTheStoredFloor()
    {
        // A rate-limit back-off stored as SnapshotIntervalMinutes is never undone by a narrower jamming window.
        SyncSchedule schedule = Schedule(rssiMinutes: 2, advanced: true, windows: [Window(startMinute: 600, endMinute: 780, minutes: 1)]);
        schedule.SnapshotIntervalMinutes = 2;

        Assert.Equal(120, SnapshotIntervals.CaptureSpacing(schedule, Noon, TimeZoneInfo.Utc));
    }

    [Fact]
    public void Effective_RssiBelowOneMinute_ClampsToOneMinute()
    {
        SyncSchedule schedule = Schedule(rssiMinutes: 0, advanced: false, windows: []);

        Assert.Equal(SyncSchedule.MinimumSnapshotIntervalMinutes * 60, SnapshotIntervals.Effective(schedule, Noon, TimeZoneInfo.Utc));
    }

    private static SyncSchedule Schedule(int rssiMinutes, bool advanced, IEnumerable<JammingScheduleWindow> windows)
    {
        return new SyncSchedule
        {
            ProviderAccountId = Guid.NewGuid(),
            SnapshotRssiIntervalMinutes = rssiMinutes,
            UseAdvancedJammingSchedule = advanced,
            JammingWindows = windows.ToList(),
        };
    }

    private static JammingScheduleWindow Window(int startMinute, int endMinute, int minutes)
    {
        return new JammingScheduleWindow
        {
            DayOfWeek = 6,
            StartMinuteOfDay = startMinute,
            EndMinuteOfDay = endMinute,
            SnapshotRssiIntervalMinutes = minutes,
        };
    }
}
