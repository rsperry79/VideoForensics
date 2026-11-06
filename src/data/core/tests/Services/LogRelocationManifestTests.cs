using VideoForensics.Data.Core.Services;

using Xunit;

namespace VideoForensics.Data.Core.Tests.Services
{
    public class LogRelocationManifestTests : IDisposable
    {
        private readonly string _source = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        private readonly string _logs = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        public LogRelocationManifestTests()
        {
            _ = Directory.CreateDirectory(_source);
            _ = Directory.CreateDirectory(_logs);
        }

        public void Dispose()
        {
            foreach (string dir in new[] { _source, _logs })
            {
                try
                { Directory.Delete(dir, recursive: true); }
                catch { }
            }
        }

        [Fact]
        public void Reconcile_LinesWrittenAfterTheCopy_AreAppendedBeforeTheOldFileIsRemoved()
        {
            string oldFile = Path.Combine(_source, "audit-20261103.json");
            string newFile = Path.Combine(_logs, "audit-20261103.json");
            File.WriteAllText(oldFile, "a\n");
            File.WriteAllText(newFile, "a\n");
            LogRelocationManifest.Write(_logs, [new LogRelocationManifest.Entry(oldFile, "audit-20261103.json", 2L)]);

            // The old server kept writing to its open file after the copy
            File.AppendAllText(oldFile, "b\n");

            LogRelocationManifest.Reconcile(_logs);

            Assert.Equal("a\nb\n", File.ReadAllText(newFile));
            Assert.False(File.Exists(oldFile));
            Assert.False(File.Exists(Path.Combine(_logs, LogRelocationManifest.FileName)));
        }

        [Fact]
        public void Discard_AfterAFailedMove_RemovesTheCopiesAndKeepsTheOriginal()
        {
            string oldFile = Path.Combine(_source, "audit-20261103.json");
            string newFile = Path.Combine(_logs, "audit-20261103.json");
            File.WriteAllText(oldFile, "a\nb\n");
            File.WriteAllText(newFile, "a\n");
            LogRelocationManifest.Write(_logs, [new LogRelocationManifest.Entry(oldFile, "audit-20261103.json", 2L)]);

            LogRelocationManifest.Discard(_logs);

            Assert.Equal("a\nb\n", File.ReadAllText(oldFile));
            Assert.False(File.Exists(newFile));
            Assert.False(File.Exists(Path.Combine(_logs, LogRelocationManifest.FileName)));
        }

        [Fact]
        public void Reconcile_NoManifest_LeavesFilesAlone()
        {
            string file = Path.Combine(_logs, "audit-20261103.json");
            File.WriteAllText(file, "a\n");

            LogRelocationManifest.Reconcile(_logs);

            Assert.Equal("a\n", File.ReadAllText(file));
        }
    }
}
