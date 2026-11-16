using VideoForensics.Data.Common.Contracts;

namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>Maps bitrate data-layer types to their DTOs.</summary>
    public static class MediaBitrateDtoMapping
    {
        /// <summary>Converts a MeasuredBitrate to its DTO.</summary>
        public static MeasuredBitrateDto ToDto(this MeasuredBitrate value) =>
            new(value.RecordedAtUtc, value.VideoBitrateBps, value.LightingMode, value.RedFlags, value.MediaItemId);

        /// <summary>Converts a MeasuredBitrateDto to the data-layer MeasuredBitrate.</summary>
        public static MeasuredBitrate ToDomain(this MeasuredBitrateDto dto) =>
            new(dto.RecordedAtUtc, dto.VideoBitrateBps, dto.LightingMode, dto.RedFlags, dto.MediaItemId);

        /// <summary>Converts an AllTimeBitrateBaseline to its DTO.</summary>
        public static AllTimeBitrateBaselineDto ToDto(this AllTimeBitrateBaseline value) =>
            new(value.Count, value.MeanBps, value.StdDevBps);

        /// <summary>Converts an AllTimeBitrateBaselineDto to the data-layer AllTimeBitrateBaseline.</summary>
        public static AllTimeBitrateBaseline ToDomain(this AllTimeBitrateBaselineDto dto) =>
            new(dto.Count, dto.MeanBps, dto.StdDevBps);
    }
}
