using Xunit;

namespace VideoForensics.Core.Providers.Core.Tests
{
    public class FfmpegBinDirectoryLocatorTests
    {
        private static string Ext => OperatingSystem.IsWindows() ? ".exe" : "";

        private static string CreateTempDir()
        {
            var dir = Path.Combine(Path.GetTempPath(), "vf-bindir-" + Guid.NewGuid().ToString("N"));
            _ = Directory.CreateDirectory(dir);
            return dir;
        }

        private static void TouchFile(string dir, string baseName)
        {
            File.WriteAllText(Path.Combine(dir, baseName + Ext), "dummy");
        }

        [Fact]
        public void FindBinDirectory_FirstDirWithBothExecutables_ReturnsThatDirectory()
        {
            var empty = CreateTempDir();
            var good = CreateTempDir();
            try
            {
                TouchFile(good, "ffmpeg");
                TouchFile(good, "ffprobe");

                var result = FfmpegBinDirectoryLocator.FindBinDirectory(new[] { empty, good });

                Assert.Equal(good, result);
            }
            finally
            {
                Directory.Delete(empty, true);
                Directory.Delete(good, true);
            }
        }

        [Fact]
        public void FindBinDirectory_OnlyFfmpegPresent_ReturnsNull()
        {
            var dir = CreateTempDir();
            try
            {
                TouchFile(dir, "ffmpeg");

                var result = FfmpegBinDirectoryLocator.FindBinDirectory(new[] { dir });

                Assert.Null(result);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Fact]
        public void FindBinDirectory_NullAndWhitespaceCandidates_AreSkipped()
        {
            var good = CreateTempDir();
            try
            {
                TouchFile(good, "ffmpeg");
                TouchFile(good, "ffprobe");

                string?[] candidates = { null, "   ", good };
                var result = FfmpegBinDirectoryLocator.FindBinDirectory(candidates);

                Assert.Equal(good, result);
            }
            finally
            {
                Directory.Delete(good, true);
            }
        }

        [Fact]
        public void FindExecutable_ReturnsFirstDirectoryContainingFile()
        {
            var empty = CreateTempDir();
            var good = CreateTempDir();
            try
            {
                TouchFile(good, "cloudflared");

                var result = FfmpegBinDirectoryLocator.FindExecutable(new string?[] { empty, good }, "cloudflared");

                Assert.Equal(Path.Combine(good, "cloudflared" + Ext), result);
            }
            finally
            {
                Directory.Delete(empty, true);
                Directory.Delete(good, true);
            }
        }

        [Fact]
        public void FindExecutable_NoMatch_ReturnsNull()
        {
            var dir = CreateTempDir();
            try
            {
                var result = FfmpegBinDirectoryLocator.FindExecutable(new string?[] { dir, null }, "cloudflared");

                Assert.Null(result);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Fact]
        public void DefaultCandidates_AppBaseThenPathEntries_YieldsInOrderAndSkipsEmpty()
        {
            var pathVariable = string.Join(Path.PathSeparator, "C:\\first", "", "\"C:\\second\"", "C:\\third");

            var result = FfmpegBinDirectoryLocator.DefaultCandidates("C:\\app", pathVariable).ToList();

            Assert.Equal(new[] { "C:\\app", "C:\\first", "C:\\second", "C:\\third" }, result);
        }

        [Fact]
        public void DefaultCandidates_MultiplePathVariables_YieldsAppBaseThenEachInOrder()
        {
            var processPath = "C:\\process";
            var userPath = "C:\\user";
            var machinePath = "C:\\machine";

            var result = FfmpegBinDirectoryLocator.DefaultCandidates("C:\\app", new[] { processPath, null, userPath, machinePath }).ToList();

            Assert.Equal(new[] { "C:\\app", "C:\\process", "C:\\user", "C:\\machine" }, result);
        }

        [Fact]
        public void DefaultCandidates_PathEntryWithEnvironmentVariable_IsExpanded()
        {
            var pathVariable = "%SystemRoot%\\bin";

            var result = FfmpegBinDirectoryLocator.DefaultCandidates("C:\\app", pathVariable).ToList();

            Assert.Equal(new[] { "C:\\app", Environment.ExpandEnvironmentVariables("%SystemRoot%\\bin") }, result);
        }
    }
}
