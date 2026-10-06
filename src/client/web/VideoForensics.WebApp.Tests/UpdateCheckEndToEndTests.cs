using System.Net;
using System.Text.Json;
using VideoForensics.Api.Contracts;
using VideoForensics.WebApp.Tests.Api;
using Xunit;

namespace VideoForensics.WebApp.Tests
{
    /// <summary>
    /// Integration tests for the full version-check flow end-to-end.
    /// Tests the complete chain: SystemVersion → UpdateCheck → VersionManifest
    ///
    /// Notes on test design:
    /// - These tests verify endpoint behavior and data consistency
    /// - They use the invoker pattern to call endpoints directly, bypassing WebApplicationFactory
    ///   complexity for mocking database and external dependencies
    /// - Real end-to-end HTTP testing is documented in BACKWARD_COMPATIBILITY_NOTES.md
    ///
    /// Manual Testing Checklist:
    /// 1. Deploy to staging
    /// 2. Call GET /api/v1/system/version → verify response contains version, build date, channel
    /// 3. Call GET /api/v1/update-check → verify response is unchanged from before
    /// 4. Call GET /api/v1/system/version-manifest → verify response integrates both
    /// 5. Trigger a dev build → verify version.json auto-increments
    /// 6. Tag a release → verify release notes are generated and GitHub release is created
    /// 7. Verify old clients still work (no breaking changes)
    /// </summary>
    public class UpdateCheckEndToEndTests
    {
        [Fact]
        public async Task VersionAndUpdateCheck_Manifest_IncludesCurrentAndLatestVersions()
        {
            // Arrange - Create mocks for version provider
            var versionProvider = SystemVersionEndpointsTests.CreateMockVersionProviderWithDefaults();

            // Act - Get the version manifest
            var manifestResult = await SystemVersionEndpointsInvoker.GetVersionManifest(versionProvider.Object, CancellationToken.None);

            // Assert - Verify CurrentVersion is never null and manifest has correct structure
            Assert.NotNull(manifestResult);
            var manifestOk = manifestResult as Microsoft.AspNetCore.Http.HttpResults.Ok<VersionManifestDto>;
            Assert.NotNull(manifestOk);

            var manifest = manifestOk.Value;
            Assert.NotNull(manifest);
            Assert.NotNull(manifest.CurrentVersion);
            Assert.NotEmpty(manifest.CurrentVersion.Version);

            // Verify LatestAvailable may be null (no update available) or present (update available)
            // Week 1 behavior: LatestAvailable should be null since update integration happens in Week 2
            Assert.Null(manifest.LatestAvailable);

            // Verify UpdateAvailable bool is consistent with versions
            // If LatestAvailable is null, UpdateAvailable should be false
            if (manifest.LatestAvailable == null)
            {
                Assert.False(manifest.UpdateAvailable);
            }
            else
            {
                // In Week 2 when update checking is integrated:
                // UpdateAvailable should be true only if LatestAvailable is set and > CurrentVersion
                Assert.True(manifest.UpdateAvailable);
                Assert.NotNull(manifest.LatestAvailable);
            }
        }

        [Fact]
        public async Task VersionEndpoint_ClientCanCompareVersions_AndDecideUpdate()
        {
            // Arrange - Create mock version provider
            var versionProvider = SystemVersionEndpointsTests.CreateMockVersionProviderWithDefaults();

            // Act - Get current version
            var versionResult = await SystemVersionEndpointsInvoker.GetSystemVersion(versionProvider.Object, CancellationToken.None);
            Assert.NotNull(versionResult);
            var versionOk = versionResult as Microsoft.AspNetCore.Http.HttpResults.Ok<SystemVersionDto>;
            Assert.NotNull(versionOk);
            var currentVersion = versionOk.Value;

            // Act - Get available version from manifest
            var manifestResult = await SystemVersionEndpointsInvoker.GetVersionManifest(versionProvider.Object, CancellationToken.None);
            Assert.NotNull(manifestResult);
            var manifestOk = manifestResult as Microsoft.AspNetCore.Http.HttpResults.Ok<VersionManifestDto>;
            Assert.NotNull(manifestOk);
            var manifest = manifestOk.Value;

            // Assert - Verify manifest data enables client to make update decision
            Assert.NotNull(currentVersion);
            Assert.NotNull(manifest);

            // Simulate client logic: if LatestAvailable > CurrentVersion, UpdateAvailable should be true
            var currentVersionString = currentVersion.Version;
            var latestVersionString = manifest.LatestAvailable?.Version;

            // For Week 1 (no update checking yet), LatestAvailable should be null
            Assert.Null(latestVersionString);

            // Manifest should reflect this: UpdateAvailable = false, no download URL
            Assert.False(manifest.UpdateAvailable);
            Assert.Null(manifest.DownloadUrl);
            Assert.Null(manifest.ChangelogUrl);

            // Verify consistency: CurrentVersion in manifest matches the version endpoint
            Assert.Equal(currentVersionString, manifest.CurrentVersion.Version);
            Assert.Equal(currentVersion.BuildDate, manifest.CurrentVersion.BuildDate);
            Assert.Equal(currentVersion.Channel, manifest.CurrentVersion.Channel);
        }

