using VideoForensics.Core.Providers.Common.Services;

using Xunit;

namespace VideoForensics.Core.Providers.Common.Tests.Services
{
    /// <summary>Placeholder detection is a pure function of connected state and two hashes, so these tests need no fixtures.</summary>
    public class SnapshotPlaceholderDetectorTests
    {
        private const string Hash = "ab12cd34ef56";
        private const string OtherHash = "99ffee00dd11";

        [Fact]
        public void Check_NotConnectedWithDifferentPreviousHash_ReturnsOffline()
        {
            SnapshotPlaceholderReason? reason = SnapshotPlaceholderDetector.Check(false, Hash, OtherHash);

            Assert.Equal(SnapshotPlaceholderReason.Offline, reason);
        }

        [Fact]
        public void Check_NotConnectedAndIdenticalHash_ReturnsOfflineBecauseOfflineWins()
        {
            SnapshotPlaceholderReason? reason = SnapshotPlaceholderDetector.Check(false, Hash, Hash);

            Assert.Equal(SnapshotPlaceholderReason.Offline, reason);
        }

        [Fact]
        public void Check_ConnectedAndSameHash_ReturnsIdenticalFrame()
        {
            SnapshotPlaceholderReason? reason = SnapshotPlaceholderDetector.Check(true, Hash, Hash);

            Assert.Equal(SnapshotPlaceholderReason.IdenticalFrame, reason);
        }

        [Fact]
        public void Check_SameHashDifferentCase_ReturnsIdenticalFrame()
        {
            SnapshotPlaceholderReason? reason = SnapshotPlaceholderDetector.Check(true, "AB12CD34EF56", "ab12cd34ef56");

            Assert.Equal(SnapshotPlaceholderReason.IdenticalFrame, reason);
        }

        [Fact]
        public void Check_ConnectionUnknownAndSameHash_ReturnsIdenticalFrame()
        {
            SnapshotPlaceholderReason? reason = SnapshotPlaceholderDetector.Check(null, Hash, Hash);

            Assert.Equal(SnapshotPlaceholderReason.IdenticalFrame, reason);
        }

        [Fact]
        public void Check_ConnectedWithNoPreviousHash_ReturnsNull()
        {
            SnapshotPlaceholderReason? reason = SnapshotPlaceholderDetector.Check(true, Hash, null);

            Assert.Null(reason);
        }

        [Fact]
        public void Check_ConnectedWithEmptyPreviousHash_ReturnsNull()
        {
            SnapshotPlaceholderReason? reason = SnapshotPlaceholderDetector.Check(true, Hash, string.Empty);

            Assert.Null(reason);
        }

        [Fact]
        public void Check_ConnectedWithDifferentHash_ReturnsNull()
        {
            SnapshotPlaceholderReason? reason = SnapshotPlaceholderDetector.Check(true, Hash, OtherHash);

            Assert.Null(reason);
        }

        [Fact]
        public void Check_WhitespaceSha256_ThrowsArgumentException()
        {
            _ = Assert.Throws<ArgumentException>(() => SnapshotPlaceholderDetector.Check(true, "   ", null));
        }
    }
}
