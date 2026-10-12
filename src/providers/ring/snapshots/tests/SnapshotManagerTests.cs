using Xunit;

namespace VideoForensics.Providers.Ring.Snapshots.Tests
{
    public class SnapshotManagerTests
    {
        [Fact]
        public void SnapshotManager_RequiresSession()
        {
            try
            {
                var manager = new SnapshotManager(null!);
                Assert.Fail("Expected ArgumentNullException");
            }
            catch (ArgumentNullException)
            {
                // Expected
            }
        }

        [Fact]
        public void SnapshotManager_CanBeConstructedWithValidSession()
        {
            var session = new VideoForensics.Providers.Ring.Session("user", "pass");
            var manager = new SnapshotManager(session);
            Assert.NotNull(manager);
        }
    }
}
