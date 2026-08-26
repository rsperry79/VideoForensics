using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using VideoForensics.Providers.Common.Helpers.Contracts;

#nullable enable

namespace VideoForensics.Providers.Common.Helpers.Json.Converters
{
    /// <summary>
    /// Converts JSON string or number values to nullable double.
    /// Some APIs return numeric values as strings (e.g., "-45.5" for signal strength).
    /// Returns null for unparseable string values.
    /// </summary>
    public class FlexibleDoubleConverter : JsonConverter<double?>, IFlexibleJsonConverter
    {
        public override double? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return reader.TokenType switch
            {
                JsonTokenType.Number => reader.GetDouble(),
                JsonTokenType.String => double.TryParse(reader.GetString(), out var result) ? result : null,
                JsonTokenType.Null => null,
                _ => null
            };
        }

        public override void Write(Utf8JsonWriter writer, double? value, JsonSerializerOptions options)
        {
            if (value.HasValue)
                writer.WriteNumberValue(value.Value);
            else
                writer.WriteNullValue();
        }

        public override bool CanConvert(Type typeToConvert) =>
            typeToConvert == typeof(double?) || typeToConvert == typeof(double);
    }
}
