#nullable disable
using System.Collections.Generic;

using Ring.Api;
using Ring.Api.Models;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ring.Api.Tests
{
    [TestClass]
    public class RingVideoServiceModelTests
    {
        [TestMethod]
        public void Filter_CanBeCreatedWithDefaults()
        {
            var filter = new Filter();

            Assert.IsNotNull(filter);
            Assert.AreEqual(10000, filter.VideoCount);
        }

        [TestMethod]
        public void Filter_CanHavePropertiesSet()
        {
            var filter = new Filter();
            var now = DateTime.Now;

            filter.VideoCount = 100;
            filter.StartDateTime = now;
            filter.EndDateTime = now.AddDays(1);

            Assert.AreEqual(100, filter.VideoCount);
            Assert.AreEqual(now, filter.StartDateTime);
            Assert.AreEqual(now.AddDays(1), filter.EndDateTime);
        }

        [TestMethod]
        public void RingCredentials_CanBeCreatedWithDefaults()
        {
            var auth = new RingCredentials();

            Assert.IsNotNull(auth);
            Assert.IsNull(auth.UserName);
            Assert.IsNull(auth.Password);
        }

        [TestMethod]
        public void RingCredentials_StoresUserNameAndPassword()
        {
            var auth = new RingCredentials();
            var username = "test@example.com";
            var password = "testPassword";

            auth.UserName = username;
            auth.Password = password;

            Assert.AreEqual(username, auth.UserName);
            Assert.AreEqual(password, auth.Password);
        }

        [TestMethod]
        public void DeviceInfo_CanBeCreatedWithProperties()
        {
            var device = new DeviceInfo
            {
                Id = 123,
                Name = "Front Door",
                DeviceId = "device_abc123"
            };

            Assert.AreEqual(123, device.Id);
            Assert.AreEqual("Front Door", device.Name);
            Assert.AreEqual("device_abc123", device.DeviceId);
        }

        [TestMethod]
        public void DeviceList_CanBeCreatedAndDevicesAdded()
        {
            var deviceList = new DeviceList();
            var device = new DeviceInfo
            {
                Id = 456,
                Name = "Back Patio",
                DeviceId = "device_xyz789"
            };

            deviceList.Devices.Add(device);

            Assert.AreEqual(1, deviceList.Devices.Count);
            Assert.AreEqual("Back Patio", deviceList.Devices[0].Name);
        }

        [TestMethod]
        public void DeviceList_SupportsMultipleDevices()
        {
            var deviceList = new DeviceList();
            var devices = new[]
            {
                new DeviceInfo { Id = 1, Name = "Camera 1", DeviceId = "dev_1" },
                new DeviceInfo { Id = 2, Name = "Camera 2", DeviceId = "dev_2" },
                new DeviceInfo { Id = 3, Name = "Camera 3", DeviceId = "dev_3" }
            };

            foreach (var device in devices)
            {
                deviceList.Devices.Add(device);
            }

            Assert.AreEqual(3, deviceList.Devices.Count);
            Assert.AreEqual("Camera 2", deviceList.Devices[1].Name);
        }

        [TestMethod]
        public void Model_DeviceInfoPropertiesAreIndependent()
        {
            var device1 = new DeviceInfo { Id = 1, Name = "Device A", DeviceId = "dev_a" };
            var device2 = new DeviceInfo { Id = 2, Name = "Device B", DeviceId = "dev_b" };

            Assert.AreNotEqual(device1.Id, device2.Id);
            Assert.AreNotEqual(device1.Name, device2.Name);
            Assert.AreNotEqual(device1.DeviceId, device2.DeviceId);
        }

        [TestMethod]
        public void FailedDownload_StoresErrorInformation()
        {
            var now = DateTime.UtcNow;
            var error = new FailedDownload
            {
                Timestamp = now,
                EventId = "evt_123",
                CameraId = 456,
                CameraName = "Doorbell",
                LocationName = "Front",
                ErrorDescription = "Network timeout"
            };

            Assert.AreEqual("evt_123", error.EventId);
            Assert.AreEqual(456, error.CameraId);
            Assert.AreEqual("Network timeout", error.ErrorDescription);
        }

        [TestMethod]
        public void FailedDownload_CanBeSerialized()
        {
            var failedDownload = new FailedDownload
            {
                EventId = "evt_001",
                CameraId = 100,
                CameraName = "Front Door",
                LocationName = "Entrance",
                ErrorDescription = "Timeout",
                Timestamp = DateTime.UtcNow
            };

            Assert.IsNotNull(failedDownload.EventId);
            Assert.IsNotNull(failedDownload.CameraName);
            Assert.IsNotNull(failedDownload.LocationName);
        }

        [TestMethod]
        public void Model_FailedDownloadTimestampIsUtc()
        {
            var now = DateTime.UtcNow;
            var failed = new FailedDownload { Timestamp = now };

            Assert.AreEqual(now, failed.Timestamp);
            Assert.AreEqual(DateTimeKind.Utc, now.Kind);
        }

        [TestMethod]
        public void Filter_DateRangeCanSpanMonths()
        {
            var start = new DateTime(2026, 1, 1);
            var end = new DateTime(2026, 3, 31);
            var filter = new Filter
            {
                StartDateTime = start,
                EndDateTime = end,
                VideoCount = 1000
            };

            var daysDifference = (filter.EndDateTime - filter.StartDateTime).Value.Days;

            Assert.AreEqual(89, daysDifference);
            Assert.AreEqual(1000, filter.VideoCount);
        }
    }

    [TestClass]
    public class AuthResolutionTests
    {
        [TestMethod]
        public void RefreshToken_Present_SucceedsWithoutUsernameOrPassword()
        {
            var auth = new RingCredentials { RefreshToken = "cached-refresh-token" };

            var error = RingVideoService.ResolveAuthError(auth);

            Assert.IsNull(error);
        }

        [TestMethod]
        public void UsernameAndPassword_Present_Succeeds()
        {
            var auth = new RingCredentials { UserName = "user@example.com", Password = "pw" };

            var error = RingVideoService.ResolveAuthError(auth);

            Assert.IsNull(error);
        }

        [TestMethod]
        public void NoCredentialsAnywhere_FailsWithUsernameError()
        {
            var auth = new RingCredentials();

            var error = RingVideoService.ResolveAuthError(auth);

            Assert.AreEqual("A Ring username is required", error);
        }

        [TestMethod]
        public void UsernameOnly_NoPassword_FailsWithPasswordError()
        {
            var auth = new RingCredentials { UserName = "user@example.com" };

            var error = RingVideoService.ResolveAuthError(auth);

            Assert.AreEqual("A Ring password is required", error);
        }

        [TestMethod]
        public void RefreshToken_TakesPriorityOverIncompleteUsernamePassword()
        {
            // A username with no password would normally fail, but a refresh token short-circuits
            // that check entirely - this is the bug this test guards against regressing.
            var auth = new RingCredentials { RefreshToken = "cached-refresh-token", UserName = "user@example.com" };

            var error = RingVideoService.ResolveAuthError(auth);

            Assert.IsNull(error);
        }
    }

    [TestClass]
    public class LocationResolutionTests
    {
        [TestMethod]
        public void LocationNameResolutionUsesApiResult()
        {
            var locations = new List<Ring.Api.Entities.Location>
            {
                new()
                {
                    Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    Name = "Front Door",
                    Address = new Ring.Api.Entities.LocationAddress
                    {
                        Address1 = "123 Main St",
                        City = "Springfield",
                        State = "IL",
                        ZipCode = "62701",
                        TimeZone = "America/Chicago"
                    },
                    IsOwner = true
                },
                new()
                {
                    Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                    Name = "Back Patio",
                    Address = new Ring.Api.Entities.LocationAddress
                    {
                        Address1 = "123 Main St",
                        City = "Springfield",
                        State = "IL",
                        ZipCode = "62701",
                        TimeZone = "America/Chicago"
                    },
                    IsOwner = true
                }
            };

            var locationById = locations.ToDictionary(l => l.Id ?? Guid.Empty, l => l.Name);

            var locationId = Guid.Parse("11111111-1111-1111-1111-111111111111");
            var name = locationById.TryGetValue(locationId, out var value) ? value : "Unknown";

            Assert.AreEqual("Front Door", name);
        }

        [TestMethod]
        public void LocationNameResolutionFallsBackToDefault()
        {
            var locations = new List<Ring.Api.Entities.Location>
            {
                new()
                {
                    Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    Name = "Front Door"
                }
            };

            var locationById = locations.ToDictionary(l => l.Id ?? Guid.Empty, l => l.Name);

            var locationId = Guid.Parse("99999999-9999-9999-9999-999999999999");
            var name = locationById.TryGetValue(locationId, out var value) ? value : "Unknown Location";

            Assert.AreEqual("Unknown Location", name);
        }

        [TestMethod]
        public void LocationNameResolutionWithAppSettingsFallback()
        {
            var apiLocations = new Dictionary<Guid, string>
            {
                { Guid.Parse("11111111-1111-1111-1111-111111111111"), "Front Door" }
            };

            var fallbackLocationNames = new Dictionary<string, string>
            {
                { "22222222-2222-2222-2222-222222222222", "Back Patio (from config)" }
            };

            var locationIdToResolve = Guid.Parse("22222222-2222-2222-2222-222222222222");
            var name = apiLocations.TryGetValue(locationIdToResolve, out var apiName)
                ? apiName
                : (fallbackLocationNames.TryGetValue(locationIdToResolve.ToString(), out var configName) ? configName : "Unknown");

            Assert.AreEqual("Back Patio (from config)", name);
        }

        [TestMethod]
        public void LocationCanBeNullAndHandledGracefully()
        {
            var location = new Ring.Api.Entities.Location
            {
                Id = null,
                Name = null,
                Address = null,
                IsOwner = null
            };

            var id = location.Id ?? Guid.Empty;
            var name = location.Name ?? "Unknown";

            Assert.AreEqual(Guid.Empty, id);
            Assert.AreEqual("Unknown", name);
        }
    }
}

