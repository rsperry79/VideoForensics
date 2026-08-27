#nullable enable

namespace VideoForensics.Providers.Common.Helpers.Contracts
{
    /// <summary>
    /// Marker interface for flexible JSON converters that handle type inconsistencies in API responses.
    /// Some APIs return inconsistent types for the same field (e.g., a field could be "true", 1, or "1").
    /// These converters normalize such variations.
    /// </summary>
    public interface IFlexibleJsonConverter
    {
        // Marker interface - implementations are System.Text.Json.Serialization.JsonConverter<T>
    }
}
