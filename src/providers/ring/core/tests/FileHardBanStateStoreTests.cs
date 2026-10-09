using System;
using System.IO;

using VideoForensics.Providers.Common.Helpers.Platform;
using Xunit;

namespace VideoForensics.Providers.Ring.Core.Tests
{
    /// <summary>Tests for <see cref="FileHardBanStateStore"/>, the file-backed hard-ban persistence.</summary>
    public class FileHardBanStateStoreTests : IDisposable
    {
        private readonly string _testDirectory;

        public FileHardBanStateStoreTests()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), "vf-hardbanstore-tests-" + Guid.NewGuid().ToString("N"));
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, true);
            }
        }

        [Fact]
        public void Write_ThenRead_RoundTripsValue()
        {
            var store = new FileHardBanStateStore(_testDirectory);
            var until = new DateTime(2030, 1, 2, 3, 4, 5, DateTimeKind.Utc);

            store.Write(until);

            Assert.Equal(until, store.Read());
            Assert.Equal(DateTimeKind.Utc, store.Read()!.Value.Kind);
        }

        [Fact]
        public void Read_WithNoFile_ReturnsNull()
        {
            var store = new FileHardBanStateStore(_testDirectory);

            Assert.Null(store.Read());
        }

        [Fact]
        public void Read_WithInvalidContent_ReturnsNull()
        {
            Directory.CreateDirectory(_testDirectory);
            var store = new FileHardBanStateStore(_testDirectory);
            File.WriteAllText(store.FilePath, "not_a_valid_number");

            Assert.Null(store.Read());
        }

        [Fact]
        public void Clear_AfterWrite_RemovesValueAndFile()
        {
            var store = new FileHardBanStateStore(_testDirectory);
            store.Write(DateTime.UtcNow.AddHours(1));

            store.Clear();

            Assert.Null(store.Read());
            Assert.False(File.Exists(store.FilePath));
        }

        [Fact]
        public void Clear_WithNoFile_DoesNotThrow()
        {
            var store = new FileHardBanStateStore(_testDirectory);

            store.Clear();

            Assert.Null(store.Read());
        }

        [Fact]
        public void Write_WithMissingDirectory_CreatesDirectory()
        {
            var store = new FileHardBanStateStore(_testDirectory);
            Assert.False(Directory.Exists(_testDirectory));

            store.Write(DateTime.UtcNow.AddHours(1));

            Assert.True(Directory.Exists(_testDirectory));
            Assert.True(File.Exists(store.FilePath));
        }

        [Fact]
        public void FilePath_WithDirectory_IsInsideDirectory()
        {
            var store = new FileHardBanStateStore(_testDirectory);

            Assert.Equal(Path.Combine(_testDirectory, "ring_hard_ban.txt"), store.FilePath);
        }

        [Fact]
        public void FilePath_WithoutDirectory_IsProgramDataApplicationDirectory()
        {
            var store = new FileHardBanStateStore();

            string expected = Path.Combine(new PlatformDirectoryService().GetApplicationDataDirectory(), "ring_hard_ban.txt");
            Assert.Equal(expected, store.FilePath);
        }
    }
}
