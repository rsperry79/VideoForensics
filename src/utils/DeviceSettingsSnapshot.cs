using System;
using System.Collections.Generic;
using System.Text.Json;

namespace KoenZomers.Ring.Api.Utils
{
    /// <summary>
    /// Per-doorbot settings values captured from the raw "devices" response before any destructive
    /// call runs, so the endpoint that changes one of them can restore it afterward instead of
    /// leaving the account in a different state than it found it in.
    /// </summary>
    public sealed record DoorbotSettingsSnapshot(int? Volume, int? ChimeType, bool? ChimeEnabled, int? ChimeDuration, bool? NightModeEnabled, bool? MotionDetectionEnabled);

    /// <summary>
    /// Parses the raw JSON body of GET ring_devices to pull out the settings values the tester's
    /// destructive endpoints can mutate (volume, chime type, night mode, motion detection), keyed
    /// by doorbot id. The KoenZomers.Ring.Api <see cref="Entities.Doorbot"/> class doesn't
    /// model these fields at all, so this reads the raw payload directly rather than the
    /// deserialized entity. Field paths below are confirmed against a live capture (not inferred):
    /// settings.doorbell_volume, settings.chime_settings.{type,enable,duration},
    /// settings.night_mode_on, settings.motion_detection_enabled. Never throws - a doorbot/field
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
                foreach (var arrayName in new[] { "doorbots", "authorized_doorbots", "stickup_cams" })
                {
                    if (!doc.RootElement.TryGetProperty(arrayName, out var array) || array.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    foreach (var device in array.EnumerateArray())
                    {
                        if (!device.TryGetProperty("id", out var idProp) || !idProp.TryGetInt64(out var id))
                        {
                            continue;
                        }
                        if (result.ContainsKey(id))
                        {
                            continue;
                        }

                        if (!device.TryGetProperty("settings", out var settings) || settings.ValueKind != JsonValueKind.Object)
                        {
                            continue;
                        }

                        int? volume = settings.TryGetProperty("doorbell_volume", out var v) && v.TryGetInt32(out var vi) ? vi : null;
                        bool? nightMode = TryGetBool(settings, "night_mode_on");
                        bool? motionDetection = TryGetBool(settings, "motion_detection_enabled");

                        int? chimeType = null;
                        bool? chimeEnabled = null;
                        int? chimeDuration = null;
                        if (settings.TryGetProperty("chime_settings", out var chime) && chime.ValueKind == JsonValueKind.Object)
                        {
                            chimeType = chime.TryGetProperty("type", out var ct) && ct.TryGetInt32(out var cti) ? cti : null;
                            chimeEnabled = TryGetBool(chime, "enable");
                            chimeDuration = chime.TryGetProperty("duration", out var cd) && cd.TryGetInt32(out var cdi) ? cdi : null;
                        }

                        result[id] = new DoorbotSettingsSnapshot(volume, chimeType, chimeEnabled, chimeDuration, nightMode, motionDetection);
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

        private static bool? TryGetBool(JsonElement obj, string propertyName) =>
            obj.TryGetProperty(propertyName, out var prop)
                ? prop.ValueKind switch
                {
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Number when prop.TryGetInt32(out var i) => i != 0,
                    _ => (bool?)null
                }
                : null;
    }
}
