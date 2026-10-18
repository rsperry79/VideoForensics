using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

using Moq;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting;
using VideoForensics.WebApp.Api;

using Xunit;

namespace VideoForensics.WebApp.Tests
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
            var dir = Path.Combine(_root, "bin-" + Guid.NewGuid().ToString("N"));
            _ = Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "ffmpeg" + Ext), "dummy");
            File.WriteAllText(Path.Combine(dir, "ffprobe" + Ext), "dummy");
            return dir;
        }

        private string CreateCloudflaredFile()
        {
            var path = Path.Combine(_root, "cloudflared" + Ext);
            File.WriteAllText(path, "dummy");
            return path;
        }

        private static (ForensicsConfiguration Config, Mock<IForensicsConfigurationService> ConfigService, Mock<ISecurityAuditLogger> AuditLog) CreateMocks(string? ffmpegDir = null, string? cloudflaredPath = null)
        {
            var config = new ForensicsConfiguration { FfmpegBinDirectory = ffmpegDir, CloudflaredPath = cloudflaredPath };

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
            var (config, configService, auditLog) = CreateMocks(ffmpegDir: "C:\\old\\bin");
            var missing = Path.Combine(_root, "does-not-exist");

            IResult result = await InvokeAsync(new SetSoftwarePathsRequest(missing, null), config, configService, auditLog);

            var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, statusResult.StatusCode);
            Assert.Equal("C:\\old\\bin", config.FfmpegBinDirectory);
            configService.Verify(cs => cs.SaveConfigurationAsync(It.IsAny<IForensicsConfiguration>(), It.IsAny<CancellationToken>()), Times.Never);
            auditLog.Verify(al => al.LogAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task SetSoftwarePathsAsync_ValidPaths_SavesAndAudits()
        {
            var (config, configService, auditLog) = CreateMocks(ffmpegDir: "C:\\old\\bin", cloudflaredPath: null);
            var binDir = CreateBinDirectory();
            var cloudflared = CreateCloudflaredFile();

            IResult result = await InvokeAsync(new SetSoftwarePathsRequest(binDir, cloudflared), config, configService, auditLog);

            var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
            Assert.Equal(StatusCodes.Status200OK, statusResult.StatusCode);
            Assert.Equal(binDir, config.FfmpegBinDirectory);
            Assert.Equal(cloudflared, config.CloudflaredPath);
            configService.Verify(cs => cs.SaveConfigurationAsync(config, It.IsAny<CancellationToken>()), Times.Once);
            auditLog.Verify(al => al.LogAsync(
                SecurityAuditEventTypes.SoftwarePathsChanged,
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<string>(),
                It.Is<string>(d => d.Contains("C:\\old\\bin") && d.Contains(binDir) && d.Contains(cloudflared)),
                true,
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SetSoftwarePathsAsync_EmptyValues_ClearsSettings()
        {
            var (config, configService, auditLog) = CreateMocks(ffmpegDir: "C:\\old\\bin", cloudflaredPath: "C:\\old\\cloudflared.exe");

            IResult result = await InvokeAsync(new SetSoftwarePathsRequest("", ""), config, configService, auditLog);

            var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
            Assert.Equal(StatusCodes.Status200OK, statusResult.StatusCode);
            Assert.True(string.IsNullOrEmpty(config.FfmpegBinDirectory));
            Assert.True(string.IsNullOrEmpty(config.CloudflaredPath));
            configService.Verify(cs => cs.SaveConfigurationAsync(config, It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
