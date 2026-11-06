using System.Globalization;
using System.Text.Json;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Providers.Ring.Entities;

namespace VideoForensics.Providers.Ring.Services
{
    /// <summary>
    /// Moves the Ring location fields through the provider-agnostic location metadata (text values), then onto
    /// LocationMetadata (generic fields) and RingLocationDetails (Ring-only fields). A value whose JSON type does not fit
    /// is left out of the metadata, so nothing is stored for it.
    /// </summary>
    public static class RingLocationDetailsMapper
    {
        private static readonly string[] EntitlementKeys =
        [
            "business_command_center", "guard_patrol", "private_guard_response", "vm_video_verification", "vm_virtual_security_guard",
        ];

        /// <summary>Adds the Ring location values to the metadata dictionary as text. Missing, null or unusable values are omitted.</summary>
        public static void AddLocationValues(Dictionary<string, string> metadata, Entities.Location location)
        {
            ArgumentNullException.ThrowIfNull(metadata);
            ArgumentNullException.ThrowIfNull(location);

            AddText(metadata, "address2", location.Address?.Address2);
            AddText(metadata, "country", location.Address?.Country);
            AddText(metadata, "cross_street", location.Address?.CrossStreet);
            AddText(metadata, "created_at", location.CreatedAt);
            AddText(metadata, "updated_at", location.UpdatedAt);
            AddLong(metadata, "owner_id", location.OwnerId);
            AddBool(metadata, "user_verified", location.UserVerified);
            AddBool(metadata, "is_jobsite", location.IsJobsite);
            AddText(metadata, "location_resource_id", location.LocationResourceId);
            AddText(metadata, "location_type", location.LocationType);
            AddText(metadata, "geo_service_verified", location.GeoServiceVerified);

            if (AsObject(location.GeoCoordinates) is { } geo)
            {
                AddDouble(metadata, "latitude", Property(geo, "latitude"));
                AddDouble(metadata, "longitude", Property(geo, "longitude"));
            }

            if (AsObject(location.Entitlements) is { } entitlements)
            {
                foreach (string key in EntitlementKeys)
                {
                    AddBool(metadata, key, Property(entitlements, key));
                }
            }
        }

        /// <summary>Builds the Ring-only row for a location from the metadata text. Unparseable values are null.</summary>
        public static RingLocationDetails Map(Guid locationId, IReadOnlyDictionary<string, string> metadata)
        {
            ArgumentNullException.ThrowIfNull(metadata);

            return new RingLocationDetails
            {
                Id = Guid.NewGuid(),
                LocationId = locationId,
                CreatedAtUtc = ParseUtc(Value(metadata, "created_at")),
                UpdatedAtUtc = ParseUtc(Value(metadata, "updated_at")),
                OwnerId = ParseLong(Value(metadata, "owner_id")),
                BusinessCommandCenter = ParseBool(Value(metadata, "business_command_center")),
                GuardPatrol = ParseBool(Value(metadata, "guard_patrol")),
                PrivateGuardResponse = ParseBool(Value(metadata, "private_guard_response")),
                VmVideoVerification = ParseBool(Value(metadata, "vm_video_verification")),
                VmVirtualSecurityGuard = ParseBool(Value(metadata, "vm_virtual_security_guard")),
                UserVerified = ParseBool(Value(metadata, "user_verified")),
                IsJobsite = ParseBool(Value(metadata, "is_jobsite")),
                GeoServiceVerified = Value(metadata, "geo_service_verified"),
                LocationType = Value(metadata, "location_type"),
                LocationResourceId = Value(metadata, "location_resource_id"),
            };
        }

        /// <summary>Sets the generic LocationMetadata fields that Ring sends beyond the address and timezone.</summary>
        public static void ApplyGenericFields(LocationMetadata target, IReadOnlyDictionary<string, string> metadata)
        {
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(metadata);

            target.StreetAddress2 = Value(metadata, "address2");
            target.CrossStreet = Value(metadata, "cross_street");
            target.Country = Value(metadata, "country");
            target.Latitude = ParseDouble(Value(metadata, "latitude"));
            target.Longitude = ParseDouble(Value(metadata, "longitude"));
        }

        private static string? Value(IReadOnlyDictionary<string, string> metadata, string key)
        {
            return metadata.TryGetValue(key, out string? value) ? value : null;
        }

        private static JsonElement? AsObject(JsonElement? value)
        {
            return value is { ValueKind: JsonValueKind.Object } obj ? obj : null;
        }

        private static JsonElement? Property(JsonElement obj, string name)
        {
            return obj.TryGetProperty(name, out JsonElement value) ? value : null;
        }

        private static void AddText(Dictionary<string, string> metadata, string key, JsonElement? value)
        {
            if (value is { } v && RingJsonValue.TryText(v, out string? text) && !string.IsNullOrWhiteSpace(text))
            {
                metadata[key] = text;
            }
        }

        private static void AddBool(Dictionary<string, string> metadata, string key, JsonElement? value)
        {
            if (value is { } v && RingJsonValue.TryBool(v, out bool? flag) && flag is bool b)
            {
                metadata[key] = b ? "true" : "false";
            }
        }

        private static void AddLong(Dictionary<string, string> metadata, string key, JsonElement? value)
        {
            if (value is { } v && RingJsonValue.TryLong(v, out long? number) && number is long n)
            {
                metadata[key] = n.ToString(CultureInfo.InvariantCulture);
            }
        }

        private static void AddDouble(Dictionary<string, string> metadata, string key, JsonElement? value)
        {
            if (value is { } v && RingJsonValue.TryDouble(v, out double? number) && number is double d)
            {
                metadata[key] = d.ToString("R", CultureInfo.InvariantCulture);
            }
        }

        private static DateTime? ParseUtc(string? text)
        {
            return text != null && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset when)
                ? when.UtcDateTime
                : null;
        }

        private static long? ParseLong(string? text)
        {
            return text != null && long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long n) ? n : null;
        }

        private static double? ParseDouble(string? text)
        {
            return text != null && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) ? d : null;
        }

        private static bool? ParseBool(string? text)
        {
            return text != null && bool.TryParse(text, out bool b) ? b : null;
        }
    }
}
