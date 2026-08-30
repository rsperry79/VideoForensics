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
            switch (reader.TokenType)
            {
                case JsonTokenType.String:
                    return reader.GetString();
                case JsonTokenType.Number:
                    return reader.GetDouble().ToString();
                case JsonTokenType.True:
                    return "true";
                case JsonTokenType.False:
                    return "false";
                case JsonTokenType.Null:
                    return null;
                default:
                    // Some devices report object/array shapes for this field (e.g. Ring's
                    // stickup_cams led_status coming back as {"seconds_remaining":0} instead of a
                    // string). Skip() consumes the whole value so the reader ends up positioned
                    // correctly for whatever comes next - without it, System.Text.Json throws
                    // "converter read too much or not enough" and the whole containing object
                    // fails to deserialize.
                    reader.Skip();
                    return null;
            }
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
