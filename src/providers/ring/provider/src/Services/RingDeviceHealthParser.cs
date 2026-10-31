using System.Text.Json;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Providers.Ring.Services
{
    /// <summary>
    /// The parsed Ring <c>health</c> object: the provider-agnostic row, the Ring-only row, the battery rows (unsaved; Id and DeviceId are
    /// assigned when built) and notes for every value that was read but not stored (type and reason only, never the value).
    /// </summary>
    public sealed record RingReportedHealth(
        DeviceReportedHealth Health,
        RingDeviceHealthExtras Extras,
        IReadOnlyList<DeviceReportedBattery> Batteries,
        IReadOnlyList<string> Unstored);

    /// <summary>Parses a Ring health object into typed rows. Unknown keys and unexpected shapes are noted, never stored as JSON.</summary>
    public static class RingDeviceHealthParser
    {
        /// <summary>Parses the health object. Null input (no health object sent) yields null so stored rows stay untouched.</summary>
        public static RingReportedHealth? Parse(Entities.DeviceHealth? h)
        {
            if (h == null)
            {
                return null;
            }

            var g = new DeviceReportedHealth();
            var x = new RingDeviceHealthExtras();
            var notes = new List<string>();
            var batteries = new List<DeviceReportedBattery>();

            // Properties the client model already types.
            g.Connected = h.Connected;
            g.Rssi = h.Rssi;
            g.LastUpdateTime = h.LastUpdateTime;
            x.DeviceType = h.DeviceType;
            x.FirmwareVersion = h.FirmwareVersion;
            x.FirmwareVersionStatus = h.FirmwareVersionStatus;
            x.BatteryPresent = h.BatteryPresent;
            x.BatteryPercentage = h.BatteryPercentage;
            x.BatteryPercentageCategory = h.BatteryPercentageCategory;
            x.BatteryVoltage = h.BatteryVoltage;
            x.BatteryVoltageCategory = h.BatteryVoltageCategory;
            x.RssiCategory = h.RssiCategory;
            x.NetworkConnectionValue = h.NetworkConnectionValue;
            x.WifiName = h.WifiName;
            x.PacketLoss = h.PacketLoss;
            x.PacketLossCategory = h.PacketLossCategory;
            x.OtaStatus = h.OtaStatus;
            x.ExtPowerState = h.ExtPowerState;
            x.WifiIsRingNetwork = h.WifiIsRingNetwork;
            x.PtzConnected = h.PtzConnected;

            // Typed on the client model but deliberately not stored here (the DeviceHealths capture columns cover them).
            NoteIfPresent(notes, "id", h.Id != null);
            NoteIfPresent(notes, "latest_signal_strength", h.LatestSignalStrength != null);
            NoteIfPresent(notes, "latest_signal_category", h.LatestSignalCategory != null);
            NoteIfPresent(notes, "average_signal_strength", h.AverageSignalStrength != null);
            NoteIfPresent(notes, "average_signal_category", h.AverageSignalCategory != null);
            NoteIfPresent(notes, "firmware", h.Firmware != null);
            NoteIfPresent(notes, "updated_at", h.UpdatedAt != null);
            NoteIfPresent(notes, "packet_loss_strength", h.PacketLossStrength != null);
            NoteIfPresent(notes, "supported_rpc_commands", h.SupportedRpcCommands != null);

            foreach (KeyValuePair<string, JsonElement> entry in h.AdditionalFields ?? new Dictionary<string, JsonElement>())
            {
                if (entry.Key == "batteries")
                {
                    ParseBatteries(entry.Value, batteries, notes);
                }
                else if (!Apply(entry.Key, entry.Value, g, x))
                {
                    notes.Add($"health.{entry.Key} not stored: unknown key or unexpected shape ({entry.Value.ValueKind})");
                }
            }

            return new RingReportedHealth(g, x, batteries, notes);
        }

        private static void NoteIfPresent(List<string> notes, string key, bool present)
        {
            if (present)
            {
                notes.Add($"health.{key} not stored: no column for it");
            }
        }

        /// <summary>Applies a key held in the extension data. False when the key is unknown or its value has an unexpected shape.</summary>
        private static bool Apply(string key, JsonElement v, DeviceReportedHealth g, RingDeviceHealthExtras x)
        {
            switch (key)
            {
                case "floodlight_on": { bool ok = RingJsonValue.TryBool(v, out bool? r); g.FloodlightOn = r; return ok; }
                case "white_led_on": { bool ok = RingJsonValue.TryBool(v, out bool? r); g.WhiteLedOn = r; return ok; }
                case "siren_on": { bool ok = RingJsonValue.TryBool(v, out bool? r); g.SirenOn = r; return ok; }
                case "battery_error": { bool ok = RingJsonValue.TryBool(v, out bool? r); g.BatteryError = r; return ok; }
                case "battery_temp": { bool ok = RingJsonValue.TryDouble(v, out double? r); g.BatteryTemp = r; return ok; }
                case "status_time": { bool ok = RingJsonValue.TryLong(v, out long? r); g.StatusTime = r; return ok; }
                case "uptime_sec": { bool ok = RingJsonValue.TryLong(v, out long? r); g.UptimeSec = r; return ok; }
                case "bandwidth": { bool ok = RingJsonValue.TryLong(v, out long? r); g.Bandwidth = r; return ok; }
                case "current_bandwidth": { bool ok = RingJsonValue.TryLong(v, out long? r); g.CurrentBandwidth = r; return ok; }
                case "current_bandwidth_mb": { bool ok = RingJsonValue.TryDecimal(v, out decimal? r); g.CurrentBandwidthMb = r; return ok; }
                case "stream_resolution": { bool ok = RingJsonValue.TryInt(v, out int? r); g.StreamResolution = r; return ok; }
                case "video_packets_total": { bool ok = RingJsonValue.TryLong(v, out long? r); g.VideoPacketsTotal = r; return ok; }
                case "firmware_avg_bitrate": { bool ok = RingJsonValue.TryLong(v, out long? r); g.FirmwareAvgBitrate = r; return ok; }
                case "sidewalk_connection": { bool ok = RingJsonValue.TryBool(v, out bool? r); x.SidewalkConnection = r; return ok; }
                case "transformer_voltage": { bool ok = RingJsonValue.TryDecimal(v, out decimal? r); x.TransformerVoltage = r; return ok; }
                case "transformer_voltage_category": { bool ok = RingJsonValue.TryText(v, out string? r); x.TransformerVoltageCategory = r; return ok; }
                case "transformer_score": { bool ok = RingJsonValue.TryText(v, out string? r); x.TransformerScore = r; return ok; }
                case "hatch_open": { bool ok = RingJsonValue.TryBool(v, out bool? r); x.HatchOpen = r; return ok; }
                case "package_warning_active": { bool ok = RingJsonValue.TryBool(v, out bool? r); x.PackageWarningActive = r; return ok; }
                case "rss_connected": { bool ok = RingJsonValue.TryBool(v, out bool? r); x.RssConnected = r; return ok; }
                case "second_battery_percentage_category": { bool ok = RingJsonValue.TryText(v, out string? r); x.SecondBatteryPercentageCategory = r; return ok; }
                case "second_battery_voltage_category": { bool ok = RingJsonValue.TryText(v, out string? r); x.SecondBatteryVoltageCategory = r; return ok; }
                case "bandwidth_last_time": { bool ok = RingJsonValue.TryLong(v, out long? r); x.BandwidthLastTime = r; return ok; }
                case "night_mode_on": { bool ok = RingJsonValue.TryBool(v, out bool? r); x.NightModeOn = r; return ok; }
                case "vod_enabled": { bool ok = RingJsonValue.TryBool(v, out bool? r); x.VodEnabled = r; return ok; }
                case "external_connection": { bool ok = RingJsonValue.TryBool(v, out bool? r); x.ExternalConnection = r; return ok; }
                default: return false;
            }
        }

        private static void ParseBatteries(JsonElement list, List<DeviceReportedBattery> batteries, List<string> notes)
        {
            if (list.ValueKind != JsonValueKind.Array)
            {
                if (list.ValueKind != JsonValueKind.Null)
                {
                    notes.Add($"health.batteries not stored: expected an array ({list.ValueKind})");
                }

                return;
            }

            int position = 0;
            foreach (JsonElement item in list.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    notes.Add($"health.batteries[{position}] not stored: expected an object ({item.ValueKind})");
                    position++;
                    continue;
                }

                var b = new DeviceReportedBattery { Position = position };
                foreach (JsonProperty p in item.EnumerateObject())
                {
                    if (!ApplyBattery(p.Name, p.Value, b))
                    {
                        notes.Add($"health.batteries.{p.Name} not stored: unknown key or unexpected shape ({p.Value.ValueKind})");
                    }
                    else if (p.Value.ValueKind == JsonValueKind.Null
                        && p.Name is "battery_id" or "battery_auth_status" or "battery_family" or "battery_fw_version" or "battery_ota_status")
                    {
                        notes.Add($"health.batteries.{p.Name} is null; its column type is a guess");
                    }
                }

                batteries.Add(b);
                position++;
            }
        }

        private static bool ApplyBattery(string key, JsonElement v, DeviceReportedBattery b)
        {
            switch (key)
            {
                case "battery_number": { bool ok = RingJsonValue.TryInt(v, out int? r); b.BatteryNumber = r; return ok; }
                case "battery_id": { bool ok = RingJsonValue.TryText(v, out string? r); b.BatteryId = r; return ok; }
                case "battery_percentage": { bool ok = RingJsonValue.TryInt(v, out int? r); b.Percentage = r; return ok; }
                case "battery_present": { bool ok = RingJsonValue.TryBool(v, out bool? r); b.Present = r; return ok; }
                case "battery_charging": { bool ok = RingJsonValue.TryBool(v, out bool? r); b.Charging = r; return ok; }
                case "battery_voltage": { bool ok = RingJsonValue.TryDecimal(v, out decimal? r); b.Voltage = r; return ok; }
                case "battery_percentage_category": { bool ok = RingJsonValue.TryText(v, out string? r); b.PercentageCategory = r; return ok; }
                case "battery_voltage_category": { bool ok = RingJsonValue.TryText(v, out string? r); b.VoltageCategory = r; return ok; }
                case "battery_temp": { bool ok = RingJsonValue.TryDouble(v, out double? r); b.Temp = r; return ok; }
                case "battery_auth_status": { bool ok = RingJsonValue.TryText(v, out string? r); b.AuthStatus = r; return ok; }
                case "battery_family": { bool ok = RingJsonValue.TryText(v, out string? r); b.Family = r; return ok; }
                case "battery_fw_version": { bool ok = RingJsonValue.TryText(v, out string? r); b.FwVersion = r; return ok; }
                case "battery_ota_status": { bool ok = RingJsonValue.TryText(v, out string? r); b.OtaStatus = r; return ok; }
                default: return false;
            }
        }
    }
}
