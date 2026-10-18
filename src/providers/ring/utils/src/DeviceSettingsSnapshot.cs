using System.Collections.Generic;
using System.Text.Json;

namespace VideoForensics.Providers.Ring
{
    /// <summary>
    /// Per-doorbot settings values captured from the raw "devices" response before any destructive
    /// call runs, so the endpoint that changes one of them can restore it afterward instead of
    /// leaving the account in a different state than it found it in.
    /// </summary>
    public sealed record DoorbotSettingsSnapshot(int? Volume, int? ChimeType, bool? ChimeEnabled, int? ChimeDuration, bool? NightModeEnabled, bool? MotionDetectionEnabled, string? Description = null);

    /// <summary>
    /// Parses the raw JSON body of GET ring_devices to pull out the settings values the tester's
    /// destructive endpoints can mutate (volume, chime type, night mode, motion detection) plus the device
    /// description (name, needed by the form-encoded volume/chime-type calls), keyed
    /// by doorbot id. The Ring.Ring.Api <see cref="Entities.Doorbot"/> class doesn't
    /// model these fields at all, so this reads the raw payload directly rather than the
    /// deserialized entity. Field paths: settings.volume_settings.doorbell_volume (root settings.doorbell_volume
    /// as fallback), settings.chime_settings.{type,enable,duration}, settings.night_mode_on,
    /// settings.motion_settings.motion_detection_enabled (root settings.motion_detection_enabled as fallback). Never throws - a doorbot/field
    /// that can't be found just means restore is skipped for it later, not a failed run.
    /// </summary>
    public static class DeviceSettingsSnapshot
    {
        public static Dictionary<long, DoorbotSettingsSnapshot> ParseFromDevicesJson(string devicesJson)
        {
            var result = new Dictionary<long, DoorbotSettingsSnapshot>();

            try
            {
                using var doc = JsonDocument.Parse(devicesJson);
                foreach (string? arrayName in new[] { "doorbots", "authorized_doorbots", "stickup_cams" })
                {
                    if (!doc.RootElement.TryGetProperty(arrayName, out JsonElement array) || array.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    foreach (JsonElement device in array.EnumerateArray())
                    {
                        if (!device.TryGetProperty("id", out JsonElement idProp) || !idProp.TryGetInt64(out long id))
                        {
                            continue;
                        }

                        if (result.ContainsKey(id))
                        {
                            continue;
                        }

                        if (!device.TryGetProperty("settings", out JsonElement settings) || settings.ValueKind != JsonValueKind.Object)
                        {
                            continue;
                        }

                        // Real captures carry doorbell_volume and motion_detection_enabled under volume_settings and
                        // motion_settings, not at the settings root. The root read stays as a fallback.
                        int? volume = TryGetInt(settings, "doorbell_volume") ?? TryGetInt(NestedObject(settings, "volume_settings"), "doorbell_volume");
                        bool? nightMode = TryGetBool(settings, "night_mode_on");
                        bool? motionDetection = TryGetBool(settings, "motion_detection_enabled") ?? TryGetBool(NestedObject(settings, "motion_settings"), "motion_detection_enabled");

                        int? chimeType = null;
                        bool? chimeEnabled = null;
                        int? chimeDuration = null;
                        if (settings.TryGetProperty("chime_settings", out JsonElement chime) && chime.ValueKind == JsonValueKind.Object)
                        {
                            chimeType = chime.TryGetProperty("type", out JsonElement ct) && ct.TryGetInt32(out int cti) ? cti : null;
                            chimeEnabled = TryGetBool(chime, "enable");
                            chimeDuration = chime.TryGetProperty("duration", out JsonElement cd) && cd.TryGetInt32(out int cdi) ? cdi : null;
                        }

                        string? description = device.TryGetProperty("description", out JsonElement desc) && desc.ValueKind == JsonValueKind.String ? desc.GetString() : null;

                        result[id] = new DoorbotSettingsSnapshot(volume, chimeType, chimeEnabled, chimeDuration, nightMode, motionDetection, description);
                    }
                }
            }
            catch
            {
                // Malformed/unexpected shape - return whatever was parsed before the failure (or
                // empty). Restore for affected doorbots is simply skipped, not fatal to the run.
            }

            return result;
        }

        /// <summary>Returns the named child object, or a default element when it is absent or not an object.</summary>
        private static JsonElement NestedObject(JsonElement obj, string propertyName)
        {
            return obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(propertyName, out JsonElement child) && child.ValueKind == JsonValueKind.Object
                ? child
                : default;
        }

        private static int? TryGetInt(JsonElement obj, string propertyName)
        {
            return obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(propertyName, out JsonElement prop) && prop.TryGetInt32(out int i)
                ? i
                : null;
        }

        private static bool? TryGetBool(JsonElement obj, string propertyName)
        {
            return obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(propertyName, out JsonElement prop)
                ? prop.ValueKind switch
                {
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Number when prop.TryGetInt32(out int i) => i != 0,
                    _ => null
                }
                : null;
        }
    }
}

