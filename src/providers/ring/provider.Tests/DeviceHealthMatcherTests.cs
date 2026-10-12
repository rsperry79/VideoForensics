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
                Doorbots = [new() { Id = 111, Health = health }]
            };

            DeviceHealth? result = DeviceHealthMatcher.FindDeviceHealth(devices, "111");

            Assert.Same(health, result);
        }

        [Fact]
        public void FindDeviceHealth_MatchesStickupCamById_ReturnsHealth()
        {
            var health = new DeviceHealth { BatteryPercentage = 77 };
            var devices = new Devices
            {
                StickupCams = [new() { Id = 222, Health = health }]
            };

            DeviceHealth? result = DeviceHealthMatcher.FindDeviceHealth(devices, "222");

            Assert.Same(health, result);
        }

        [Fact]
        public void FindDeviceHealth_MatchesAuthorizedDoorbotById_ReturnsHealth()
        {
            var health = new DeviceHealth { Connected = false };
            var devices = new Devices
            {
                AuthorizedDoorbots = [new() { Id = 333, Health = health }]
            };

            DeviceHealth? result = DeviceHealthMatcher.FindDeviceHealth(devices, "333");

            Assert.Same(health, result);
        }

        [Fact]
        public void FindDeviceHealth_NoMatchingDevice_ReturnsNull()
        {
            var devices = new Devices
            {
                Doorbots = [new() { Id = 111, Health = new DeviceHealth() }]
            };

            DeviceHealth? result = DeviceHealthMatcher.FindDeviceHealth(devices, "does-not-exist");

            Assert.Null(result);
        }

        [Fact]
        public void FindDeviceHealth_NullDevices_ReturnsNull()
        {
            DeviceHealth? result = DeviceHealthMatcher.FindDeviceHealth(null, "111");

            Assert.Null(result);
        }
    }
}
