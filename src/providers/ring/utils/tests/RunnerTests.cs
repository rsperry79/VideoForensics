using Xunit;
using VideoForensics.Providers.Ring.Utils;

namespace VideoForensics.Providers.Ring.Utils.Tests
{
    public class RunnerTests
    {
        [Fact]
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

        [Fact]
        public void Runner_CanBeConstructedWithValidSession()
        {
            var session = new VideoForensics.Providers.Ring.Session("user", "pass");
            var outputDir = Path.Combine(Path.GetTempPath(), "test");
            var runner = new Runner(session, outputDir, quiet: true);
            Assert.NotNull(runner);
        }
    }
}
