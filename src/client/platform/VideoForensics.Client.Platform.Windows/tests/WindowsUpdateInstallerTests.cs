using Xunit;

namespace VideoForensics.Client.Platform.Windows.Tests
{
    public class WindowsUpdateInstallerTests
    {
        [Fact]
        public void PackageExtension_IsExe()
        {
            // Windows releases ship a .exe installer; the update check picks the asset by this extension.
            var installer = new WindowsUpdateInstaller(Microsoft.Extensions.Logging.Abstractions.NullLogger<WindowsUpdateInstaller>.Instance);

            Assert.Equal(".exe", installer.PackageExtension);
        }

        [Fact]
        public async Task DownloadAndInvokeAsync_UnverifiedInstall_IsRefusedAndWritesNoFile()
        {
            // The release .exe has no hash or signature check, so it must never be downloaded or run.
            var installer = new WindowsUpdateInstaller(Microsoft.Extensions.Logging.Abstractions.NullLogger<WindowsUpdateInstaller>.Instance);
            string name = $"vf-refused-{Guid.NewGuid():N}.exe";
            string tempPath = Path.Combine(Path.GetTempPath(), name);
            var asset = new VideoForensics.Client.Common.Contracts.GitHubReleaseAsset(name, "https://example.invalid/installer.exe", 1);

            _ = await Assert.ThrowsAsync<InvalidOperationException>(() => installer.DownloadAndInvokeAsync(asset, CancellationToken.None));

            Assert.False(File.Exists(tempPath));
        }
    }
}
