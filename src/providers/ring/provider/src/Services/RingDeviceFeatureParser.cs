using System.Text.Json;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Providers.Ring.Services
{
    /// <summary>
    /// The parsed contents of one device's Ring <c>features</c> object: the scalar flags (as an unsaved DeviceFeatures template)
    /// and one unsaved RingDeviceFeatureState per feature object. Ids and DeviceId are assigned when the rows are built.
    /// Unstored holds one note per value that was read but not stored (key, reason and JSON type only, never the value).
    /// </summary>
    public sealed record RingFeatureDetails(DeviceFeatures Scalars, IReadOnlyList<RingDeviceFeatureState> States, IReadOnlyList<string>? Unstored = null);

    /// <summary>
    /// Parses the Ring <c>features</c> keys that have no dedicated client property into typed columns. Objects become
    /// feature-state rows (every value lands in a typed column or a reason row, never raw JSON); non-object leaves become
    /// DeviceFeatures columns. Unknown leaf keys and unknown nested shapes are not stored; each one is noted in Unstored.
    /// </summary>
    public static class RingDeviceFeatureParser
    {
        private static readonly HashSet<string> ScalarKeys =
        [
            "cfes_eligible", "package_warning", "auto_zoom_track", "automated_siren", "alexa_plus_greetings", "remote_access_control",
            "auto_shutoff_device", "short_on_long_off", "tracklight", "motion_zone_recommendation", "rich_notifications_eligible",
            "show_offline_motion_events", "sheila_camera_eligible", "sheila_camera_processing_eligible", "dynamic_network_switching_eligible",
            "vehicle_detection_eligible", "animal_detection_eligible", "show_24x7_lite", "recording_24x7_eligible", "property_view"
        ];

        private static readonly HashSet<string> StateKeys =
        [
            "eligibility", "enablement", "is_enabled", "is_eligible", "allows_no_location", "max_digital_zoom_level", "rt_max_digital_zoom_level",
            "score", "affected_features", "learning", "recording_mode", "recording_enabled", "recording_state", "recording_mode_availability"
        ];

        /// <summary>Parses the extension-data keys of a features object. Null input yields empty details.</summary>
        public static RingFeatureDetails Parse(IReadOnlyDictionary<string, JsonElement>? additional)
        {
            var scalars = new DeviceFeatures();
            var states = new List<RingDeviceFeatureState>();
            var notes = new List<string>();
            foreach (KeyValuePair<string, JsonElement> entry in additional ?? new Dictionary<string, JsonElement>())
            {
                JsonElement value = entry.Value;
                if (value.ValueKind == JsonValueKind.Object)
                {
                    // An object (even property_view) becomes a state row; the plain-boolean DeviceFeatures columns stay null for it.
                    states.Add(ParseState(entry.Key, value, notes));
                }
                else
                {
                    ApplyScalar(scalars, entry.Key, value, notes);
                }
            }

            return new RingFeatureDetails(scalars, states, notes);
        }

        private static void ApplyScalar(DeviceFeatures f, string key, JsonElement v, List<string> notes)
        {
            if (!ScalarKeys.Contains(key))
            {
                // Null is how Ring says "not sent" (or an object-shaped feature that is absent), so it is not worth a note.
                if (v.ValueKind != JsonValueKind.Null)
                {
                    notes.Add($"features.{key} not stored: no column for it ({v.ValueKind})");
                }

                return;
            }

            bool? flag = AsBool(v);
            if (flag == null && v.ValueKind != JsonValueKind.Null)
            {
                notes.Add($"features.{key} not stored: expected a boolean ({v.ValueKind})");
            }

            switch (key)
            {
                case "cfes_eligible": f.CfesEligible = flag; break;
                case "package_warning": f.PackageWarning = flag; break;
                case "auto_zoom_track": f.AutoZoomTrack = flag; break;
                case "automated_siren": f.AutomatedSiren = flag; break;
                case "alexa_plus_greetings": f.AlexaPlusGreetings = flag; break;
                case "remote_access_control": f.RemoteAccessControl = flag; break;
                case "auto_shutoff_device": f.AutoShutoffDevice = flag; break;
                case "short_on_long_off": f.ShortOnLongOff = flag; break;
                case "tracklight": f.Tracklight = flag; break;
                case "motion_zone_recommendation": f.MotionZoneRecommendation = flag; break;
                case "rich_notifications_eligible": f.RichNotificationsEligible = flag; break;
                case "show_offline_motion_events": f.ShowOfflineMotionEvents = flag; break;
                case "sheila_camera_eligible": f.SheilaCameraEligible = flag; break;
                case "sheila_camera_processing_eligible": f.SheilaCameraProcessingEligible = flag; break;
                case "dynamic_network_switching_eligible": f.DynamicNetworkSwitchingEligible = flag; break;
                case "vehicle_detection_eligible": f.VehicleDetectionEligible = flag; break;
                case "animal_detection_eligible": f.AnimalDetectionEligible = flag; break;
                case "show_24x7_lite": f.Show24x7Lite = flag; break;
                case "recording_24x7_eligible": f.Recording24x7Eligible = flag; break;
                case "property_view": f.PropertyView = flag; break;
                default: break;
            }
        }

        private static RingDeviceFeatureState ParseState(string key, JsonElement obj, List<string> notes)
        {
            string path = $"features.{key}";
            var state = new RingDeviceFeatureState { FeatureKey = key };
            NoteUnknownKeys(obj, StateKeys, path, notes);
            if (TryObject(obj, "eligibility", path, notes, out JsonElement eligibility))
            {
                string p = $"{path}.eligibility";
                NoteUnknownKeys(eligibility, ["eligible", "ineligibility_reasons"], p, notes);
                state.Eligible = Bool(eligibility, "eligible", p, notes);
                AddReasons(state, eligibility, "ineligibility_reasons", RingDeviceFeatureReason.Ineligibility, p, notes);
            }

            if (TryObject(obj, "enablement", path, notes, out JsonElement enablement))
            {
                string p = $"{path}.enablement";
                NoteUnknownKeys(enablement, ["allowed", "enabled", "disallow_reasons"], p, notes);
                state.Allowed = Bool(enablement, "allowed", p, notes);
                state.Enabled = Bool(enablement, "enabled", p, notes);
                AddReasons(state, enablement, "disallow_reasons", RingDeviceFeatureReason.Disallow, p, notes);
            }

            state.IsEnabled = Bool(obj, "is_enabled", path, notes);
            state.IsEligible = Bool(obj, "is_eligible", path, notes);
            state.AllowsNoLocation = Bool(obj, "allows_no_location", path, notes);
            state.MaxDigitalZoomLevel = Number(obj, "max_digital_zoom_level", path, notes);
            state.RtMaxDigitalZoomLevel = Number(obj, "rt_max_digital_zoom_level", path, notes);
            state.Score = Text(obj, "score", path, notes);
            AddReasons(state, obj, "affected_features", RingDeviceFeatureReason.AffectedFeature, path, notes);
            state.Learning = Text(obj, "learning", path, notes);
            NoteGuessedValue(obj, "learning", path, notes);
            state.RecordingMode = Text(obj, "recording_mode", path, notes);
            state.RecordingEnabled = Bool(obj, "recording_enabled", path, notes);
            state.RecordingState = Text(obj, "recording_state", path, notes);
            if (TryObject(obj, "recording_mode_availability", path, notes, out JsonElement availability))
            {
                string p = $"{path}.recording_mode_availability";
                NoteUnknownKeys(availability, ["continuous"], p, notes);
                if (TryObject(availability, "continuous", p, notes, out JsonElement continuous))
                {
                    p += ".continuous";
                    NoteUnknownKeys(continuous, ["supported", "available", "unavailability_reason"], p, notes);
                    state.ContinuousSupported = Bool(continuous, "supported", p, notes);
                    state.ContinuousAvailable = Bool(continuous, "available", p, notes);
                    NoteGuessedValue(continuous, "available", p, notes);
                    state.ContinuousUnavailabilityReason = Text(continuous, "unavailability_reason", p, notes);
                }
            }

            return state;
        }

        /// <summary>Notes each property of an object that no column reads, with its JSON type.</summary>
        private static void NoteUnknownKeys(JsonElement obj, IReadOnlyCollection<string> known, string path, List<string> notes)
        {
            foreach (JsonProperty p in obj.EnumerateObject())
            {
                if (!known.Contains(p.Name))
                {
                    notes.Add($"{path}.{p.Name} not stored: unknown key ({p.Value.ValueKind})");
                }
            }
        }

        /// <summary>True when the property is an object; a non-null value of another shape is noted and skipped.</summary>
        private static bool TryObject(JsonElement parent, string property, string path, List<string> notes, out JsonElement value)
        {
            if (parent.TryGetProperty(property, out value) && value.ValueKind == JsonValueKind.Object)
            {
                return true;
            }

            if (value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined))
            {
                notes.Add($"{path}.{property} not stored: expected an object ({value.ValueKind})");
            }

            return false;
        }

        /// <summary>For a column whose type is a guess (null in every sample): note the JSON type of a non-null value, never the value.</summary>
        private static void NoteGuessedValue(JsonElement parent, string property, string path, List<string> notes)
        {
            if (parent.TryGetProperty(property, out JsonElement v) && v.ValueKind != JsonValueKind.Null)
            {
                notes.Add($"{path}.{property} has a value ({v.ValueKind}); its column type is a guess");
            }
        }

        private static void AddReasons(RingDeviceFeatureState state, JsonElement parent, string property, string kind, string path, List<string> notes)
        {
            if (!parent.TryGetProperty(property, out JsonElement list) || list.ValueKind == JsonValueKind.Null)
            {
                return;
            }

            if (list.ValueKind != JsonValueKind.Array)
            {
                notes.Add($"{path}.{property} not stored: expected an array ({list.ValueKind})");
                return;
            }

            int position = 0;
            foreach (JsonElement item in list.EnumerateArray())
            {
                string? reason = AsText(item);
                if (reason != null)
                {
                    state.Reasons.Add(new RingDeviceFeatureReason { Kind = kind, Position = position, Reason = reason });
                }
                else if (item.ValueKind != JsonValueKind.Null)
                {
                    notes.Add($"{path}.{property}[{position}] not stored: expected a scalar ({item.ValueKind})");
                }

                position++;
            }
        }

        private static bool? Bool(JsonElement parent, string property, string path, List<string> notes)
        {
            if (!parent.TryGetProperty(property, out JsonElement v))
            {
                return null;
            }

            bool? result = AsBool(v);
            if (result == null && v.ValueKind != JsonValueKind.Null)
            {
                notes.Add($"{path}.{property} not stored: expected a boolean ({v.ValueKind})");
            }

            return result;
        }

        private static double? Number(JsonElement parent, string property, string path, List<string> notes)
        {
            if (!parent.TryGetProperty(property, out JsonElement v))
            {
                return null;
            }

            if (v.ValueKind == JsonValueKind.Number)
            {
                return v.GetDouble();
            }

            if (v.ValueKind != JsonValueKind.Null)
            {
                notes.Add($"{path}.{property} not stored: expected a number ({v.ValueKind})");
            }

            return null;
        }

        private static string? Text(JsonElement parent, string property, string path, List<string> notes)
        {
            if (!parent.TryGetProperty(property, out JsonElement v))
            {
                return null;
            }

            string? result = AsText(v);
            if (result == null && v.ValueKind != JsonValueKind.Null)
            {
                notes.Add($"{path}.{property} not stored: expected a scalar ({v.ValueKind})");
            }

            return result;
        }

        private static bool? AsBool(JsonElement v)
        {
            return v.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null
            };
        }

        /// <summary>Scalar JSON as text; null, objects and arrays yield null (nested JSON is never stored as a string).</summary>
        private static string? AsText(JsonElement v)
        {
            return v.ValueKind switch
            {
                JsonValueKind.String => v.GetString(),
                JsonValueKind.Number => v.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => null
            };
        }
    }
}
