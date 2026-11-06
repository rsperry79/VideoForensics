using VideoForensics.Core.Api.Contracts;

namespace VideoForensics.Client.Ui.Shared.Contracts;

/// <summary>
/// The per-second video bitrate of one clip. The server computes it on first request and saves it on the clip; pages read
/// the same series on either host.
/// </summary>
public interface IMediaBitrateSeriesSource
{
    /// <summary>Returns the series for a clip, or null when the server cannot read the clip.</summary>
    /// <param name="mediaItemId">The clip to read.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<BitrateSecondDto>?> GetAsync(Guid mediaItemId, CancellationToken ct);
}
