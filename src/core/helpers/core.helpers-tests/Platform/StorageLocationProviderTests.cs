using System;

using VideoForensics.Providers.Common.Helpers.Platform;

using Xunit;

namespace VideoForensics.Providers.Common.Helpers.Tests.Platform
{
    public class StorageLocationProviderTests
    {
        private readonly StorageLocationProvider _provider = new StorageLocationProvider();

        #region GetDefaultRoot Tests

        [Fact]
        public void GetDefaultRoot_Database_ReturnsNonEmptyPath()
        {
            string result = _provider.GetDefaultRoot(StorageCategory.Database);
            Assert.NotEmpty(result);
        }

        [Fact]
        public void GetDefaultRoot_Database_ReturnsAbsolutePath()
        {
            string result = _provider.GetDefaultRoot(StorageCategory.Database);
            Assert.True(System.IO.Path.IsPathRooted(result));
        }

        [Fact]
        public void GetDefaultRoot_Logs_ReturnsNonEmptyPath()
        {
            string result = _provider.GetDefaultRoot(StorageCategory.Logs);
            Assert.NotEmpty(result);
        }

        [Fact]
        public void GetDefaultRoot_Logs_ReturnsAbsolutePath()
        {
            string result = _provider.GetDefaultRoot(StorageCategory.Logs);
            Assert.True(System.IO.Path.IsPathRooted(result));
        }

        [Fact]
        public void GetDefaultRoot_Media_ReturnsNonEmptyPath()
        {
            string result = _provider.GetDefaultRoot(StorageCategory.Media);
            Assert.NotEmpty(result);
        }

        [Fact]
        public void GetDefaultRoot_Media_ReturnsAbsolutePath()
        {
            string result = _provider.GetDefaultRoot(StorageCategory.Media);
            Assert.True(System.IO.Path.IsPathRooted(result));
        }

        [Fact]
        public void GetDefaultRoot_TempDownload_ReturnsNonEmptyPath()
        {
            string result = _provider.GetDefaultRoot(StorageCategory.TempDownload);
            Assert.NotEmpty(result);
        }

        [Fact]
        public void GetDefaultRoot_Reports_ReturnsNonEmptyPath()
        {
            string result = _provider.GetDefaultRoot(StorageCategory.Reports);
            Assert.NotEmpty(result);
        }

        [Fact]
        public void GetDefaultRoot_Backup_ReturnsNonEmptyPath()
        {
            string result = _provider.GetDefaultRoot(StorageCategory.Backup);
            Assert.NotEmpty(result);
        }

        [Fact]
        public void GetDefaultRoot_Keys_ReturnsNonEmptyPath()
        {
            string result = _provider.GetDefaultRoot(StorageCategory.Keys);
            Assert.NotEmpty(result);
        }

        [Fact]
        public void GetDefaultRoot_WindowsDatabase_ReturnsPathEndingWithVideoForensics()
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            string result = _provider.GetDefaultRoot(StorageCategory.Database);
            Assert.EndsWith("VideoForensics", result);
            Assert.DoesNotContain("Logs", result);
            Assert.DoesNotContain("media", result);
        }

