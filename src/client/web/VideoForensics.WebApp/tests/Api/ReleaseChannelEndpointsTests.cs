using Microsoft.AspNetCore.Http;

using Moq;

using System.Security.Claims;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.WebApp.Api;
using VideoForensics.Client.WebApp.Auth;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Client.Hosting;

using Xunit;

namespace VideoForensics.Client.WebApp.Tests
{
    public class ReleaseChannelEndpointsTests
    {
        private static (Mock<IForensicsConfiguration> Config, Mock<IForensicsConfigurationService> ConfigService, Mock<ISecurityAuditLogger> AuditLog, Mock<INetworkTierResolver> TierResolver) CreateMocks(UpdateReleaseChannel currentChannel = UpdateReleaseChannel.Stable)
        {
            var config = new Mock<IForensicsConfiguration>();
            _ = config.SetupProperty(c => c.ReleaseChannel, currentChannel);

            var configService = new Mock<IForensicsConfigurationService>();
            _ = configService.Setup(cs => cs.SaveConfigurationAsync(It.IsAny<IForensicsConfiguration>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var auditLog = new Mock<ISecurityAuditLogger>();
            _ = auditLog.Setup(al => al.LogAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var tierResolver = new Mock<INetworkTierResolver>();
            _ = tierResolver.Setup(t => t.ResolveClientIp(It.IsAny<HttpContext>())).Returns("127.0.0.1");

            return (config, configService, auditLog, tierResolver);
        }

        private static DefaultHttpContext CreateOperatorContext()
        {
            var context = new DefaultHttpContext();
            Claim[] claims = new[] { new Claim(VideoForensicsClaimTypes.OperatorId, Guid.NewGuid().ToString()) };
            context.User = new ClaimsPrincipal(new ClaimsIdentity(claims));
            return context;
        }

        [Fact]
        public async Task SetReleaseChannelAsync_ValidTesting_SavesConfigAndChangesChannel()
        {
            (Mock<IForensicsConfiguration>? config, Mock<IForensicsConfigurationService>? configService, Mock<ISecurityAuditLogger>? auditLog, Mock<INetworkTierResolver>? tierResolver) = CreateMocks(UpdateReleaseChannel.Stable);
            var request = new SetReleaseChannelRequest("Testing");

            IResult result = await ReleaseChannelEndpoints.SetReleaseChannelAsync(
                request, config.Object, configService.Object, auditLog.Object, tierResolver.Object, CreateOperatorContext(), CancellationToken.None);

            IStatusCodeHttpResult statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
            Assert.Equal(StatusCodes.Status200OK, statusResult.StatusCode);
            Assert.Equal(UpdateReleaseChannel.Testing, config.Object.ReleaseChannel);
            configService.Verify(cs => cs.SaveConfigurationAsync(config.Object, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SetReleaseChannelAsync_ValidTesting_WritesUrgentAuditEntry()
        {
            (Mock<IForensicsConfiguration>? config, Mock<IForensicsConfigurationService>? configService, Mock<ISecurityAuditLogger>? auditLog, Mock<INetworkTierResolver>? tierResolver) = CreateMocks(UpdateReleaseChannel.Stable);
            var request = new SetReleaseChannelRequest("Testing");

            _ = await ReleaseChannelEndpoints.SetReleaseChannelAsync(
                request, config.Object, configService.Object, auditLog.Object, tierResolver.Object, CreateOperatorContext(), CancellationToken.None);

            auditLog.Verify(al => al.LogAsync(
                SecurityAuditEventTypes.ReleaseChannelChanged,
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                "127.0.0.1",
                It.IsAny<string>(),
                true,
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SetReleaseChannelAsync_InvalidChannelString_ReturnsBadRequest()
        {
            (Mock<IForensicsConfiguration>? config, Mock<IForensicsConfigurationService>? configService, Mock<ISecurityAuditLogger>? auditLog, Mock<INetworkTierResolver>? tierResolver) = CreateMocks(UpdateReleaseChannel.Stable);
            var request = new SetReleaseChannelRequest("Nightly");

            IResult result = await ReleaseChannelEndpoints.SetReleaseChannelAsync(
                request, config.Object, configService.Object, auditLog.Object, tierResolver.Object, CreateOperatorContext(), CancellationToken.None);

            IStatusCodeHttpResult statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, statusResult.StatusCode);
        }

        [Fact]
        public async Task SetReleaseChannelAsync_InvalidChannelString_DoesNotSaveOrAudit()
        {
            (Mock<IForensicsConfiguration>? config, Mock<IForensicsConfigurationService>? configService, Mock<ISecurityAuditLogger>? auditLog, Mock<INetworkTierResolver>? tierResolver) = CreateMocks(UpdateReleaseChannel.Stable);
            var request = new SetReleaseChannelRequest("Nightly");

            _ = await ReleaseChannelEndpoints.SetReleaseChannelAsync(
                request, config.Object, configService.Object, auditLog.Object, tierResolver.Object, CreateOperatorContext(), CancellationToken.None);

            Assert.Equal(UpdateReleaseChannel.Stable, config.Object.ReleaseChannel);
            configService.Verify(cs => cs.SaveConfigurationAsync(It.IsAny<IForensicsConfiguration>(), It.IsAny<CancellationToken>()), Times.Never);
            auditLog.Verify(al => al.LogAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task SetReleaseChannelAsync_NumericEnumValue_ReturnsBadRequest()
        {
            // Enum.TryParse accepts numeric strings such as "5" even when they are not defined members;
            // the endpoint must reject them rather than store an undefined channel.
            (Mock<IForensicsConfiguration>? config, Mock<IForensicsConfigurationService>? configService, Mock<ISecurityAuditLogger>? auditLog, Mock<INetworkTierResolver>? tierResolver) = CreateMocks(UpdateReleaseChannel.Stable);
            var request = new SetReleaseChannelRequest("5");

            IResult result = await ReleaseChannelEndpoints.SetReleaseChannelAsync(
                request, config.Object, configService.Object, auditLog.Object, tierResolver.Object, CreateOperatorContext(), CancellationToken.None);

            IStatusCodeHttpResult statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, statusResult.StatusCode);
            configService.Verify(cs => cs.SaveConfigurationAsync(It.IsAny<IForensicsConfiguration>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task SetReleaseChannelAsync_LowercaseTesting_IsAccepted()
        {
            (Mock<IForensicsConfiguration>? config, Mock<IForensicsConfigurationService>? configService, Mock<ISecurityAuditLogger>? auditLog, Mock<INetworkTierResolver>? tierResolver) = CreateMocks(UpdateReleaseChannel.Stable);
            var request = new SetReleaseChannelRequest("testing");

            IResult result = await ReleaseChannelEndpoints.SetReleaseChannelAsync(
                request, config.Object, configService.Object, auditLog.Object, tierResolver.Object, CreateOperatorContext(), CancellationToken.None);

            IStatusCodeHttpResult statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
            Assert.Equal(StatusCodes.Status200OK, statusResult.StatusCode);
            Assert.Equal(UpdateReleaseChannel.Testing, config.Object.ReleaseChannel);
        }

        [Fact]
        public async Task SetReleaseChannelAsync_ChangeFromTestingToStable_AuditDetailsContainOldAndNewValues()
        {
            (Mock<IForensicsConfiguration>? config, Mock<IForensicsConfigurationService>? configService, Mock<ISecurityAuditLogger>? auditLog, Mock<INetworkTierResolver>? tierResolver) = CreateMocks(UpdateReleaseChannel.Testing);
            var request = new SetReleaseChannelRequest("Stable");

            _ = await ReleaseChannelEndpoints.SetReleaseChannelAsync(
                request, config.Object, configService.Object, auditLog.Object, tierResolver.Object, CreateOperatorContext(), CancellationToken.None);

            auditLog.Verify(al => al.LogAsync(
                SecurityAuditEventTypes.ReleaseChannelChanged,
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<string>(),
                "Release channel changed from 'Testing' to 'Stable'",
                true,
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SetReleaseChannelAsync_UnchangedValue_StillSucceedsAndSaves()
        {
            (Mock<IForensicsConfiguration>? config, Mock<IForensicsConfigurationService>? configService, Mock<ISecurityAuditLogger>? auditLog, Mock<INetworkTierResolver>? tierResolver) = CreateMocks(UpdateReleaseChannel.Stable);
            var request = new SetReleaseChannelRequest("Stable");

            IResult result = await ReleaseChannelEndpoints.SetReleaseChannelAsync(
                request, config.Object, configService.Object, auditLog.Object, tierResolver.Object, CreateOperatorContext(), CancellationToken.None);

            IStatusCodeHttpResult statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
            Assert.Equal(StatusCodes.Status200OK, statusResult.StatusCode);
            Assert.Equal(UpdateReleaseChannel.Stable, config.Object.ReleaseChannel);
            configService.Verify(cs => cs.SaveConfigurationAsync(config.Object, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public void GetReleaseChannel_TestingConfigured_ReturnsTestingName()
        {
            (Mock<IForensicsConfiguration>? config, _, _, _) = CreateMocks(UpdateReleaseChannel.Testing);

            ReleaseChannelDto dto = ReleaseChannelEndpoints.GetReleaseChannel(config.Object);

            Assert.Equal("Testing", dto.Channel);
        }
    }
}
