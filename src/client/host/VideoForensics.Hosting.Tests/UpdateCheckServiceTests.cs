using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Moq;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Hosting.BackgroundServices;

using Xunit;

namespace VideoForensics.Hosting.Tests
{
    public class UpdateCheckServiceTests
    {
        private static (UpdateCheckService Service, Mock<IGitHubReleaseClient> GitHubClient, Mock<IUpdateInstaller> Installer)
            CreateService(IForensicsConfiguration? config = null, Func<string>? currentVersionProvider = null)
        {
            var gitHubClient = new Mock<IGitHubReleaseClient>();
            var installer = new Mock<IUpdateInstaller>();

            var services = new ServiceCollection();
            _ = services.AddSingleton(gitHubClient.Object);
            _ = services.AddSingleton(installer.Object);
            ServiceProvider provider = services.BuildServiceProvider();

            IServiceScopeFactory scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

            var service = new UpdateCheckService(
                scopeFactory,
                config ?? new ForensicsConfiguration(),
                gitHubClient.Object,
                installer.Object,
                Mock.Of<ILogger<UpdateCheckService>>(),
                currentVersionProvider ?? (() => "1.0.0"));

            return (service, gitHubClient, installer);
        }

        [Fact]
        public async Task RunOneTickAsync_DisabledViaConfig_DoesNotCallGitHubClient()
        {
            var config = new ForensicsConfiguration { EnableUpdateCheck = false };
            (UpdateCheckService service, Mock<IGitHubReleaseClient> gitHubClient, _) = CreateService(config);

            await service.RunOneTickAsync(CancellationToken.None);

            gitHubClient.Verify(c => c.GetLatestReleaseAsync(It.IsAny<CancellationToken>()), Times.Never);
            gitHubClient.Verify(c => c.GetLatestTestingReleaseAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RunOneTickAsync_NewerVersionAvailable_SetsUpdateAvailableTrueAndLatestVersion()
        {
            var config = new ForensicsConfiguration { ReleaseChannel = UpdateReleaseChannel.Stable };
            (UpdateCheckService service, Mock<IGitHubReleaseClient> gitHubClient, _) = CreateService(config, () => "1.0.0");

            var release = new GitHubReleaseInfo(
                "v99.0.0",
                "https://github.com/rsperry79/VideoForensics/releases/tag/v99.0.0",
                false,
                false,
                new[] { new GitHubReleaseAsset("VideoForensics-99.0.0.exe", "https://example.com/download", 1000) }
            );
            _ = gitHubClient.Setup(c => c.GetLatestReleaseAsync(It.IsAny<CancellationToken>())).ReturnsAsync(release);

            await service.RunOneTickAsync(CancellationToken.None);

            UpdateCheckState state = service.GetState();
            Assert.True(state.UpdateAvailable);
            Assert.Equal("99.0.0", state.LatestVersion);
        }

        [Fact]
        public async Task RunOneTickAsync_AlreadyOnLatestVersion_SetsUpdateAvailableFalse()
        {
            var config = new ForensicsConfiguration { ReleaseChannel = UpdateReleaseChannel.Stable };
            (UpdateCheckService service, Mock<IGitHubReleaseClient> gitHubClient, _) = CreateService(config, () => "99.0.0");

            var release = new GitHubReleaseInfo(
                "v99.0.0",
                "https://github.com/rsperry79/VideoForensics/releases/tag/v99.0.0",
                false,
                false,
                new[] { new GitHubReleaseAsset("VideoForensics-99.0.0.exe", "https://example.com/download", 1000) }
            );
            _ = gitHubClient.Setup(c => c.GetLatestReleaseAsync(It.IsAny<CancellationToken>())).ReturnsAsync(release);

            await service.RunOneTickAsync(CancellationToken.None);

            UpdateCheckState state = service.GetState();
            Assert.False(state.UpdateAvailable);
        }

        [Fact]
        public async Task RunOneTickAsync_StableChannel_CallsGetLatestReleaseAsync_NotTestingVariant()
        {
            var config = new ForensicsConfiguration { ReleaseChannel = UpdateReleaseChannel.Stable };
            (UpdateCheckService service, Mock<IGitHubReleaseClient> gitHubClient, _) = CreateService(config);

            _ = gitHubClient.Setup(c => c.GetLatestReleaseAsync(It.IsAny<CancellationToken>())).ReturnsAsync((GitHubReleaseInfo?)null);

            await service.RunOneTickAsync(CancellationToken.None);

            gitHubClient.Verify(c => c.GetLatestReleaseAsync(It.IsAny<CancellationToken>()), Times.Once);
            gitHubClient.Verify(c => c.GetLatestTestingReleaseAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RunOneTickAsync_TestingChannel_CallsGetLatestTestingReleaseAsync_NotStableVariant()
        {
            var config = new ForensicsConfiguration { ReleaseChannel = UpdateReleaseChannel.Testing };
            (UpdateCheckService service, Mock<IGitHubReleaseClient> gitHubClient, _) = CreateService(config);

            _ = gitHubClient.Setup(c => c.GetLatestTestingReleaseAsync(It.IsAny<CancellationToken>())).ReturnsAsync((GitHubReleaseInfo?)null);

            await service.RunOneTickAsync(CancellationToken.None);

            gitHubClient.Verify(c => c.GetLatestTestingReleaseAsync(It.IsAny<CancellationToken>()), Times.Once);
            gitHubClient.Verify(c => c.GetLatestReleaseAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RunOneTickAsync_NotifyOnlyMode_DoesNotInvokeInstaller()
        {
            var config = new ForensicsConfiguration
            {
                UpdateMode = UpdateCheckMode.NotifyOnly,
                ReleaseChannel = UpdateReleaseChannel.Stable
            };
            (UpdateCheckService service, Mock<IGitHubReleaseClient> gitHubClient, Mock<IUpdateInstaller> installer) =
                CreateService(config, () => "1.0.0");

            var release = new GitHubReleaseInfo(
                "v99.0.0",
                "https://github.com/rsperry79/VideoForensics/releases/tag/v99.0.0",
                false,
                false,
                new[] { new GitHubReleaseAsset("VideoForensics-99.0.0.exe", "https://example.com/download", 1000) }
            );
            _ = gitHubClient.Setup(c => c.GetLatestReleaseAsync(It.IsAny<CancellationToken>())).ReturnsAsync(release);

            await service.RunOneTickAsync(CancellationToken.None);

            installer.Verify(i => i.DownloadAndInvokeAsync(It.IsAny<GitHubReleaseAsset>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RunOneTickAsync_AutoDownloadAndInstallMode_InvokesInstallerWithMatchingAsset()
        {
            var config = new ForensicsConfiguration
            {
                UpdateMode = UpdateCheckMode.AutoDownloadAndInstall,
                ReleaseChannel = UpdateReleaseChannel.Stable
            };
            (UpdateCheckService service, Mock<IGitHubReleaseClient> gitHubClient, Mock<IUpdateInstaller> installer) =
                CreateService(config, () => "1.0.0");

            // Include both a Windows and a Debian asset so this test passes regardless of the
            // OS it actually runs on - UpdateCheckService picks .exe on Windows and .deb
            // elsewhere (RuntimeInformation.IsOSPlatform), and this test only cares that the
            // installer gets invoked with whichever asset matched, not which platform matched.
            var windowsAsset = new GitHubReleaseAsset("VideoForensics-99.0.0.exe", "https://example.com/download.exe", 1000);
            var debianAsset = new GitHubReleaseAsset("videoforensics_99.0.0_amd64.deb", "https://example.com/download.deb", 1000);
            var release = new GitHubReleaseInfo(
                "v99.0.0",
                "https://github.com/rsperry79/VideoForensics/releases/tag/v99.0.0",
                false,
                false,
                new[] { windowsAsset, debianAsset }
            );
            _ = gitHubClient.Setup(c => c.GetLatestReleaseAsync(It.IsAny<CancellationToken>())).ReturnsAsync(release);

            await service.RunOneTickAsync(CancellationToken.None);

            installer.Verify(i => i.DownloadAndInvokeAsync(It.IsAny<GitHubReleaseAsset>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RunOneTickAsync_GitHubClientReturnsNull_SetsErrorMessageAndDoesNotThrow()
        {
            var config = new ForensicsConfiguration { ReleaseChannel = UpdateReleaseChannel.Stable };
            (UpdateCheckService service, Mock<IGitHubReleaseClient> gitHubClient, _) = CreateService(config);

            _ = gitHubClient.Setup(c => c.GetLatestReleaseAsync(It.IsAny<CancellationToken>())).ReturnsAsync((GitHubReleaseInfo?)null);

            // Must not throw
            await service.RunOneTickAsync(CancellationToken.None);

            UpdateCheckState state = service.GetState();
            Assert.NotNull(state.ErrorMessage);
            Assert.NotNull(state.LastCheckedUtc);
        }

        [Fact]
        public async Task RunOneTickAsync_GitHubClientThrows_IsSwallowedAndDoesNotPropagate()
        {
            var config = new ForensicsConfiguration { ReleaseChannel = UpdateReleaseChannel.Stable };
            (UpdateCheckService service, Mock<IGitHubReleaseClient> gitHubClient, _) = CreateService(config);

            _ = gitHubClient.Setup(c => c.GetLatestReleaseAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("GitHub API error"));

            // Must not throw
            await service.RunOneTickAsync(CancellationToken.None);

            UpdateCheckState state = service.GetState();
            Assert.NotNull(state.ErrorMessage);
        }

        [Fact]
        public async Task RunOneTickAsync_MalformedReleaseTag_SetsErrorMessageAndDoesNotThrow()
        {
            var config = new ForensicsConfiguration { ReleaseChannel = UpdateReleaseChannel.Stable };
            (UpdateCheckService service, Mock<IGitHubReleaseClient> gitHubClient, _) = CreateService(config);

            var release = new GitHubReleaseInfo(
                "not-a-version",
                "https://github.com/rsperry79/VideoForensics/releases/tag/not-a-version",
                false,
                false,
                new[] { new GitHubReleaseAsset("file.exe", "https://example.com/download", 1000) }
            );
            _ = gitHubClient.Setup(c => c.GetLatestReleaseAsync(It.IsAny<CancellationToken>())).ReturnsAsync(release);

            // Must not throw
            await service.RunOneTickAsync(CancellationToken.None);

            UpdateCheckState state = service.GetState();
            Assert.NotNull(state.ErrorMessage);
            Assert.NotNull(state.LastCheckedUtc);
        }

        [Fact]
        public async Task TriggerCheckNowAsync_InvokesSameTickLogic()
        {
            var config = new ForensicsConfiguration { ReleaseChannel = UpdateReleaseChannel.Stable };
            (UpdateCheckService service, Mock<IGitHubReleaseClient> gitHubClient, _) = CreateService(config, () => "1.0.0");

            var release = new GitHubReleaseInfo(
                "v99.0.0",
                "https://github.com/rsperry79/VideoForensics/releases/tag/v99.0.0",
                false,
                false,
                new[] { new GitHubReleaseAsset("VideoForensics-99.0.0.exe", "https://example.com/download", 1000) }
            );
            _ = gitHubClient.Setup(c => c.GetLatestReleaseAsync(It.IsAny<CancellationToken>())).ReturnsAsync(release);

            await service.TriggerCheckNowAsync(CancellationToken.None);

            gitHubClient.Verify(c => c.GetLatestReleaseAsync(It.IsAny<CancellationToken>()), Times.Once);
            UpdateCheckState state = service.GetState();
            Assert.True(state.UpdateAvailable);
            Assert.Equal("99.0.0", state.LatestVersion);
        }

        [Fact]
        public void UpdateCheckService_ResolvedViaContainer_DoesNotThrowWhenCurrentVersionProviderNotRegistered()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton(Mock.Of<IForensicsConfiguration>());
            services.AddSingleton(Mock.Of<IGitHubReleaseClient>());
            services.AddSingleton(Mock.Of<IUpdateInstaller>());
            services.AddSingleton<UpdateCheckService>();

            using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });

            UpdateCheckService service = provider.GetRequiredService<UpdateCheckService>();

            Assert.NotNull(service);
        }
    }
}
