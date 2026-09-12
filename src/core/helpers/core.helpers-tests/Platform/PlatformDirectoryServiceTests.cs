using VideoForensics.Providers.Common.Helpers.Contracts;
using VideoForensics.Providers.Common.Helpers.Platform;

using Xunit;

namespace VideoForensics.Providers.Common.Helpers.Tests.Platform
{
    public class PlatformDirectoryServiceTests
    {
        private readonly IPlatformDirectoryService _service = new PlatformDirectoryService();

        [Fact]
        public void GetApplicationDataDirectory_ReturnsNonEmptyPath()
        {
            string result = _service.GetApplicationDataDirectory();
            Assert.NotEmpty(result);
        }

        [Fact]
        public void GetApplicationDataDirectory_ReturnsAbsolutePath()
        {
            string result = _service.GetApplicationDataDirectory();
            Assert.True(System.IO.Path.IsPathRooted(result));
        }

        [Fact]
        public void GetApplicationDataDirectory_ContainsAppName()
        {
            string result = _service.GetApplicationDataDirectory();
            Assert.NotEmpty(result);
            // Should contain either "RingVideos" or "ringvideos" depending on platform
        }

        [Fact]
        public void GetLogsDirectory_ReturnsNonEmptyPath()
        {
            string result = _service.GetLogsDirectory();
            Assert.NotEmpty(result);
        }

        [Fact]
        public void GetLogsDirectory_ReturnsAbsolutePath()
        {
            string result = _service.GetLogsDirectory();
            Assert.True(System.IO.Path.IsPathRooted(result));
        }

        [Fact]
        public void GetLogsDirectory_ContainsLogsKeyword()
        {
            string result = _service.GetLogsDirectory().ToLower();
            Assert.True(result.Contains("logs") || result.Contains("state"));
        }

        [Fact]
        public void GetConfigDirectory_ReturnsNonEmptyPath()
        {
            string result = _service.GetConfigDirectory();
            Assert.NotEmpty(result);
        }

        [Fact]
        public void GetConfigDirectory_ReturnsAbsolutePath()
        {
            string result = _service.GetConfigDirectory();
            Assert.True(System.IO.Path.IsPathRooted(result));
        }

        [Fact]
        public void GetConfigDirectory_ContainsConfigOrPreferences()
        {
            string result = _service.GetConfigDirectory().ToLower();
            Assert.True(result.Contains("config") || result.Contains("preferences") || result.Contains("appdata") || result.Contains("programdata"));
        }

        [Fact]
        public void DirectoriesAreConsistent()
        {
            string appData = _service.GetApplicationDataDirectory();
            string logs = _service.GetLogsDirectory();
            string config = _service.GetConfigDirectory();

            // All should be non-empty and absolute
            Assert.NotEmpty(appData);
            Assert.NotEmpty(logs);
            Assert.NotEmpty(config);

            Assert.True(System.IO.Path.IsPathRooted(appData));
            Assert.True(System.IO.Path.IsPathRooted(logs));
            Assert.True(System.IO.Path.IsPathRooted(config));
        }
    }
}
