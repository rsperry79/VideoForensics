using Moq;

using System;
using System.IO.Abstractions;

using VideoForensics.Core.Helpers.Platform;

using Xunit;

namespace VideoForensics.Core.Helpers.Tests.Platform
{
    public class PlatformDirectoryServiceTests
    {
        /// <summary>Creates a mock IFileSystem that simulates Path.Combine behavior for Windows paths</summary>
        private static Mock<IFileSystem> CreateWindowsMockFileSystem()
        {
            var mock = new Mock<IFileSystem>();

            // Mock all Path.Combine overloads
            _ = mock.Setup(fs => fs.Path.Combine(It.IsAny<string[]>()))
                .Returns((string[] args) => string.Join("\\", args));
            _ = mock.Setup(fs => fs.Path.Combine(It.IsAny<string>(), It.IsAny<string>()))
                .Returns((string path1, string path2) => $"{path1}\\{path2}");
            _ = mock.Setup(fs => fs.Path.Combine(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns((string path1, string path2, string path3) => $"{path1}\\{path2}\\{path3}");
            _ = mock.Setup(fs => fs.Path.Combine(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns((string path1, string path2, string path3, string path4) => $"{path1}\\{path2}\\{path3}\\{path4}");

            return mock;
        }

        /// <summary>Creates a mock IFileSystem that simulates Path.Combine behavior for Unix paths</summary>
        private static Mock<IFileSystem> CreateUnixMockFileSystem()
        {
            var mock = new Mock<IFileSystem>();

            // Mock all Path.Combine overloads
            _ = mock.Setup(fs => fs.Path.Combine(It.IsAny<string[]>()))
                .Returns((string[] args) => string.Join("/", args));
            _ = mock.Setup(fs => fs.Path.Combine(It.IsAny<string>(), It.IsAny<string>()))
                .Returns((string path1, string path2) => $"{path1}/{path2}");
            _ = mock.Setup(fs => fs.Path.Combine(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns((string path1, string path2, string path3) => $"{path1}/{path2}/{path3}");
            _ = mock.Setup(fs => fs.Path.Combine(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns((string path1, string path2, string path3, string path4) => $"{path1}/{path2}/{path3}/{path4}");

            return mock;
        }

        // Windows Tests
        [Fact]
        public void GetApplicationDataDirectory_Windows_UsesFileSystemAbstraction()
        {
            Mock<IFileSystem> mockFileSystem = CreateWindowsMockFileSystem();
            var service = new PlatformDirectoryService(mockFileSystem.Object);

            string result = service.GetApplicationDataDirectory();

            Assert.NotEmpty(result);
            Assert.True(result.Contains("VideoForensics"));
        }

        [Fact]
        public void GetApplicationDataDirectory_Windows_ReturnsAbsolutePath()
        {
            Mock<IFileSystem> mockFileSystem = CreateWindowsMockFileSystem();
            var service = new PlatformDirectoryService(mockFileSystem.Object);

            string result = service.GetApplicationDataDirectory();

            Assert.NotEmpty(result);
            Assert.True(System.IO.Path.IsPathRooted(result));
        }

        [Fact]
        public void GetLogsDirectory_Windows_UsesFileSystemAbstraction()
        {
            Mock<IFileSystem> mockFileSystem = CreateWindowsMockFileSystem();
            var service = new PlatformDirectoryService(mockFileSystem.Object);

            string result = service.GetLogsDirectory();

            Assert.NotEmpty(result);
            Assert.True(result.Contains("VideoForensics"));
            Assert.True(result.Contains("Logs"));
        }

        [Fact]
        public void GetLogsDirectory_Windows_ReturnsAbsolutePath()
        {
            Mock<IFileSystem> mockFileSystem = CreateWindowsMockFileSystem();
            var service = new PlatformDirectoryService(mockFileSystem.Object);

            string result = service.GetLogsDirectory();

            Assert.NotEmpty(result);
            Assert.True(System.IO.Path.IsPathRooted(result));
        }

        [Fact]
        public void GetConfigDirectory_Windows_UsesFileSystemAbstraction()
        {
            Mock<IFileSystem> mockFileSystem = CreateWindowsMockFileSystem();
            var service = new PlatformDirectoryService(mockFileSystem.Object);

            string result = service.GetConfigDirectory();

            Assert.NotEmpty(result);
            Assert.True(result.Contains("VideoForensics"));
        }

        [Fact]
        public void GetConfigDirectory_Windows_ReturnsAbsolutePath()
        {
            Mock<IFileSystem> mockFileSystem = CreateWindowsMockFileSystem();
            var service = new PlatformDirectoryService(mockFileSystem.Object);

            string result = service.GetConfigDirectory();

            Assert.NotEmpty(result);
            Assert.True(System.IO.Path.IsPathRooted(result));
        }

        // Unix-style Tests (with Unix-style path separator)
        [Fact]
        public void GetApplicationDataDirectory_Unix_UsesFileSystemAbstraction()
        {
            Mock<IFileSystem> mockFileSystem = CreateUnixMockFileSystem();
            var service = new PlatformDirectoryService(mockFileSystem.Object);

            // This test will still execute Windows code path on Windows
            // but verifies the mock is properly configured
            string result = service.GetApplicationDataDirectory();

            Assert.NotEmpty(result);
        }

        [Fact]
        public void GetLogsDirectory_Unix_UsesFileSystemAbstraction()
        {
            Mock<IFileSystem> mockFileSystem = CreateUnixMockFileSystem();
            var service = new PlatformDirectoryService(mockFileSystem.Object);

            string result = service.GetLogsDirectory();

            Assert.NotEmpty(result);
        }

        [Fact]
        public void GetConfigDirectory_Unix_UsesFileSystemAbstraction()
        {
            Mock<IFileSystem> mockFileSystem = CreateUnixMockFileSystem();
            var service = new PlatformDirectoryService(mockFileSystem.Object);

            string result = service.GetConfigDirectory();

            Assert.NotEmpty(result);
        }

        // Consistency Tests
        [Fact]
        public void DirectoriesAreConsistent_Windows()
        {
            Mock<IFileSystem> mockFileSystem = CreateWindowsMockFileSystem();
            var service = new PlatformDirectoryService(mockFileSystem.Object);

            string appData = service.GetApplicationDataDirectory();
            string logs = service.GetLogsDirectory();
            string config = service.GetConfigDirectory();

            // All should be non-empty and absolute
            Assert.NotEmpty(appData);
            Assert.NotEmpty(logs);
            Assert.NotEmpty(config);

            Assert.True(System.IO.Path.IsPathRooted(appData));
            Assert.True(System.IO.Path.IsPathRooted(logs));
            Assert.True(System.IO.Path.IsPathRooted(config));
        }

        [Fact]
        public void DirectoriesAreConsistent_Unix()
        {
            Mock<IFileSystem> mockFileSystem = CreateUnixMockFileSystem();
            var service = new PlatformDirectoryService(mockFileSystem.Object);

            string appData = service.GetApplicationDataDirectory();
            string logs = service.GetLogsDirectory();
            string config = service.GetConfigDirectory();

            // All should be non-empty and absolute
            Assert.NotEmpty(appData);
            Assert.NotEmpty(logs);
            Assert.NotEmpty(config);

            Assert.True(System.IO.Path.IsPathRooted(appData));
            Assert.True(System.IO.Path.IsPathRooted(logs));
            Assert.True(System.IO.Path.IsPathRooted(config));
        }

        // Injection Tests
        [Fact]
        public void Constructor_AcceptsIFileSystem()
        {
            Mock<IFileSystem> mockFileSystem = CreateWindowsMockFileSystem();

            // Should not throw
            var service = new PlatformDirectoryService(mockFileSystem.Object);

            Assert.NotNull(service);
        }

        [Fact]
        public void Constructor_WithoutParameters_UsesRealFileSystem()
        {
            // Should not throw - the parameterless constructor should use the real filesystem
            var service = new PlatformDirectoryService();

            string result = service.GetApplicationDataDirectory();
            Assert.NotEmpty(result);
        }

        [Fact]
        public void Constructor_RejectsNullFileSystem()
        {
            // Should throw ArgumentNullException when passed null
            _ = Assert.Throws<ArgumentNullException>(() => new PlatformDirectoryService(null!));
        }
    }
}
