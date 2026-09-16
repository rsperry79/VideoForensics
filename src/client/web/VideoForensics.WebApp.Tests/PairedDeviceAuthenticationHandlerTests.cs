using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

using System.Security.Claims;
using System.Text.Encodings.Web;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting;
using VideoForensics.WebApp.Auth;

using Xunit;

namespace VideoForensics.WebApp.Tests
{
    public class PairedDeviceAuthenticationHandlerTests
    {
        private static async Task<AuthenticateResult> AuthenticateAsync(
            PairedDeviceAuthenticationHandler handler, string scheme, HttpContext httpContext)
        {
            var schemeObj = new AuthenticationScheme(scheme, scheme, typeof(PairedDeviceAuthenticationHandler));
            await handler.InitializeAsync(schemeObj, httpContext);
            return await handler.AuthenticateAsync();
        }

        private static PairedDeviceAuthenticationHandler CreateHandler(
            Mock<ISessionTokenService> tokenService, Mock<IPairedDeviceRepository> repo, Mock<INetworkTierResolver> tierResolver, Mock<IOperatorRepository>? operatorRepository = null)
        {
            var options = new Mock<IOptionsMonitor<AuthenticationSchemeOptions>>();
            _ = options.Setup(o => o.Get(It.IsAny<string>())).Returns(new AuthenticationSchemeOptions());
            ILoggerFactory loggerFactory = LoggerFactory.Create(b => { });
            UrlEncoder encoder = UrlEncoder.Default;
            operatorRepository ??= new Mock<IOperatorRepository>();
            return new PairedDeviceAuthenticationHandler(options.Object, loggerFactory, encoder, tokenService.Object, repo.Object, tierResolver.Object, operatorRepository.Object);
        }

        private static HttpContext CreateHttpContextWithAuthorizationHeader(string token)
        {
            var context = new DefaultHttpContext();
            context.Request.Headers["Authorization"] = $"Bearer {token}";
            return context;
        }

        private static HttpContext CreateHttpContextWithAccessTokenQuery(string token, string path = "/hubs/notifications")
        {
            var context = new DefaultHttpContext();
            context.Request.Path = path;
            context.Request.QueryString = new QueryString($"?access_token={token}");
            return context;
        }

