using Microsoft.AspNetCore.Http.HttpResults;

using Moq;

using VideoForensics.Client.WebApp.Api;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Core.Providers.Common.Contracts;

using Xunit;

namespace VideoForensics.Client.WebApp.Tests.Api
{
    /// <summary>GET /api/v1/auth/status returns the provider's non-secret session status to the remote client.</summary>
    public class AuthStatusEndpoint_Tests
    {
        private static readonly ProviderAuthStatus Authenticated = new(true, "Bearer", new DateTime(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc), "client");

        [Fact]
        public async Task GetStatus_NoProvider_UsesDefaultServiceAndReturnsDto()
        {
            var defaultService = new Mock<IProviderAuthService>();
            _ = defaultService.Setup(s => s.GetAuthStatusDetailsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Authenticated);
            var multi = new Mock<IMultiProviderAuthService>();

            Results<Ok<ProviderAuthStatusDto>, NoContent> result =
                await AuthEndpoints.GetStatusAsync(null, defaultService.Object, multi.Object, CancellationToken.None);

            Ok<ProviderAuthStatusDto> ok = Assert.IsType<Ok<ProviderAuthStatusDto>>(result.Result);
            Assert.True(ok.Value!.IsAuthenticated);
            Assert.Equal("Bearer", ok.Value.TokenType);
            Assert.Equal("client", ok.Value.Scope);
            multi.Verify(m => m.GetService(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task GetStatus_NamedProvider_UsesThatServiceNotTheDefault()
        {
            var defaultService = new Mock<IProviderAuthService>();
            var wyze = new Mock<IProviderAuthService>();
            _ = wyze.Setup(s => s.GetAuthStatusDetailsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new ProviderAuthStatus(false, null, null, null));
            var multi = new Mock<IMultiProviderAuthService>();
            _ = multi.Setup(m => m.GetService("Wyze")).Returns(wyze.Object);

            Results<Ok<ProviderAuthStatusDto>, NoContent> result =
                await AuthEndpoints.GetStatusAsync("Wyze", defaultService.Object, multi.Object, CancellationToken.None);

            Ok<ProviderAuthStatusDto> ok = Assert.IsType<Ok<ProviderAuthStatusDto>>(result.Result);
            Assert.False(ok.Value!.IsAuthenticated);
            defaultService.Verify(s => s.GetAuthStatusDetailsAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task GetStatus_ProviderReportsNothing_ReturnsNoContent()
        {
            var defaultService = new Mock<IProviderAuthService>();
            _ = defaultService.Setup(s => s.GetAuthStatusDetailsAsync(It.IsAny<CancellationToken>())).ReturnsAsync((ProviderAuthStatus?)null);

            Results<Ok<ProviderAuthStatusDto>, NoContent> result =
                await AuthEndpoints.GetStatusAsync(null, defaultService.Object, new Mock<IMultiProviderAuthService>().Object, CancellationToken.None);

            _ = Assert.IsType<NoContent>(result.Result);
        }
    }
}
