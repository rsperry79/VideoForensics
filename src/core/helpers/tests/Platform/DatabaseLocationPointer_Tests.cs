using System;
using System.IO;

using VideoForensics.Core.Helpers.Platform;

using Xunit;

namespace VideoForensics.Core.Helpers.Tests.Platform
{
    public class DatabaseLocationPointer_Tests : IDisposable
    {
        private readonly string _dataRoot = Path.Combine(Path.GetTempPath(), "vf-pointer-" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            if (Directory.Exists(_dataRoot))
            {
                Directory.Delete(_dataRoot, recursive: true);
            }
        }

        [Fact]
        public void Read_NoPointerFile_ReturnsNull()
        {
            Assert.Null(DatabaseLocationPointer.Read(_dataRoot));
        }

        [Fact]
        public void Write_ThenRead_ReturnsSamePath()
        {
            const string databaseRoot = @"E:\VideoForensics\Database";

            DatabaseLocationPointer.Write(_dataRoot, databaseRoot);

            Assert.Equal(databaseRoot, DatabaseLocationPointer.Read(_dataRoot));
        }

        [Fact]
        public void Read_CorruptPointerFile_ReturnsNull()
        {
            _ = Directory.CreateDirectory(_dataRoot);
            File.WriteAllText(Path.Combine(_dataRoot, DatabaseLocationPointer.FileName), "{ not json");

            Assert.Null(DatabaseLocationPointer.Read(_dataRoot));
        }

        [Fact]
        public void Read_BlankPath_ReturnsNull()
        {
            DatabaseLocationPointer.Write(_dataRoot, "   ");

            Assert.Null(DatabaseLocationPointer.Read(_dataRoot));
        }
    }
}
