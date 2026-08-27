using System;

#nullable enable

namespace VideoForensics.Providers.Common.Helpers.Contracts
{
    /// <summary>Modes for JSON serialization and deserialization</summary>
    public enum JsonSerializationMode
    {
        /// Compact JSON with safe escaping (default)
        Default,
        /// Indented JSON for readability
        Pretty,
        /// Unsafe escaping for API responses
        Raw
    }

    /// <summary>Provides platform-agnostic JSON serialization with multiple modes</summary>
    public interface IJsonSerializer
    {
        /// <summary>Serializes an object to JSON</summary>
        /// <param name="obj">Object to serialize</param>
        /// <param name="mode">Serialization mode</param>
        /// <returns>JSON string</returns>
        string Serialize<T>(T obj, JsonSerializationMode mode = JsonSerializationMode.Default);

        /// <summary>Deserializes JSON string to an object</summary>
        /// <param name="json">JSON string</param>
        /// <param name="mode">Deserialization mode</param>
        /// <returns>Deserialized object or null</returns>
        T? Deserialize<T>(string json, JsonSerializationMode mode = JsonSerializationMode.Default);

        /// <summary>Deserializes UTF-8 JSON bytes to an object</summary>
        /// <param name="utf8Json">UTF-8 encoded JSON bytes</param>
        /// <param name="mode">Deserialization mode</param>
        /// <returns>Deserialized object or null</returns>
        T? Deserialize<T>(ReadOnlySpan<byte> utf8Json, JsonSerializationMode mode = JsonSerializationMode.Default);
    }
}
