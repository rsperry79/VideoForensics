using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>Maps the network tier between the data layer and its DTO, by member name.</summary>
    public static class NetworkTierDtoMapping
    {
        /// <summary>Converts a data-layer NetworkTier to its DTO enum.</summary>
        public static NetworkTierDto ToDto(this NetworkTier value) =>
            Enum.Parse<NetworkTierDto>(value.ToString());

        /// <summary>Converts a NetworkTierDto to the data-layer enum.</summary>
        public static NetworkTier ToDomain(this NetworkTierDto value) =>
            Enum.Parse<NetworkTier>(value.ToString());
    }
}
