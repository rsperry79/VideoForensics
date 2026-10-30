namespace VideoForensics.Data.Common.Entities
{
    /// <summary>
    /// Snapshot/RSSI spacing rules. The capture service and the signal history chart both use these, so the chart's
    /// expected checks match the spacing the service actually runs.
    /// </summary>
    public static class SnapshotIntervals
    {
        /// <summary>
        /// The interval in effect at <paramref name="nowUtc"/>: SnapshotRssiIntervalSeconds, or the narrowest active
        /// jamming window when advanced jamming is on. Always clamped to the minimum.
        /// </summary>
        /// <param name="timeZone">Zone used to evaluate jamming windows; null means the server's local zone.</param>
        public static int Effective(SyncSchedule schedule, DateTime nowUtc, TimeZoneInfo? timeZone = null)
        {
            ArgumentNullException.ThrowIfNull(schedule);

            int interval = schedule.SnapshotRssiIntervalSeconds;

            if (schedule.UseAdvancedJammingSchedule && schedule.JammingWindows.Count > 0)
            {
                DateTime utc = nowUtc.Kind == DateTimeKind.Utc ? nowUtc : DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);
                DateTime local = TimeZoneInfo.ConvertTimeFromUtc(utc, timeZone ?? TimeZoneInfo.Local);
                int day = (int)local.DayOfWeek;
                int minute = (local.Hour * 60) + local.Minute;

                int? best = null;
                foreach (JammingScheduleWindow window in schedule.JammingWindows)
                {
                    if (Applies(window, day, minute) && (best is null || window.SnapshotRssiIntervalSeconds < best))
                    {
                        best = window.SnapshotRssiIntervalSeconds;
                    }
                }

                if (best is not null)
                {
                    interval = best.Value;
                }
            }

            return Math.Max(interval, SyncSchedule.MinimumSnapshotIntervalSeconds);
        }

        /// <summary>
        /// The spacing the capture service runs at: the effective interval, raised to the stored SnapshotIntervalSeconds
        /// floor. That floor keeps an adaptive rate-limit back-off from being undone by the settings form.
        /// </summary>
        public static int CaptureSpacing(SyncSchedule schedule, DateTime nowUtc, TimeZoneInfo? timeZone = null)
        {
            return Math.Max(Effective(schedule, nowUtc, timeZone), schedule.SnapshotIntervalSeconds ?? 0);
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
