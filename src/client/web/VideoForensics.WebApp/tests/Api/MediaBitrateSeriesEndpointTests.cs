using Microsoft.AspNetCore.Http.HttpResults;

using Moq;

using VideoForensics.Client.WebApp.Api;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Core.Providers.Core;
using VideoForensics.Data.Core.Contracts;

using Xunit;

namespace VideoForensics.Client.WebApp.Tests.Api
{
    /// <summary>The bitrate-series endpoint returns the clip's per-second points, or 404 for a clip the server cannot read.</summary>
    public class MediaBitrateSeriesEndpointTests
    {
        private static readonly Guid ClipId = Guid.Parse("44444444-4444-4444-4444-444444444444");

        [Fact]
        public async Task MediaBitrateSeriesEndpoint_KnownClip_ReturnsPoints()
        {
            var series = new Mock<IMediaBitrateSeriesService>();
            _ = series.Setup(s => s.GetAsync(ClipId, It.IsAny<CancellationToken>()))
                .ReturnsAsync([new BitrateSecond(0, 120), new BitrateSecond(1, 80)]);

            Results<Ok<IReadOnlyList<BitrateSecondDto>>, NotFound> result = await MediaApiEndpoints.GetBitrateSeriesAsync(ClipId, series.Object, CancellationToken.None);

            Ok<IReadOnlyList<BitrateSecondDto>> ok = Assert.IsType<Ok<IReadOnlyList<BitrateSecondDto>>>(result.Result);
            Assert.Equal(2, ok.Value!.Count);
            Assert.Equal(120, ok.Value[0].Kbps);
        }

        [Fact]
        public async Task MediaBitrateSeriesEndpoint_UnreadableClip_ReturnsNotFound()
        {
            var series = new Mock<IMediaBitrateSeriesService>();
            _ = series.Setup(s => s.GetAsync(ClipId, It.IsAny<CancellationToken>())).ReturnsAsync((IReadOnlyList<BitrateSecond>?)null);

            Results<Ok<IReadOnlyList<BitrateSecondDto>>, NotFound> result = await MediaApiEndpoints.GetBitrateSeriesAsync(ClipId, series.Object, CancellationToken.None);

            _ = Assert.IsType<NotFound>(result.Result);
        }
    }
}
