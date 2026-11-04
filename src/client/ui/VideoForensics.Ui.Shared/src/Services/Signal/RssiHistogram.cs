namespace VideoForensics.Client.Ui.Shared.Services.Signal;

/// <summary>One bar of the RSSI histogram.</summary>
/// <param name="XFraction">Where the reading falls in the window: 0 at the window start, 1 at its end.</param>
/// <param name="HeightFraction">How weak the signal is: 0 at the strongest reading, 1 at the weakest.</param>
/// <param name="CapturedAtUtc">When the reading was taken. A click on the bar seeks the player to this instant.</param>
public sealed record RssiBar(double XFraction, double HeightFraction, DateTime CapturedAtUtc);

/// <summary>Turns RSSI readings into histogram bars for a window. Readings without a signal, and readings outside the window, are left out.</summary>
public static class RssiHistogram
{
    /// <summary>The dBm value that gives a bar of zero height (a strong signal).</summary>
    private const double StrongestDbm = -30;

    /// <summary>The dBm value that gives a full-height bar (a very weak signal).</summary>
    private const double WeakestDbm = -90;

    /// <summary>Builds one bar per reading that has a signal and falls inside the window.</summary>
    /// <param name="readings">The readings, in any order.</param>
    /// <param name="startUtc">The window start (UTC).</param>
    /// <param name="endUtc">The window end (UTC).</param>
    public static IReadOnlyList<RssiBar> Bars(IReadOnlyList<SignalSample> readings, DateTime startUtc, DateTime endUtc)
    {
        double span = (endUtc - startUtc).TotalSeconds;
        if (span <= 0)
        {
            return [];
        }

        var bars = new List<RssiBar>();
        foreach (SignalSample reading in readings)
        {
            if (reading.RssiDbm is not int rssi || reading.CapturedAtUtc < startUtc || reading.CapturedAtUtc > endUtc)
            {
                continue;
            }

            double x = (reading.CapturedAtUtc - startUtc).TotalSeconds / span;
            double weakness = (StrongestDbm - rssi) / (StrongestDbm - WeakestDbm);
            bars.Add(new RssiBar(x, Math.Clamp(weakness, 0, 1), reading.CapturedAtUtc));
        }

        return bars;
    }
}
