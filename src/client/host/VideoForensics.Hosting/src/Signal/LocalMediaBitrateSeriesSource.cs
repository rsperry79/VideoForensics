using VideoForensics.Client.Ui.Shared.Contracts;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Core.Contracts;

namespace VideoForensics.Client.Hosting.Signal
{
    /// <summary>In-process <see cref="IMediaBitrateSeriesSource"/> for the WebApp: reads the series from the server's own service.</summary>
    public sealed class LocalMediaBitrateSeriesSource(IMediaBitrateSeriesService series) : IMediaBitrateSeriesSource
    {
        /// <inheritdoc />
        public async Task<IReadOnlyList<BitrateSecondDto>?> GetAsync(Guid mediaItemId, CancellationToken ct)
        {
            IReadOnlyList<VideoForensics.Core.Providers.Core.BitrateSecond>? points = await series.GetAsync(mediaItemId, ct);
            return points?.Select(p => p.ToDto()).ToList();
        }
    }
}
