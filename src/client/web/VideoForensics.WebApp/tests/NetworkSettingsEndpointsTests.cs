using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

using Moq;

using System.Security.Claims;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting;
using VideoForensics.WebApp.Api;
using VideoForensics.WebApp.Auth;

using Xunit;

namespace VideoForensics.WebApp.Tests
{
    public class NetworkSettingsEndpointsTests
    {
        private static (Mock<IForensicsConfiguration> Config, Mock<IForensicsConfigurationService> ConfigService, Mock<IStepUpAuthService> StepUpAuth, Mock<ISecurityAuditLogger> AuditLog, Mock<INetworkTierResolver> TierResolver, Mock<IFirewallRuleManager> FirewallRuleManager, Mock<IConfiguration> Configuration) CreateMocks(NetworkTier currentTier = NetworkTier.Local)
        {
            var config = new Mock<IForensicsConfiguration>();
            _ = config.Setup(c => c.ConfiguredNetworkTier).Returns(currentTier);
            _ = config.SetupProperty(c => c.ConfiguredNetworkTier);

            var configService = new Mock<IForensicsConfigurationService>();
            _ = configService.Setup(cs => cs.SaveConfigurationAsync(It.IsAny<IForensicsConfiguration>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var stepUpAuth = new Mock<IStepUpAuthService>();
            _ = stepUpAuth.Setup(s => s.Validate(It.IsAny<string>(), It.IsAny<Guid>())).Returns(true);

            var auditLog = new Mock<ISecurityAuditLogger>();
            _ = auditLog.Setup(al => al.LogAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var tierResolver = new Mock<INetworkTierResolver>();
            _ = tierResolver.Setup(t => t.ResolveClientIp(It.IsAny<HttpContext>())).Returns("127.0.0.1");

            var firewallRuleManager = new Mock<IFirewallRuleManager>();
            _ = firewallRuleManager.Setup(f => f.EnsureRuleExistsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _ = firewallRuleManager.Setup(f => f.RemoveRuleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var configuration = new Mock<IConfiguration>();
            _ = configuration.Setup(c => c["ASPNETCORE_URLS"]).Returns("https://localhost:5162");

            return (config, configService, stepUpAuth, auditLog, tierResolver, firewallRuleManager, configuration);
        }

        private static DefaultHttpContext CreateHttpContextWithStepUpToken(Guid pairedDeviceId, string stepUpToken = "valid-step-up-token")
        {
            var context = new DefaultHttpContext();
            var claims = new[]
            {
                new Claim(VideoForensicsClaimTypes.PairedDeviceId, pairedDeviceId.ToString()),
                new Claim(VideoForensicsClaimTypes.OperatorId, Guid.NewGuid().ToString())
            };
            var identity = new ClaimsIdentity(claims);
            context.User = new ClaimsPrincipal(identity);
            context.Request.Headers["X-StepUp-Token"] = new StringValues(stepUpToken);
            return context;
        }

        [Fact]
        public async Task SetNetworkTierAsync_WidenToNetwork_CallsEnsureRuleExists()
        {
            (var config, var configService, var stepUpAuth, var auditLog, var tierResolver, var firewallRuleManager, var configuration) = CreateMocks(currentTier: NetworkTier.Local);
            var request = new SetNetworkTierRequest(NetworkTier.Network);
            Guid pairedDeviceId = Guid.NewGuid();
            var context = CreateHttpContextWithStepUpToken(pairedDeviceId);

            IResult result = await NetworkSettingsEndpoints.SetNetworkTierAsync(
                request, config.Object, configService.Object, stepUpAuth.Object, auditLog.Object, tierResolver.Object, firewallRuleManager.Object, configuration.Object, context, CancellationToken.None);

            var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
            Assert.Equal(StatusCodes.Status200OK, statusResult.StatusCode);

            // Verify firewall rule was added when widening
            firewallRuleManager.Verify(f => f.EnsureRuleExistsAsync("VideoForensics", 5162, It.IsAny<CancellationToken>()), Times.Once);
            firewallRuleManager.Verify(f => f.RemoveRuleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task SetNetworkTierAsync_NarrowToLocal_CallsRemoveRule()
        {
            (var config, var configService, var stepUpAuth, var auditLog, var tierResolver, var firewallRuleManager, var configuration) = CreateMocks(currentTier: NetworkTier.Network);
            var request = new SetNetworkTierRequest(NetworkTier.Local);
            Guid pairedDeviceId = Guid.NewGuid();
            var context = CreateHttpContextWithStepUpToken(pairedDeviceId);

            IResult result = await NetworkSettingsEndpoints.SetNetworkTierAsync(
                request, config.Object, configService.Object, stepUpAuth.Object, auditLog.Object, tierResolver.Object, firewallRuleManager.Object, configuration.Object, context, CancellationToken.None);

            var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
            Assert.Equal(StatusCodes.Status200OK, statusResult.StatusCode);

            // Verify firewall rule was removed when narrowing
            firewallRuleManager.Verify(f => f.RemoveRuleAsync("VideoForensics", It.IsAny<CancellationToken>()), Times.Once);
            firewallRuleManager.Verify(f => f.EnsureRuleExistsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task SetNetworkTierAsync_WidenToInternet_CallsEnsureRuleExists()
        {
            (var config, var configService, var stepUpAuth, var auditLog, var tierResolver, var firewallRuleManager, var configuration) = CreateMocks(currentTier: NetworkTier.Network);
            var request = new SetNetworkTierRequest(NetworkTier.Internet);
            Guid pairedDeviceId = Guid.NewGuid();
            var context = CreateHttpContextWithStepUpToken(pairedDeviceId);

            IResult result = await NetworkSettingsEndpoints.SetNetworkTierAsync(
                request, config.Object, configService.Object, stepUpAuth.Object, auditLog.Object, tierResolver.Object, firewallRuleManager.Object, configuration.Object, context, CancellationToken.None);

            var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
            Assert.Equal(StatusCodes.Status200OK, statusResult.StatusCode);

            // Internet is still listening on all interfaces like Network, so rule should be ensured
            firewallRuleManager.Verify(f => f.EnsureRuleExistsAsync("VideoForensics", 5162, It.IsAny<CancellationToken>()), Times.Once);
            firewallRuleManager.Verify(f => f.RemoveRuleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task SetNetworkTierAsync_WidenWithoutStepUpToken_ReturnsForbidden()
        {
            (var config, var configService, var stepUpAuth, var auditLog, var tierResolver, var firewallRuleManager, var configuration) = CreateMocks(currentTier: NetworkTier.Local);
            var request = new SetNetworkTierRequest(NetworkTier.Network);
            Guid pairedDeviceId = Guid.NewGuid();

            var context = new DefaultHttpContext();
            var claims = new[]
            {
                new Claim(VideoForensicsClaimTypes.PairedDeviceId, pairedDeviceId.ToString()),
                new Claim(VideoForensicsClaimTypes.OperatorId, Guid.NewGuid().ToString())
            };
            var identity = new ClaimsIdentity(claims);
            context.User = new ClaimsPrincipal(identity);
            // No X-StepUp-Token header

            IResult result = await NetworkSettingsEndpoints.SetNetworkTierAsync(
                request, config.Object, configService.Object, stepUpAuth.Object, auditLog.Object, tierResolver.Object, firewallRuleManager.Object, configuration.Object, context, CancellationToken.None);

            var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
            Assert.Equal(StatusCodes.Status403Forbidden, statusResult.StatusCode);

            // Firewall rule should not be touched if step-up auth fails
            firewallRuleManager.Verify(f => f.EnsureRuleExistsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
            firewallRuleManager.Verify(f => f.RemoveRuleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
