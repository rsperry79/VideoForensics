using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

using Moq;

using VideoForensics.Client.Ui.Shared.Contracts;
using VideoForensics.Client.Ui.Shared.Services.Signal;
using VideoForensics.Client.WebApp.Api;
using VideoForensics.Core.Api.Contracts;

using Xunit;

namespace VideoForensics.Client.WebApp.Tests.Api
{
    /// <summary>The bit rate analysis endpoint validates its query at the boundary and returns the service's figures as DTOs.</summary>
    public class BitrateAnalysisEndpointTests
    {
        private static readonly DateTime From = new(2026, 10, 19, 0, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime To = From.AddDays(7);

        [Fact]
        public async Task GetBitrateAnalysisAsync_ValidQuery_PassesIdsAndReturnsDto()
        {
            var id = Guid.NewGuid();
            var service = new Mock<IBitrateAnalysisService>();
            _ = service
                .Setup(s => s.AnalyzeAsync(It.Is<IReadOnlyList<Guid>>(l => l.Count == 1 && l[0] == id), From, To, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new BitrateAnalysis([]));

            IResult result = await MediaApiEndpoints.GetBitrateAnalysisAsync(id.ToString("D"), From, To, service.Object, CancellationToken.None);

            Ok<BitrateAnalysisDto> ok = Assert.IsType<Ok<BitrateAnalysisDto>>(result);
            Assert.Empty(ok.Value!.Devices);
        }

        [Fact]
        public async Task GetBitrateAnalysisAsync_MissingRange_ReturnsBadRequest()
        {
            var service = new Mock<IBitrateAnalysisService>();

            IResult result = await MediaApiEndpoints.GetBitrateAnalysisAsync(null, From, null, service.Object, CancellationToken.None);

            _ = Assert.IsType<BadRequest>(result);
            service.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task GetBitrateAnalysisAsync_FromAfterTo_ReturnsBadRequest()
        {
            var service = new Mock<IBitrateAnalysisService>();

            IResult result = await MediaApiEndpoints.GetBitrateAnalysisAsync(null, To, From, service.Object, CancellationToken.None);

            _ = Assert.IsType<BadRequest>(result);
        }

        [Fact]
        public async Task GetBitrateAnalysisAsync_BadGuid_ReturnsBadRequest()
        {
            var service = new Mock<IBitrateAnalysisService>();

            IResult result = await MediaApiEndpoints.GetBitrateAnalysisAsync("not-a-guid", From, To, service.Object, CancellationToken.None);

            _ = Assert.IsType<BadRequest>(result);
            service.VerifyNoOtherCalls();
        }
    }
}
