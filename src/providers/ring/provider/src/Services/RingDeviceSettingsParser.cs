using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Providers.Ring.Services
{
    /// <summary>
    /// The two rows read from one device's settings: the Ring-only scalars (null when the device has no settings object) and the
    /// provider-agnostic general settings. Unstored holds a note for every value read but not stored, with the key and JSON type only, never the value.
    /// </summary>
    public sealed record RingSettingsGroups(RingDeviceSettings? Ring, DeviceGeneralSettings? General, IReadOnlyList<string> Unstored);

    /// <summary>
    /// Parses the scalar settings keys (and the small groups they sit in) into <see cref="RingDeviceSettings"/> and <see cref="DeviceGeneralSettings"/>
    /// rows, and removes the keys it types from the settings remainder JSON. A key with a wrong JSON type is noted and not stored; a typed null is
    /// stored as null without a note. Keys Ring sends that are not typed here stay in the remainder, so no value is dropped.
    /// </summary>
    public static class RingDeviceSettingsParser
    {
        private const string MotionSettingsKey = "motion_settings";
        private const string SnoozeKey = "motion_snooze_preset_profile";

        /// <summary>
        /// Builds the rows for one device. hasSettings is false when Ring sent no settings object: both rows are then null and stored rows stay untouched.
        /// An empty settings object writes rows of nulls. createdAt is the top-level created_at text (may be null).
        /// </summary>
        public static RingSettingsGroups Parse(bool hasSettings, IReadOnlyDictionary<string, JsonElement>? fields, string? createdAt)
        {
            var notes = new List<string>();
            if (!hasSettings)
            {
                return new RingSettingsGroups(null, null, notes);
            }

            IReadOnlyDictionary<string, JsonElement> f = fields ?? new Dictionary<string, JsonElement>();
            var ring = new RingDeviceSettings { RingCreatedAt = ParseCreatedAt(createdAt, notes) };
            ApplyRing(f, ring, notes);
            var general = new DeviceGeneralSettings();
            ApplyGeneral(f, general, notes);
            return new RingSettingsGroups(ring, general, notes);
        }

        /// <summary>
        /// Removes the keys this parser types from the settings remainder. Keys inside a typed group are removed one by one, and a group left empty is
        /// removed too. A typed group that is not an object is removed whole, since its value is noted and not stored.
        /// </summary>
        internal static void RemoveTypedKeys(JsonObject root)
        {
            foreach (string[] path in TypedPaths)
            {
                RemovePath(root, path, 0);
            }
        }

        private static readonly string[][] TypedPaths =
        [
            ["stark_enabled"],
            ["stark_enrolled"],
            ["enable_rich_notifications"],
            ["rich_notifications_billing_eligible"],
            ["rich_notifications_face_crop_enabled"],
            ["rich_notifications_scene_source"],
            ["people_detection_eligible"],
            ["terms_of_service_accepted"],
            ["motion_sensitivity_profile"],
            ["lite_24x7_footage_ttl"],
            ["lite_24x7", "enabled"],
            ["lite_24x7", "frequency_secs"],
            ["lite_24x7", "resolution_p"],
            ["lite_24x7", "subscribed"],
            ["server_settings", "ring_media_server_enabled"],
            ["server_settings", "ring_media_server_host"],
            ["sheila_settings", "cv_processing_enabled"],
            ["sheila_settings", "local_storage_enabled"],
            ["video_settings", "encryption_enabled"],
            ["video_settings", "encryption_method"],
            ["video_settings", "encryption_eligibility_mode"],
            ["video_settings", "recording_24x7_mode"],
            ["cv_settings", "search_types", "natural_language_search", "enabled"],
            ["cv_settings", "search_types", "natural_language_search", "mode"],
            ["concierge_settings", "mode"],
            ["concierge_settings", "alexa_settings", "delay_ms"],
            ["concierge_settings", "autoreply_settings", "delay_ms"],
            ["advanced_pir_motion_zones", "zone1_sensitivity"],
            ["advanced_pir_motion_zones", "zone2_sensitivity"],
            ["advanced_pir_motion_zones", "zone3_sensitivity"],
            ["advanced_pir_motion_zones", "zone4_sensitivity"],
            ["advanced_pir_motion_zones", "zone5_sensitivity"],
            ["advanced_pir_motion_zones", "zone6_sensitivity"],
            ["audio_settings", "enable_live_view_audio_override"],
            ["active_streaming_event_led_enabled"],
            ["flick_elim_recommended_mode"],
            ["enable_vod"],
            ["exposure_control"],
            ["vod_suspended"],
            ["pir_sensitivity_1"],
            ["enable_audio_recording"],
            ["advanced_motion_detection_enabled"],
            ["advanced_motion_detection_human_only_mode"],
            ["live_view_preset_profile"],
            ["motion_snooze_preset_profile"],
            [MotionSettingsKey, SnoozeKey],
            ["voice_volume"],
            ["live_view_disabled"],
            ["motion_announcement"],
            ["loitering_threshold"],
            ["user_specified_recording_ttl"],
            ["extended_live_view"],
            ["floodlight_settings", "brightness"],
            ["status_led_enable"],
        ];

        private static void ApplyRing(IReadOnlyDictionary<string, JsonElement> f, RingDeviceSettings r, List<string> notes)
        {
            r.StarkEnabled = Bool(f, notes, "stark_enabled");
            r.StarkEnrolled = Bool(f, notes, "stark_enrolled");
            r.EnableRichNotifications = Bool(f, notes, "enable_rich_notifications");
            r.RichNotificationsBillingEligible = Bool(f, notes, "rich_notifications_billing_eligible");
            r.RichNotificationsFaceCropEnabled = Bool(f, notes, "rich_notifications_face_crop_enabled");
            r.RichNotificationsSceneSource = Text(f, notes, "rich_notifications_scene_source");
            r.PeopleDetectionEligible = Bool(f, notes, "people_detection_eligible");
            r.TermsOfServiceAccepted = Text(f, notes, "terms_of_service_accepted");
            r.MotionSensitivityProfile = Int(f, notes, "motion_sensitivity_profile");
            r.Lite24x7FootageTtl = Int(f, notes, "lite_24x7_footage_ttl");
            r.Lite24x7Enabled = Bool(f, notes, "lite_24x7", "enabled");
            r.Lite24x7FrequencySecs = Int(f, notes, "lite_24x7", "frequency_secs");
            r.Lite24x7ResolutionP = Int(f, notes, "lite_24x7", "resolution_p");
            r.Lite24x7Subscribed = Bool(f, notes, "lite_24x7", "subscribed");
            r.RingMediaServerEnabled = Bool(f, notes, "server_settings", "ring_media_server_enabled");
            r.RingMediaServerHost = Text(f, notes, "server_settings", "ring_media_server_host");
            r.SheilaCvProcessingEnabled = Bool(f, notes, "sheila_settings", "cv_processing_enabled");
            r.SheilaLocalStorageEnabled = Bool(f, notes, "sheila_settings", "local_storage_enabled");
            r.EncryptionEnabled = Bool(f, notes, "video_settings", "encryption_enabled");
            r.EncryptionMethod = Int(f, notes, "video_settings", "encryption_method");
            r.EncryptionEligibilityMode = Text(f, notes, "video_settings", "encryption_eligibility_mode");
            r.Recording24x7Mode = Int(f, notes, "video_settings", "recording_24x7_mode");
            r.NaturalLanguageSearchEnabled = Bool(f, notes, "cv_settings", "search_types", "natural_language_search", "enabled");
            r.NaturalLanguageSearchMode = Text(f, notes, "cv_settings", "search_types", "natural_language_search", "mode");
            r.ConciergeMode = Text(f, notes, "concierge_settings", "mode");
            r.ConciergeAlexaDelayMs = Int(f, notes, "concierge_settings", "alexa_settings", "delay_ms");
            r.ConciergeAutoreplyDelayMs = Int(f, notes, "concierge_settings", "autoreply_settings", "delay_ms");
            r.PirZone1Sensitivity = Int(f, notes, "advanced_pir_motion_zones", "zone1_sensitivity");
            r.PirZone2Sensitivity = Int(f, notes, "advanced_pir_motion_zones", "zone2_sensitivity");
            r.PirZone3Sensitivity = Int(f, notes, "advanced_pir_motion_zones", "zone3_sensitivity");
            r.PirZone4Sensitivity = Int(f, notes, "advanced_pir_motion_zones", "zone4_sensitivity");
            r.PirZone5Sensitivity = Int(f, notes, "advanced_pir_motion_zones", "zone5_sensitivity");
            r.PirZone6Sensitivity = Int(f, notes, "advanced_pir_motion_zones", "zone6_sensitivity");
            r.EnableLiveViewAudioOverride = Bool(f, notes, "audio_settings", "enable_live_view_audio_override");
            r.ActiveStreamingEventLedEnabled = Bool(f, notes, "active_streaming_event_led_enabled");
            r.FlickElimRecommendedMode = Text(f, notes, "flick_elim_recommended_mode");
            r.EnableVod = Flag(f, notes, "enable_vod");
            r.ExposureControl = Int(f, notes, "exposure_control");
            r.VodSuspended = Flag(f, notes, "vod_suspended");
            r.PirSensitivity1 = Int(f, notes, "pir_sensitivity_1");
            r.EnableAudioRecording = Bool(f, notes, "enable_audio_recording");
            r.AdvancedMotionDetectionEnabled = Bool(f, notes, "advanced_motion_detection_enabled");
            r.AdvancedMotionDetectionHumanOnlyMode = Bool(f, notes, "advanced_motion_detection_human_only_mode");
            r.LiveViewPresetProfile = Text(f, notes, "live_view_preset_profile");
            r.MotionSnoozePresetProfile = Text(f, notes, SnoozeKey) ?? Text(f, notes, MotionSettingsKey, SnoozeKey);
        }

        private static void ApplyGeneral(IReadOnlyDictionary<string, JsonElement> f, DeviceGeneralSettings g, List<string> notes)
        {
            g.VoiceVolume = Int(f, notes, "voice_volume");
            g.LiveViewDisabled = Bool(f, notes, "live_view_disabled");
            g.MotionAnnouncement = Bool(f, notes, "motion_announcement");
            g.LoiteringThreshold = Int(f, notes, "loitering_threshold");
            g.UserSpecifiedRecordingTtl = Int(f, notes, "user_specified_recording_ttl");
            g.ExtendedLiveView = Int(f, notes, "extended_live_view");
            g.StatusLedEnabled = Bool(f, notes, "status_led_enable");
            g.FloodlightBrightness = Int(f, notes, "floodlight_settings", "brightness");
        }

        /// <summary>created_at as UTC. Null when absent; text that is not an ISO-8601 date-time is noted (without the text) and not stored.</summary>
        private static DateTime? ParseCreatedAt(string? text, List<string> notes)
        {
            if (text == null)
            {
                return null;
            }

            if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTimeOffset parsed))
            {
                return parsed.UtcDateTime;
            }

            notes.Add("created_at not stored: not an ISO-8601 date-time (String)");
            return null;
        }

        private static bool? Bool(IReadOnlyDictionary<string, JsonElement> f, List<string> notes, params string[] path)
        {
            JsonElement? v = Find(f, notes, path);
            if (v is not { } e)
            {
                return null;
            }

            bool ok = RingJsonValue.TryBool(e, out bool? r);
            return Stored(ok, r, path, e, notes);
        }

        private static int? Int(IReadOnlyDictionary<string, JsonElement> f, List<string> notes, params string[] path)
        {
            JsonElement? v = Find(f, notes, path);
            if (v is not { } e)
            {
                return null;
            }

            bool ok = RingJsonValue.TryInt(e, out int? r);
            return Stored(ok, r, path, e, notes);
        }

        private static string? Text(IReadOnlyDictionary<string, JsonElement> f, List<string> notes, params string[] path)
        {
            JsonElement? v = Find(f, notes, path);
            if (v is not { } e)
            {
                return null;
            }

            bool ok = RingJsonValue.TryText(e, out string? r);
            return Stored(ok, r, path, e, notes);
        }

        /// <summary>A Ring 0/1 flag stored as a boolean. Booleans are read as they are; any other shape is noted and not stored.</summary>
        private static bool? Flag(IReadOnlyDictionary<string, JsonElement> f, List<string> notes, params string[] path)
        {
            JsonElement? v = Find(f, notes, path);
            if (v is not { } e)
            {
                return null;
            }

            if (e.ValueKind == JsonValueKind.Number && RingJsonValue.TryLong(e, out long? n) && n is 0 or 1)
            {
                return n == 1;
            }

            bool ok = RingJsonValue.TryBool(e, out bool? r);
            return Stored(ok, r, path, e, notes);
        }

        /// <summary>Returns the stored value, or null with a note when the JSON shape did not fit. A typed null is ok and needs no note.</summary>
        private static T? Stored<T>(bool ok, T? value, string[] path, JsonElement e, List<string> notes) where T : struct
        {
            if (!ok)
            {
                notes.Add($"{string.Join('.', path)} not stored: unexpected shape ({e.ValueKind})");
                return null;
            }

            return value;
        }

        private static string? Stored(bool ok, string? value, string[] path, JsonElement e, List<string> notes)
        {
            if (!ok)
            {
                notes.Add($"{string.Join('.', path)} not stored: unexpected shape ({e.ValueKind})");
                return null;
            }

            return value;
        }

        /// <summary>
        /// Walks a key path through objects. Absent or null yields null with no note. A group on the way that is not an object is noted once (its
        /// name and JSON type only) and yields null.
        /// </summary>
        private static JsonElement? Find(IReadOnlyDictionary<string, JsonElement> f, List<string> notes, string[] path)
        {
            if (!f.TryGetValue(path[0], out JsonElement node))
            {
                return null;
            }

            for (int i = 1; i < path.Length; i++)
            {
                if (node.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                {
                    return null;
                }

                if (node.ValueKind != JsonValueKind.Object)
                {
                    string note = $"{string.Join('.', path, 0, i)} not stored: expected an object ({node.ValueKind})";
                    if (!notes.Contains(note))
                    {
                        notes.Add(note);
                    }

                    return null;
                }

                if (!node.TryGetProperty(path[i], out node))
                {
                    return null;
                }
            }

            return node;
        }

        /// <summary>Removes the key at path[i] from the object, and the parents on the path that the removal leaves empty.</summary>
        private static void RemovePath(JsonObject parent, string[] path, int i)
        {
            string key = path[i];
            if (i == path.Length - 1 || parent[key] is not JsonObject child)
            {
                _ = parent.Remove(key);
                return;
            }

            RemovePath(child, path, i + 1);
            if (child.Count == 0)
            {
                _ = parent.Remove(key);
            }
        }
    }
}
