namespace VideoForensics.Client.Ui.Shared.Services;

/// <summary>
/// The single place that decides how fast a clip may play and when it is muted. <c>MediaPlayer</c> applies it to every
/// requested rate, so a change to speed limits touches only the implementation registered in DI.
/// </summary>
public interface IMediaPlaybackPolicy
{
    /// <summary>Returns the rate to apply for a requested rate: capped to the maximum, and 1 for a rate that cannot play.</summary>
    double ResolveRate(double requestedRate);

    /// <summary>True when playback at <paramref name="effectiveRate"/> must be muted, because audio at that speed is noise.</summary>
    bool ShouldMute(double effectiveRate);
}
