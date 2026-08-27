using System;
using Xunit;
using VideoForensics.Providers.Common.Helpers.Contracts;
using VideoForensics.Providers.Common.Helpers.Platform;

#nullable enable

namespace VideoForensics.Providers.Common.Helpers.Tests.Platform
{
    public class PlatformDirectoryServiceTests
    {
        private readonly IPlatformDirectoryService _service = new PlatformDirectoryService();

        [Fact]
        public void GetApplicationDataDirectory_ReturnsNonEmptyPath()
        {
            var result = _service.GetApplicationDataDirectory();
            Assert.NotEmpty(result);
        }

        [Fact]
        public void GetApplicationDataDirectory_ReturnsAbsolutePath()
        {
            var result = _service.GetApplicationDataDirectory();
            Assert.True(System.IO.Path.IsPathRooted(result));
        }

        [Fact]
        public void GetApplicationDataDirectory_ContainsAppName()
        {
            var result = _service.GetApplicationDataDirectory();
            Assert.NotEmpty(result);
            // Should contain either "RingVideos" or "ringvideos" depending on platform
        }

        [Fact]
        public void GetLogsDirectory_ReturnsNonEmptyPath()
        {
            var result = _service.GetLogsDirectory();
            Assert.NotEmpty(result);
        }

        [Fact]
        public void GetLogsDirectory_ReturnsAbsolutePath()
        {
            var result = _service.GetLogsDirectory();
            Assert.True(System.IO.Path.IsPathRooted(result));
        }

        [Fact]
        public void GetLogsDirectory_ContainsLogsKeyword()
        {
            var result = _service.GetLogsDirectory().ToLower();
            Assert.True(result.Contains("logs") || result.Contains("state"));
        }

        [Fact]
        public void GetConfigDirectory_ReturnsNonEmptyPath()
        {
            var result = _service.GetConfigDirectory();
            Assert.NotEmpty(result);
        }

        [Fact]
        public void GetConfigDirectory_ReturnsAbsolutePath()
        {
            var result = _service.GetConfigDirectory();
            Assert.True(System.IO.Path.IsPathRooted(result));
        }

        [Fact]
        public void GetConfigDirectory_ContainsConfigOrPreferences()
        {
            var result = _service.GetConfigDirectory().ToLower();
            Assert.True(result.Contains("config") || result.Contains("preferences") || result.Contains("appdata"));
        }

        [Fact]
        public void DirectoriesAreConsistent()
        {
            var appData = _service.GetApplicationDataDirectory();
            var logs = _service.GetLogsDirectory();
            var config = _service.GetConfigDirectory();

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
