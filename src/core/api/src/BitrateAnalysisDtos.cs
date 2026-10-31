namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>
    /// Wire DTO for one measured clip bit rate. <paramref name="Kbps"/> is the stream-reported video bit rate in kbps;
    /// <paramref name="Collapsed"/> is true when the clip carries the bit-rate-collapse red flag.
    /// </summary>
    public sealed record BitrateSampleDto(DateTime CapturedAtUtc, int Kbps, string? LightingMode, bool Collapsed);

    /// <summary>
    /// Wire DTO for one camera's bit rate figures over a window. Reuses the signal stats, bucket and deviation DTOs, so
    /// their dBm-named fields hold kbps here. <paramref name="Cells"/> holds 168 stats, day-major with Monday first
    /// (index = day * 24 + hour); <paramref name="HourOfDay"/> holds 24 and <paramref name="DayOfWeek"/> 7.
    /// </summary>
    public sealed record BitrateDeviceAnalysisDto(
        Guid DeviceId,
        string Name,
        string? ProviderName,
        string ProviderDeviceId,
        SignalStatsDto AllTime,
        SignalStatsDto PeriodStats,
        IReadOnlyList<BitrateSampleDto> Readings,
        IReadOnlyList<SignalDeviationDto> Deviations,
        IReadOnlyList<SignalStatsDto> Cells,
        IReadOnlyList<SignalStatsDto> HourOfDay,
        IReadOnlyList<SignalStatsDto> DayOfWeek,
        IReadOnlyList<SignalBucketDto> Hourly,
        IReadOnlyList<SignalBucketDto> Daily,
        IReadOnlyList<SignalBucketDto> Weekly);

    /// <summary>Wire DTO for the bit rate figures of a set of cameras over a window.</summary>
    public sealed record BitrateAnalysisDto(IReadOnlyList<BitrateDeviceAnalysisDto> Devices);
}
