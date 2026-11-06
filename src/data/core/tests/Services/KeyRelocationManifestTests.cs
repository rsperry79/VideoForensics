using VideoForensics.Data.Core.Services;

using Xunit;

namespace VideoForensics.Data.Core.Tests.Services
{
    public class KeyRelocationManifestTests : IDisposable
    {
        private readonly string _source = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        private readonly string _keys = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        public KeyRelocationManifestTests()
        {
            _ = Directory.CreateDirectory(_source);
            _ = Directory.CreateDirectory(_keys);
        }

        public void Dispose()
        {
            foreach (string dir in new[] { _source, _keys })
            {
                try
                { Directory.Delete(dir, recursive: true); }
                catch { }
            }
        }

        [Fact]
        public void Reconcile_MatchingHash_DeletesTheOriginalAndTheManifest()
        {
            string original = Path.Combine(_source, "master.key");
            string copy = Path.Combine(_keys, "master.key");
            File.WriteAllText(original, "secret");
            File.WriteAllText(copy, "secret");
            KeyRelocationManifest.Write(_keys, [new KeyRelocationManifest.Entry(original, "master.key", KeyRelocationManifest.HashOf(copy))]);

            KeyRelocationManifest.Reconcile(_keys);

            Assert.False(File.Exists(original));
            Assert.Equal("secret", File.ReadAllText(copy));
            Assert.False(File.Exists(Path.Combine(_keys, KeyRelocationManifest.FileName)));
        }

        [Fact]
        public void Reconcile_HashMismatch_KeepsTheOriginalAndTheManifest()
        {
            string original = Path.Combine(_source, "master.key");
            string copy = Path.Combine(_keys, "master.key");
            File.WriteAllText(original, "secret");
            File.WriteAllText(copy, "corrupted");
            KeyRelocationManifest.Write(_keys, [new KeyRelocationManifest.Entry(original, "master.key", KeyRelocationManifest.HashOf(original))]);

            KeyRelocationManifest.Reconcile(_keys);

            Assert.True(File.Exists(original));
            Assert.True(File.Exists(Path.Combine(_keys, KeyRelocationManifest.FileName)));
        }
    }
}
