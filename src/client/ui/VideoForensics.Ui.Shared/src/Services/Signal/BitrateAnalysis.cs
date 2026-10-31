namespace VideoForensics.Client.Ui.Shared.Services.Signal;

/// <summary>
/// The bit rate calculations for a set of cameras over a window, computed on the server. The bit rate page reads this
/// instead of computing it from the raw history. It reuses the signal stats, buckets, calendar and deviation types by
/// feeding them kbps in place of dBm: wherever those types say "Rssi" or "Dbm" (<c>SignalSample.RssiDbm</c>,
/// <c>SignalDeviationWindow.WorstRssiDbm</c>), the value here is kbps. <c>SignalStats.PercentWeak</c> is RSSI-specific
/// and is not meaningful for bit rate.
/// </summary>
public sealed record BitrateAnalysis(IReadOnlyList<BitrateDeviceAnalysis> Devices);

/// <summary>One clip's measured video bit rate.</summary>
/// <param name="CapturedAtUtc">When the clip was recorded.</param>
/// <param name="Kbps">The stream-reported video bit rate in kbps (bits per second / 1000, rounded).</param>
/// <param name="LightingMode">Lighting class of the clip ("Ir", "ColorNight" or "Day"); null if not analysed.</param>
/// <param name="Collapsed">True when the clip carries the bit-rate-collapse red flag.</param>
public sealed record BitrateSample(DateTime CapturedAtUtc, int Kbps, string? LightingMode, bool Collapsed)
{
    /// <summary>The reading as a signal sample so the shared stats, bucket, calendar and deviation maths apply; kbps stands in for dBm.</summary>
    public SignalSample ToSignalSample()
    {
        return new SignalSample(CapturedAtUtc, Kbps);
    }
}

/// <summary>One camera's bit rate figures for the window. Stats, deviation and bucket fields hold kbps (see <see cref="BitrateAnalysis"/>).</summary>
/// <param name="DeviceId">The camera.</param>
/// <param name="Name">The name to show; falls back to the provider ID when the device has none.</param>
/// <param name="ProviderName">The provider that owns the camera, for the settings link.</param>
/// <param name="ProviderDeviceId">The provider's device ID, shown on the card.</param>
/// <param name="AllTime">Stats over the whole history: the bars, the table's all-time figures and the deviation baseline.</param>
/// <param name="PeriodStats">Stats over the readings in the window.</param>
/// <param name="Readings">Every measured clip in the window, in time order.</param>
/// <param name="Deviations">Hours with a reading more than 1 std dev from the all-time mean.</param>
/// <param name="Calendar">The week-by-hour grid and its profiles.</param>
/// <param name="Hourly">The window grouped into one-hour buckets.</param>
/// <param name="Daily">The window grouped into days.</param>
/// <param name="Weekly">The window grouped into weeks.</param>
public sealed record BitrateDeviceAnalysis(
    Guid DeviceId,
    string Name,
    string? ProviderName,
    string ProviderDeviceId,
    SignalStats AllTime,
    SignalStats PeriodStats,
    IReadOnlyList<BitrateSample> Readings,
    IReadOnlyList<SignalDeviationWindow> Deviations,
    SignalCalendar Calendar,
    IReadOnlyList<SignalBucketStats> Hourly,
    IReadOnlyList<SignalBucketStats> Daily,
    IReadOnlyList<SignalBucketStats> Weekly);
