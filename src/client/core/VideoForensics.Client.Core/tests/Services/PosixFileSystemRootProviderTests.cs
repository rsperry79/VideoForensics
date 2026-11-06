using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Core.Services;

using Xunit;

namespace VideoForensics.Client.Core.Tests.Services
{
    /// <summary>
    /// Tests for <see cref="PosixFileSystemRootProvider"/>: POSIX hosts expose exactly one root, <c>/</c>.
    /// </summary>
    public class PosixFileSystemRootProviderTests
    {
        /// <summary>Verifies the provider returns a single root named and pathed "/".</summary>
        [Fact]
        public void GetRoots_Always_ReturnsSingleSlashRoot()
        {
            var provider = new PosixFileSystemRootProvider();

            IReadOnlyList<FileSystemRoot> roots = provider.GetRoots();

            FileSystemRoot root = Assert.Single(roots);
            Assert.Equal("/", root.Name);
            Assert.Equal("/", root.Path);
        }
    }
}
