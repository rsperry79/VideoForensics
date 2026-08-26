using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using VideoForensics.Providers.Common.Helpers.Contracts;

#nullable enable

namespace VideoForensics.Providers.Common.Helpers.Json.Converters
{
    /// <summary>
    /// Converts JSON string, number, or boolean values to a string.
    /// Some APIs return inconsistent types for the same field (e.g., LedStatus can be "on", 1, or true).
    /// </summary>
    public class FlexibleStringConverter : JsonConverter<string>, IFlexibleJsonConverter
    {
        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return reader.TokenType switch
            {
                JsonTokenType.String => reader.GetString(),
                JsonTokenType.Number => reader.GetDouble().ToString(),
                JsonTokenType.True => "true",
                JsonTokenType.False => "false",
                JsonTokenType.Null => null,
                _ => null
            };
        }

        public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
        {
            if (value == null)
                writer.WriteNullValue();
            else
                writer.WriteStringValue(value);
        }
    }
}
