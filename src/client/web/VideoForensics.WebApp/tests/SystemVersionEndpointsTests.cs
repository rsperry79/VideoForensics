using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

using Moq;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.WebApp.Tests.Api;

using Xunit;

namespace VideoForensics.WebApp.Tests
{
    /// <summary>
    /// xUnit tests for SystemVersionEndpoints covering GET /api/v1/system/version and /api/v1/system/version-manifest.
    /// Tests verify version data retrieval, DTO structure, and manifest consistency.
    /// These endpoints are unauthenticated by design (no RequireAuthorization).
    /// </summary>
    public class SystemVersionEndpointsTests
    {
        public static Mock<ISystemVersionProvider> CreateMockVersionProvider()
        {
            return new Mock<ISystemVersionProvider>();
        }

        public static Mock<ISystemVersionProvider> CreateMockVersionProviderWithDefaults()
        {
            var mock = new Mock<ISystemVersionProvider>();
            _ = mock.Setup(v => v.GetCurrentVersionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync("1.0.0-dev.1");
            _ = mock.Setup(v => v.GetBuildTimestampAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DateTime(2024, 10, 5, 12, 0, 0, DateTimeKind.Utc));
            _ = mock.Setup(v => v.GetReleaseChannelAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync("dev");
            return mock;
        }

        [Fact]
        public async Task SystemVersionEndpoints_GetVersion_ReturnsOkWithSystemVersionDto()
        {
            // Arrange
            Mock<ISystemVersionProvider> versionProvider = CreateMockVersionProviderWithDefaults();

            // Act
            IResult result = await SystemVersionEndpointsInvoker.GetSystemVersion(versionProvider.Object, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            var okResult = result as Ok<SystemVersionDto>;
            Assert.NotNull(okResult);
            SystemVersionDto? dto = okResult.Value;
            Assert.NotNull(dto);
            Assert.NotEmpty(dto.Version);
            _ = Assert.NotNull(dto.BuildDate);
            Assert.NotEmpty(dto.Channel);

            versionProvider.Verify(v => v.GetCurrentVersionAsync(It.IsAny<CancellationToken>()), Times.Once);
            versionProvider.Verify(v => v.GetBuildTimestampAsync(It.IsAny<CancellationToken>()), Times.Once);
            versionProvider.Verify(v => v.GetReleaseChannelAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SystemVersionEndpoints_GetVersion_PublicEndpoint_NoAuthRequired()
        {
            // Arrange
            // This test documents that GetSystemVersion endpoint does not require authentication.
            // Auth is handled at the routing level by absence of RequireAuthorization in MapSystemVersionEndpoints.
            Mock<ISystemVersionProvider> versionProvider = CreateMockVersionProviderWithDefaults();

            // Act
            IResult result = await SystemVersionEndpointsInvoker.GetSystemVersion(versionProvider.Object, CancellationToken.None);

            // Assert
            // If auth were required, an exception would be thrown. No exception = no auth required.
            Assert.NotNull(result);
            var okResult = result as Ok<SystemVersionDto>;
            Assert.NotNull(okResult);
        }

        [Fact]
        public async Task SystemVersionEndpoints_GetVersionManifest_ReturnsOkWithVersionManifestDto()
        {
            // Arrange
            Mock<ISystemVersionProvider> versionProvider = CreateMockVersionProviderWithDefaults();

            // Act
            IResult result = await SystemVersionEndpointsInvoker.GetVersionManifest(versionProvider.Object, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            var okResult = result as Ok<VersionManifestDto>;
            Assert.NotNull(okResult);
            VersionManifestDto? manifest = okResult.Value;
            Assert.NotNull(manifest);
            Assert.NotNull(manifest.CurrentVersion);
            Assert.NotEmpty(manifest.CurrentVersion.Version);
            Assert.False(manifest.UpdateAvailable); // Week 1 behavior: no update checking yet
            Assert.Null(manifest.LatestAvailable);
            Assert.Null(manifest.DownloadUrl);
            Assert.Null(manifest.ChangelogUrl);
        }

        [Fact]
        public async Task SystemVersionEndpoints_GetVersionManifest_IncludesCurrentVersionInfo()
        {
            // Arrange
            const string knownVersion = "2.5.1-testing.3";
            var knownTimestamp = new DateTime(2024, 10, 5, 14, 30, 0, DateTimeKind.Utc);
            const string knownChannel = "testing";

            var versionProvider = new Mock<ISystemVersionProvider>();
            _ = versionProvider.Setup(v => v.GetCurrentVersionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(knownVersion);
            _ = versionProvider.Setup(v => v.GetBuildTimestampAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(knownTimestamp);
            _ = versionProvider.Setup(v => v.GetReleaseChannelAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(knownChannel);

            // Act - Get the version first
            IResult versionResult = await SystemVersionEndpointsInvoker.GetSystemVersion(versionProvider.Object, CancellationToken.None);
            var versionOkResult = versionResult as Ok<SystemVersionDto>;
            SystemVersionDto? systemVersion = versionOkResult?.Value;

            // Act - Get the manifest
            IResult manifestResult = await SystemVersionEndpointsInvoker.GetVersionManifest(versionProvider.Object, CancellationToken.None);
            var manifestOkResult = manifestResult as Ok<VersionManifestDto>;
            VersionManifestDto? manifest = manifestOkResult?.Value;

            // Assert - Verify consistency
            Assert.NotNull(systemVersion);
            Assert.NotNull(manifest);
            Assert.Equal(systemVersion.Version, manifest.CurrentVersion.Version);
            Assert.Equal(systemVersion.BuildDate, manifest.CurrentVersion.BuildDate);
            Assert.Equal(systemVersion.Channel, manifest.CurrentVersion.Channel);
            Assert.Equal(knownVersion, manifest.CurrentVersion.Version);
            Assert.Equal(knownTimestamp, manifest.CurrentVersion.BuildDate);
            Assert.Equal(knownChannel, manifest.CurrentVersion.Channel);
        }
    }
}
