using Xunit;

namespace VideoForensics.Providers.Ring.Utils.Tests
{
    public class RunnerTests
    {
        [Fact]
        public void Runner_RequiresSession()
        {
            string outputDir = Path.Combine(Path.GetTempPath(), "test");
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

        [Fact]
        public void Runner_CanBeConstructedWithValidSession()
        {
            var session = new VideoForensics.Providers.Ring.Session("user", "pass");
            string outputDir = Path.Combine(Path.GetTempPath(), "test");
            var runner = new Runner(session, outputDir, quiet: true);
            Assert.NotNull(runner);
        }
    }
}
