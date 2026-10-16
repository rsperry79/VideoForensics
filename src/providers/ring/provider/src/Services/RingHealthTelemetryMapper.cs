namespace VideoForensics.Providers.Ring.Services
{
    /// <summary>
    /// Converts Ring's raw health telemetry fields into the typed values stored on DeviceHealths.
    /// Unknown or unrecognised encodings map to null rather than a guessed value.
    /// </summary>
    public static class RingHealthTelemetryMapper
    {
        /// <summary>Ring's ext_power_state is a 0/1 flag; any other value is unknown.</summary>
        public static bool? ToExternalPowerConnected(int? extPowerState)
        {
            return extPowerState switch
            {
                1 => true,
                0 => false,
                _ => null
            };
        }

        /// <summary>
        /// Ring's last_update_time is epoch seconds in some responses and epoch milliseconds in others.
        /// Values at or above 1e12 are read as milliseconds (1e12 seconds is year ~33658, 1e12 ms is 2001),
        /// so the unit is inferred from magnitude. Null, non-positive or out-of-range values return null.
        /// </summary>
        public static DateTime? ToHeartbeatUtc(long? lastUpdateTime)
        {
            if (lastUpdateTime is not > 0)
            {
                return null;
            }

            long value = lastUpdateTime.Value;
            try
            {
                DateTimeOffset when = value >= 1_000_000_000_000L
                    ? DateTimeOffset.FromUnixTimeMilliseconds(value)
                    : DateTimeOffset.FromUnixTimeSeconds(value);
                return when.UtcDateTime;
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }
    }
}
