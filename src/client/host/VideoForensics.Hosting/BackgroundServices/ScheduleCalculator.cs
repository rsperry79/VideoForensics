using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Hosting.BackgroundServices
{
    /// <summary>
    /// Pure scheduling decisions for <see cref="SyncSchedule"/> rows: is a task due, when does it run next,
    /// and which snapshot/RSSI interval applies right now. Kept free of I/O so it is trivially unit-testable.
    ///
    /// Jamming window semantics (documented reading of <see cref="JammingScheduleWindow"/>):
    /// a window applies on its DayOfWeek (0=Sunday) for minutes [StartMinuteOfDay, EndMinuteOfDay) - start
    /// inclusive, end exclusive, evaluated in the supplied time zone (windows carry no zone of their own).
    /// The repository/UI only accept End &gt; Start (same-day), so an overnight period is normally two windows;
    /// as a defensive extension End &lt;= Start is read as wrapping past midnight into the next day.
    /// When several windows match, the smallest (most frequent) interval wins. Windows are ignored unless
    /// UseAdvancedJammingSchedule is true.
    /// </summary>
    public static class ScheduleCalculator
    {
        /// <summary>True when the event poll has never run (no stored next run) or its stored next run is at or before <paramref name="nowUtc"/>.</summary>
        public static bool IsEventDue(SyncSchedule schedule, DateTime nowUtc)
        {
            return schedule.EventNextRunUtc is null || schedule.EventNextRunUtc.Value <= nowUtc;
        }

        /// <summary>Next event run: now plus the event interval, clamped to <see cref="SyncSchedule.MinimumPollIntervalMinutes"/>.</summary>
        public static DateTime NextEventRunUtc(SyncSchedule schedule, DateTime nowUtc)
        {
            return nowUtc.AddMinutes(Math.Max(schedule.EventPollIntervalMinutes, SyncSchedule.MinimumPollIntervalMinutes));
        }

        /// <summary>True when the snapshot/RSSI poll has never run or its stored next run is at or before <paramref name="nowUtc"/>.</summary>
        public static bool IsSnapshotDue(SyncSchedule schedule, DateTime nowUtc)
        {
            return schedule.SnapshotNextRunUtc is null || schedule.SnapshotNextRunUtc.Value <= nowUtc;
        }

        /// <summary>Next snapshot/RSSI run: now plus the currently effective interval.</summary>
        /// <param name="timeZone">Zone used to evaluate jamming windows; null means the server's local zone.</param>
        public static DateTime NextSnapshotRunUtc(SyncSchedule schedule, DateTime nowUtc, TimeZoneInfo? timeZone = null)
        {
            return nowUtc.AddMinutes(EffectiveSnapshotIntervalMinutes(schedule, nowUtc, timeZone));
        }

        /// <summary>
        /// The snapshot/RSSI interval in effect at <paramref name="nowUtc"/>: SnapshotRssiIntervalMinutes unless
        /// UseAdvancedJammingSchedule is set and at least one window applies (smallest wins). Always clamped to the minimum.
        /// </summary>
        /// <param name="timeZone">Zone used to evaluate jamming windows; null means the server's local zone.</param>
        public static int EffectiveSnapshotIntervalMinutes(SyncSchedule schedule, DateTime nowUtc, TimeZoneInfo? timeZone = null)
        {
            int interval = schedule.SnapshotRssiIntervalMinutes;

            if (schedule.UseAdvancedJammingSchedule && schedule.JammingWindows.Count > 0)
            {
                DateTime utc = nowUtc.Kind == DateTimeKind.Utc ? nowUtc : DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);
                DateTime local = TimeZoneInfo.ConvertTimeFromUtc(utc, timeZone ?? TimeZoneInfo.Local);
                int day = (int)local.DayOfWeek;
                int minute = (local.Hour * 60) + local.Minute;

                int? best = null;
                foreach (JammingScheduleWindow window in schedule.JammingWindows)
                {
                    if (Applies(window, day, minute) && (best is null || window.SnapshotRssiIntervalMinutes < best))
                    {
                        best = window.SnapshotRssiIntervalMinutes;
                    }
                }

                if (best is not null)
                {
                    interval = best.Value;
                }
            }

            return Math.Max(interval, SyncSchedule.MinimumPollIntervalMinutes);
        }

        private static bool Applies(JammingScheduleWindow window, int day, int minute)
        {
            if (window.EndMinuteOfDay > window.StartMinuteOfDay)
            {
                return window.DayOfWeek == day && minute >= window.StartMinuteOfDay && minute < window.EndMinuteOfDay;
            }

            // Overnight: tail of the start day, then the head of the following day.
            return (window.DayOfWeek == day && minute >= window.StartMinuteOfDay)
                || ((window.DayOfWeek + 1) % 7 == day && minute < window.EndMinuteOfDay);
        }
    }
}