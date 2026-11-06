using Microsoft.Extensions.Logging;

using Moq;

using VideoForensics.Data.Common.Matching;
using VideoForensics.Data.Core.Contracts;
using VideoForensics.Providers.Ring.Entities;
using VideoForensics.Providers.Ring.Services;

using Xunit;

namespace VideoForensics.Providers.Ring.Tests.Services
{
    /// <summary>
    /// The download path resolves a doorbot history row to its string ding id before it writes an Events row. Only a
    /// single device-scoped exact match counts; anything else leaves the row unlinked and the event is skipped.
    /// </summary>
    public class RingDingIdLinkTests
    {
        private const int DeviceId = 86401464;
        private const long HistoryId = 1234567890123456789;

        [Fact]
        public async Task ResolveDingIdAsync_OneCandidateEqualsTheId_ReturnsMatchedWithThatDingId()
        {
            var data = new Mock<IVideoForensicsDataClient>();
            _ = data.Setup(d => d.GetDingIdCandidatesAsync(DeviceId, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { "1234567890123456789" });
            RingMediaDownloadService service = CreateService(data.Object);

            DingIdMatchResult result = await service.ResolveDingIdAsync(BuildHistoryEvent(), CancellationToken.None);

            Assert.Equal(DingIdMatchOutcome.Matched, result.Outcome);
            Assert.Equal("1234567890123456789", result.DingId);
        }

        [Fact]
        public async Task ResolveDingIdAsync_OneCandidateOnlySharesPrefix_ReturnsUnmatched()
        {
            var data = new Mock<IVideoForensicsDataClient>();
            _ = data.Setup(d => d.GetDingIdCandidatesAsync(DeviceId, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { "1234567890123456001" });
            RingMediaDownloadService service = CreateService(data.Object);

            DingIdMatchResult result = await service.ResolveDingIdAsync(BuildHistoryEvent(), CancellationToken.None);

            Assert.Equal(DingIdMatchOutcome.Unmatched, result.Outcome);
            Assert.Null(result.DingId);
        }

        [Fact]
        public async Task ResolveDingIdAsync_NoCandidates_ReturnsUnmatched()
        {
            var data = new Mock<IVideoForensicsDataClient>();
            _ = data.Setup(d => d.GetDingIdCandidatesAsync(DeviceId, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<string>());
            RingMediaDownloadService service = CreateService(data.Object);

            DingIdMatchResult result = await service.ResolveDingIdAsync(BuildHistoryEvent(), CancellationToken.None);

            Assert.Equal(DingIdMatchOutcome.Unmatched, result.Outcome);
            Assert.Null(result.DingId);
        }

        [Fact]
        public async Task ResolveDingIdAsync_TwoEqualCandidates_ReturnsAmbiguous()
        {
            var data = new Mock<IVideoForensicsDataClient>();
            _ = data.Setup(d => d.GetDingIdCandidatesAsync(DeviceId, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { "1234567890123456789", "1234567890123456789" });
            RingMediaDownloadService service = CreateService(data.Object);

            DingIdMatchResult result = await service.ResolveDingIdAsync(BuildHistoryEvent(), CancellationToken.None);

            Assert.Equal(DingIdMatchOutcome.Ambiguous, result.Outcome);
            Assert.Null(result.DingId);
        }

        private static DoorbotHistoryEvent BuildHistoryEvent()
        {
            return new DoorbotHistoryEvent { Id = HistoryId, Doorbot = new Doorbot { Id = DeviceId } };
        }

        private static RingMediaDownloadService CreateService(IVideoForensicsDataClient dataClient)
        {
            return new RingMediaDownloadService(
                new Mock<ILogger>().Object,
                new Mock<ISessionProvider>().Object,
                dataClient,
                metadataTagger: null,
                notificationDispatcher: null);
        }
    }
}
