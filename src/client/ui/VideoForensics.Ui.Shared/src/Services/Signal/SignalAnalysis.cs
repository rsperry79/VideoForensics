namespace VideoForensics.Client.Ui.Shared.Services.Signal;

/// <summary>
/// The signal calculations for a set of cameras over a window, computed on the server. The signal history page reads
/// this instead of computing it from the raw history, so every client shows the same figures.
/// </summary>
public sealed record SignalAnalysis(IReadOnlyList<SignalDeviceAnalysis> Devices);

/// <summary>One camera's figures for the window.</summary>
/// <param name="DeviceId">The camera.</param>
/// <param name="Name">The name to show; falls back to the provider ID when the device has none.</param>
/// <param name="ProviderName">The provider that owns the camera, for the settings link.</param>
/// <param name="ProviderDeviceId">The provider's device ID, shown on the card.</param>
/// <param name="AllTime">Stats over the whole history: the bars, the table's all-time figures and the deviation baseline.</param>
/// <param name="PeriodStats">Stats over the readings in the window.</param>
/// <param name="Readings">Every reading in the window, including those with no signal, in time order.</param>
/// <param name="Periods">No-signal, low-signal and missed-check periods for the overlays.</param>
/// <param name="Deviations">Hours with a reading more than 1 std dev from the all-time mean.</param>
/// <param name="Calendar">The week-by-hour grid and its profiles.</param>
/// <param name="Hourly">The window grouped into one-hour buckets.</param>
/// <param name="Daily">The window grouped into days.</param>
/// <param name="Weekly">The window grouped into weeks.</param>
public sealed record SignalDeviceAnalysis(
    Guid DeviceId,
    string Name,
    string? ProviderName,
    string ProviderDeviceId,
    SignalStats AllTime,
    SignalStats PeriodStats,
    IReadOnlyList<SignalSample> Readings,
    IReadOnlyList<SignalPeriod> Periods,
    IReadOnlyList<SignalDeviationWindow> Deviations,
    SignalCalendar Calendar,
    IReadOnlyList<SignalBucketStats> Hourly,
    IReadOnlyList<SignalBucketStats> Daily,
    IReadOnlyList<SignalBucketStats> Weekly);