        [Fact]
        public void GetDefaultRoot_WindowsLogs_ReturnsPathContainingLogsSubfolder()
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            string result = _provider.GetDefaultRoot(StorageCategory.Logs);
            Assert.Contains("VideoForensics", result);
            Assert.True(result.EndsWith("Logs") || result.Contains("Logs"), "Logs path should contain Logs subfolder");
        }

        [Fact]
        public void GetDefaultRoot_WindowsMedia_ReturnsPathContainingMediaSubfolder()
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            string result = _provider.GetDefaultRoot(StorageCategory.Media);
            Assert.Contains("VideoForensics", result);
            Assert.Contains("media", result);
        }

        [Fact]
        public void GetDefaultRoot_LinuxDatabase_ReturnsLinuxPath()
        {
            if (OperatingSystem.IsWindows())
            {
                return;
            }

            string result = _provider.GetDefaultRoot(StorageCategory.Database);
            Assert.Equal("/var/lib/videoforensics", result);
        }

        [Fact]
        public void GetDefaultRoot_LinuxLogs_ReturnsLinuxPath()
        {
            if (OperatingSystem.IsWindows())
            {
                return;
            }

            string result = _provider.GetDefaultRoot(StorageCategory.Logs);
            Assert.Equal("/var/log/videoforensics", result);
        }

        [Fact]
        public void GetDefaultRoot_LinuxMedia_ReturnsPathContainingMedia()
        {
            if (OperatingSystem.IsWindows())
            {
                return;
            }

            string result = _provider.GetDefaultRoot(StorageCategory.Media);
            Assert.Contains("/var/lib/videoforensics", result);
            Assert.Contains("media", result);
        }

        [Fact]
        public void GetDefaultRoot_AllCategoriesReturnAbsolutePaths()
        {
            foreach (StorageCategory category in System.Enum.GetValues(typeof(StorageCategory)))
            {
                string result = _provider.GetDefaultRoot(category);
                Assert.True(System.IO.Path.IsPathRooted(result), $"Path for {category} should be absolute");
            }
        }

        #endregion

        #region GetEffectiveRoot Tests

        [Fact]
        public void GetEffectiveRoot_WithNullOverride_ReturnDefaultRoot()
        {
            string defaultRoot = _provider.GetDefaultRoot(StorageCategory.Database);
            string effectiveRoot = _provider.GetEffectiveRoot(StorageCategory.Database, null);
            Assert.Equal(defaultRoot, effectiveRoot);
        }

        [Fact]
        public void GetEffectiveRoot_WithEmptyStringOverride_ReturnDefaultRoot()
        {
            string defaultRoot = _provider.GetDefaultRoot(StorageCategory.Logs);
            string effectiveRoot = _provider.GetEffectiveRoot(StorageCategory.Logs, string.Empty);
            Assert.Equal(defaultRoot, effectiveRoot);
        }

        [Fact]
        public void GetEffectiveRoot_WithWhitespaceOverride_ReturnDefaultRoot()
        {
            string defaultRoot = _provider.GetDefaultRoot(StorageCategory.Media);
            string effectiveRoot = _provider.GetEffectiveRoot(StorageCategory.Media, "   ");
            Assert.Equal(defaultRoot, effectiveRoot);
        }

        [Fact]
        public void GetEffectiveRoot_WithValidOverride_ReturnOverride()
        {
            string override_path = "/custom/storage/path";
            string result = _provider.GetEffectiveRoot(StorageCategory.Database, override_path);
            Assert.Equal(override_path, result);
        }

        [Fact]
        public void GetEffectiveRoot_WithValidWindowsOverride_ReturnOverride()
        {
            string override_path = @"C:\Custom\Storage\Path";
            string result = _provider.GetEffectiveRoot(StorageCategory.Logs, override_path);
            Assert.Equal(override_path, result);
        }

        [Fact]
        public void GetEffectiveRoot_WithTabWhitespaceOverride_ReturnDefaultRoot()
        {
            string defaultRoot = _provider.GetDefaultRoot(StorageCategory.Backup);
            string effectiveRoot = _provider.GetEffectiveRoot(StorageCategory.Backup, "\t");
            Assert.Equal(defaultRoot, effectiveRoot);
        }

        [Fact]
        public void GetEffectiveRoot_WithNewlineWhitespaceOverride_ReturnDefaultRoot()
        {
            string defaultRoot = _provider.GetDefaultRoot(StorageCategory.Keys);
            string effectiveRoot = _provider.GetEffectiveRoot(StorageCategory.Keys, "\n");
            Assert.Equal(defaultRoot, effectiveRoot);
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public void GetDefaultRoot_InvalidCategory_ThrowsArgumentOutOfRangeException()
        {
            // Cast an out-of-range value to StorageCategory
            var invalidCategory = (StorageCategory)999;
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _provider.GetDefaultRoot(invalidCategory));
        }

        [Fact]
        public void GetEffectiveRoot_InvalidCategory_ThrowsArgumentOutOfRangeException()
        {
            // Cast an out-of-range value to StorageCategory
            var invalidCategory = (StorageCategory)(-1);
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _provider.GetEffectiveRoot(invalidCategory, null));
        }

        #endregion

        #region Category Consistency Tests

        [Fact]
        public void AllCategories_HaveConsistentBehavior()
        {
            // Verify that each category always returns the same path
            foreach (StorageCategory category in System.Enum.GetValues(typeof(StorageCategory)))
            {
                string result1 = _provider.GetDefaultRoot(category);
                string result2 = _provider.GetDefaultRoot(category);
                Assert.Equal(result1, result2);
            }
        }

        [Fact]
        public void GetEffectiveRoot_AlwaysReturnsAbsolutePath_WhenNoOverride()
        {
            foreach (StorageCategory category in System.Enum.GetValues(typeof(StorageCategory)))
            {
                string result = _provider.GetEffectiveRoot(category, null);
                Assert.True(System.IO.Path.IsPathRooted(result),
                    $"GetEffectiveRoot for {category} without override should return absolute path");
            }
        }

        #endregion
    }
}
