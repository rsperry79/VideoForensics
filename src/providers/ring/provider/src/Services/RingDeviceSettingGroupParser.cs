using System.Text.Json;
using System.Text.Json.Nodes;

using VideoForensics.Core.Providers.Common.Contracts;

namespace VideoForensics.Providers.Ring.Services
{
    /// <summary>The typed settings parsed from a device's settings keys (see <see cref="RingDeviceSettingGroupParser"/>).</summary>
    public sealed record TypedSettingDetails(
        OfflineMotionDetail? OfflineMotion,
        IReadOnlyList<DetectionTypeDetail> DetectionTypes,
        IReadOnlyList<DetectionThresholdDetail> Thresholds);

    /// <summary>
    /// Parses the settings keys Ring sends in AdditionalFields into typed rows, so they can be queried by column.
    /// Keys parsed here are removed from the remainder JSON, which keeps only keys with no typed home.
    /// </summary>
    public static class RingDeviceSettingGroupParser
    {
        private const string OfflineMotionKey = "offline_motion_event_settings";
        private const string CvSettingsKey = "cv_settings";
        private const string DetectionTypesKey = "detection_types";
        private const string ThresholdKey = "threshold";
        private const string CvPaidKey = "cv_paid_features";
        private const string OtherPaidKey = "other_paid_features";
        private const string NetworkKey = "network_settings";

        /// <summary>Builds the typed rows for one device. Null fields (no settings sent) yield empty groups.</summary>
        public static TypedSettingDetails Parse(Dictionary<string, JsonElement>? fields)
        {
            if (fields == null)
            {
                return new TypedSettingDetails(null, [], []);
            }

            OfflineMotionDetail? offline = null;
            if (fields.TryGetValue(OfflineMotionKey, out JsonElement offlineElement) && offlineElement.ValueKind == JsonValueKind.Object)
            {
                offline = new OfflineMotionDetail(
                    Bool(offlineElement, "subscribed"),
                    Bool(offlineElement, "enabled"),
                    Int(offlineElement, "max_upload_kb"),
                    Int(offlineElement, "resolution_p"),
                    Int(offlineElement, "frequency_after_secs"),
                    Int(offlineElement, "period_after_secs"));
            }

            var detectionTypes = new List<DetectionTypeDetail>();
            var thresholds = new List<DetectionThresholdDetail>();
            if (fields.TryGetValue(CvSettingsKey, out JsonElement cv) && cv.ValueKind == JsonValueKind.Object)
            {
                if (cv.TryGetProperty(DetectionTypesKey, out JsonElement types) && types.ValueKind == JsonValueKind.Object)
                {
                    foreach (JsonProperty type in types.EnumerateObject().Where(p => p.Value.ValueKind == JsonValueKind.Object))
                    {
                        detectionTypes.Add(new DetectionTypeDetail(type.Name, Bool(type.Value, "enabled"), Text(type.Value, "mode"), Bool(type.Value, "record"), Bool(type.Value, "notification")));
                    }
                }

                if (cv.TryGetProperty(ThresholdKey, out JsonElement threshold) && threshold.ValueKind == JsonValueKind.Object)
                {
                    foreach (JsonProperty entry in threshold.EnumerateObject().Where(p => p.Value.ValueKind == JsonValueKind.Number))
                    {
                        thresholds.Add(new DetectionThresholdDetail(entry.Name, entry.Value.GetDouble()));
                    }
                }
            }

            return new TypedSettingDetails(offline, detectionTypes, thresholds);
        }

        /// <summary>
        /// The settings JSON left after the typed keys are removed: the offline object, the paid-feature and network groups, cv_settings.detection_types,
        /// and the numeric cv_settings.threshold entries. Null when nothing is left.
        /// </summary>
        public static string? RemainderJson(Dictionary<string, JsonElement>? fields)
        {
            if (fields == null)
            {
                return null;
            }

            var root = new JsonObject();
            foreach (KeyValuePair<string, JsonElement> field in fields)
            {
                root[field.Key] = JsonNode.Parse(field.Value.GetRawText());
            }

            // Keys the settings-row parser types (RingDeviceSettings, DeviceGeneralSettings) are not stored a second time in the remainder.
            RingDeviceSettingsParser.RemoveTypedKeys(root);
            _ = root.Remove(OfflineMotionKey);
            // These groups have typed rows (RingDevicePaidFeatures, DeviceNetworkSettings, RingDeviceNetworkDiagnosis), so they are not stored twice.
            _ = root.Remove(CvPaidKey);
            _ = root.Remove(OtherPaidKey);
            _ = root.Remove(NetworkKey);
            if (root[CvSettingsKey] is JsonObject cv)
            {
                _ = cv.Remove(DetectionTypesKey);
                if (cv[ThresholdKey] is JsonObject threshold)
                {
                    foreach (string name in threshold.Select(p => p.Key).ToList())
                    {
                        if (threshold[name] is JsonValue value && value.TryGetValue(out double _))
                        {
                            _ = threshold.Remove(name);
                        }
                    }

                    if (threshold.Count == 0)
                    {
                        _ = cv.Remove(ThresholdKey);
                    }
                }

                if (cv.Count == 0)
                {
                    _ = root.Remove(CvSettingsKey);
                }
            }

            return root.Count > 0 ? root.ToJsonString() : null;
        }

        private static bool? Bool(JsonElement obj, string name)
        {
            return obj.TryGetProperty(name, out JsonElement v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False ? v.GetBoolean() : null;
        }

        private static int? Int(JsonElement obj, string name)
        {
            return obj.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out int n) ? n : null;
        }

        private static string? Text(JsonElement obj, string name)
        {
            return obj.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        }
    }
}
