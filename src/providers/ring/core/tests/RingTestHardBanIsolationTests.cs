using VideoForensics.Core.Helpers.Platform;

namespace VideoForensics.Providers.Ring.Core.Tests
{
    /// <summary>
    /// Verifies the test-process hard-ban store cannot touch the machine-wide application data file.
    /// </summary>
    public class RingTestHardBanIsolationTests
    {
        [Fact]
        public void TestStore_FilePath_IsInsideTempDirectory_NotMachineWideAppData()
        {
            // Arrange
            string machineWideDirectory = new PlatformDirectoryService().GetApplicationDataDirectory();

            // Act
            string storePath = RingTestHardBanIsolation.Store.FilePath;

            // Assert
            Assert.StartsWith(Path.GetTempPath(), storePath, StringComparison.OrdinalIgnoreCase);
            Assert.False(
                storePath.StartsWith(machineWideDirectory, StringComparison.OrdinalIgnoreCase),
                $"Test hard-ban store must not be under the machine-wide directory '{machineWideDirectory}'.");
        }
    }
}
