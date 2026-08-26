using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using VideoForensics.Providers.Common.Helpers.Contracts;

#nullable enable

namespace VideoForensics.Providers.Common.Helpers.Json.Converters
{
    /// <summary>
    /// Converts JSON string or number values to nullable decimal.
    /// Some APIs return numeric values as strings (e.g., "4.1" for voltage measurements).
    /// Returns null for unparseable string values.
    /// </summary>
    public class FlexibleDecimalConverter : JsonConverter<decimal?>, IFlexibleJsonConverter
    {
        public override decimal? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return reader.TokenType switch
            {
                JsonTokenType.Number => reader.GetDecimal(),
                JsonTokenType.String => decimal.TryParse(reader.GetString(), out var result) ? result : null,
                JsonTokenType.Null => null,
                _ => null
            };
        }

        public override void Write(Utf8JsonWriter writer, decimal? value, JsonSerializerOptions options)
        {
            if (value.HasValue)
                writer.WriteNumberValue(value.Value);
            else
                writer.WriteNullValue();
        }

        public override bool CanConvert(Type typeToConvert) =>
            typeToConvert == typeof(decimal?) || typeToConvert == typeof(decimal);
    }
}
