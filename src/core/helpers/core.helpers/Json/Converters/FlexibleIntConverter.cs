using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using VideoForensics.Providers.Common.Helpers.Contracts;

#nullable enable

namespace VideoForensics.Providers.Common.Helpers.Json.Converters
{
    /// <summary>
    /// Converts JSON string or number values to nullable integer.
    /// Some APIs return integer values as strings (e.g., "42" for battery_level).
    /// Returns null for unparseable string values.
    /// </summary>
    public class FlexibleIntConverter : JsonConverter<int?>, IFlexibleJsonConverter
    {
        public override int? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return reader.TokenType switch
            {
                JsonTokenType.Number => reader.GetInt32(),
                JsonTokenType.String => int.TryParse(reader.GetString(), out var result) ? result : null,
                JsonTokenType.Null => null,
                _ => null
            };
        }

        public override void Write(Utf8JsonWriter writer, int? value, JsonSerializerOptions options)
        {
            if (value.HasValue)
                writer.WriteNumberValue(value.Value);
            else
                writer.WriteNullValue();
        }

        public override bool CanConvert(Type typeToConvert) =>
            typeToConvert == typeof(int?) || typeToConvert == typeof(int);
    }
}
