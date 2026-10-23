using VideoForensics.Hosting.BackgroundServices;
using Xunit;

namespace VideoForensics.Hosting.Tests
{
    public class ElevatedPollingWindowTrackerTests
    {
        [Fact]
        public void EnterElevated_ThenIsElevated_ReturnsTrueWithinWindow()
        {
            var tracker = new ElevatedPollingWindowTracker();
            Guid deviceId = Guid.NewGuid();

            tracker.EnterElevated(deviceId, TimeSpan.FromSeconds(10));

            Assert.True(tracker.IsElevated(deviceId));
        }

        [Fact]
        public void IsElevated_AfterWindowExpires_ReturnsFalse()
        {
            var tracker = new ElevatedPollingWindowTracker();
            Guid deviceId = Guid.NewGuid();

            tracker.EnterElevated(deviceId, TimeSpan.FromMilliseconds(100));
            Assert.True(tracker.IsElevated(deviceId));

            System.Threading.Thread.Sleep(150);

            Assert.False(tracker.IsElevated(deviceId));
        }

        [Fact]
        public void IsElevated_DeviceNeverEntered_ReturnsFalse()
        {
            var tracker = new ElevatedPollingWindowTracker();
            Guid deviceId = Guid.NewGuid();

            Assert.False(tracker.IsElevated(deviceId));
        }

        [Fact]
        public void Clear_RemovesDeviceFromElevatedSet()
        {
            var tracker = new ElevatedPollingWindowTracker();
            Guid deviceId = Guid.NewGuid();

            tracker.EnterElevated(deviceId, TimeSpan.FromSeconds(10));
            Assert.True(tracker.IsElevated(deviceId));

            tracker.Clear(deviceId);

            Assert.False(tracker.IsElevated(deviceId));
        }

        [Fact]
        public void ElevatedDeviceIds_ReturnsOnlyCurrentlyElevatedDevices()
        {
            var tracker = new ElevatedPollingWindowTracker();
            Guid device1 = Guid.NewGuid();
            Guid device2 = Guid.NewGuid();
            Guid device3 = Guid.NewGuid();

            tracker.EnterElevated(device1, TimeSpan.FromSeconds(10));
            tracker.EnterElevated(device2, TimeSpan.FromMilliseconds(50));
            tracker.EnterElevated(device3, TimeSpan.FromSeconds(10));

            System.Threading.Thread.Sleep(100);

            IReadOnlyCollection<Guid> elevated = tracker.ElevatedDeviceIds;

            Assert.Contains(device1, elevated);
            Assert.DoesNotContain(device2, elevated);
            Assert.Contains(device3, elevated);
            Assert.Equal(2, elevated.Count);
        }
    }
}
