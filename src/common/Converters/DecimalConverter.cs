using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ring.Api.Converters;

/// <summary>
/// Converts JSON string or number values to decimal.
/// Ring API sometimes returns numeric values as strings (e.g., "4.1" for battery_voltage).
/// </summary>
public class DecimalConverter : JsonConverter<decimal?>
{
    public override decimal? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Number:
                return reader.GetDecimal();
            case JsonTokenType.String:
            {
                var stringValue = reader.GetString();
                if (decimal.TryParse(stringValue, out var result))
                    return result;
                return null;
            }
            case JsonTokenType.Null:
                return null;
            default:
                return null;
        }
    }

    public override void Write(Utf8JsonWriter writer, decimal? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
            writer.WriteNumberValue(value.Value);
        else
            writer.WriteNullValue();
    }

    public override bool CanConvert(Type typeToConvert) => true;
}
