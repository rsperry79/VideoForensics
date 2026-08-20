using Microsoft.VisualStudio.TestTools.UnitTesting;
using KoenZomers.Ring.Api.Utils;

namespace Ring.Api.Utils.Tests
{
    [TestClass]
    public class RunnerTests
    {
        [TestMethod]
        public void Runner_RequiresSession()
        {
            var outputDir = Path.Combine(Path.GetTempPath(), "test");
            try
            {
                var runner = new Runner(null!, outputDir, quiet: true);
                Assert.Fail("Expected ArgumentNullException");
            }
            catch (ArgumentNullException)
            {
                // Expected
            }
        }

        [TestMethod]
        public void Runner_CanBeConstructedWithValidSession()
        {
            var session = new KoenZomers.Ring.Api.Session("user", "pass");
            var outputDir = Path.Combine(Path.GetTempPath(), "test");
            var runner = new Runner(session, outputDir, quiet: true);
            Assert.IsNotNull(runner);
        }
    }
}
