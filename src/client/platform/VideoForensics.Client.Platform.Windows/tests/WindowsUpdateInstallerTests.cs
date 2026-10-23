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
    }
}
