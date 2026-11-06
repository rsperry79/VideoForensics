using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

using Moq;

using VideoForensics.Client.WebApp.Api;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.WebApp.Tests.Api
{
    /// <summary>The snapshot-capture endpoints validate the window at the boundary and pick the current or attempts query.</summary>
    public class SnapshotCaptureEndpointTests
    {
        private static readonly Guid DeviceId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private static readonly Guid CaptureId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        private static readonly DateTimeOffset From = new(2026, 10, 19, 0, 0, 0, TimeSpan.Zero);

        private static int StatusOf(IResult result)
        {
            return Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode ?? 200;
        }

        [Fact]
        public async Task ListAsync_DefaultAttemptsOnly_QueriesCurrentCaptures()
        {
            var captures = new Mock<ISnapshotCaptureRepository>();
            _ = captures.Setup(c => c.GetCurrentForDeviceAsync(DeviceId, From, From.AddHours(1), It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);

            IResult result = await SnapshotCaptureEndpoints.ListAsync(DeviceId, From, From.AddHours(1), captures.Object, CancellationToken.None);

            _ = Assert.IsType<Ok<List<SnapshotCaptureDto>>>(result);
            captures.Verify(c => c.GetCurrentForDeviceAsync(DeviceId, From, From.AddHours(1), It.IsAny<CancellationToken>()), Times.Once);
            captures.Verify(c => c.GetAttemptsForDeviceAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ListAsync_AttemptsOnly_QueriesAttempts()
        {
            var captures = new Mock<ISnapshotCaptureRepository>();
            _ = captures.Setup(c => c.GetAttemptsForDeviceAsync(DeviceId, From, From.AddHours(1), It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);

            IResult result = await SnapshotCaptureEndpoints.ListAsync(DeviceId, From, From.AddHours(1), captures.Object, CancellationToken.None, attemptsOnly: true);

            Assert.Equal(200, StatusOf(result));
            captures.Verify(c => c.GetAttemptsForDeviceAsync(DeviceId, From, From.AddHours(1), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ListAsync_ToNotAfterFrom_ReturnsBadRequestWithoutQuerying()
        {
            var captures = new Mock<ISnapshotCaptureRepository>();

            IResult result = await SnapshotCaptureEndpoints.ListAsync(DeviceId, From, From, captures.Object, CancellationToken.None);

            Assert.Equal(400, StatusOf(result));
            captures.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task GetAsync_Missing_ReturnsNotFound()
        {
            var captures = new Mock<ISnapshotCaptureRepository>();
            _ = captures.Setup(c => c.GetAsync(CaptureId, It.IsAny<CancellationToken>())).ReturnsAsync((SnapshotCapture?)null);

            IResult result = await SnapshotCaptureEndpoints.GetAsync(CaptureId, captures.Object, CancellationToken.None);

            Assert.Equal(404, StatusOf(result));
        }
    }
}
