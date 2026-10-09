using Moq;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Providers.Common.Contracts;

using Xunit;

namespace VideoForensics.Hosting.Tests
{
    public class DownloadProgressSnapshotTests
    {
        [Fact]
        public void DownloadProgressDtoMapping_ToDownloadProgressSnapshot_NeverDrainsActivityLog()
        {
            var service = new Mock<IVideoDownloadService>();
            service.Setup(s => s.GetProgress()).Returns(new DownloadStatus(true, 1, 4, 100L, "a.mp4", 2, 8, 200L, 3, 1.5));
            service.Setup(s => s.GetCurrentDevice()).Returns((1, 3, "Garage"));
            service.Setup(s => s.DrainActivityLog()).Returns(new List<string> { "should not be consumed" });

            // Called twice to prove a snapshot consumes nothing and can be repeated safely.
            _ = service.Object.ToDownloadProgressSnapshot();
            _ = service.Object.ToDownloadProgressSnapshot();

            service.Verify(s => s.DrainActivityLog(), Times.Never);
        }

        [Fact]
        public void DownloadProgressDtoMapping_ToDownloadProgressSnapshot_ReturnsEmptyActivityAndCopiesState()
        {
            var status = new DownloadStatus(false, 0, 0, 0L, null, 5, 9, 700L, 1, 0.0);

            var service = new Mock<IVideoDownloadService>();
            service.Setup(s => s.GetProgress()).Returns(status);
            service.Setup(s => s.GetCurrentDevice()).Returns((0, 0, string.Empty));
            service.Setup(s => s.DrainActivityLog()).Returns(new List<string> { "pending line" });
            service.Setup(s => s.GetPreScanCounts()).Returns(new Dictionary<string, int> { ["dev-2"] = 4 });
            service.Setup(s => s.GetLastError()).Returns("boom");
            service.Setup(s => s.GetRemainingReason()).Returns("Rate limited");

            var dto = service.Object.ToDownloadProgressSnapshot();

            Assert.Empty(dto.Activity);
            Assert.Equal(status.ToDto(), dto.Progress);
            Assert.Equal(4, dto.PreScanCounts["dev-2"]);
            Assert.Equal("boom", dto.LastError);
            Assert.Equal("Rate limited", dto.RemainingReason);
        }
    }
}
