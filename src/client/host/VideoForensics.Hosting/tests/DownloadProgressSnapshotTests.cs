using VideoForensics.Client.Common.Contracts;
using VideoForensics.Core.Api.Contracts;

namespace VideoForensics.Client.Hosting.Tests
{
    public class DownloadProgressSnapshotTests
    {
        [Fact]
        public void DownloadProgressDtoMapping_ToDownloadProgressDto_CarriesActivityLinesAndZeroedNumericFields()
        {
            var log = new Mock<ISyncActivityLog>();
            _ = log.Setup(l => l.Drain()).Returns(["sync started", "discovered 3 devices"]);

            DownloadProgressDto dto = log.Object.ToDownloadProgressDto();

            Assert.Equal(new[] { "sync started", "discovered 3 devices" }, dto.Activity);
            Assert.Equal(new DownloadStatusDto(false, 0, 0, 0L, null, 0, 0, 0L, 0, 0.0), dto.Progress);
            Assert.Equal(0, dto.CurrentDeviceIndex);
            Assert.Equal(0, dto.CurrentDeviceTotal);
            Assert.Null(dto.CurrentDeviceName);
            Assert.Empty(dto.PreScanCounts);
            Assert.Null(dto.LastError);
            Assert.Null(dto.RemainingReason);
        }

        [Fact]
        public void DownloadProgressDtoMapping_ToDownloadProgressDto_DrainsSyncLogExactlyOnce()
        {
            var log = new Mock<ISyncActivityLog>();
            _ = log.Setup(l => l.Drain()).Returns(Array.Empty<string>());

            _ = log.Object.ToDownloadProgressDto();

            log.Verify(l => l.Drain(), Times.Once);
        }

        [Fact]
        public void DownloadProgressDtoMapping_IdleSnapshot_HasNoActivityAndNeutralState()
        {
            DownloadProgressDto dto = DownloadProgressDtoMapping.IdleSnapshot();

            Assert.Empty(dto.Activity);
            Assert.Equal(new DownloadStatusDto(false, 0, 0, 0L, null, 0, 0, 0L, 0, 0.0), dto.Progress);
            Assert.Equal(0, dto.CurrentDeviceIndex);
            Assert.Equal(0, dto.CurrentDeviceTotal);
            Assert.Null(dto.CurrentDeviceName);
            Assert.Empty(dto.PreScanCounts);
            Assert.Null(dto.LastError);
            Assert.Null(dto.RemainingReason);
        }
    }
}
