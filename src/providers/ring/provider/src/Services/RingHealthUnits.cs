namespace VideoForensics.Providers.Ring.Services
{
    /// <summary>
    /// Conversions for Ring health values that arrive in non-obvious units. The raw values stay stored as sent (forensic data); the
    /// converted values are siblings. All factors live here so an assumption can be corrected in one place.
    /// </summary>
    public static class RingHealthUnits
    {
        /// <summary>status_time is a tick count of this clock (Hz) since the Unix epoch (verified against last_update_time).</summary>
        public const long StatusTimeTicksPerSecond = 32768;

        /// <summary>ASSUMPTION (check due 2026-11-01): battery_temp is deci-kelvin, so kelvin = raw / this divisor.</summary>
        public const decimal BatteryTempDivisor = 10m;

        /// <summary>Kelvin to Celsius offset.</summary>
        public const decimal KelvinOffset = 273.15m;

        /// <summary>Raw battery_temp accepted for conversion (-123 C to 177 C as deci-kelvin); anything else is treated as a wrong unit.</summary>
        public const double BatteryTempRawMin = 1500;

        /// <inheritdoc cref="BatteryTempRawMin"/>
        public const double BatteryTempRawMax = 4500;

        private static readonly DateTime MinStatusUtc = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime MaxStatusUtc = new(2100, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>Converts status_time ticks to UTC. Null when the result is outside 2000-01-01..2100-01-01 or would overflow.</summary>
        public static DateTime? StatusTimeToUtc(long ticks)
        {
            if (ticks <= 0)
            {
                return null;
            }

            // Split into whole seconds and remainder so large values cannot overflow the tick arithmetic.
            long seconds = ticks / StatusTimeTicksPerSecond;
            long remainder = ticks % StatusTimeTicksPerSecond;
            if (seconds > (long)(MaxStatusUtc - DateTime.UnixEpoch).TotalSeconds)
            {
                return null;
            }

            DateTime result = DateTime.UnixEpoch.AddSeconds(seconds).AddTicks(remainder * TimeSpan.TicksPerSecond / StatusTimeTicksPerSecond);
            return result >= MinStatusUtc && result < MaxStatusUtc ? result : null;
        }

        /// <summary>Converts a raw battery_temp (assumed deci-kelvin) to Celsius rounded to 2 dp. Null when outside the accepted raw range.</summary>
        public static decimal? BatteryTempToCelsius(double raw)
        {
            if (double.IsNaN(raw) || raw < BatteryTempRawMin || raw > BatteryTempRawMax)
            {
                return null;
            }

            return Math.Round((decimal)raw / BatteryTempDivisor - KelvinOffset, 2, MidpointRounding.AwayFromZero);
        }
    }
}
