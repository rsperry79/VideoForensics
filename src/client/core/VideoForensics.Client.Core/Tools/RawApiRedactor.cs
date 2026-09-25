using System.Text.Json;
using System.Text.RegularExpressions;

namespace VideoForensics.Client.Core.Tools
{
    /// <summary>
    /// Utility for redacting sensitive information from raw API calls for safe display.
    /// </summary>
    public static class RawApiRedactor
    {
        private static readonly string[] SensitiveUrlParams = new[]
        {
            "access_token", "refresh_token", "token", "auth", "auth_token",
            "api_key", "apikey", "password", "client_secret", "code"
        };

        private static readonly string[] SensitiveJsonFields = new[]
        {
            "access_token", "refresh_token", "token", "auth", "auth_token",
            "api_key", "apikey", "password", "client_secret", "code",
            "authorization", "hardware_id", "session_token"
        };

        /// <summary>
        /// Redacts sensitive query parameters from a URL.
        /// Replaces the values of parameters named (case-insensitively) with known sensitive names with "REDACTED".
        /// Tolerates malformed URLs and returns them as-is.
        /// </summary>
        /// <param name="url">The URL to redact.</param>
        /// <returns>The URL with sensitive query parameter values replaced with "REDACTED".</returns>
        public static string RedactUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return url;
            }

            try
            {
                int queryIndex = url.IndexOf('?');
                if (queryIndex < 0)
                {
                    return url;
                }

                string baseUrl = url[..queryIndex];
                string queryString = url[(queryIndex + 1)..];

                var redactedParams = new List<string>();
                foreach (string param in queryString.Split('&'))
                {
                    int eqIndex = param.IndexOf('=');
                    if (eqIndex < 0)
                    {
                        redactedParams.Add(param);
                        continue;
                    }

                    string paramName = param[..eqIndex];
                    string paramValue = param[(eqIndex + 1)..];

                    if (IsSensitiveParamName(paramName))
                    {
                        redactedParams.Add($"{paramName}=REDACTED");
                    }
                    else
                    {
                        redactedParams.Add(param);
                    }
                }

                return $"{baseUrl}?{string.Join("&", redactedParams)}";
            }
            catch
            {
                // Return as-is on any error
                return url;
            }
        }

        /// <summary>
        /// Redacts sensitive fields from a JSON string.
        /// If the body is valid JSON, recursively replaces values of properties with sensitive names with "REDACTED"
        /// and returns re-serialized JSON. If not valid JSON, returns the body unchanged.
        /// </summary>
        /// <param name="body">The JSON body to redact, or null.</param>
        /// <returns>The redacted body (or empty string if body was null).</returns>
        public static string RedactBody(string? body)
        {
            if (string.IsNullOrEmpty(body))
            {
                return "";
            }

            try
            {
                using (JsonDocument doc = JsonDocument.Parse(body))
                {
                    JsonElement root = doc.RootElement;
                    JsonElement redacted = RedactJsonElement(root);
                    return JsonSerializer.Serialize(redacted, new JsonSerializerOptions { WriteIndented = false });
                }
            }
            catch
            {
                // Not valid JSON, return as-is
                return body;
            }
        }

        private static JsonElement RedactJsonElement(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Object => RedactJsonObject(element),
                JsonValueKind.Array => RedactJsonArray(element),
                _ => element
            };
        }

        private static JsonElement RedactJsonObject(JsonElement obj)
        {
            var options = new JsonSerializerOptions { WriteIndented = false };
            using (var stream = new MemoryStream())
            {
                using (var writer = new Utf8JsonWriter(stream))
                {
                    writer.WriteStartObject();
                    foreach (JsonProperty property in obj.EnumerateObject())
                    {
                        writer.WritePropertyName(property.Name);

                        if (IsSensitiveFieldName(property.Name))
                        {
                            writer.WriteStringValue("REDACTED");
                        }
                        else if (property.Value.ValueKind == JsonValueKind.Object)
                        {
                            JsonElement redactedNested = RedactJsonElement(property.Value);
                            WriteJsonElement(writer, redactedNested);
                        }
                        else if (property.Value.ValueKind == JsonValueKind.Array)
                        {
                            JsonElement redactedArray = RedactJsonElement(property.Value);
                            WriteJsonElement(writer, redactedArray);
                        }
                        else
                        {
                            WriteJsonElement(writer, property.Value);
                        }
                    }
                    writer.WriteEndObject();
                }
                stream.Position = 0;
                using (JsonDocument redactedDoc = JsonDocument.Parse(stream))
                {
                    return redactedDoc.RootElement.Clone();
                }
            }
        }

        private static JsonElement RedactJsonArray(JsonElement arr)
        {
            var options = new JsonSerializerOptions { WriteIndented = false };
            using (var stream = new MemoryStream())
            {
                using (var writer = new Utf8JsonWriter(stream))
                {
                    writer.WriteStartArray();
                    foreach (JsonElement item in arr.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.Object)
                        {
                            JsonElement redactedItem = RedactJsonElement(item);
                            WriteJsonElement(writer, redactedItem);
                        }
                        else if (item.ValueKind == JsonValueKind.Array)
                        {
                            JsonElement redactedItem = RedactJsonElement(item);
                            WriteJsonElement(writer, redactedItem);
                        }
                        else
                        {
                            WriteJsonElement(writer, item);
                        }
                    }
                    writer.WriteEndArray();
                }
                stream.Position = 0;
                using (JsonDocument redactedDoc = JsonDocument.Parse(stream))
                {
                    return redactedDoc.RootElement.Clone();
                }
            }
        }

        private static void WriteJsonElement(Utf8JsonWriter writer, JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Null:
                    writer.WriteNullValue();
                    break;
                case JsonValueKind.True:
                    writer.WriteBooleanValue(true);
                    break;
                case JsonValueKind.False:
                    writer.WriteBooleanValue(false);
                    break;
                case JsonValueKind.Number:
                    if (element.TryGetInt64(out long lval))
                    {
                        writer.WriteNumberValue(lval);
                    }
                    else if (element.TryGetDouble(out double dval))
                    {
                        writer.WriteNumberValue(dval);
                    }
                    break;
                case JsonValueKind.String:
                    writer.WriteStringValue(element.GetString());
                    break;
                case JsonValueKind.Array:
                    {
                        writer.WriteStartArray();
                        foreach (JsonElement item in element.EnumerateArray())
                        {
                            WriteJsonElement(writer, item);
                        }
                        writer.WriteEndArray();
                        break;
                    }
                case JsonValueKind.Object:
                    {
                        writer.WriteStartObject();
                        foreach (JsonProperty prop in element.EnumerateObject())
                        {
                            writer.WritePropertyName(prop.Name);
                            WriteJsonElement(writer, prop.Value);
                        }
                        writer.WriteEndObject();
                        break;
                    }
            }
        }

        private static bool IsSensitiveParamName(string paramName)
        {
            return SensitiveUrlParams.Any(s => string.Equals(s, paramName, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsSensitiveFieldName(string fieldName)
        {
            return SensitiveJsonFields.Any(s => string.Equals(s, fieldName, StringComparison.OrdinalIgnoreCase));
        }
    }
}