        [Fact]
        public async Task HandleAuthenticateAsync_NoAuthorizationHeader_ReturnsNoResult()
        {
            // Arrange
            var tokenService = new Mock<ISessionTokenService>();
            var repo = new Mock<IPairedDeviceRepository>();
            var tierResolver = new Mock<INetworkTierResolver>();

            PairedDeviceAuthenticationHandler handler = CreateHandler(tokenService, repo, tierResolver);
            var context = new DefaultHttpContext();

            // Act
            AuthenticateResult result = await AuthenticateAsync(handler, PairedDeviceAuthenticationDefaults.SchemeName, context);

            // Assert
            Assert.False(result.Succeeded);
            Assert.True(result.None);
            tokenService.Verify(t => t.Validate(It.IsAny<string>()), Times.Never);
            repo.Verify(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task HandleAuthenticateAsync_ValidSessionToken_SucceedsWithClaimsFromPrincipal()
        {
            // Arrange
            var operatorId = Guid.NewGuid();
            var pairedDeviceId = Guid.NewGuid();
            var tokenService = new Mock<ISessionTokenService>();
            var repo = new Mock<IPairedDeviceRepository>();
            var tierResolver = new Mock<INetworkTierResolver>();
            var operatorRepository = new Mock<IOperatorRepository>();

            var principal = new SessionPrincipal(
                operatorId, pairedDeviceId, OperatorRole.Admin, DateTime.UtcNow, DateTime.UtcNow.AddHours(12));
            _ = tokenService.Setup(t => t.Validate("valid-token")).Returns(principal);

            var device = new PairedDevice
            {
                Id = pairedDeviceId,
                OperatorId = operatorId,
                DeviceName = "Test Device",
                Role = OperatorRole.Admin,
                PairedAtUtc = DateTime.UtcNow
            };
            _ = repo.Setup(r => r.GetAsync(pairedDeviceId, It.IsAny<CancellationToken>())).ReturnsAsync(device);
            _ = tierResolver.Setup(t => t.ResolveTier(It.IsAny<HttpContext>())).Returns(NetworkTier.Local);

            var op = new Operator
            {
                Id = operatorId,
                DisplayName = "Test Operator",
                CreatedAtUtc = DateTime.UtcNow,
                Active = true,
                IsApproved = true
            };
            _ = operatorRepository.Setup(o => o.GetAsync(operatorId, It.IsAny<CancellationToken>())).ReturnsAsync(op);

            PairedDeviceAuthenticationHandler handler = CreateHandler(tokenService, repo, tierResolver, operatorRepository);
            HttpContext context = CreateHttpContextWithAuthorizationHeader("valid-token");

            // Act
            AuthenticateResult result = await AuthenticateAsync(handler, PairedDeviceAuthenticationDefaults.SchemeName, context);

            // Assert
            Assert.True(result.Succeeded);
            Assert.NotNull(result.Ticket);
            ClaimsPrincipal principal_ = result.Ticket.Principal;
            Assert.Contains(principal_.Claims, c => c.Type == VideoForensicsClaimTypes.OperatorId && c.Value == operatorId.ToString());
            Assert.Contains(principal_.Claims, c => c.Type == VideoForensicsClaimTypes.PairedDeviceId && c.Value == pairedDeviceId.ToString());
            Assert.Contains(principal_.Claims, c => c.Type == VideoForensicsClaimTypes.Role && c.Value == OperatorRole.Admin.ToString());
            Assert.Contains(principal_.Claims, c => c.Type == VideoForensicsClaimTypes.NetworkTier && c.Value == NetworkTier.Local.ToString());
        }

        [Fact]
        public async Task HandleAuthenticateAsync_ValidSessionTokenButRevokedDevice_Fails()
        {
            // Arrange
            var operatorId = Guid.NewGuid();
            var pairedDeviceId = Guid.NewGuid();
            var tokenService = new Mock<ISessionTokenService>();
            var repo = new Mock<IPairedDeviceRepository>();
            var tierResolver = new Mock<INetworkTierResolver>();
            var operatorRepository = new Mock<IOperatorRepository>();

            var principal = new SessionPrincipal(
                operatorId, pairedDeviceId, OperatorRole.Admin, DateTime.UtcNow, DateTime.UtcNow.AddHours(12));
            _ = tokenService.Setup(t => t.Validate("revoked-token")).Returns(principal);

            var device = new PairedDevice
            {
                Id = pairedDeviceId,
                OperatorId = operatorId,
                DeviceName = "Revoked Device",
                Role = OperatorRole.Admin,
                PairedAtUtc = DateTime.UtcNow,
                RevokedAtUtc = DateTime.UtcNow.AddMinutes(-5)
            };
            _ = repo.Setup(r => r.GetAsync(pairedDeviceId, It.IsAny<CancellationToken>())).ReturnsAsync(device);

            PairedDeviceAuthenticationHandler handler = CreateHandler(tokenService, repo, tierResolver, operatorRepository);
            HttpContext context = CreateHttpContextWithAuthorizationHeader("revoked-token");

            // Act
            AuthenticateResult result = await AuthenticateAsync(handler, PairedDeviceAuthenticationDefaults.SchemeName, context);

            // Assert
            Assert.False(result.Succeeded);
            Assert.Equal("Invalid or expired credential.", result.Failure?.Message);
            tierResolver.Verify(t => t.ResolveTier(It.IsAny<HttpContext>()), Times.Never);
        }

        [Fact]
        public async Task HandleAuthenticateAsync_ValidSessionTokenButDeviceNotFound_Fails()
        {
            // Arrange
            var operatorId = Guid.NewGuid();
            var pairedDeviceId = Guid.NewGuid();
            var tokenService = new Mock<ISessionTokenService>();
            var repo = new Mock<IPairedDeviceRepository>();
            var tierResolver = new Mock<INetworkTierResolver>();
            var operatorRepository = new Mock<IOperatorRepository>();

            var principal = new SessionPrincipal(
                operatorId, pairedDeviceId, OperatorRole.Admin, DateTime.UtcNow, DateTime.UtcNow.AddHours(12));
            _ = tokenService.Setup(t => t.Validate("unknown-token")).Returns(principal);
            _ = repo.Setup(r => r.GetAsync(pairedDeviceId, It.IsAny<CancellationToken>())).ReturnsAsync((PairedDevice?)null);

            PairedDeviceAuthenticationHandler handler = CreateHandler(tokenService, repo, tierResolver, operatorRepository);
            HttpContext context = CreateHttpContextWithAuthorizationHeader("unknown-token");

            // Act
            AuthenticateResult result = await AuthenticateAsync(handler, PairedDeviceAuthenticationDefaults.SchemeName, context);

            // Assert
            Assert.False(result.Succeeded);
            Assert.Equal("Invalid or expired credential.", result.Failure?.Message);
        }

        [Fact]
        public async Task HandleAuthenticateAsync_InvalidSessionTokenButValidFallbackApiKey_SucceedsWithClaimsFromDevice()
        {
            // Arrange
            var operatorId = Guid.NewGuid();
            var pairedDeviceId = Guid.NewGuid();
            var tokenService = new Mock<ISessionTokenService>();
            var repo = new Mock<IPairedDeviceRepository>();
            var tierResolver = new Mock<INetworkTierResolver>();
            var operatorRepository = new Mock<IOperatorRepository>();

            _ = tokenService.Setup(t => t.Validate(It.IsAny<string>())).Returns((SessionPrincipal?)null);

            var device = new PairedDevice
            {
                Id = pairedDeviceId,
                OperatorId = operatorId,
                DeviceName = "Fallback Device",
                Role = OperatorRole.Review,
                PairedAtUtc = DateTime.UtcNow
            };
            _ = repo.Setup(r => r.GetByFallbackApiKeyHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(device);
            _ = tierResolver.Setup(t => t.ResolveTier(It.IsAny<HttpContext>())).Returns(NetworkTier.Network);

            PairedDeviceAuthenticationHandler handler = CreateHandler(tokenService, repo, tierResolver, operatorRepository);
            HttpContext context = CreateHttpContextWithAuthorizationHeader("some-api-key");

            // Act
            AuthenticateResult result = await AuthenticateAsync(handler, PairedDeviceAuthenticationDefaults.SchemeName, context);

            // Assert
            Assert.True(result.Succeeded);
            Assert.NotNull(result.Ticket);
            ClaimsPrincipal principal = result.Ticket.Principal;
            Assert.Contains(principal.Claims, c => c.Type == VideoForensicsClaimTypes.OperatorId && c.Value == operatorId.ToString());
            Assert.Contains(principal.Claims, c => c.Type == VideoForensicsClaimTypes.PairedDeviceId && c.Value == pairedDeviceId.ToString());
            Assert.Contains(principal.Claims, c => c.Type == VideoForensicsClaimTypes.Role && c.Value == OperatorRole.Review.ToString());
            Assert.Contains(principal.Claims, c => c.Type == VideoForensicsClaimTypes.NetworkTier && c.Value == NetworkTier.Network.ToString());
        }

        [Fact]
        public async Task HandleAuthenticateAsync_InvalidFallbackApiKey_Fails()
        {
            // Arrange
            var tokenService = new Mock<ISessionTokenService>();
            var repo = new Mock<IPairedDeviceRepository>();
            var tierResolver = new Mock<INetworkTierResolver>();

            _ = tokenService.Setup(t => t.Validate(It.IsAny<string>())).Returns((SessionPrincipal?)null);
            _ = repo.Setup(r => r.GetByFallbackApiKeyHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((PairedDevice?)null);

            PairedDeviceAuthenticationHandler handler = CreateHandler(tokenService, repo, tierResolver);
            HttpContext context = CreateHttpContextWithAuthorizationHeader("invalid-api-key");

            // Act
            AuthenticateResult result = await AuthenticateAsync(handler, PairedDeviceAuthenticationDefaults.SchemeName, context);

            // Assert
            Assert.False(result.Succeeded);
            Assert.Equal("Invalid or expired credential.", result.Failure?.Message);
            tierResolver.Verify(t => t.ResolveTier(It.IsAny<HttpContext>()), Times.Never);
        }

        [Fact]
        public async Task HandleAuthenticateAsync_RevokedFallbackDevice_Fails()
        {
            // Arrange
            var operatorId = Guid.NewGuid();
            var pairedDeviceId = Guid.NewGuid();
            var tokenService = new Mock<ISessionTokenService>();
            var repo = new Mock<IPairedDeviceRepository>();
            var tierResolver = new Mock<INetworkTierResolver>();

            _ = tokenService.Setup(t => t.Validate(It.IsAny<string>())).Returns((SessionPrincipal?)null);

            var device = new PairedDevice
            {
                Id = pairedDeviceId,
                OperatorId = operatorId,
                DeviceName = "Revoked Fallback Device",
                Role = OperatorRole.ReadOnly,
                PairedAtUtc = DateTime.UtcNow,
                RevokedAtUtc = DateTime.UtcNow.AddHours(-1)
            };
            _ = repo.Setup(r => r.GetByFallbackApiKeyHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(device);

            PairedDeviceAuthenticationHandler handler = CreateHandler(tokenService, repo, tierResolver);
            HttpContext context = CreateHttpContextWithAuthorizationHeader("revoked-api-key");

            // Act
            AuthenticateResult result = await AuthenticateAsync(handler, PairedDeviceAuthenticationDefaults.SchemeName, context);

            // Assert
            Assert.False(result.Succeeded);
            Assert.Equal("Invalid or expired credential.", result.Failure?.Message);
            tierResolver.Verify(t => t.ResolveTier(It.IsAny<HttpContext>()), Times.Never);
        }

        [Fact]
        public async Task HandleAuthenticateAsync_ExtractsTokenFromSignalRAccessTokenQuery()
        {
            // Arrange
            var operatorId = Guid.NewGuid();
            var pairedDeviceId = Guid.NewGuid();
            var tokenService = new Mock<ISessionTokenService>();
            var repo = new Mock<IPairedDeviceRepository>();
            var tierResolver = new Mock<INetworkTierResolver>();
            var operatorRepository = new Mock<IOperatorRepository>();

            var principal = new SessionPrincipal(
                operatorId, pairedDeviceId, OperatorRole.SuperAdmin, DateTime.UtcNow, DateTime.UtcNow.AddHours(12));
            _ = tokenService.Setup(t => t.Validate("signalr-token")).Returns(principal);

            var device = new PairedDevice
            {
                Id = pairedDeviceId,
                OperatorId = operatorId,
                DeviceName = "SignalR Device",
                Role = OperatorRole.SuperAdmin,
                PairedAtUtc = DateTime.UtcNow
            };
            _ = repo.Setup(r => r.GetAsync(pairedDeviceId, It.IsAny<CancellationToken>())).ReturnsAsync(device);
            _ = tierResolver.Setup(t => t.ResolveTier(It.IsAny<HttpContext>())).Returns(NetworkTier.Local);

            var op = new Operator
            {
                Id = operatorId,
                DisplayName = "Test Operator",
                CreatedAtUtc = DateTime.UtcNow,
                Active = true,
                IsApproved = true
            };
            _ = operatorRepository.Setup(o => o.GetAsync(operatorId, It.IsAny<CancellationToken>())).ReturnsAsync(op);

            PairedDeviceAuthenticationHandler handler = CreateHandler(tokenService, repo, tierResolver, operatorRepository);
            HttpContext context = CreateHttpContextWithAccessTokenQuery("signalr-token", "/hubs/notifications");

            // Act
            AuthenticateResult result = await AuthenticateAsync(handler, PairedDeviceAuthenticationDefaults.SchemeName, context);

            // Assert
            Assert.True(result.Succeeded);
            Assert.NotNull(result.Ticket);
            ClaimsPrincipal principal_ = result.Ticket.Principal;
            Assert.Contains(principal_.Claims, c => c.Type == VideoForensicsClaimTypes.Role && c.Value == OperatorRole.SuperAdmin.ToString());
            tokenService.Verify(t => t.Validate("signalr-token"), Times.Once);
        }

        [Fact]
        public async Task HandleAuthenticateAsync_IgnoresAccessTokenQueryForNonHubPath()
        {
            // Arrange
            var tokenService = new Mock<ISessionTokenService>();
            var repo = new Mock<IPairedDeviceRepository>();
            var tierResolver = new Mock<INetworkTierResolver>();

            _ = tokenService.Setup(t => t.Validate(It.IsAny<string>())).Returns((SessionPrincipal?)null);
            _ = repo.Setup(r => r.GetByFallbackApiKeyHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((PairedDevice?)null);

            PairedDeviceAuthenticationHandler handler = CreateHandler(tokenService, repo, tierResolver);
            HttpContext context = CreateHttpContextWithAccessTokenQuery("query-token", "/api/devices");

            // Act
            AuthenticateResult result = await AuthenticateAsync(handler, PairedDeviceAuthenticationDefaults.SchemeName, context);

            // Assert
            Assert.False(result.Succeeded);
            Assert.True(result.None);
            tokenService.Verify(t => t.Validate(It.IsAny<string>()), Times.Never);
        }
    }
}
