using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace VideoForensics.Providers.Ring.Services
{
    /// <summary>
    /// Reduces a raw Ring event payload (the serialized <c>DoorbotHistoryEvent</c> that used to be stored whole in
    /// <c>Events.MetadataJson</c> / <c>MediaItems.MetadataJson</c>) to the part that has no structured home.
    /// </summary>
    /// <remarks>
    /// Rule: a value kept only in a JSON column is not in the database. Every path in <see cref="KnownPaths"/> is written to
    /// typed columns or child rows by <see cref="RingEventHistoryMapper"/> (the <c>RingDoorbotHistory*</c> / <c>RingEvent*</c>
    /// tables), so it is removed here. What is left is stored as <c>MetadataJson</c>; nothing left means <c>null</c>.
    /// The integrity hashes still cover the full raw payload (see <see cref="Split"/>), so the raw payload can no longer be
    /// re-hashed from the database alone.
    /// Paths are relative to the event root: object keys joined with '.', array elements as '[]'. A path is a leaf (scalar)
    /// path; a null value, or an object or array that ends up empty, carries no data and is dropped. Only leaves in the set
    /// are removed, so a value of an unexpected shape at a known key (for example an object under
    /// <c>cv_properties.key_moments</c>, which is stored only when it is text) stays in the remainder.
    /// </remarks>
    public static class RingEventPayloadRemainder
    {
        /// <summary>The event-root-relative leaf paths the Ring mapper persists structurally.</summary>
        public static readonly IReadOnlySet<string> KnownPaths = new HashSet<string>(StringComparer.Ordinal)
        {
            "id",
            "created_at",
            "answered",
            "kind",
            "favorite",
            "snapshot_url",
            "duration",
            "is_e2ee",
            "had_subscription",
            "owner_id",
            "riid",
            "recording.status",
            "doorbot.id",
            "doorbot.description",
            "doorbot.type",
            "properties.is_alexa",
            "properties.is_sidewalk",
            "properties.is_autoreply",
            "properties.stark_reviewed",
            "properties.package_pickup_detected",
            "properties.detection_confidence",
            "device_placement.room",
            "device_placement.area",
            "device_placement.zone_id",
            "geolocation.latitude",
            "geolocation.longitude",
            "geolocation.accuracy",
            "geolocation.timestamp",
            "geolocation.source",
            "last_location.latitude",
            "last_location.longitude",
            "last_location.accuracy",
            "last_location.timestamp",
            "last_location.name",
            "siren.armed",
            "siren.triggered_at",
            "siren.trigger_type",
            "siren.alarm_state",
            "cv_properties.person_detected",
            "cv_properties.stream_broken",
            "cv_properties.detection_type",
            "cv_properties.detection_types[].detection_type",
            "cv_properties.detection_types[].verified_timestamps[]",
            "cv_properties.security_alerts.severity",
            "cv_properties.security_alerts.alerts[]",
            "cv_properties.profiles[].id",
            "cv_properties.profiles[].name",
            "cv_properties.profiles[].confidence",
            "cv_properties.profiles[].thumbnail_url",
            "cv_properties.full_description",
            "cv_properties.short_description",
            "cv_properties.similarity",
            "cv_properties.anomaly",
            "cv_properties.detection_details.confidence",
            "cv_properties.detection_details.model_version",
            "cv_properties.detection_details.zones[].id",
            "cv_properties.detection_details.zones[].name",
            "cv_properties.detection_details.zones[].confidence",
            "cv_properties.tags[]",
            "cv_properties.key_moments",
            DerivedCreatedAtDateTime,
        };

        /// <summary>
        /// Paths in <see cref="KnownPaths"/> that are not Ring API keys and so have no field-map entry: the serialized model
        /// also writes the get-only <c>DoorbotHistoryEvent.CreatedAtDateTime</c>, a parse of <c>created_at</c>.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> NotApiKeys = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [DerivedCreatedAtDateTime] = "computed from created_at by DoorbotHistoryEvent; not sent by Ring",
        };

        private const string DerivedCreatedAtDateTime = "CreatedAtDateTime";

        // Keys that only a raw event carries at the root. An app marker has none of them.
        private static readonly string[] RawEventRootKeys = ["created_at", "doorbot", "cv_properties", "answered", "kind", "recording", "snapshot_url"];

        /// <summary>
        /// Serializes a raw API response exactly as before (default <see cref="JsonSerializer"/> options), hashes that full
        /// JSON (SHA-256, upper-case hex), and returns the hash with the remainder to store. A null response gives
        /// <c>(null, "")</c>.
        /// </summary>
        /// <param name="apiResponse">The typed Ring response, for example a <c>DoorbotHistoryEvent</c>.</param>
        public static (string? RemainderJson, string RawHash) Split(object? apiResponse)
        {
            if (apiResponse == null)
            {
                return (null, string.Empty);
            }

            string json = JsonSerializer.Serialize(apiResponse);
            return (Compute(json), HashOf(json));
        }

        /// <summary>SHA-256 of the UTF-8 bytes of <paramref name="json"/> as upper-case hex: the integrity hash format.</summary>
        public static string HashOf(string json)
        {
            ArgumentNullException.ThrowIfNull(json);
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
        }

        /// <summary>
        /// The part of <paramref name="rawJson"/> that is not in <see cref="KnownPaths"/>, as compact JSON, or null when
        /// nothing is left. Null, blank and JSON null input give null. Text that is not valid JSON is returned unchanged:
        /// it cannot be judged, so nothing is dropped.
        /// </summary>
        public static string? Compute(string? rawJson)
        {
            if (string.IsNullOrWhiteSpace(rawJson))
            {
                return null;
            }

            JsonNode? root;
            try
            {
                root = JsonNode.Parse(rawJson);
            }
            catch (JsonException)
            {
                return rawJson;
            }

            JsonNode? kept = Prune(root, string.Empty);
            return kept?.ToJsonString();
        }

        /// <summary>
        /// True when <paramref name="root"/> is a raw Ring event payload (an object carrying event keys such as
        /// <c>created_at</c> or <c>doorbot</c>), false for app markers such as <c>{"type":"snapshot"}</c> and for anything
        /// that is not an object.
        /// </summary>
        public static bool IsRawEventPayload(JsonElement root)
        {
            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            foreach (string key in RawEventRootKeys)
            {
                if (root.TryGetProperty(key, out _))
                {
                    return true;
                }
            }

            return false;
        }

        private static JsonNode? Prune(JsonNode? node, string path)
        {
            switch (node)
            {
                case JsonObject obj:
                {
                    var kept = new JsonObject();
                    foreach (KeyValuePair<string, JsonNode?> property in obj)
                    {
                        JsonNode? child = Prune(property.Value, path.Length == 0 ? property.Key : path + "." + property.Key);
                        if (child is not null)
                        {
                            kept.Add(property.Key, child);
                        }
                    }

                    return kept.Count == 0 ? null : kept;
                }

                case JsonArray array:
                {
                    var kept = new JsonArray();
                    foreach (JsonNode? item in array)
                    {
                        JsonNode? child = Prune(item, path + "[]");
                        if (child is not null)
                        {
                            kept.Add(child);
                        }
                    }

                    return kept.Count == 0 ? null : kept;
                }

                case null:
                    return null;

                default:
                    return KnownPaths.Contains(path) ? null : node.DeepClone();
            }
        }
    }
}
