using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VideoForensics.Providers.Ring.Converters
{
    /// <summary>
    /// Reads a Ring cv.tags array. Ring sends each tag as an object with a "tag" property; plain strings are
    /// accepted too. Entries of any other shape are skipped, so one odd tag does not fail the whole event.
    /// </summary>
    public class CvTagListConverter : JsonConverter<List<string>?>
    {
        public override List<string>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartArray)
            {
                reader.Skip();
                return null;
            }

            var tags = new List<string>();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.String:
                        tags.Add(reader.GetString()!);
                        break;
                    case JsonTokenType.StartObject:
                        string? tag = ReadTagProperty(ref reader);
                        if (tag is not null)
                        {
                            tags.Add(tag);
                        }

                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            return tags;
        }

        public override void Write(Utf8JsonWriter writer, List<string>? value, JsonSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNullValue();
                return;
            }

            writer.WriteStartArray();
            foreach (string tag in value)
            {
                writer.WriteStringValue(tag);
            }

            writer.WriteEndArray();
        }

        // Reads one tag object and returns its "tag" string; the reader ends on the object's EndObject.
        private static string? ReadTagProperty(ref Utf8JsonReader reader)
        {
            string? tag = null;
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    bool isTag = reader.ValueTextEquals("tag");
                    reader.Read();
                    if (isTag && reader.TokenType == JsonTokenType.String)
                    {
                        tag = reader.GetString();
                    }
                    else
                    {
                        reader.Skip();
                    }
                }
            }

            return tag;
        }
    }
}
