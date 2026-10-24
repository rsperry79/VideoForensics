using Microsoft.AspNetCore.Http;

using Moq;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.WebApp.Api;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Client.Hosting;

using Xunit;

namespace VideoForensics.Client.WebApp.Tests.Api
{
    public class SoftwarePathsEndpointsTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "vf-softpaths-" + Guid.NewGuid().ToString("N"));
        private static string Ext => OperatingSystem.IsWindows() ? ".exe" : string.Empty;

        public SoftwarePathsEndpointsTests()
        {
            _ = Directory.CreateDirectory(_root);
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }

        private string CreateBinDirectory()
        {
            string dir = Path.Combine(_root, "bin-" + Guid.NewGuid().ToString("N"));
            _ = Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "ffmpeg" + Ext), "dummy");
            File.WriteAllText(Path.Combine(dir, "ffprobe" + Ext), "dummy");
            return dir;
        }

        private static (ForensicsConfiguration Config, Mock<IForensicsConfigurationService> ConfigService, Mock<ISecurityAuditLogger> AuditLog) CreateMocks(string? ffmpegDir = null)
        {
            var config = new ForensicsConfiguration { FfmpegBinDirectory = ffmpegDir };

            var configService = new Mock<IForensicsConfigurationService>();
            _ = configService.Setup(cs => cs.SaveConfigurationAsync(It.IsAny<IForensicsConfiguration>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var auditLog = new Mock<ISecurityAuditLogger>();
            _ = auditLog.Setup(al => al.LogAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            return (config, configService, auditLog);
        }

        private static Task<IResult> InvokeAsync(
            SetSoftwarePathsRequest request,
            ForensicsConfiguration config,
            Mock<IForensicsConfigurationService> configService,
            Mock<ISecurityAuditLogger> auditLog)
        {
            var tierResolver = new Mock<INetworkTierResolver>();
            _ = tierResolver.Setup(t => t.ResolveClientIp(It.IsAny<HttpContext>())).Returns("127.0.0.1");

            return SoftwarePathsEndpoints.SetSoftwarePathsAsync(
                request,
                config,
                configService.Object,
                auditLog.Object,
                tierResolver.Object,
                new DefaultHttpContext(),
                CancellationToken.None);
        }

        [Fact]
        public async Task SetSoftwarePathsAsync_NonexistentFfmpegDirectory_ReturnsBadRequestAndChangesNothing()
        {
            (ForensicsConfiguration? config, Mock<IForensicsConfigurationService>? configService, Mock<ISecurityAuditLogger>? auditLog) = CreateMocks(ffmpegDir: "C:\\old\\bin");
            string missing = Path.Combine(_root, "does-not-exist");

            IResult result = await InvokeAsync(new SetSoftwarePathsRequest(missing), config, configService, auditLog);

            IStatusCodeHttpResult statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, statusResult.StatusCode);
            Assert.Equal("C:\\old\\bin", config.FfmpegBinDirectory);
            configService.Verify(cs => cs.SaveConfigurationAsync(It.IsAny<IForensicsConfiguration>(), It.IsAny<CancellationToken>()), Times.Never);
            auditLog.Verify(al => al.LogAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task SetSoftwarePathsAsync_ValidPath_SavesAndAudits()
        {
            (ForensicsConfiguration? config, Mock<IForensicsConfigurationService>? configService, Mock<ISecurityAuditLogger>? auditLog) = CreateMocks(ffmpegDir: "C:\\old\\bin");
            string binDir = CreateBinDirectory();

            IResult result = await InvokeAsync(new SetSoftwarePathsRequest(binDir), config, configService, auditLog);

            IStatusCodeHttpResult statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
            Assert.Equal(StatusCodes.Status200OK, statusResult.StatusCode);
            Assert.Equal(binDir, config.FfmpegBinDirectory);
            configService.Verify(cs => cs.SaveConfigurationAsync(config, It.IsAny<CancellationToken>()), Times.Once);
            auditLog.Verify(al => al.LogAsync(
                SecurityAuditEventTypes.SoftwarePathsChanged,
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<string>(),
                It.Is<string>(d => d.Contains("C:\\old\\bin") && d.Contains(binDir)),
                true,
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SetSoftwarePathsAsync_EmptyValue_ClearsSetting()
        {
            (ForensicsConfiguration? config, Mock<IForensicsConfigurationService>? configService, Mock<ISecurityAuditLogger>? auditLog) = CreateMocks(ffmpegDir: "C:\\old\\bin");

            IResult result = await InvokeAsync(new SetSoftwarePathsRequest(string.Empty), config, configService, auditLog);

            IStatusCodeHttpResult statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
            Assert.Equal(StatusCodes.Status200OK, statusResult.StatusCode);
            Assert.True(string.IsNullOrEmpty(config.FfmpegBinDirectory));
            configService.Verify(cs => cs.SaveConfigurationAsync(config, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SetSoftwarePathsAsync_SaveFails_RestoresPreviousValuesAndRethrows()
        {
            (ForensicsConfiguration? config, Mock<IForensicsConfigurationService>? configService, Mock<ISecurityAuditLogger>? auditLog) = CreateMocks(ffmpegDir: @"C:\old\bin");
            string binDir = CreateBinDirectory();
            _ = configService.Setup(cs => cs.SaveConfigurationAsync(It.IsAny<IForensicsConfiguration>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new IOException("disk full"));

            _ = await Assert.ThrowsAsync<IOException>(() =>
                InvokeAsync(new SetSoftwarePathsRequest(binDir), config, configService, auditLog));

            // The shared config must not keep a value that was never persisted.
            Assert.Equal(@"C:\old\bin", config.FfmpegBinDirectory);
            auditLog.Verify(al => al.LogAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
