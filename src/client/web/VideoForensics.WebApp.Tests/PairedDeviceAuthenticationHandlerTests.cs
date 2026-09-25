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
            Mock<ISessionTokenService> tokenService, Mock<IPairedDeviceRepository> repo, Mock<INetworkTierResolver> tierResolver, Mock<IOperatorRepository>? operatorRepository = null, Mock<IOperatorCredentialRepository>? credentialRepository = null, Mock<ISessionTierHeaderProtector>? headerProtector = null)
        {
            var options = new Mock<IOptionsMonitor<AuthenticationSchemeOptions>>();
            _ = options.Setup(o => o.Get(It.IsAny<string>())).Returns(new AuthenticationSchemeOptions());
            ILoggerFactory loggerFactory = LoggerFactory.Create(b => { });
            UrlEncoder encoder = UrlEncoder.Default;
            operatorRepository ??= new Mock<IOperatorRepository>();
            credentialRepository ??= new Mock<IOperatorCredentialRepository>();
            headerProtector ??= new Mock<ISessionTierHeaderProtector>();
            return new PairedDeviceAuthenticationHandler(options.Object, loggerFactory, encoder, tokenService.Object, repo.Object, credentialRepository.Object, tierResolver.Object, operatorRepository.Object, headerProtector.Object);
        }

        private static HttpContext CreateHttpContextWithAuthorizationHeaderAndTierHeader(string token, string tierHeaderValue)
        {
            var context = new DefaultHttpContext();
            context.Request.Headers["Authorization"] = $"Bearer {token}";
            context.Request.Headers[SessionTierHeaderNames.HeaderName] = tierHeaderValue;
            return context;
        }

        /// <summary>Builds an Operator + session-token setup shared by the session-tier-header tests below.</summary>
        private static (Guid operatorId, Guid pairedDeviceId, Mock<ISessionTokenService> tokenService, Mock<IPairedDeviceRepository> repo, Mock<IOperatorRepository> operatorRepository) SetUpValidServiceDeviceSession(string token)
        {
            var operatorId = Guid.NewGuid();
            var pairedDeviceId = Guid.NewGuid();
            var securityStamp = Guid.NewGuid();
            var tokenService = new Mock<ISessionTokenService>();
            var repo = new Mock<IPairedDeviceRepository>();
            var operatorRepository = new Mock<IOperatorRepository>();

            var principal = new SessionPrincipal(
                operatorId, pairedDeviceId, CredentialKind.ServiceDevice, OperatorRole.SuperAdmin, securityStamp, DateTime.UtcNow, DateTime.UtcNow.AddHours(12));
            _ = tokenService.Setup(t => t.Validate(token)).Returns(principal);

            var device = new PairedDevice
            {
                Id = pairedDeviceId,
                OperatorId = operatorId,
                DeviceName = "Test Device",
                Role = OperatorRole.SuperAdmin,
                PairedAtUtc = DateTime.UtcNow
            };
            _ = repo.Setup(r => r.GetAsync(pairedDeviceId, It.IsAny<CancellationToken>())).ReturnsAsync(device);

            var op = new Operator
            {
                Id = operatorId,
                DisplayName = "Test Operator",
                CreatedAtUtc = DateTime.UtcNow,
                Active = true,
                IsApproved = true,
                Username = $"test-operator-{operatorId:N}",
                FirstName = "Test",
                LastName = "Operator",
                Email = $"{operatorId:N}@test.invalid",
                SecurityStamp = securityStamp
            };
            _ = operatorRepository.Setup(o => o.GetAsync(operatorId, It.IsAny<CancellationToken>())).ReturnsAsync(op);

            return (operatorId, pairedDeviceId, tokenService, repo, operatorRepository);
        }

        private static NetworkTier ClaimedTier(AuthenticateResult result)
        {
            string? value = result.Ticket?.Principal.FindFirst(VideoForensicsClaimTypes.NetworkTier)?.Value;
            Assert.NotNull(value);
            return Enum.Parse<NetworkTier>(value);
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
            var securityStamp = Guid.NewGuid();
            var tokenService = new Mock<ISessionTokenService>();
            var repo = new Mock<IPairedDeviceRepository>();
            var tierResolver = new Mock<INetworkTierResolver>();
            var operatorRepository = new Mock<IOperatorRepository>();

            var principal = new SessionPrincipal(
                operatorId, pairedDeviceId, CredentialKind.ServiceDevice, OperatorRole.Admin, securityStamp, DateTime.UtcNow, DateTime.UtcNow.AddHours(12));
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
                IsApproved = true,
                Username = $"test-operator-{operatorId:N}",
                FirstName = "Test",
                LastName = "Operator",
                Email = $"{operatorId:N}@test.invalid",
                SecurityStamp = securityStamp
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
            var securityStamp = Guid.NewGuid();
            var tokenService = new Mock<ISessionTokenService>();
            var repo = new Mock<IPairedDeviceRepository>();
            var tierResolver = new Mock<INetworkTierResolver>();
            var operatorRepository = new Mock<IOperatorRepository>();

            var principal = new SessionPrincipal(
                operatorId, pairedDeviceId, CredentialKind.ServiceDevice, OperatorRole.Admin, securityStamp, DateTime.UtcNow, DateTime.UtcNow.AddHours(12));
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

            var op = new Operator
            {
                Id = operatorId,
                DisplayName = "Test Operator",
                CreatedAtUtc = DateTime.UtcNow,
                Active = true,
                IsApproved = true,
                Username = $"test-operator-{operatorId:N}",
                FirstName = "Test",
                LastName = "Operator",
                Email = $"{operatorId:N}@test.invalid",
                SecurityStamp = securityStamp
            };
            _ = operatorRepository.Setup(o => o.GetAsync(operatorId, It.IsAny<CancellationToken>())).ReturnsAsync(op);

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
            var securityStamp = Guid.NewGuid();
            var tokenService = new Mock<ISessionTokenService>();
            var repo = new Mock<IPairedDeviceRepository>();
            var tierResolver = new Mock<INetworkTierResolver>();
            var operatorRepository = new Mock<IOperatorRepository>();

            var principal = new SessionPrincipal(
                operatorId, pairedDeviceId, CredentialKind.ServiceDevice, OperatorRole.Admin, securityStamp, DateTime.UtcNow, DateTime.UtcNow.AddHours(12));
            _ = tokenService.Setup(t => t.Validate("unknown-token")).Returns(principal);
            _ = repo.Setup(r => r.GetAsync(pairedDeviceId, It.IsAny<CancellationToken>())).ReturnsAsync((PairedDevice?)null);

            var op = new Operator
            {
                Id = operatorId,
                DisplayName = "Test Operator",
                CreatedAtUtc = DateTime.UtcNow,
                Active = true,
                IsApproved = true,
                Username = $"test-operator-{operatorId:N}",
                FirstName = "Test",
                LastName = "Operator",
                Email = $"{operatorId:N}@test.invalid",
                SecurityStamp = securityStamp
            };
            _ = operatorRepository.Setup(o => o.GetAsync(operatorId, It.IsAny<CancellationToken>())).ReturnsAsync(op);

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
            var securityStamp = Guid.NewGuid();
            var tokenService = new Mock<ISessionTokenService>();
            var repo = new Mock<IPairedDeviceRepository>();
            var tierResolver = new Mock<INetworkTierResolver>();
            var operatorRepository = new Mock<IOperatorRepository>();

            var principal = new SessionPrincipal(
                operatorId, pairedDeviceId, CredentialKind.ServiceDevice, OperatorRole.SuperAdmin, securityStamp, DateTime.UtcNow, DateTime.UtcNow.AddHours(12));
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
                IsApproved = true,
                Username = $"test-operator-{operatorId:N}",
                FirstName = "Test",
                LastName = "Operator",
                Email = $"{operatorId:N}@test.invalid",
                SecurityStamp = securityStamp
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

        // --- Session-tier header (X-VF-Session-Tier) tests: PairedDeviceAuthenticationHandler must
        // recover the calling Blazor circuit's REAL network tier from this header for a self-HTTP
        // call, rather than trusting the self-call's own (always-loopback) connection. See
        // SelfHttpServiceExtensions/SessionTierHeaderProtector for the sender side. ---

        [Fact]
        public async Task HandleAuthenticateAsync_InternetTierHeaderOnLoopbackRequest_OverridesLocalToInternet()
        {
            // Arrange
            const string token = "loopback-with-internet-header";
            (Guid operatorId, _, Mock<ISessionTokenService> tokenService, Mock<IPairedDeviceRepository> repo, Mock<IOperatorRepository> operatorRepository) = SetUpValidServiceDeviceSession(token);
            var tierResolver = new Mock<INetworkTierResolver>();
            _ = tierResolver.Setup(t => t.ResolveTier(It.IsAny<HttpContext>())).Returns(NetworkTier.Local);

            var headerProtector = new Mock<ISessionTierHeaderProtector>();
            NetworkTier outTier = NetworkTier.Internet;
            Guid outOperatorId = operatorId;
            _ = headerProtector.Setup(p => p.TryUnprotect("protected-header", out outTier, out outOperatorId)).Returns(true);

            PairedDeviceAuthenticationHandler handler = CreateHandler(tokenService, repo, tierResolver, operatorRepository, headerProtector: headerProtector);
            HttpContext context = CreateHttpContextWithAuthorizationHeaderAndTierHeader(token, "protected-header");

            // Act
            AuthenticateResult result = await AuthenticateAsync(handler, PairedDeviceAuthenticationDefaults.SchemeName, context);

            // Assert - even though the request itself arrived over loopback, the session's real
            // (more restrictive) Internet tier from the header wins, so SuperAdminLocal is denied.
            Assert.True(result.Succeeded);
            Assert.Equal(NetworkTier.Internet, ClaimedTier(result));
        }

        [Fact]
        public async Task HandleAuthenticateAsync_ValidLocalTierHeaderOnLoopbackRequest_ResolvesLocal()
        {
            // Arrange
            const string token = "loopback-with-local-header";
            (Guid operatorId, _, Mock<ISessionTokenService> tokenService, Mock<IPairedDeviceRepository> repo, Mock<IOperatorRepository> operatorRepository) = SetUpValidServiceDeviceSession(token);
            var tierResolver = new Mock<INetworkTierResolver>();
            _ = tierResolver.Setup(t => t.ResolveTier(It.IsAny<HttpContext>())).Returns(NetworkTier.Local);

            var headerProtector = new Mock<ISessionTierHeaderProtector>();
            NetworkTier outTier = NetworkTier.Local;
            Guid outOperatorId = operatorId;
            _ = headerProtector.Setup(p => p.TryUnprotect("protected-header", out outTier, out outOperatorId)).Returns(true);

            PairedDeviceAuthenticationHandler handler = CreateHandler(tokenService, repo, tierResolver, operatorRepository, headerProtector: headerProtector);
            HttpContext context = CreateHttpContextWithAuthorizationHeaderAndTierHeader(token, "protected-header");

            // Act
            AuthenticateResult result = await AuthenticateAsync(handler, PairedDeviceAuthenticationDefaults.SchemeName, context);

            // Assert
            Assert.True(result.Succeeded);
            Assert.Equal(NetworkTier.Local, ClaimedTier(result));
        }

        [Fact]
        public async Task HandleAuthenticateAsync_ExpiredTierHeaderOnLoopbackRequest_FailsSafeToInternet()
        {
            // Arrange
            const string token = "loopback-with-expired-header";
            (Guid operatorId, _, Mock<ISessionTokenService> tokenService, Mock<IPairedDeviceRepository> repo, Mock<IOperatorRepository> operatorRepository) = SetUpValidServiceDeviceSession(token);
            var tierResolver = new Mock<INetworkTierResolver>();
            _ = tierResolver.Setup(t => t.ResolveTier(It.IsAny<HttpContext>())).Returns(NetworkTier.Local);

            var headerProtector = new Mock<ISessionTierHeaderProtector>();
            NetworkTier outTier = default;
            Guid outOperatorId = default;
            // Expired header: the protector itself reports it as unrecoverable (see
            // SessionTierHeaderProtectorTests for the expiry check itself).
            _ = headerProtector.Setup(p => p.TryUnprotect("expired-header", out outTier, out outOperatorId)).Returns(false);

            PairedDeviceAuthenticationHandler handler = CreateHandler(tokenService, repo, tierResolver, operatorRepository, headerProtector: headerProtector);
            HttpContext context = CreateHttpContextWithAuthorizationHeaderAndTierHeader(token, "expired-header");

            // Act
            AuthenticateResult result = await AuthenticateAsync(handler, PairedDeviceAuthenticationDefaults.SchemeName, context);

            // Assert
            Assert.True(result.Succeeded);
            Assert.Equal(NetworkTier.Internet, ClaimedTier(result));
        }

        [Fact]
        public async Task HandleAuthenticateAsync_TamperedTierHeaderOnLoopbackRequest_FailsSafeToInternet()
        {
            // Arrange
            const string token = "loopback-with-tampered-header";
            (Guid operatorId, _, Mock<ISessionTokenService> tokenService, Mock<IPairedDeviceRepository> repo, Mock<IOperatorRepository> operatorRepository) = SetUpValidServiceDeviceSession(token);
            var tierResolver = new Mock<INetworkTierResolver>();
            _ = tierResolver.Setup(t => t.ResolveTier(It.IsAny<HttpContext>())).Returns(NetworkTier.Local);

            var headerProtector = new Mock<ISessionTierHeaderProtector>();
            NetworkTier outTier = default;
            Guid outOperatorId = default;
            _ = headerProtector.Setup(p => p.TryUnprotect("tampered-header", out outTier, out outOperatorId)).Returns(false);

            PairedDeviceAuthenticationHandler handler = CreateHandler(tokenService, repo, tierResolver, operatorRepository, headerProtector: headerProtector);
            HttpContext context = CreateHttpContextWithAuthorizationHeaderAndTierHeader(token, "tampered-header");

            // Act
            AuthenticateResult result = await AuthenticateAsync(handler, PairedDeviceAuthenticationDefaults.SchemeName, context);

            // Assert
            Assert.True(result.Succeeded);
            Assert.Equal(NetworkTier.Internet, ClaimedTier(result));
        }

        [Fact]
        public async Task HandleAuthenticateAsync_TierHeaderOperatorMismatchOnLoopbackRequest_FailsSafeToInternet()
        {
            // Arrange
            const string token = "loopback-with-mismatched-operator-header";
            (Guid operatorId, _, Mock<ISessionTokenService> tokenService, Mock<IPairedDeviceRepository> repo, Mock<IOperatorRepository> operatorRepository) = SetUpValidServiceDeviceSession(token);
            var tierResolver = new Mock<INetworkTierResolver>();
            _ = tierResolver.Setup(t => t.ResolveTier(It.IsAny<HttpContext>())).Returns(NetworkTier.Local);

            var headerProtector = new Mock<ISessionTierHeaderProtector>();
            NetworkTier outTier = NetworkTier.Local;
            Guid outOperatorId = Guid.NewGuid(); // A DIFFERENT operator than the authenticated one.
            _ = headerProtector.Setup(p => p.TryUnprotect("mismatched-header", out outTier, out outOperatorId)).Returns(true);

            PairedDeviceAuthenticationHandler handler = CreateHandler(tokenService, repo, tierResolver, operatorRepository, headerProtector: headerProtector);
            HttpContext context = CreateHttpContextWithAuthorizationHeaderAndTierHeader(token, "mismatched-header");

            // Act
            AuthenticateResult result = await AuthenticateAsync(handler, PairedDeviceAuthenticationDefaults.SchemeName, context);

            // Assert - the header decrypted fine but names a DIFFERENT operator than the one this
            // request authenticated as; a header cannot be replayed across operators.
            Assert.True(result.Succeeded);
            Assert.Equal(NetworkTier.Internet, ClaimedTier(result));
        }

        [Fact]
        public async Task HandleAuthenticateAsync_LocalTierHeaderOnNonLoopbackRequest_IgnoredResolvesByIp()
        {
            // Arrange - a genuinely remote caller cannot use the header to claim Local: the
            // connection itself is not loopback, so the header must be ignored entirely and the
            // tier resolved from the real connection, same as if no header were sent at all.
            const string token = "remote-with-local-header";
            (Guid operatorId, _, Mock<ISessionTokenService> tokenService, Mock<IPairedDeviceRepository> repo, Mock<IOperatorRepository> operatorRepository) = SetUpValidServiceDeviceSession(token);
            var tierResolver = new Mock<INetworkTierResolver>();
            _ = tierResolver.Setup(t => t.ResolveTier(It.IsAny<HttpContext>())).Returns(NetworkTier.Network);

            var headerProtector = new Mock<ISessionTierHeaderProtector>();
            NetworkTier outTier = NetworkTier.Local;
            Guid outOperatorId = operatorId;
            _ = headerProtector.Setup(p => p.TryUnprotect(It.IsAny<string>(), out outTier, out outOperatorId)).Returns(true);

            PairedDeviceAuthenticationHandler handler = CreateHandler(tokenService, repo, tierResolver, operatorRepository, headerProtector: headerProtector);
            HttpContext context = CreateHttpContextWithAuthorizationHeaderAndTierHeader(token, "some-header-value");

            // Act
            AuthenticateResult result = await AuthenticateAsync(handler, PairedDeviceAuthenticationDefaults.SchemeName, context);

            // Assert
            Assert.True(result.Succeeded);
            Assert.Equal(NetworkTier.Network, ClaimedTier(result));
            headerProtector.Verify(p => p.TryUnprotect(It.IsAny<string>(), out outTier, out outOperatorId), Times.Never);
        }

        [Fact]
        public async Task HandleAuthenticateAsync_LoopbackWithoutTierHeader_UnchangedBehavior()
        {
            // Arrange - no X-VF-Session-Tier header at all: behavior must be identical to before
            // this feature existed (local tools/MAUI on the same machine legitimately use loopback
            // with no such header).
            const string token = "loopback-no-header";
            (Guid operatorId, _, Mock<ISessionTokenService> tokenService, Mock<IPairedDeviceRepository> repo, Mock<IOperatorRepository> operatorRepository) = SetUpValidServiceDeviceSession(token);
            var tierResolver = new Mock<INetworkTierResolver>();
            _ = tierResolver.Setup(t => t.ResolveTier(It.IsAny<HttpContext>())).Returns(NetworkTier.Local);
            var headerProtector = new Mock<ISessionTierHeaderProtector>();

            PairedDeviceAuthenticationHandler handler = CreateHandler(tokenService, repo, tierResolver, operatorRepository, headerProtector: headerProtector);
            HttpContext context = CreateHttpContextWithAuthorizationHeader(token);

            // Act
            AuthenticateResult result = await AuthenticateAsync(handler, PairedDeviceAuthenticationDefaults.SchemeName, context);

            // Assert
            Assert.True(result.Succeeded);
            Assert.Equal(NetworkTier.Local, ClaimedTier(result));
            headerProtector.Verify(p => p.TryUnprotect(It.IsAny<string>(), out It.Ref<NetworkTier>.IsAny, out It.Ref<Guid>.IsAny), Times.Never);
        }
    }
}
