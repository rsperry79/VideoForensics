namespace VideoForensics.Data.Common.Entities
{
    /// <summary>
    /// Snapshot/RSSI spacing rules. The capture service and the signal history chart both use these, so the chart's
    /// expected checks match the spacing the service actually runs.
    /// </summary>
    public static class SnapshotIntervals
    {
        /// <summary>
        /// The interval in effect at <paramref name="nowUtc"/>, in seconds: SnapshotRssiIntervalMinutes, or the narrowest active
        /// jamming window when advanced jamming is on. Always clamped to the minimum.
        /// </summary>
        /// <param name="timeZone">Zone used to evaluate jamming windows; null means the server's local zone.</param>
        public static int Effective(SyncSchedule schedule, DateTime nowUtc, TimeZoneInfo? timeZone = null)
        {
            ArgumentNullException.ThrowIfNull(schedule);

            // Stored in minutes; the runtime works in seconds.
            int interval = schedule.SnapshotRssiIntervalMinutes * 60;

            if (schedule.UseAdvancedJammingSchedule && schedule.JammingWindows.Count > 0)
            {
                DateTime utc = nowUtc.Kind == DateTimeKind.Utc ? nowUtc : DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);
                DateTime local = TimeZoneInfo.ConvertTimeFromUtc(utc, timeZone ?? TimeZoneInfo.Local);
                int day = (int)local.DayOfWeek;
                int minute = (local.Hour * 60) + local.Minute;

                int? best = null;
                foreach (JammingScheduleWindow window in schedule.JammingWindows)
                {
                    int windowSeconds = window.SnapshotRssiIntervalMinutes * 60;
                    if (Applies(window, day, minute) && (best is null || windowSeconds < best))
                    {
                        best = windowSeconds;
                    }
                }

                if (best is not null)
                {
                    interval = best.Value;
                }
            }

            return Math.Max(interval, SyncSchedule.MinimumSnapshotIntervalMinutes * 60);
        }

        /// <summary>
        /// The spacing the capture service runs at, in seconds: the effective interval, raised to the stored SnapshotIntervalMinutes
        /// floor. That floor keeps an adaptive rate-limit back-off from being undone by the settings form.
        /// </summary>
        public static int CaptureSpacing(SyncSchedule schedule, DateTime nowUtc, TimeZoneInfo? timeZone = null)
        {
            return Math.Max(Effective(schedule, nowUtc, timeZone), (schedule.SnapshotIntervalMinutes ?? 0) * 60);
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
