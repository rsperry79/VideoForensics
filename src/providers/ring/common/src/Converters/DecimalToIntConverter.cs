using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VideoForensics.Providers.Ring.Converters
{
    /// <summary>
    /// Reads a Ring value that should be a whole number but may arrive as a decimal (for example a history event's
    /// duration of 40.0) or as a numeric string. The value is rounded half away from zero to the nearest int. Null,
    /// non-numeric and out-of-range values read as null so one odd value does not fail the whole history page.
    /// </summary>
    /// <remarks>
    /// Stored columns stay int: the converter normalises on read, so no schema change is needed.
    /// </remarks>
    public class DecimalToIntConverter : JsonConverter<int?>
    {
        public override int? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.Number:
                    return reader.TryGetInt32(out int whole) ? whole : ToInt(reader.GetDouble());
                case JsonTokenType.String:
                    string? text = reader.GetString();
                    return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
                        ? ToInt(parsed)
                        : null;
                case JsonTokenType.Null:
                    return null;
                default:
                    reader.Skip();
                    return null;
            }
        }

        public override void Write(Utf8JsonWriter writer, int? value, JsonSerializerOptions options)
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

        private static int? ToInt(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return null;
            }

            double rounded = Math.Round(value, MidpointRounding.AwayFromZero);
            return rounded is >= int.MinValue and <= int.MaxValue ? (int)rounded : null;
        }
    }
}
