using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VideoForensics.Providers.Ring.Converters
{
    /// <summary>
    /// Reads a Ring zone state as a number. Motion zones in zone_settings send "enabled" or "disabled",
    /// which map to 1 and 0 (the convention the zone tests already use); advanced zones send numbers, which
    /// pass through. Any other value reads as null so one odd state does not fail the whole device.
    /// </summary>
    public class ZoneStateConverter : JsonConverter<long?>
    {
        public override long? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.Number:
                    return reader.TryGetInt64(out long number) ? number : null;
                case JsonTokenType.String:
                    string? text = reader.GetString();
                    if (string.Equals(text, "enabled", StringComparison.OrdinalIgnoreCase))
                    {
                        return 1;
                    }

                    if (string.Equals(text, "disabled", StringComparison.OrdinalIgnoreCase))
                    {
                        return 0;
                    }

                    return long.TryParse(text, out long parsed) ? parsed : null;
                case JsonTokenType.Null:
                    return null;
                default:
                    reader.Skip();
                    return null;
            }
        }

        public override void Write(Utf8JsonWriter writer, long? value, JsonSerializerOptions options)
        {
            if (value.HasValue)
            {
                writer.WriteNumberValue(value.Value);
            }
            else
            {
                writer.WriteNullValue();
            }
        }
    }
}
