using VideoForensics.Client.Platform.Windows.Services;

using Xunit;

namespace VideoForensics.Client.Platform.Windows.Tests.Services
{
    /// <summary>
    /// Tests for <see cref="UserDocumentsBackupStagingLocator"/>: where the backup export is staged before the save dialog.
    /// </summary>
    public class UserDocumentsBackupStagingLocatorTests
    {
        /// <summary>Verifies that staging goes under VideoForensics\Backups beneath the documents folder, as the page did before.</summary>
        [Fact]
        public async Task GetStagingDirectory_DocumentsRootGiven_ReturnsVideoForensicsBackups()
        {
            string documentsRoot = Path.Combine(Path.GetTempPath(), "vf-docs");
            var locator = new UserDocumentsBackupStagingLocator(() => documentsRoot);

            Assert.Equal(Path.Combine(documentsRoot, "VideoForensics", "Backups"), await locator.GetExportDirectoryAsync(CancellationToken.None));
        }

        /// <summary>Verifies the default constructor uses the real user documents folder and keeps the same subfolder.</summary>
        [Fact]
        public async Task GetStagingDirectory_Default_EndsWithVideoForensicsBackups()
        {
            var locator = new UserDocumentsBackupStagingLocator();

            Assert.EndsWith(Path.Combine("VideoForensics", "Backups"), await locator.GetExportDirectoryAsync(CancellationToken.None));
        }
    }
}
