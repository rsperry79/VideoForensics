using Xunit;

namespace VideoForensics.Client.Platform.Windows.Tests
{
    public class PendingUpdateProbeTests
    {
        private static string NewRoot() => Path.Combine(Path.GetTempPath(), "vf-pending-tests", Guid.NewGuid().ToString("N"));

        private static void Package(string root, string folder)
        {
            string dir = Path.Combine(root, folder);
            _ = Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "manifest.json"), "{}");
        }

        [Fact]
        public void PendingUpdateProbe_MissingFolder_IsNotPending()
        {
            Assert.False(PendingUpdateProbe.HasPending(NewRoot()));
        }

        [Fact]
        public void PendingUpdateProbe_StagedPackage_IsPending()
        {
            string root = NewRoot();
            Package(root, "1.1.0");

            Assert.True(PendingUpdateProbe.HasPending(root));
        }

        [Theory]
        [InlineData(".staging")]
        [InlineData("previous")]
        [InlineData("1.1.0.rejected")]
        public void PendingUpdateProbe_NonPackageFolders_AreNotPending(string folder)
        {
            string root = NewRoot();
            Package(root, folder);

            Assert.False(PendingUpdateProbe.HasPending(root));
        }

        [Fact]
        public void PendingUpdateProbe_FolderWithoutManifest_IsNotPending()
        {
            string root = NewRoot();
            _ = Directory.CreateDirectory(Path.Combine(root, "1.1.0"));

            Assert.False(PendingUpdateProbe.HasPending(root));
        }
    }
}
