using VideoForensics.Data.Core.Services;

using Xunit;

namespace VideoForensics.Data.Core.Tests
{
    public class SnapshotTimelineOptionsTests
    {
        [Fact]
        public void SnapshotTimelineOptions_Default_UsesProgramDataSnapshotRoot()
        {
            Assert.Equal(@"C:\ProgramData\VideoForensics\Snapshots", new SnapshotTimelineOptions().SnapshotRoot);
        }
    }
}
