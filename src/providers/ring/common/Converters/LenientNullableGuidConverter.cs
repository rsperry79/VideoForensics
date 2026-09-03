using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VideoForensics.Providers.Ring.Converters
{
    /// <summary>
    /// Ring's location/device APIs occasionally return a location_id that isn't a well-formed GUID
    /// at all - observed on what looks like a shared/guest location, where Ring hands back an
    /// opaque id like "c22vpq-55qqu-0" instead of a UUID, on both the /locations entry and every
    /// device that belongs to it. The default Guid? converter throws JsonException in that case,
    /// aborting device discovery entirely.
    ///
    /// Rather than discarding that value as null (which drops the location and orphans its devices
    /// - they'd have nothing to group under and no way to be told apart from devices that genuinely
    /// have no location), this deterministically hashes the raw string into a stable pseudo-Guid.
    /// It's not Ring's real id and must never be sent back to Ring's API, but hashing the same raw
    /// string always produces the same pseudo-Guid within this process, so the /locations entry and
    /// its devices' own location_id fields still correlate correctly with each other.
    /// </summary>
    public class LenientNullableGuidConverter : JsonConverter<Guid?>
    {
        public override Guid? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                var value = reader.GetString();
                if (string.IsNullOrEmpty(value))
                {
                    return null;
                }

                if (Guid.TryParse(value, out var guid))
                {
                    return guid;
                }

                return DerivePseudoGuid(value);
            }

            return null;
        }

        private static Guid DerivePseudoGuid(string value)
        {
            var hash = MD5.HashData(Encoding.UTF8.GetBytes(value));
            return new Guid(hash);
        }

        public override void Write(Utf8JsonWriter writer, Guid? value, JsonSerializerOptions options)
        {
            if (value.HasValue)
            {
                writer.WriteStringValue(value.Value);
            }
            else
            {
                writer.WriteNullValue();
            }
        }
    }
}
