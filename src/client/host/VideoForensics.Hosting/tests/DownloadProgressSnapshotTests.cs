using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Hosting.Contracts;
using VideoForensics.Client.Hosting.Services;
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

        [Fact]
        public void DownloadProgressDtoMapping_ToDownloadProgressDto_WithStore_CarriesProgressAndDrainsActivity()
        {
            var log = new Mock<ISyncActivityLog>();
            _ = log.Setup(l => l.Drain()).Returns(["sync started"]);
            var store = new SyncProgressStore();
            store.Set(new SyncProgress(true, 5, 3, 2048L, "a.mp4", 2, 2, 4, "Garage"));

            DownloadProgressDto dto = log.Object.ToDownloadProgressDto(store);

            Assert.Equal(new DownloadStatusDto(true, 3, 5, 2048L, "a.mp4", 0, 0, 0L, 2, 0.0), dto.Progress);
            Assert.Equal(2, dto.CurrentDeviceIndex);
            Assert.Equal(4, dto.CurrentDeviceTotal);
            Assert.Equal("Garage", dto.CurrentDeviceName);
            Assert.Equal(new[] { "sync started" }, dto.Activity);
            log.Verify(l => l.Drain(), Times.Once);
        }

        [Fact]
        public void DownloadProgressDtoMapping_IdleSnapshot_WithStore_CarriesProgressAndLeavesActivityInLog()
        {
            var log = new SyncActivityLog();
            log.Append("pending line");
            var store = new SyncProgressStore();
            store.Set(new SyncProgress(true, 5, 3, 2048L, "a.mp4", 2, 2, 4, "Garage"));

            DownloadProgressDto dto = DownloadProgressDtoMapping.IdleSnapshot(store);

            Assert.Equal(new DownloadStatusDto(true, 3, 5, 2048L, "a.mp4", 0, 0, 0L, 2, 0.0), dto.Progress);
            Assert.Equal("Garage", dto.CurrentDeviceName);
            Assert.Empty(dto.Activity);
            Assert.Equal(new[] { "pending line" }, log.Drain());
        }

        [Fact]
        public void DownloadProgressDtoMapping_IdleSnapshot_NoStore_StaysNeutral()
        {
            DownloadProgressDto dto = DownloadProgressDtoMapping.IdleSnapshot(null);

            Assert.Equal(new DownloadStatusDto(false, 0, 0, 0L, null, 0, 0, 0L, 0, 0.0), dto.Progress);
            Assert.Null(dto.CurrentDeviceName);
        }
    }
}
