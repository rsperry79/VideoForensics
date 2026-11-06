namespace VideoForensics.Client.Ui.Shared.Services;

/// <summary>Caps the playback rate at 8x and mutes above 4x.</summary>
public sealed class DefaultMediaPlaybackPolicy : IMediaPlaybackPolicy
{
    /// <summary>The fastest rate the player will apply.</summary>
    public const double MaxRate = 8.0;

    /// <summary>Rates above this are muted.</summary>
    public const double MuteAboveRate = 4.0;

    /// <inheritdoc />
    public double ResolveRate(double requestedRate)
    {
        // Browsers reject zero, negative and non-finite rates; fall back to normal speed rather than throw.
        if (!double.IsFinite(requestedRate) || requestedRate <= 0)
        {
            return 1.0;
        }

        return Math.Min(requestedRate, MaxRate);
    }

    /// <inheritdoc />
    public bool ShouldMute(double effectiveRate) => effectiveRate > MuteAboveRate;
}
