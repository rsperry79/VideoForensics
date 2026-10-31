using System.Text.Json;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Providers.Ring.Services
{
    /// <summary>
    /// The three settings groups read from a doorbot or stickup cam settings object: the paid flags, the network settings and the network
    /// diagnosis. Each is a typed row (null when Ring sent no object). Unstored holds a note for every value read but not stored, with the key
    /// and JSON type only, never the value.
    /// </summary>
    public sealed record RingNetworkGroups(
        RingDevicePaidFeatures? PaidFeatures,
        DeviceNetworkSettings? NetworkSettings,
        RingDeviceNetworkDiagnosis? NetworkDiagnosis,
        IReadOnlyList<string> Unstored);

    /// <summary>
    /// Parses <c>cv_paid_features</c>, <c>other_paid_features</c> and <c>network_settings</c> (with its <c>network_diagnosis</c>) into typed rows.
    /// Unknown keys and values whose JSON type does not fit their column are noted, never stored as JSON. An absent or null group yields null
    /// (stored rows stay untouched); an empty object yields a row of nulls.
    /// </summary>
    public static class RingDeviceNetworkParser
    {
        private const string CvPaidKey = "cv_paid_features";
        private const string OtherPaidKey = "other_paid_features";
        private const string NetworkKey = "network_settings";
        private const string DiagnosisKey = "network_diagnosis";

        /// <summary>Parses the three groups from a settings object's AdditionalFields. Null fields (no settings sent) yield no rows and no notes.</summary>
        public static RingNetworkGroups Parse(Dictionary<string, JsonElement>? fields)
        {
            var notes = new List<string>();
            if (fields == null)
            {
                return new RingNetworkGroups(null, null, null, notes);
            }

            RingDevicePaidFeatures? paid = ParsePaidFeatures(fields, notes);
            DeviceNetworkSettings? network = null;
            RingDeviceNetworkDiagnosis? diagnosis = null;
            if (TryObject(Get(fields, NetworkKey), NetworkKey, notes, out JsonElement net))
            {
                network = new DeviceNetworkSettings();
                foreach (JsonProperty p in net.EnumerateObject())
                {
                    // The diagnosis object is parsed on its own below, so its keys are not reported as unknown network keys here.
                    if (p.Name == DiagnosisKey)
                    {
                        continue;
                    }

                    if (!ApplyNetwork(p.Name, p.Value, network))
                    {
                        notes.Add($"{NetworkKey}.{p.Name} not stored: unknown key or unexpected shape ({p.Value.ValueKind})");
                    }
                }

                diagnosis = ParseDiagnosis(net, notes);
            }

            return new RingNetworkGroups(paid, network, diagnosis, notes);
        }

        /// <summary>
        /// Builds one paid-feature row from both groups. The row is written as a whole, so a group Ring omits leaves its columns null.
        /// Returns null when neither group is an object.
        /// </summary>
        private static RingDevicePaidFeatures? ParsePaidFeatures(Dictionary<string, JsonElement> fields, List<string> notes)
        {
            bool hasCv = TryObject(Get(fields, CvPaidKey), CvPaidKey, notes, out JsonElement cv);
            bool hasOther = TryObject(Get(fields, OtherPaidKey), OtherPaidKey, notes, out JsonElement other);
            if (!hasCv && !hasOther)
            {
                return null;
            }

            var row = new RingDevicePaidFeatures();
            if (hasCv)
            {
                foreach (JsonProperty p in cv.EnumerateObject())
                {
                    if (!ApplyCvPaid(p.Name, p.Value, row))
                    {
                        notes.Add($"{CvPaidKey}.{p.Name} not stored: unknown key or unexpected shape ({p.Value.ValueKind})");
                    }
                }
            }

            if (hasOther)
            {
                foreach (JsonProperty p in other.EnumerateObject())
                {
                    if (!ApplyOtherPaid(p.Name, p.Value, row))
                    {
                        notes.Add($"{OtherPaidKey}.{p.Name} not stored: unknown key or unexpected shape ({p.Value.ValueKind})");
                    }
                }
            }

            return row;
        }

        /// <summary>Parses network_settings.network_diagnosis. Absent or null yields null; a non-object is noted and yields null.</summary>
        private static RingDeviceNetworkDiagnosis? ParseDiagnosis(JsonElement network, List<string> notes)
        {
            if (!network.TryGetProperty(DiagnosisKey, out JsonElement value)
                || !TryObject(value, $"{NetworkKey}.{DiagnosisKey}", notes, out JsonElement diagnosis))
            {
                return null;
            }

            var row = new RingDeviceNetworkDiagnosis();
            foreach (JsonProperty p in diagnosis.EnumerateObject())
            {
                if (!ApplyDiagnosis(p.Name, p.Value, row))
                {
                    notes.Add($"{NetworkKey}.{DiagnosisKey}.{p.Name} not stored: unknown key or unexpected shape ({p.Value.ValueKind})");
                }
            }

            return row;
        }

        /// <summary>Reads a group as an object. A null or absent group is false with no note; a non-object is false with a note.</summary>
        private static bool TryObject(JsonElement? value, string path, List<string> notes, out JsonElement obj)
        {
            obj = default;
            if (value is not { } v || v.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                return false;
            }

            if (v.ValueKind == JsonValueKind.Object)
            {
                obj = v;
                return true;
            }

            notes.Add($"{path} not stored: expected an object ({v.ValueKind})");
            return false;
        }

        private static JsonElement? Get(Dictionary<string, JsonElement> fields, string key)
        {
            return fields.TryGetValue(key, out JsonElement v) ? v : null;
        }

        /// <summary>Applies a cv_paid_features key. False when the key is unknown or its value has an unexpected shape.</summary>
        private static bool ApplyCvPaid(string key, JsonElement v, RingDevicePaidFeatures p)
        {
            switch (key)
            {
                case "baby_cry": { bool ok = RingJsonValue.TryBool(v, out bool? r); p.CvPaidBabyCry = r; return ok; }
                case "car_alarm": { bool ok = RingJsonValue.TryBool(v, out bool? r); p.CvPaidCarAlarm = r; return ok; }
                case "co2_smoke_alarm": { bool ok = RingJsonValue.TryBool(v, out bool? r); p.CvPaidCo2SmokeAlarm = r; return ok; }
                case "dog_bark": { bool ok = RingJsonValue.TryBool(v, out bool? r); p.CvPaidDogBark = r; return ok; }
                case "general_sound": { bool ok = RingJsonValue.TryBool(v, out bool? r); p.CvPaidGeneralSound = r; return ok; }
                case "glass_break": { bool ok = RingJsonValue.TryBool(v, out bool? r); p.CvPaidGlassBreak = r; return ok; }
                case "human": { bool ok = RingJsonValue.TryBool(v, out bool? r); p.CvPaidHuman = r; return ok; }
                case "loitering": { bool ok = RingJsonValue.TryBool(v, out bool? r); p.CvPaidLoitering = r; return ok; }
                case "motion": { bool ok = RingJsonValue.TryBool(v, out bool? r); p.CvPaidMotion = r; return ok; }
                case "other_motion": { bool ok = RingJsonValue.TryBool(v, out bool? r); p.CvPaidOtherMotion = r; return ok; }
                case "package_delivery": { bool ok = RingJsonValue.TryBool(v, out bool? r); p.CvPaidPackageDelivery = r; return ok; }
                case "package_pickup": { bool ok = RingJsonValue.TryBool(v, out bool? r); p.CvPaidPackagePickup = r; return ok; }
                case "cv_triggers": { bool ok = RingJsonValue.TryBool(v, out bool? r); p.CvPaidCvTriggers = r; return ok; }
                case "vehicle": { bool ok = RingJsonValue.TryBool(v, out bool? r); p.CvPaidVehicle = r; return ok; }
                case "animal": { bool ok = RingJsonValue.TryBool(v, out bool? r); p.CvPaidAnimal = r; return ok; }
                default: return false;
            }
        }

        /// <summary>Applies an other_paid_features key. False when the key is unknown or its value has an unexpected shape.</summary>
        private static bool ApplyOtherPaid(string key, JsonElement v, RingDevicePaidFeatures p)
        {
            switch (key)
            {
                case "alexa_concierge": { bool ok = RingJsonValue.TryBool(v, out bool? r); p.OtherPaidAlexaConcierge = r; return ok; }
                case "sheila_cv": { bool ok = RingJsonValue.TryBool(v, out bool? r); p.OtherPaidSheilaCv = r; return ok; }
                case "sheila_recording": { bool ok = RingJsonValue.TryBool(v, out bool? r); p.OtherPaidSheilaRecording = r; return ok; }
                case "critical_alerts": { bool ok = RingJsonValue.TryBool(v, out bool? r); p.OtherPaidCriticalAlerts = r; return ok; }
                case "system_level_pip": { bool ok = RingJsonValue.TryBool(v, out bool? r); p.OtherPaidSystemLevelPip = r; return ok; }
                case "extended_live_view": { bool ok = RingJsonValue.TryBool(v, out bool? r); p.OtherPaidExtendedLiveView = r; return ok; }
                case "snapshot_capture_plus": { bool ok = RingJsonValue.TryBool(v, out bool? r); p.OtherPaidSnapshotCapturePlus = r; return ok; }
                case "live_speak": { bool ok = RingJsonValue.TryBool(v, out bool? r); p.OtherPaidLiveSpeak = r; return ok; }
                case "closed_caption": { bool ok = RingJsonValue.TryBool(v, out bool? r); p.OtherPaidClosedCaption = r; return ok; }
                case "daily_digest": { bool ok = RingJsonValue.TryBool(v, out bool? r); p.OtherPaidDailyDigest = r; return ok; }
                case "ding_call": { bool ok = RingJsonValue.TryBool(v, out bool? r); p.OtherPaidDingCall = r; return ok; }
                case "natural_language_search": { bool ok = RingJsonValue.TryBool(v, out bool? r); p.OtherPaidNaturalLanguageSearch = r; return ok; }
                case "multicam_live_view": { bool ok = RingJsonValue.TryBool(v, out bool? r); p.OtherPaidMulticamLiveView = r; return ok; }
                case "package_protection": { bool ok = RingJsonValue.TryBool(v, out bool? r); p.OtherPaidPackageProtection = r; return ok; }
                case "alarm_triggered_recording": { bool ok = RingJsonValue.TryBool(v, out bool? r); p.OtherPaidAlarmTriggeredRecording = r; return ok; }
                case "recording_24x7": { bool ok = RingJsonValue.TryBool(v, out bool? r); p.OtherPaidRecording24x7 = r; return ok; }
                default: return false;
            }
        }

        /// <summary>Applies a network_settings key (other than the diagnosis object). False when the key is unknown or its value has an unexpected shape.</summary>
        private static bool ApplyNetwork(string key, JsonElement v, DeviceNetworkSettings n)
        {
            switch (key)
            {
                case "mac_address_ble": { bool ok = RingJsonValue.TryText(v, out string? r); n.MacAddressBle = r; return ok; }
                case "mac_address_wifi_24": { bool ok = RingJsonValue.TryText(v, out string? r); n.MacAddressWifi24 = r; return ok; }
                case "mac_address_wifi_5": { bool ok = RingJsonValue.TryText(v, out string? r); n.MacAddressWifi5 = r; return ok; }
                case "max_dynamic_listen_interval": { bool ok = RingJsonValue.TryText(v, out string? r); n.MaxDynamicListenInterval = r; return ok; }
                case "multi_net_pref": { bool ok = RingJsonValue.TryInt(v, out int? r); n.MultiNetPref = r; return ok; }
                case "data_restriction_enabled": { bool ok = RingJsonValue.TryBool(v, out bool? r); n.DataRestrictionEnabled = r; return ok; }
                default: return false;
            }
        }

        /// <summary>Applies a network_diagnosis key. False when the key is unknown or its value has an unexpected shape.</summary>
        private static bool ApplyDiagnosis(string key, JsonElement v, RingDeviceNetworkDiagnosis d)
        {
            switch (key)
            {
                case "channel_analysis": { bool ok = RingJsonValue.TryBool(v, out bool? r); d.ChannelAnalysis = r; return ok; }
                case "periodic_diagnosis": { bool ok = RingJsonValue.TryBool(v, out bool? r); d.PeriodicDiagnosis = r; return ok; }
                case "lan_performance": { bool ok = RingJsonValue.TryBool(v, out bool? r); d.LanPerformance = r; return ok; }
                case "tcp_iperf": { bool ok = RingJsonValue.TryBool(v, out bool? r); d.TcpIperf = r; return ok; }
                case "udp_iperf": { bool ok = RingJsonValue.TryBool(v, out bool? r); d.UdpIperf = r; return ok; }
                case "tcp_host": { bool ok = RingJsonValue.TryText(v, out string? r); d.TcpHost = r; return ok; }
                case "udp_host": { bool ok = RingJsonValue.TryText(v, out string? r); d.UdpHost = r; return ok; }
                case "tcp_port": { bool ok = RingJsonValue.TryInt(v, out int? r); d.TcpPort = r; return ok; }
                case "udp_port": { bool ok = RingJsonValue.TryInt(v, out int? r); d.UdpPort = r; return ok; }
                case "udp_bandwidth": { bool ok = RingJsonValue.TryLong(v, out long? r); d.UdpBandwidth = r; return ok; }
                default: return false;
            }
        }
    }
}
