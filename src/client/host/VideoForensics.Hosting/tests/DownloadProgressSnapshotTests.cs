using VideoForensics.Client.Common.Contracts;
using VideoForensics.Core.Providers.Common.Contracts;

namespace VideoForensics.Hosting.Tests
{
    public class DownloadProgressSnapshotTests
    {
        [Fact]
        public void DownloadProgressDtoMapping_ToDownloadProgressSnapshot_NeverDrainsActivityLog()
        {
            var service = new Mock<IVideoDownloadService>();
            _ = service.Setup(s => s.GetProgress()).Returns(new DownloadStatus(true, 1, 4, 100L, "a.mp4", 2, 8, 200L, 3, 1.5));
            _ = service.Setup(s => s.GetCurrentDevice()).Returns((1, 3, "Garage"));
            _ = service.Setup(s => s.DrainActivityLog()).Returns(["should not be consumed"]);

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
            _ = service.Setup(s => s.GetProgress()).Returns(status);
            _ = service.Setup(s => s.GetCurrentDevice()).Returns((0, 0, string.Empty));
            _ = service.Setup(s => s.DrainActivityLog()).Returns(["pending line"]);
            _ = service.Setup(s => s.GetPreScanCounts()).Returns(new Dictionary<string, int> { ["dev-2"] = 4 });
            _ = service.Setup(s => s.GetLastError()).Returns("boom");
            _ = service.Setup(s => s.GetRemainingReason()).Returns("Rate limited");

            Core.Api.Contracts.DownloadProgressDto dto = service.Object.ToDownloadProgressSnapshot();

            Assert.Empty(dto.Activity);
            Assert.Equal(status.ToDto(), dto.Progress);
            Assert.Equal(4, dto.PreScanCounts["dev-2"]);
            Assert.Equal("boom", dto.LastError);
            Assert.Equal("Rate limited", dto.RemainingReason);
        }
    }
}