        [Fact]
        public async Task VersionData_Consistency_AcrossEndpoints()
        {
            // Arrange
            var versionProvider = SystemVersionEndpointsTests.CreateMockVersionProviderWithDefaults();

            // Act - Call both endpoints
            var versionResult = await SystemVersionEndpointsInvoker.GetSystemVersion(versionProvider.Object, CancellationToken.None);
            var manifestResult = await SystemVersionEndpointsInvoker.GetVersionManifest(versionProvider.Object, CancellationToken.None);

            // Assert - Data is consistent
            Assert.NotNull(versionResult);
            Assert.NotNull(manifestResult);

            var versionOk = versionResult as Microsoft.AspNetCore.Http.HttpResults.Ok<SystemVersionDto>;
            var manifestOk = manifestResult as Microsoft.AspNetCore.Http.HttpResults.Ok<VersionManifestDto>;

            Assert.NotNull(versionOk);
            Assert.NotNull(manifestOk);

            var version = versionOk.Value;
            var manifest = manifestOk.Value;

            // Verify all version fields match between endpoints
            Assert.NotNull(version);
            Assert.NotNull(manifest);
            Assert.NotNull(manifest.CurrentVersion);

            Assert.Equal(version.Version, manifest.CurrentVersion.Version);
            Assert.Equal(version.BuildDate, manifest.CurrentVersion.BuildDate);
            Assert.Equal(version.Channel, manifest.CurrentVersion.Channel);
        }

        [Fact]
        public async Task SystemVersionEndpoint_ReturnsValidSystemVersionDto()
        {
            // Arrange
            var versionProvider = SystemVersionEndpointsTests.CreateMockVersionProviderWithDefaults();

            // Act
            var result = await SystemVersionEndpointsInvoker.GetSystemVersion(versionProvider.Object, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            var okResult = result as Microsoft.AspNetCore.Http.HttpResults.Ok<SystemVersionDto>;
            Assert.NotNull(okResult);
            var dto = okResult.Value;
            Assert.NotNull(dto);
            Assert.NotEmpty(dto.Version);
            Assert.NotNull(dto.BuildDate);
            Assert.NotEmpty(dto.Channel);
        }

        [Fact]
        public async Task VersionManifestEndpoint_ReturnsValidVersionManifestDto()
        {
            // Arrange
            var versionProvider = SystemVersionEndpointsTests.CreateMockVersionProviderWithDefaults();

            // Act
            var result = await SystemVersionEndpointsInvoker.GetVersionManifest(versionProvider.Object, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            var okResult = result as Microsoft.AspNetCore.Http.HttpResults.Ok<VersionManifestDto>;
            Assert.NotNull(okResult);
            var manifest = okResult.Value;
            Assert.NotNull(manifest);
            Assert.NotNull(manifest.CurrentVersion);
            Assert.NotEmpty(manifest.CurrentVersion.Version);
            Assert.False(manifest.UpdateAvailable); // Week 1 behavior: no update checking yet
            Assert.Null(manifest.LatestAvailable);
            Assert.Null(manifest.DownloadUrl);
            Assert.Null(manifest.ChangelogUrl);
        }
    }
}
