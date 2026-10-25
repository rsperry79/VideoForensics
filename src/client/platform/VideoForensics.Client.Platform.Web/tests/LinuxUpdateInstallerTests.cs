using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace VideoForensics.Client.Platform.Web.Tests
{
    public class LinuxUpdateInstallerTests
    {
        [Fact]
        public void LinuxUpdateInstaller_PackageExtension_IsDeb()
        {
            // Debian releases ship a .deb package; the update check picks the asset by this extension.
            var installer = new LinuxUpdateInstaller(NullLogger<LinuxUpdateInstaller>.Instance);

            Assert.Equal(".deb", installer.PackageExtension);
        }

        [Fact]
        public void LinuxUpdateInstaller_NullLogger_Throws()
        {
            _ = Assert.Throws<ArgumentNullException>(() => new LinuxUpdateInstaller(null!));
        }
    }
}
