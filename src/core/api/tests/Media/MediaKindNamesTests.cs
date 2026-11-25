using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Core.Api.Contracts.Tests
{
    public class MediaKindNamesTests
    {
        [Fact]
        public void MediaKindNames_MatchTheServerMediaKinds()
        {
            // Why: clients compare against these strings, so they must equal the stored values exactly.
            Assert.Equal(MediaKinds.Snapshot, MediaKindNames.Snapshot);
            Assert.Equal(MediaKinds.Timelapse, MediaKindNames.Timelapse);
        }
    }
}
