using Moq;

using VideoForensics.Client.Hosting.Services;
using VideoForensics.Data.Core.Contracts;

using Xunit;

namespace VideoForensics.Client.Hosting.Tests.Services
{
    public class AwaitingDingIdResolverTests
    {
        [Fact]
        public async Task ResolveAsync_ExactCandidatePresent_ReturnsTheDingId()
        {
            var client = new Mock<IVideoForensicsDataClient>();
            _ = client.Setup(c => c.GetDingIdCandidatesAsync(86401464, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<string> { "7710029108920869304", "7697741117292700088" });

            string? dingId = await AwaitingDingIdResolver.ResolveAsync("7710029108920869304", 86401464, client.Object, CancellationToken.None);

            Assert.Equal("7710029108920869304", dingId);
        }

        [Fact]
        public async Task ResolveAsync_NoExactCandidate_ReturnsNull()
        {
            var client = new Mock<IVideoForensicsDataClient>();
            _ = client.Setup(c => c.GetDingIdCandidatesAsync(86401464, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<string> { "7697741117292700088" });

            string? dingId = await AwaitingDingIdResolver.ResolveAsync("7710029108920869304", 86401464, client.Object, CancellationToken.None);

            Assert.Null(dingId);
        }

        [Fact]
        public async Task ResolveAsync_SharedPrefixOnly_ReturnsNull()
        {
            var client = new Mock<IVideoForensicsDataClient>();
            _ = client.Setup(c => c.GetDingIdCandidatesAsync(86401464, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<string> { "77100291089208693041" });

            string? dingId = await AwaitingDingIdResolver.ResolveAsync("7710029108920869304", 86401464, client.Object, CancellationToken.None);

            Assert.Null(dingId);
        }
    }
}
