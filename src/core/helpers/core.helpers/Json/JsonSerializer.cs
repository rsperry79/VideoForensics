using System;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using VideoForensics.Providers.Common.Helpers.Contracts;

#nullable enable

namespace VideoForensics.Providers.Common.Helpers.Json
{
    /// <summary>Platform-agnostic JSON serialization with multiple modes</summary>
    public class JsonSerializer : IJsonSerializer
    {
        private static readonly JsonSerializerOptions DefaultOptions = new()
        {
            PropertyNameCaseInsensitive = false,
            WriteIndented = false,
            Encoder = JavaScriptEncoder.Default,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        private static readonly JsonSerializerOptions PrettyOptions = new()
        {
            PropertyNameCaseInsensitive = false,
            WriteIndented = true,
            Encoder = JavaScriptEncoder.Default,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        private static readonly JsonSerializerOptions RawOptions = new()
        {
            PropertyNameCaseInsensitive = false,
            WriteIndented = false,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        public string Serialize<T>(T obj, JsonSerializationMode mode = JsonSerializationMode.Default)
        {
            var options = mode switch
            {
                JsonSerializationMode.Pretty => PrettyOptions,
                JsonSerializationMode.Raw => RawOptions,
                _ => DefaultOptions
            };

            return System.Text.Json.JsonSerializer.Serialize(obj, options);
        }

        public T? Deserialize<T>(string json, JsonSerializationMode mode = JsonSerializationMode.Default)
        {
            try
            {
                var options = mode switch
                {
                    JsonSerializationMode.Pretty => PrettyOptions,
                    JsonSerializationMode.Raw => RawOptions,
                    _ => DefaultOptions
                };

                return System.Text.Json.JsonSerializer.Deserialize<T>(json, options);
            }
            catch
            {
                return default;
            }
        }

        public T? Deserialize<T>(ReadOnlySpan<byte> utf8Json, JsonSerializationMode mode = JsonSerializationMode.Default)
        {
            try
            {
                var options = mode switch
                {
                    JsonSerializationMode.Pretty => PrettyOptions,
                    JsonSerializationMode.Raw => RawOptions,
                    _ => DefaultOptions
                };

                return System.Text.Json.JsonSerializer.Deserialize<T>(utf8Json, options);
            }
            catch
            {
                return default;
            }
        }
    }
}
