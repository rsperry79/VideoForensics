namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>One stream-reported video bitrate sample for a media item.</summary>
    /// <param name="RecordedAtUtc">When the clip was recorded, in UTC.</param>
    /// <param name="VideoBitrateBps">Measured video bitrate, in bits per second.</param>
    /// <param name="LightingMode">Lighting mode recorded with the clip, if any.</param>
    /// <param name="RedFlags">Stored red-flag summary for the clip, if any.</param>
    /// <param name="MediaItemId">The media item the sample belongs to.</param>
    public sealed record MeasuredBitrateDto(
        DateTime RecordedAtUtc,
        long VideoBitrateBps,
        string? LightingMode,
        string? RedFlags,
        Guid MediaItemId = default);

    /// <summary>A device's all-time video bitrate baseline.</summary>
    /// <param name="Count">Number of stream-reported bitrate samples.</param>
    /// <param name="MeanBps">Mean bitrate, in bits per second.</param>
    /// <param name="StdDevBps">Sample standard deviation, in bits per second. Null when fewer than two samples.</param>
    public sealed record AllTimeBitrateBaselineDto(int Count, double MeanBps, double? StdDevBps);
}
