using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VideoForensics.Providers.Ring.Converters
{
    /// <summary>
    /// Reads a nullable double from a Ring payload that is not always a number. Ring's cv.similarity has arrived as a
    /// numeric string, an empty string, or another non-number. The default converter throws JsonException on those,
    /// which aborts the whole location history sync. A number or numeric string is kept. Anything else reads as null,
    /// so the event still syncs and only this score is missing.
    /// </summary>
    public class LenientNullableDoubleConverter : JsonConverter<double?>
    {
        public override double? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.Number:
                    return reader.TryGetDouble(out double number) ? number : null;
                case JsonTokenType.String:
                    string? text = reader.GetString();
                    return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) ? parsed : null;
                default:
                    // Why: skip the whole value (an object or array) so the reader is left at the next property.
                    reader.Skip();
                    return null;
            }
        }

        public override void Write(Utf8JsonWriter writer, double? value, JsonSerializerOptions options)
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
