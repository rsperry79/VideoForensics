using Moq;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Ui.Shared.Services;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Services
{
    /// <summary>
    /// Tests for <see cref="DirectoryBrowserService.GetRoots"/>: roots come from the injected provider.
    /// </summary>
    public class DirectoryBrowserServiceRootTests
    {
        /// <summary>Verifies each provider root is mapped to a DirectoryEntry with the same name and path.</summary>
        [Fact]
        public void GetRoots_ProviderReturnsTwoRoots_ReturnsTwoMatchingEntries()
        {
            var provider = new Mock<IFileSystemRootProvider>();
            provider.Setup(p => p.GetRoots()).Returns(new List<FileSystemRoot>
            {
                new FileSystemRoot("C:\\", "C:\\"),
                new FileSystemRoot("D:\\", "D:\\"),
            });
            var service = new DirectoryBrowserService(provider.Object);

            var entries = service.GetRoots();

            Assert.Equal(2, entries.Count);
            Assert.Equal(new DirectoryEntry("C:\\", "C:\\"), entries[0]);
            Assert.Equal(new DirectoryEntry("D:\\", "D:\\"), entries[1]);
        }
    }
}
