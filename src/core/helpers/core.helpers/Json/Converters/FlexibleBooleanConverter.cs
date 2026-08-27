using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using VideoForensics.Providers.Common.Helpers.Contracts;

#nullable enable

namespace VideoForensics.Providers.Common.Helpers.Json.Converters
{
    /// <summary>
    /// Converts JSON to a boolean value. Accepts 0, 1, true, false (case-insensitive strings).
    /// Handles APIs that return boolean values as integers or strings inconsistently.
    /// </summary>
    public class FlexibleBooleanConverter : JsonConverter<bool>, IFlexibleJsonConverter
    {
        public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return reader.TokenType switch
            {
                JsonTokenType.True => true,
                JsonTokenType.False => false,
                JsonTokenType.Number => reader.GetInt32() == 1,
                JsonTokenType.String => (reader.GetString() ?? "").ToLowerInvariant() switch
                {
                    "true" => true,
                    "1" => true,
                    "false" => false,
                    "0" => false,
                    _ => throw new JsonException($"Invalid boolean value: {reader.GetString()}")
                },
                _ => throw new JsonException($"Invalid token type for boolean: {reader.TokenType}")
            };
        }

        public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options)
        {
            writer.WriteBooleanValue(value);
        }
    }
}
