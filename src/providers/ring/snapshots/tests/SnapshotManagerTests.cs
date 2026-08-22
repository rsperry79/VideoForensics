using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace VideoForensics.Providers.Ring.Snapshots.Tests
{
    [TestClass]
    public class SnapshotManagerTests
    {
        [TestMethod]
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

        [TestMethod]
        public void SnapshotManager_CanBeConstructedWithValidSession()
        {
            var session = new Ring.Api.Session("user", "pass");
            var manager = new SnapshotManager(session);
            Assert.IsNotNull(manager);
        }
    }
}

