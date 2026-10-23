using Xunit;

namespace VideoForensics.Client.Platform.Windows.Tests
{
    public class OneDrivePathsTests
    {
        [Fact]
        public void GetOneDrivePath_ReturnsNullOrExistingDirectory()
        {
            string? result = OneDrivePaths.GetOneDrivePath();

            if (result != null)
            {
                Assert.True(Directory.Exists(result));
            }
        }

        [Fact]
        public void GetOneDrivePath_IsConsistentAcrossCalls()
        {
            string? first = OneDrivePaths.GetOneDrivePath();
            string? second = OneDrivePaths.GetOneDrivePath();

            Assert.Equal(first, second);
        }

        [Fact]
        public void GetLegacyDownloadLocation_EndsWithVideoForensicsFolder()
        {
            string result = OneDrivePaths.GetLegacyDownloadLocation();

            Assert.EndsWith("VideoForensics", result);
        }

        [Fact]
        public void GetLegacyQueryExportLocation_EndsWithVideoForensicsFolder()
        {
            string result = OneDrivePaths.GetLegacyQueryExportLocation();

            Assert.EndsWith("VideoForensics", result);
        }
    }
}
