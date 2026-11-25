using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>Maps the live-view trigger reason between the data layer and its DTO, by member name.</summary>
    public static class LiveViewTriggerReasonDtoMapping
    {
        /// <summary>Converts a data-layer LiveViewTriggerReason to its DTO enum.</summary>
        public static LiveViewTriggerReasonDto ToDto(this LiveViewTriggerReason value) =>
            Enum.Parse<LiveViewTriggerReasonDto>(value.ToString());

        /// <summary>Converts a LiveViewTriggerReasonDto to the data-layer enum.</summary>
        public static LiveViewTriggerReason ToDomain(this LiveViewTriggerReasonDto value) =>
            Enum.Parse<LiveViewTriggerReason>(value.ToString());
    }
}
