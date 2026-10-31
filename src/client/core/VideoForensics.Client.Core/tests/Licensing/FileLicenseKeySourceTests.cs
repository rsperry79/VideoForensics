using VideoForensics.Client.Core.Licensing;

using Xunit;

namespace VideoForensics.Client.Core.Tests.Licensing
{
    /// <summary>
    /// Tests for <see cref="FileLicenseKeySource"/>: the external Syncfusion key file that overrides every other source.
    /// </summary>
    public class FileLicenseKeySourceTests : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "vf-license-" + Guid.NewGuid().ToString("N"));

        public FileLicenseKeySourceTests()
        {
            Directory.CreateDirectory(_directory);
        }

        public void Dispose()
        {
            Directory.Delete(_directory, recursive: true);
        }

        /// <summary>Verifies that a missing key file reads as no key, so the registrar falls through to the next source.</summary>
        [Fact]
        public void ReadKey_FileMissing_ReturnsNull()
        {
            var source = new FileLicenseKeySource(_directory);

            Assert.Null(source.ReadKey());
        }

        /// <summary>Verifies that the key file's content is returned trimmed, since editors often leave a trailing newline.</summary>
        [Fact]
        public void ReadKey_FilePresent_ReturnsTrimmedKey()
        {
            File.WriteAllText(Path.Combine(_directory, "syncfusion-license.key"), "  abc123\r\n");
            var source = new FileLicenseKeySource(_directory);

            Assert.Equal("abc123", source.ReadKey());
        }

        /// <summary>Verifies that a blank key file counts as absent, matching the other sources.</summary>
        [Fact]
        public void ReadKey_BlankFile_ReturnsNull()
        {
            File.WriteAllText(Path.Combine(_directory, "syncfusion-license.key"), "   \r\n");
            var source = new FileLicenseKeySource(_directory);

            Assert.Null(source.ReadKey());
        }
    }
}
