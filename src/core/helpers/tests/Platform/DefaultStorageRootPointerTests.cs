using System;
using System.IO;

using VideoForensics.Core.Helpers.Platform;

using Xunit;

namespace VideoForensics.Core.Helpers.Tests.Platform
{
    /// <summary>The default storage root pointer saves the chosen default root in the data root and reads it back, or null when it is missing or corrupt.</summary>
    public class DefaultStorageRootPointerTests : IDisposable
    {
        private readonly string _dataRoot = Path.Combine(Path.GetTempPath(), "vf-default-root-" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            if (Directory.Exists(_dataRoot))
            {
                Directory.Delete(_dataRoot, recursive: true);
            }
        }

        [Fact]
        public void DefaultStorageRootPointer_Read_MissingFile_ReturnsNull()
        {
            Assert.Null(DefaultStorageRootPointer.Read(_dataRoot));
        }

        [Fact]
        public void DefaultStorageRootPointer_WriteThenRead_ReturnsTheRoot()
        {
            DefaultStorageRootPointer.Write(_dataRoot, @"D:\VideoForensics");

            Assert.Equal(@"D:\VideoForensics", DefaultStorageRootPointer.Read(_dataRoot));
        }

        [Fact]
        public void DefaultStorageRootPointer_Read_CorruptFile_ReturnsNull()
        {
            _ = Directory.CreateDirectory(_dataRoot);
            File.WriteAllText(Path.Combine(_dataRoot, DefaultStorageRootPointer.FileName), "not json");

            Assert.Null(DefaultStorageRootPointer.Read(_dataRoot));
        }
    }
}
