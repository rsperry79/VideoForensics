using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting.BackgroundServices;

using Xunit;

namespace VideoForensics.Hosting.Tests
{
    public class ScheduleCalculatorTests
    {
        // 2026-05-04 is a Monday (DayOfWeek 1).
        private static readonly DateTime Now = new(2026, 5, 4, 12, 0, 0, DateTimeKind.Utc);

        private static SyncSchedule Build(int eventMinutes = 30, int snapshotMinutes = 60, bool advanced = false)
        {
            return new SyncSchedule
            {
                ProviderAccountId = Guid.NewGuid(),
                IsEnabled = true,
                EventPollIntervalMinutes = eventMinutes,
                SnapshotRssiIntervalMinutes = snapshotMinutes,
                UseAdvancedJammingSchedule = advanced
            };
        }

        private static JammingScheduleWindow Window(int day, int start, int end, int interval)
        {
            return new JammingScheduleWindow { DayOfWeek = day, StartMinuteOfDay = start, EndMinuteOfDay = end, SnapshotRssiIntervalMinutes = interval };
        }

        [Fact]
        public void IsEventDue_NextRunIsNull_ReturnsTrue()
        {
            Assert.True(ScheduleCalculator.IsEventDue(Build(), Now));
        }

        [Fact]
        public void IsEventDue_NextRunInPast_ReturnsTrue()
        {
            SyncSchedule s = Build();
            s.EventNextRunUtc = Now.AddMinutes(-1);
            Assert.True(ScheduleCalculator.IsEventDue(s, Now));
        }

        [Fact]
        public void IsEventDue_NextRunExactlyNow_ReturnsTrue()
        {
            SyncSchedule s = Build();
            s.EventNextRunUtc = Now;
            Assert.True(ScheduleCalculator.IsEventDue(s, Now));
        }

        [Fact]
        public void IsEventDue_NextRunInFuture_ReturnsFalse()
        {
            SyncSchedule s = Build();
            s.EventNextRunUtc = Now.AddSeconds(1);
            Assert.False(ScheduleCalculator.IsEventDue(s, Now));
        }

