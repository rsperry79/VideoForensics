using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>
/// The snapshot spacing the capture service runs at. A jamming window narrows it; the stored floor only raises it.
/// Times use UTC, so the window's day and minute are fixed.
/// </summary>
public class SnapshotIntervalsTests
{
    // 2026-10-24 is a Saturday (DayOfWeek 6).
    private static readonly DateTime Noon = new(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Afternoon = new(2026, 10, 24, 14, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Effective_IgnoresJammingWindowsWhenAdvancedJammingIsOff()
    {
        SyncSchedule schedule = Schedule(rssiSeconds: 60, advanced: false, windows: [Window(startMinute: 600, endMinute: 780, seconds: 5)]);

        Assert.Equal(60, SnapshotIntervals.Effective(schedule, Noon, TimeZoneInfo.Utc));
    }

    [Fact]
    public void Effective_NarrowsToTheSmallestActiveWindow()
    {
        SyncSchedule schedule = Schedule(
            rssiSeconds: 60,
            advanced: true,
            windows:
            [
                Window(startMinute: 600, endMinute: 780, seconds: 30),
                Window(startMinute: 660, endMinute: 720, seconds: 5),
            ]);

        // 11:30 is inside both windows; the end minute is exclusive, so 12:00 would be outside the 5 s window.
        Assert.Equal(5, SnapshotIntervals.Effective(schedule, new DateTime(2026, 10, 24, 11, 30, 0, DateTimeKind.Utc), TimeZoneInfo.Utc));
    }

    [Fact]
    public void Effective_UsesTheRssiIntervalOutsideEveryWindow()
    {
        SyncSchedule schedule = Schedule(rssiSeconds: 60, advanced: true, windows: [Window(startMinute: 600, endMinute: 780, seconds: 5)]);

        Assert.Equal(60, SnapshotIntervals.Effective(schedule, Afternoon, TimeZoneInfo.Utc));
    }

    [Fact]
    public void CaptureSpacing_RaisesTheEffectiveIntervalToTheStoredFloor()
    {
        // A rate-limit back-off stored as SnapshotIntervalSeconds is never undone by a narrower jamming window.
        SyncSchedule schedule = Schedule(rssiSeconds: 60, advanced: true, windows: [Window(startMinute: 600, endMinute: 780, seconds: 5)]);
        schedule.SnapshotIntervalSeconds = 120;

        Assert.Equal(120, SnapshotIntervals.CaptureSpacing(schedule, Noon, TimeZoneInfo.Utc));
    }

    private static SyncSchedule Schedule(int rssiSeconds, bool advanced, IEnumerable<JammingScheduleWindow> windows)
    {
        return new SyncSchedule
        {
            ProviderAccountId = Guid.NewGuid(),
            SnapshotRssiIntervalSeconds = rssiSeconds,
            UseAdvancedJammingSchedule = advanced,
            JammingWindows = windows.ToList(),
        };
    }

    private static JammingScheduleWindow Window(int startMinute, int endMinute, int seconds)
    {
        return new JammingScheduleWindow
        {
            DayOfWeek = 6,
            StartMinuteOfDay = startMinute,
            EndMinuteOfDay = endMinute,
            SnapshotRssiIntervalSeconds = seconds,
        };
    }
}
