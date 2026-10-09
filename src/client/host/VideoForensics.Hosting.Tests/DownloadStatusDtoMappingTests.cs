using Moq;

using VideoForensics.Api.Contracts;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Providers.Common.Contracts;

using Xunit;

namespace VideoForensics.Hosting.Tests
{
    public class DownloadStatusDtoMappingTests
    {
        [Fact]
        public void DownloadStatusDtoMapping_RoundTrip_PreservesAllFields()
        {
            var original = new DownloadStatus(true, 3, 10, 2048L, "clip.mp4", 7, 25, 4096L, 2, 12.5);

            DownloadStatus roundTripped = original.ToDto().ToDomain();

            Assert.Equal(original, roundTripped);
        }

        [Fact]
        public void DownloadProgressDtoMapping_FromService_CopiesEveryField()
        {
            var status = new DownloadStatus(true, 1, 4, 100L, "a.mp4", 2, 8, 200L, 3, 1.5);

            var service = new Mock<IVideoDownloadService>();
            service.Setup(s => s.GetProgress()).Returns(status);
            service.Setup(s => s.GetCurrentDevice()).Returns((2, 5, "Front door"));
            service.Setup(s => s.DrainActivityLog()).Returns(new List<string> { "line one" });
            service.Setup(s => s.GetPreScanCounts()).Returns(new Dictionary<string, int> { ["dev-1"] = 6 });
            service.Setup(s => s.GetLastError()).Returns("Disk full");
            service.Setup(s => s.GetRemainingReason()).Returns("Rate limited by provider");

            DownloadProgressDto dto = service.Object.ToDownloadProgressDto();

            Assert.Equal(status.ToDto(), dto.Progress);
            Assert.Equal(2, dto.CurrentDeviceIndex);
            Assert.Equal(5, dto.CurrentDeviceTotal);
            Assert.Equal("Front door", dto.CurrentDeviceName);
            Assert.Equal(new[] { "line one" }, dto.Activity);
            Assert.Equal(6, dto.PreScanCounts["dev-1"]);
            Assert.Equal("Disk full", dto.LastError);
            Assert.Equal("Rate limited by provider", dto.RemainingReason);
        }
    }
}
