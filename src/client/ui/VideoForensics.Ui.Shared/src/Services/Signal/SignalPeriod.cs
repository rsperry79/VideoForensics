namespace VideoForensics.Client.Ui.Shared.Services.Signal;

/// <summary>What a stretch of the chart is flagged for.</summary>
public enum SignalPeriodKind
{
    /// <summary>Ring returned a check with no signal value.</summary>
    NoSignal,

    /// <summary>Signal was weaker than <see cref="SignalStats.WeakThresholdDbm"/>.</summary>
    Weak,

    /// <summary>Two or more consecutive checks were missed, so no reading covers this stretch.</summary>
    MissedChecks,
}

/// <summary>A flagged stretch of the chart, from <paramref name="StartUtc"/> to <paramref name="EndUtc"/>.</summary>
public sealed record SignalPeriod(SignalPeriodKind Kind, DateTime StartUtc, DateTime EndUtc);
