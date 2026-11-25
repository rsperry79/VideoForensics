using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>Maps the jamming confidence level between the data layer and its DTO, by member name.</summary>
    public static class JammingConfidenceDtoMapping
    {
        /// <summary>Converts a data-layer JammingConfidenceLevel to its DTO enum.</summary>
        public static JammingConfidenceLevelDto ToDto(this JammingConfidenceLevel value) =>
            Enum.Parse<JammingConfidenceLevelDto>(value.ToString());

        /// <summary>Converts a JammingConfidenceLevelDto to the data-layer enum.</summary>
        public static JammingConfidenceLevel ToDomain(this JammingConfidenceLevelDto value) =>
            Enum.Parse<JammingConfidenceLevel>(value.ToString());
    }
}
