using VideoForensics.Providers.Ring.Entities;
using VideoForensics.Providers.Ring.Services;
using Xunit;

namespace VideoForensics.Providers.Ring.Tests
{
    public class DeviceHealthMatcherTests
    {
        [Fact]
        public void FindDeviceHealth_MatchesDoorbotById_ReturnsHealth()
        {
            var health = new DeviceHealth { BatteryPercentage = 42, Connected = true };
            var devices = new Devices
            {
                Doorbots = new List<Doorbot> { new() { Id = 111, Health = health } }
            };

            var result = DeviceHealthMatcher.FindDeviceHealth(devices, "111");

            Assert.Same(health, result);
        }

        [Fact]
        public void FindDeviceHealth_MatchesStickupCamById_ReturnsHealth()
        {
            var health = new DeviceHealth { BatteryPercentage = 77 };
            var devices = new Devices
            {
                StickupCams = new List<StickupCam> { new() { Id = 222, Health = health } }
            };

            var result = DeviceHealthMatcher.FindDeviceHealth(devices, "222");

            Assert.Same(health, result);
        }

        [Fact]
        public void FindDeviceHealth_MatchesAuthorizedDoorbotById_ReturnsHealth()
        {
            var health = new DeviceHealth { Connected = false };
            var devices = new Devices
            {
                AuthorizedDoorbots = new List<Doorbot> { new() { Id = 333, Health = health } }
            };

            var result = DeviceHealthMatcher.FindDeviceHealth(devices, "333");

            Assert.Same(health, result);
        }

        [Fact]
        public void FindDeviceHealth_NoMatchingDevice_ReturnsNull()
        {
            var devices = new Devices
            {
                Doorbots = new List<Doorbot> { new() { Id = 111, Health = new DeviceHealth() } }
            };

            var result = DeviceHealthMatcher.FindDeviceHealth(devices, "does-not-exist");

            Assert.Null(result);
        }

        [Fact]
        public void FindDeviceHealth_NullDevices_ReturnsNull()
        {
            var result = DeviceHealthMatcher.FindDeviceHealth(null, "111");

            Assert.Null(result);
        }
    }
}
