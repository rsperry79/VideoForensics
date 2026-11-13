using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>Extension methods for mapping live-view session entities to wire DTOs.</summary>
    public static class LiveViewDtoMapping
    {
        /// <summary>Converts a LiveViewSession entity to a LiveViewSessionDto, sending enums as their names.</summary>
        public static LiveViewSessionDto ToDto(this LiveViewSession session)
        {
            return new LiveViewSessionDto(
                Id: session.Id,
                DeviceId: session.DeviceId,
                TriggerReason: session.TriggerReason.ToString(),
                State: session.State.ToString(),
                StartedAtUtc: session.StartedAtUtc,
                EndedAtUtc: session.EndedAtUtc,
                LastExtendedAtUtc: session.LastExtendedAtUtc,
                IsSustained: session.IsSustained,
                SustainedSinceUtc: session.SustainedSinceUtc,
                PromotionReason: session.PromotionReason,
                OperatorId: session.OperatorId,
                StopReason: session.StopReason,
                ProviderSessionRef: session.ProviderSessionRef
            );
        }

        /// <summary>Converts a LiveViewTelemetrySample entity to a LiveViewTelemetrySampleDto.</summary>
        public static LiveViewTelemetrySampleDto ToDto(this LiveViewTelemetrySample sample)
        {
            return new LiveViewTelemetrySampleDto(
                Id: sample.Id,
                SessionId: sample.SessionId,
                CapturedAtUtc: sample.CapturedAtUtc,
                FractionLost: sample.FractionLost,
                CumulativePacketsLost: sample.CumulativePacketsLost,
                JitterTicks: sample.JitterTicks,
                BitrateBps: sample.BitrateBps,
                InterferenceScore: sample.InterferenceScore
            );
        }
    }
}
