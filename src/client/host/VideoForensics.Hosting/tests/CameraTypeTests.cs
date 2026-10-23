using VideoForensics.Client.Hosting.BackgroundServices;

namespace VideoForensics.Client.Hosting.Tests
{
    /// <summary>Only camera types have snapshots. Chimes, base stations and range extenders are health-only.</summary>
    public class CameraTypeTests
    {
        [Theory]
        [InlineData("doorbot")]
        [InlineData("authorized_doorbot")]
        [InlineData("stickup_cam")]
        public void CameraTypes_HaveSnapshots(string type)
        {
            Assert.True(SnapshotCaptureService.HasCamera(type));
        }

        [Theory]
        [InlineData("chime")]
        [InlineData("base_station")]
        [InlineData("range_extender")]
        [InlineData("")]
        public void NonCameraTypes_AreSkippedForSnapshots(string type)
        {
            Assert.False(SnapshotCaptureService.HasCamera(type));
        }
    }
}