        [Fact]
        public void NextEventRunUtc_NormalInterval_AddsIntervalToNow()
        {
            Assert.Equal(Now.AddMinutes(45), ScheduleCalculator.NextEventRunUtc(Build(eventMinutes: 45), Now));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public void NextEventRunUtc_IntervalBelowMinimum_ClampsToMinimum(int interval)
        {
            Assert.Equal(Now.AddMinutes(SyncSchedule.MinimumPollIntervalMinutes), ScheduleCalculator.NextEventRunUtc(Build(eventMinutes: interval), Now));
        }

        [Fact]
        public void IsEventDue_IntervalShortenedAfterEdit_BecomesDueOnceStoredNextRunPasses()
        {
            // Stored next run was computed from the old 60-minute interval; the edit does not rewrite it.
            SyncSchedule s = Build(eventMinutes: 5);
            s.EventNextRunUtc = Now.AddMinutes(30);
            Assert.False(ScheduleCalculator.IsEventDue(s, Now));
            Assert.True(ScheduleCalculator.IsEventDue(s, Now.AddMinutes(30)));
        }

        [Fact]
        public void EffectiveSnapshotIntervalMinutes_NoWindows_ReturnsDefault()
        {
            Assert.Equal(60, ScheduleCalculator.EffectiveSnapshotIntervalMinutes(Build(snapshotMinutes: 60), Now, TimeZoneInfo.Utc));
        }

        [Fact]
        public void EffectiveSnapshotIntervalMinutes_AdvancedDisabled_IgnoresMatchingWindow()
        {
            SyncSchedule s = Build(snapshotMinutes: 60, advanced: false);
            s.JammingWindows.Add(Window(1, 11 * 60, 13 * 60, 5));
            Assert.Equal(60, ScheduleCalculator.EffectiveSnapshotIntervalMinutes(s, Now, TimeZoneInfo.Utc));
        }

        [Fact]
        public void EffectiveSnapshotIntervalMinutes_AdvancedWithMatchingWindow_ReturnsWindowInterval()
        {
            SyncSchedule s = Build(snapshotMinutes: 60, advanced: true);
            s.JammingWindows.Add(Window(1, 11 * 60, 13 * 60, 5));
            Assert.Equal(5, ScheduleCalculator.EffectiveSnapshotIntervalMinutes(s, Now, TimeZoneInfo.Utc));
        }

        [Fact]
        public void EffectiveSnapshotIntervalMinutes_WindowOnOtherDay_ReturnsDefault()
        {
            SyncSchedule s = Build(snapshotMinutes: 60, advanced: true);
            s.JammingWindows.Add(Window(2, 11 * 60, 13 * 60, 5));
            Assert.Equal(60, ScheduleCalculator.EffectiveSnapshotIntervalMinutes(s, Now, TimeZoneInfo.Utc));
        }

        [Fact]
        public void EffectiveSnapshotIntervalMinutes_StartIsInclusiveEndIsExclusive()
        {
            SyncSchedule s = Build(snapshotMinutes: 60, advanced: true);
            s.JammingWindows.Add(Window(1, 12 * 60, 13 * 60, 5));
            Assert.Equal(5, ScheduleCalculator.EffectiveSnapshotIntervalMinutes(s, Now, TimeZoneInfo.Utc));
            Assert.Equal(60, ScheduleCalculator.EffectiveSnapshotIntervalMinutes(s, Now.AddHours(1), TimeZoneInfo.Utc));
        }

        [Fact]
        public void EffectiveSnapshotIntervalMinutes_OverlappingWindows_ReturnsSmallestInterval()
        {
            SyncSchedule s = Build(snapshotMinutes: 60, advanced: true);
            s.JammingWindows.Add(Window(1, 0, 24 * 60, 20));
            s.JammingWindows.Add(Window(1, 11 * 60, 13 * 60, 7));
            Assert.Equal(7, ScheduleCalculator.EffectiveSnapshotIntervalMinutes(s, Now, TimeZoneInfo.Utc));
        }

        [Fact]
        public void EffectiveSnapshotIntervalMinutes_WindowIntervalBelowMinimum_ClampsToMinimum()
        {
            SyncSchedule s = Build(snapshotMinutes: 60, advanced: true);
            s.JammingWindows.Add(Window(1, 0, 24 * 60, 0));
            Assert.Equal(SyncSchedule.MinimumPollIntervalMinutes, ScheduleCalculator.EffectiveSnapshotIntervalMinutes(s, Now, TimeZoneInfo.Utc));
        }

        [Fact]
        public void EffectiveSnapshotIntervalMinutes_OvernightWindowBeforeMidnight_Applies()
        {
            // Monday 22:00 -> Tuesday 06:00 (End <= Start means "wraps past midnight").
            SyncSchedule s = Build(snapshotMinutes: 60, advanced: true);
            s.JammingWindows.Add(Window(1, 22 * 60, 6 * 60, 3));
            var mondayNight = new DateTime(2026, 5, 4, 23, 0, 0, DateTimeKind.Utc);
            Assert.Equal(3, ScheduleCalculator.EffectiveSnapshotIntervalMinutes(s, mondayNight, TimeZoneInfo.Utc));
        }

        [Fact]
        public void EffectiveSnapshotIntervalMinutes_OvernightWindowAfterMidnight_AppliesOnNextDay()
        {
            SyncSchedule s = Build(snapshotMinutes: 60, advanced: true);
            s.JammingWindows.Add(Window(1, 22 * 60, 6 * 60, 3));
            var tuesdayEarly = new DateTime(2026, 5, 5, 5, 45, 0, DateTimeKind.Utc);
            var tuesdayAfter = new DateTime(2026, 5, 5, 6, 0, 0, DateTimeKind.Utc);
            Assert.Equal(3, ScheduleCalculator.EffectiveSnapshotIntervalMinutes(s, tuesdayEarly, TimeZoneInfo.Utc));
            Assert.Equal(60, ScheduleCalculator.EffectiveSnapshotIntervalMinutes(s, tuesdayAfter, TimeZoneInfo.Utc));
        }

        [Fact]
        public void EffectiveSnapshotIntervalMinutes_OvernightWindowSundayWrapsToMonday_Applies()
        {
            SyncSchedule s = Build(snapshotMinutes: 60, advanced: true);
            s.JammingWindows.Add(Window(6, 23 * 60, 1 * 60, 4)); // Saturday 23:00 -> Sunday 01:00
            var sundayEarly = new DateTime(2026, 5, 10, 0, 30, 0, DateTimeKind.Utc); // Sunday
            Assert.Equal(4, ScheduleCalculator.EffectiveSnapshotIntervalMinutes(s, sundayEarly, TimeZoneInfo.Utc));
        }

        [Fact]
        public void EffectiveSnapshotIntervalMinutes_OvernightWindowBeforeStartTime_ReturnsDefault()
        {
            SyncSchedule s = Build(snapshotMinutes: 60, advanced: true);
            s.JammingWindows.Add(Window(1, 22 * 60, 6 * 60, 3));
            Assert.Equal(60, ScheduleCalculator.EffectiveSnapshotIntervalMinutes(s, Now, TimeZoneInfo.Utc));
        }

        [Fact]
        public void EffectiveSnapshotIntervalMinutes_NonUtcZone_EvaluatesWindowInThatZone()
        {
            // 12:00 UTC is 05:00 in a UTC-7 zone; a Monday 04:00-06:00 window matches only in that zone.
            TimeZoneInfo minus7 = TimeZoneInfo.CreateCustomTimeZone("test-7", TimeSpan.FromHours(-7), "test-7", "test-7");
            SyncSchedule s = Build(snapshotMinutes: 60, advanced: true);
            s.JammingWindows.Add(Window(1, 4 * 60, 6 * 60, 2));
            Assert.Equal(2, ScheduleCalculator.EffectiveSnapshotIntervalMinutes(s, Now, minus7));
            Assert.Equal(60, ScheduleCalculator.EffectiveSnapshotIntervalMinutes(s, Now, TimeZoneInfo.Utc));
        }

        [Fact]
        public void IsSnapshotDue_NextRunNullOrPast_ReturnsTrueAndFutureReturnsFalse()
        {
            SyncSchedule s = Build();
            Assert.True(ScheduleCalculator.IsSnapshotDue(s, Now));
            s.SnapshotNextRunUtc = Now.AddMinutes(-1);
            Assert.True(ScheduleCalculator.IsSnapshotDue(s, Now));
            s.SnapshotNextRunUtc = Now.AddMinutes(1);
            Assert.False(ScheduleCalculator.IsSnapshotDue(s, Now));
        }

        [Fact]
        public void NextSnapshotRunUtc_WithActiveWindow_UsesWindowInterval()
        {
            SyncSchedule s = Build(snapshotMinutes: 60, advanced: true);
            s.JammingWindows.Add(Window(1, 11 * 60, 13 * 60, 5));
            Assert.Equal(Now.AddMinutes(5), ScheduleCalculator.NextSnapshotRunUtc(s, Now, TimeZoneInfo.Utc));
        }

        [Fact]
        public void NextSnapshotRunUtc_WithoutWindow_UsesDefaultInterval()
        {
            Assert.Equal(Now.AddMinutes(60), ScheduleCalculator.NextSnapshotRunUtc(Build(snapshotMinutes: 60), Now, TimeZoneInfo.Utc));
        }

        [Fact]
        public void NextSnapshotRunUtc_DefaultIntervalBelowMinimum_ClampsToMinimum()
        {
            Assert.Equal(Now.AddMinutes(SyncSchedule.MinimumPollIntervalMinutes), ScheduleCalculator.NextSnapshotRunUtc(Build(snapshotMinutes: 0), Now, TimeZoneInfo.Utc));
        }
    }
}