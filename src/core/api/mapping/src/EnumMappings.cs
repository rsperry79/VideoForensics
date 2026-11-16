namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>Maps data-layer enums used in DTO signatures to their DTO enums, by member name.</summary>
    public static class EnumMappings
    {
        /// <summary>Converts an OperatorRole to its DTO enum.</summary>
        public static OperatorRoleDto ToDto(this VideoForensics.Data.Common.Entities.OperatorRole value) =>
            Enum.Parse<OperatorRoleDto>(value.ToString());

        /// <summary>Converts an OperatorRoleDto to the data-layer OperatorRole.</summary>
        public static VideoForensics.Data.Common.Entities.OperatorRole ToDomain(this OperatorRoleDto value) =>
            Enum.Parse<VideoForensics.Data.Common.Entities.OperatorRole>(value.ToString());

        /// <summary>Converts a TwoFactorRequirementOverride to its DTO enum.</summary>
        public static TwoFactorRequirementOverrideDto ToDto(this VideoForensics.Data.Common.Entities.TwoFactorRequirementOverride value) =>
            Enum.Parse<TwoFactorRequirementOverrideDto>(value.ToString());

        /// <summary>Converts a TwoFactorRequirementOverrideDto to the data-layer enum.</summary>
        public static VideoForensics.Data.Common.Entities.TwoFactorRequirementOverride ToDomain(this TwoFactorRequirementOverrideDto value) =>
            Enum.Parse<VideoForensics.Data.Common.Entities.TwoFactorRequirementOverride>(value.ToString());

        /// <summary>Converts a DiscrepancyType to its DTO enum.</summary>
        public static DiscrepancyTypeDto ToDto(this VideoForensics.Data.Common.Entities.DiscrepancyType value) =>
            Enum.Parse<DiscrepancyTypeDto>(value.ToString());

        /// <summary>Converts a DiscrepancyTypeDto to the data-layer DiscrepancyType.</summary>
        public static VideoForensics.Data.Common.Entities.DiscrepancyType ToDomain(this DiscrepancyTypeDto value) =>
            Enum.Parse<VideoForensics.Data.Common.Entities.DiscrepancyType>(value.ToString());
    }
}
