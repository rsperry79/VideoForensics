namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>
    /// Wire DTO for one set of signal stats. Mirrors the domain stats; a null means there was nothing to measure
    /// (no readings for Min/Max/Latest, fewer than two for StdDev).
    /// </summary>
    /// <param name="Count">Readings that have a signal value.</param>
    /// <param name="Min">Weakest reading in dBm.</param>
    /// <param name="Max">Strongest reading in dBm.</param>
    /// <param name="Average">Mean reading in dBm.</param>
    /// <param name="Latest">Most recent reading in dBm.</param>
    /// <param name="PercentWeak">Share of readings weaker than -75 dBm, as a percentage.</param>
    /// <param name="StdDev">Sample standard deviation in dBm (n - 1).</param>
    public sealed record SignalStatsDto(int Count, int? Min, int? Max, double Average, int? Latest, double PercentWeak, double? StdDev);

    /// <summary>Wire DTO for one reading. <paramref name="RssiDbm"/> is null when the provider returned no signal.</summary>
    public sealed record SignalSampleDto(DateTime CapturedAtUtc, int? RssiDbm);

    /// <summary>
    /// Wire DTO for a flagged stretch of the timeline. <paramref name="Kind"/> is "NoSignal", "Weak" or "MissedChecks".
    /// </summary>
    public sealed record SignalPeriodDto(string Kind, DateTime StartUtc, DateTime EndUtc);

    /// <summary>
    /// Wire DTO for an hour with a reading more than one std dev from the mean. <paramref name="Level"/> is "OneSigma"
    /// or "TwoSigma".
    /// </summary>
    public sealed record SignalDeviationDto(DateTime HourStartUtc, string Level, int FlaggedReadings, int WorstRssiDbm);

    /// <summary>Wire DTO for one time group of readings (an hour, day or week).</summary>
    public sealed record SignalBucketDto(DateTime StartUtc, SignalStatsDto Stats, int NoSignalCount);

    /// <summary>
    /// Wire DTO for one camera's signal figures over a window. <paramref name="Cells"/> holds 168 stats, day-major with
    /// Monday first (index = day * 24 + hour); <paramref name="HourOfDay"/> holds 24 and <paramref name="DayOfWeek"/> 7.
    /// </summary>
    public sealed record SignalDeviceAnalysisDto(
        Guid DeviceId,
        string Name,
        string? ProviderName,
        string ProviderDeviceId,
        SignalStatsDto AllTime,
        SignalStatsDto PeriodStats,
        IReadOnlyList<SignalSampleDto> Readings,
        IReadOnlyList<SignalPeriodDto> Periods,
        IReadOnlyList<SignalDeviationDto> Deviations,
        IReadOnlyList<SignalStatsDto> Cells,
        IReadOnlyList<SignalStatsDto> HourOfDay,
        IReadOnlyList<SignalStatsDto> DayOfWeek,
        IReadOnlyList<SignalBucketDto> Hourly,
        IReadOnlyList<SignalBucketDto> Daily,
        IReadOnlyList<SignalBucketDto> Weekly);

    /// <summary>Wire DTO for the signal figures of a set of cameras over a window.</summary>
    public sealed record SignalAnalysisDto(IReadOnlyList<SignalDeviceAnalysisDto> Devices);
}
