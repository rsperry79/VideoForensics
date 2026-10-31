using System.Text.Json;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Providers.Ring.Services
{
    /// <summary>
    /// The parsed contents of one device's Ring <c>features</c> object: the scalar flags (as an unsaved DeviceFeatures template)
    /// and one unsaved DeviceFeatureState per feature object. Ids and DeviceId are assigned when the rows are built.
    /// </summary>
    public sealed record RingFeatureDetails(DeviceFeatures Scalars, IReadOnlyList<DeviceFeatureState> States);

    /// <summary>
    /// Parses the Ring <c>features</c> keys that have no dedicated client property into typed columns. Objects become
    /// feature-state rows (every value lands in a typed column or a reason row, never raw JSON); non-object leaves become
    /// DeviceFeatures columns. Unknown leaf keys and unknown nested shapes are not stored.
    /// </summary>
    public static class RingDeviceFeatureParser
    {
        /// <summary>Parses the extension-data keys of a features object. Null input yields empty details.</summary>
        public static RingFeatureDetails Parse(IReadOnlyDictionary<string, JsonElement>? additional)
        {
            var scalars = new DeviceFeatures();
            var states = new List<DeviceFeatureState>();
            foreach (KeyValuePair<string, JsonElement> entry in additional ?? new Dictionary<string, JsonElement>())
            {
                JsonElement value = entry.Value;
                if (value.ValueKind == JsonValueKind.Object)
                {
                    // An object (even property_view) becomes a state row; the plain-boolean DeviceFeatures columns stay null for it.
                    states.Add(ParseState(entry.Key, value));
                }
                else
                {
                    ApplyScalar(scalars, entry.Key, value);
                }
            }

            return new RingFeatureDetails(scalars, states);
        }

        private static void ApplyScalar(DeviceFeatures f, string key, JsonElement v)
        {
            bool? flag = AsBool(v);
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

        private static DeviceFeatureState ParseState(string key, JsonElement obj)
        {
            var state = new DeviceFeatureState { FeatureKey = key };
            if (obj.TryGetProperty("eligibility", out JsonElement eligibility) && eligibility.ValueKind == JsonValueKind.Object)
            {
                state.Eligible = Bool(eligibility, "eligible");
                AddReasons(state, eligibility, "ineligibility_reasons", DeviceFeatureReason.Ineligibility);
            }

            if (obj.TryGetProperty("enablement", out JsonElement enablement) && enablement.ValueKind == JsonValueKind.Object)
            {
                state.Allowed = Bool(enablement, "allowed");
                state.Enabled = Bool(enablement, "enabled");
                AddReasons(state, enablement, "disallow_reasons", DeviceFeatureReason.Disallow);
            }

            state.IsEnabled = Bool(obj, "is_enabled");
            state.IsEligible = Bool(obj, "is_eligible");
            state.AllowsNoLocation = Bool(obj, "allows_no_location");
            state.MaxDigitalZoomLevel = Number(obj, "max_digital_zoom_level");
            state.RtMaxDigitalZoomLevel = Number(obj, "rt_max_digital_zoom_level");
            state.Score = Text(obj, "score");
            AddReasons(state, obj, "affected_features", DeviceFeatureReason.AffectedFeature);
            state.Learning = Text(obj, "learning");
            state.RecordingMode = Text(obj, "recording_mode");
            state.RecordingEnabled = Bool(obj, "recording_enabled");
            state.RecordingState = Text(obj, "recording_state");
            if (obj.TryGetProperty("recording_mode_availability", out JsonElement availability) && availability.ValueKind == JsonValueKind.Object
                && availability.TryGetProperty("continuous", out JsonElement continuous) && continuous.ValueKind == JsonValueKind.Object)
            {
                state.ContinuousSupported = Bool(continuous, "supported");
                state.ContinuousAvailable = Bool(continuous, "available");
                state.ContinuousUnavailabilityReason = Text(continuous, "unavailability_reason");
            }

            return state;
        }

        private static void AddReasons(DeviceFeatureState state, JsonElement parent, string property, string kind)
        {
            if (!parent.TryGetProperty(property, out JsonElement list) || list.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            int position = 0;
            foreach (JsonElement item in list.EnumerateArray())
            {
                string? reason = AsText(item);
                if (reason != null)
                {
                    state.Reasons.Add(new DeviceFeatureReason { Kind = kind, Position = position, Reason = reason });
                }

                position++;
            }
        }

        private static bool? Bool(JsonElement parent, string property)
        {
            return parent.TryGetProperty(property, out JsonElement v) ? AsBool(v) : null;
        }

        private static double? Number(JsonElement parent, string property)
        {
            return parent.TryGetProperty(property, out JsonElement v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;
        }

        private static string? Text(JsonElement parent, string property)
        {
            return parent.TryGetProperty(property, out JsonElement v) ? AsText(v) : null;
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
