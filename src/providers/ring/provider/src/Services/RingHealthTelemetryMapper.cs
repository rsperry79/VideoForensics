using System.Globalization;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Providers.Ring.Services
{
    /// <summary>
    /// Converts Ring's raw health telemetry fields into the typed values stored on DeviceHealths.
    /// Unknown or unrecognised encodings map to null rather than a guessed value.
    /// </summary>
    public static class RingHealthTelemetryMapper
    {
        /// <summary>Builds the DeviceHealths row for one Ring health object: every field Ring sends, mapped to its column.</summary>
        public static DeviceHealth BuildDeviceHealth(Entities.DeviceHealth health, Guid deviceGuid, DateTime capturedAtUtc)
        {
            return new DeviceHealth
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceGuid,
                IsOnline = health.Connected,
                BatteryPercentage = health.BatteryPercentage.HasValue ? health.BatteryPercentage.Value : null,
                BatteryVoltageValue = health.BatteryVoltage,
                WifiSignalRssi = health.Rssi.HasValue ? (int)Math.Round(health.Rssi.Value) : null,
                WifiName = health.WifiName,
                FirmwareVersion = health.FirmwareVersion,
                IsExternalPowerConnected = ToExternalPowerConnected(health.ExtPowerState),
                OtaStatus = health.OtaStatus,
                LastHeartbeatUtc = ToHeartbeatUtc(health.LastUpdateTime),
                CapturedAtUtc = capturedAtUtc,
                DeviceType = health.DeviceType,
                FirmwareVersionStatus = health.FirmwareVersionStatus,
                BatteryPresent = health.BatteryPresent,
                BatteryPercentageCategory = health.BatteryPercentageCategory,
                BatteryVoltageCategory = health.BatteryVoltageCategory,
                RssiCategory = health.RssiCategory,
                NetworkConnectionValue = health.NetworkConnectionValue,
                PacketLoss = health.PacketLoss,
                PacketLossCategory = health.PacketLossCategory,
                LatestSignalStrength = health.LatestSignalStrength,
                LatestSignalCategory = health.LatestSignalCategory,
                AverageSignalStrength = health.AverageSignalStrength,
                AverageSignalCategory = health.AverageSignalCategory,
                Firmware = health.Firmware,
                UpdatedAtUtc = ToUpdatedAtUtc(health.UpdatedAt),
                PacketLossStrength = health.PacketLossStrength,
                WifiIsRingNetwork = health.WifiIsRingNetwork,
                PtzConnected = health.PtzConnected,
                RingHealthId = health.Id,
                SupportedRpcCommands = (health.SupportedRpcCommands ?? []).Select((c, i) => new DeviceHealthRpcCommand { Id = Guid.NewGuid(), Position = i, Command = c }).ToList()
            };
        }

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
        /// Ring's health updated_at is an ISO-8601 string. A value without an offset is read as UTC, and
        /// null, empty or unparseable text returns null rather than a guessed time.
        /// </summary>
        public static DateTime? ToUpdatedAtUtc(string? updatedAt)
        {
            return string.IsNullOrWhiteSpace(updatedAt)
                ? null
                : DateTimeOffset.TryParse(updatedAt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset when)
                ? when.UtcDateTime
                : null;
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
