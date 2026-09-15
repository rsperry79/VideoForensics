using System.Threading;

using VideoForensics.Providers.Ring.Clients;
using VideoForensics.Providers.Ring.Entities;
using VideoForensics.Providers.Ring.Interfaces;

namespace VideoForensics.Providers.Ring.Core.Tests
{
    public class DeviceManagementClientTests
    {
        #region Mock Implementations
        private class MockDeviceDiscoveryService : IDeviceDiscoveryService
        {
            public List<Doorbot> DevicesToReturn { get; set; } = [];
            public List<Location> LocationsToReturn { get; set; } = [];
            public CancellationToken LastCancellationToken { get; set; }
            public Exception GetRingDevicesException { get; set; }
            public Exception GetLocationsException { get; set; }

            public Task<List<Doorbot>> GetRingDevices(Guid? locationId = null, CancellationToken cancellationToken = default)
            {
                LastCancellationToken = cancellationToken;
                return GetRingDevicesException != null ? throw GetRingDevicesException : Task.FromResult(DevicesToReturn);
            }

            public Task<List<Location>> GetLocations(CancellationToken cancellationToken = default)
            {
                LastCancellationToken = cancellationToken;
                return GetLocationsException != null ? throw GetLocationsException : Task.FromResult(LocationsToReturn);
            }

            public Task<Devices> GetDeviceById(string deviceId, CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }

            public Task<List<Doorbot>> GetDoorbotsInLocation(Guid locationId, CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }

            public Task<Profile> GetProfile(CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }
        }

        private class MockDeviceControlService : IDeviceControlService
        {
            public bool SetLightCalled { get; set; }
            public bool SetSirenCalled { get; set; }
            public bool SetNightModeCalled { get; set; }
            public bool SetMotionDetectionCalled { get; set; }
            public CancellationToken LastCancellationToken { get; set; }
            public Exception SetLightException { get; set; }
            public Exception SetSirenException { get; set; }
            public Exception SetNightModeException { get; set; }
            public Exception SetMotionDetectionException { get; set; }

            public Task<bool> SetLight(string doorbotId, bool on, CancellationToken cancellationToken = default)
            {
                SetLightCalled = true;
                LastCancellationToken = cancellationToken;
                return SetLightException != null ? throw SetLightException : Task.FromResult(true);
            }

            public Task<bool> SetSiren(string doorbotId, bool on, int? durationSeconds = null, CancellationToken cancellationToken = default)
            {
                SetSirenCalled = true;
                LastCancellationToken = cancellationToken;
                return SetSirenException != null ? throw SetSirenException : Task.FromResult(true);
            }

            public Task<bool> SetVolume(string doorbotId, int volume, CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }

            public Task<bool> SetNightMode(string doorbotId, bool enabled, CancellationToken cancellationToken = default)
            {
                SetNightModeCalled = true;
                LastCancellationToken = cancellationToken;
                return SetNightModeException != null ? throw SetNightModeException : Task.FromResult(true);
            }

            public Task<bool> SetMotionDetection(string doorbotId, bool enabled, CancellationToken cancellationToken = default)
            {
                SetMotionDetectionCalled = true;
                LastCancellationToken = cancellationToken;
                return SetMotionDetectionException != null ? throw SetMotionDetectionException : Task.FromResult(true);
            }

            public Task<System.Text.Json.JsonElement> GetDeviceSettings(string doorbotId, CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }
        }

        private class MockHealthMonitoringService : IHealthMonitoringService
        {
            public CancellationToken LastCancellationToken { get; set; }
            public Exception GetDoorbotHealthException { get; set; }

            public Task<DeviceHealth> GetDoorbotHealth(string doorbotId, CancellationToken cancellationToken = default)
            {
                LastCancellationToken = cancellationToken;
                return GetDoorbotHealthException != null ? throw GetDoorbotHealthException : Task.FromResult(new DeviceHealth());
            }

            public Task<DeviceHealth> GetChimeHealth(string chimeId, CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }

            public Task<System.Text.Json.JsonElement> GetMonitoringStatus(Guid locationId, CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }

            public Task<DeviceHealthResponse> GetDetailedDeviceHealth(string deviceId, CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }
        }

        private class MockLocationManagementService : ILocationManagementService
        {
            public bool SetLocationModeCalled { get; set; }
            public CancellationToken LastCancellationToken { get; set; }
            public Exception SetLocationModeException { get; set; }

            public Task<LocationMode> GetLocationMode(Guid locationId, CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }

            public Task<bool> SetLocationMode(Guid locationId, LocationMode mode, CancellationToken cancellationToken = default)
            {
                SetLocationModeCalled = true;
                LastCancellationToken = cancellationToken;
                return SetLocationModeException != null ? throw SetLocationModeException : Task.FromResult(true);
            }

            public Task<List<SharedUser>> GetSharedUsers(Guid locationId, CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }

            public Task<List<Invitation>> GetInvitations(Guid locationId, CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }

            public Task<Location> GetLocationDetails(Guid locationId, CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }
        }
        #endregion

        #region Constructor Tests
        [Fact]
        public void DeviceManagementClient_Constructor_WithValidServices_CreatesClient()
        {
            // Arrange
            var discoveryService = new MockDeviceDiscoveryService();
            var controlService = new MockDeviceControlService();
            var healthService = new MockHealthMonitoringService();
            var locationService = new MockLocationManagementService();

            // Act
            var client = new DeviceManagementClient(discoveryService, controlService, healthService, locationService);

            // Assert
            Assert.NotNull(client);
        }

        [Fact]
        public void DeviceManagementClient_Constructor_WithNullDiscoveryService_ThrowsArgumentNullException()
        {
            // Arrange
            var controlService = new MockDeviceControlService();
            var healthService = new MockHealthMonitoringService();
            var locationService = new MockLocationManagementService();

            // Act & Assert
            _ = Assert.Throws<ArgumentNullException>(() =>
                new DeviceManagementClient(null, controlService, healthService, locationService));
        }

        [Fact]
        public void DeviceManagementClient_Constructor_WithNullControlService_ThrowsArgumentNullException()
        {
            // Arrange
            var discoveryService = new MockDeviceDiscoveryService();
            var healthService = new MockHealthMonitoringService();
            var locationService = new MockLocationManagementService();

            // Act & Assert
            _ = Assert.Throws<ArgumentNullException>(() =>
                new DeviceManagementClient(discoveryService, null, healthService, locationService));
        }

        [Fact]
        public void DeviceManagementClient_Constructor_WithNullHealthService_ThrowsArgumentNullException()
        {
            // Arrange
            var discoveryService = new MockDeviceDiscoveryService();
            var controlService = new MockDeviceControlService();
            var locationService = new MockLocationManagementService();

            // Act & Assert
            _ = Assert.Throws<ArgumentNullException>(() =>
                new DeviceManagementClient(discoveryService, controlService, null, locationService));
        }

        [Fact]
        public void DeviceManagementClient_Constructor_WithNullLocationService_ThrowsArgumentNullException()
        {
            // Arrange
            var discoveryService = new MockDeviceDiscoveryService();
            var controlService = new MockDeviceControlService();
            var healthService = new MockHealthMonitoringService();

            // Act & Assert
            _ = Assert.Throws<ArgumentNullException>(() =>
                new DeviceManagementClient(discoveryService, controlService, healthService, null));
        }
        #endregion

        #region GetAllDevices Tests
        [Fact]
        public async Task GetAllDevicesAsync_ReturnsDevices()
        {
            // Arrange
            var devices = new List<Doorbot>
            {
                new() { DeviceId = "device1", Description = "Front Door" }
            };
            var discoveryService = new MockDeviceDiscoveryService { DevicesToReturn = devices };
            var client = new DeviceManagementClient(discoveryService, new MockDeviceControlService(),
                new MockHealthMonitoringService(), new MockLocationManagementService());

            // Act
            List<Doorbot> result = await client.GetAllDevicesAsync();

            // Assert
            Assert.Equal(devices, result);
        }

        [Fact]
        public async Task GetAllDevicesAsync_ReturnsEmptyListWhenNoDevices()
        {
            // Arrange
            var discoveryService = new MockDeviceDiscoveryService { DevicesToReturn = [] };
            var client = new DeviceManagementClient(discoveryService, new MockDeviceControlService(),
                new MockHealthMonitoringService(), new MockLocationManagementService());

            // Act
            List<Doorbot> result = await client.GetAllDevicesAsync();

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetAllDevicesAsync_ForwardsCancellationToken()
        {
            // Arrange
            var discoveryService = new MockDeviceDiscoveryService();
            var client = new DeviceManagementClient(discoveryService, new MockDeviceControlService(),
                new MockHealthMonitoringService(), new MockLocationManagementService());
            var cts = new CancellationTokenSource();

            // Act
            _ = await client.GetAllDevicesAsync(cts.Token);

            // Assert
            Assert.Equal(cts.Token, discoveryService.LastCancellationToken);
        }
        #endregion

        #region GetDeviceByName Tests
        [Fact]
        public async Task GetDeviceByNameAsync_WithValidName_ReturnsDevice()
        {
            // Arrange
            var devices = new List<Doorbot>
            {
                new() { DeviceId = "device1", Description = "Front Door" }
            };
            var discoveryService = new MockDeviceDiscoveryService { DevicesToReturn = devices };
            var client = new DeviceManagementClient(discoveryService, new MockDeviceControlService(),
                new MockHealthMonitoringService(), new MockLocationManagementService());

            // Act
            Doorbot result = await client.GetDeviceByNameAsync("Front Door");

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Front Door", result.Description);
        }

        [Fact]
        public async Task GetDeviceByNameAsync_WithEmptyName_ThrowsArgumentException()
        {
            // Arrange
            var discoveryService = new MockDeviceDiscoveryService();
            var client = new DeviceManagementClient(discoveryService, new MockDeviceControlService(),
                new MockHealthMonitoringService(), new MockLocationManagementService());

            // Act & Assert
            _ = await Assert.ThrowsAsync<ArgumentException>(() => client.GetDeviceByNameAsync(""));
        }

        [Fact]
        public async Task GetDeviceByNameAsync_WithNullName_ThrowsArgumentException()
        {
            // Arrange
            var discoveryService = new MockDeviceDiscoveryService();
            var client = new DeviceManagementClient(discoveryService, new MockDeviceControlService(),
                new MockHealthMonitoringService(), new MockLocationManagementService());

            // Act & Assert
            _ = await Assert.ThrowsAsync<ArgumentException>(() => client.GetDeviceByNameAsync(null));
        }

        [Fact]
        public async Task GetDeviceByNameAsync_WithNonExistentDevice_ThrowsKeyNotFoundException()
        {
            // Arrange
            var devices = new List<Doorbot>
            {
                new() { DeviceId = "device1", Description = "Front Door" }
            };
            var discoveryService = new MockDeviceDiscoveryService { DevicesToReturn = devices };
            var client = new DeviceManagementClient(discoveryService, new MockDeviceControlService(),
                new MockHealthMonitoringService(), new MockLocationManagementService());

            // Act & Assert
            _ = await Assert.ThrowsAsync<KeyNotFoundException>(() => client.GetDeviceByNameAsync("Back Door"));
        }

        [Fact]
        public async Task GetDeviceByNameAsync_IsCaseInsensitive()
        {
            // Arrange
            var devices = new List<Doorbot>
            {
                new() { DeviceId = "device1", Description = "Front Door" }
            };
            var discoveryService = new MockDeviceDiscoveryService { DevicesToReturn = devices };
            var client = new DeviceManagementClient(discoveryService, new MockDeviceControlService(),
                new MockHealthMonitoringService(), new MockLocationManagementService());

            // Act
            Doorbot result = await client.GetDeviceByNameAsync("front door");

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Front Door", result.Description);
        }

        [Fact]
        public async Task GetDeviceByNameAsync_ForwardsCancellationToken()
        {
            // Arrange
            var devices = new List<Doorbot>
            {
                new() { DeviceId = "device1", Description = "Front Door" }
            };
            var discoveryService = new MockDeviceDiscoveryService { DevicesToReturn = devices };
            var client = new DeviceManagementClient(discoveryService, new MockDeviceControlService(),
                new MockHealthMonitoringService(), new MockLocationManagementService());
            var cts = new CancellationTokenSource();

            // Act
            _ = await client.GetDeviceByNameAsync("Front Door", cts.Token);

            // Assert
            Assert.Equal(cts.Token, discoveryService.LastCancellationToken);
        }
        #endregion

        #region GetDeviceById Tests
        [Fact]
        public async Task GetDeviceByIdAsync_WithValidId_ReturnsDevice()
        {
            // Arrange
            var devices = new List<Doorbot>
            {
                new() { DeviceId = "device123", Description = "Front Door" }
            };
            var discoveryService = new MockDeviceDiscoveryService { DevicesToReturn = devices };
            var client = new DeviceManagementClient(discoveryService, new MockDeviceControlService(),
                new MockHealthMonitoringService(), new MockLocationManagementService());

            // Act
            Doorbot result = await client.GetDeviceByIdAsync("device123");

            // Assert
            Assert.NotNull(result);
            Assert.Equal("device123", result.DeviceId);
        }

        [Fact]
        public async Task GetDeviceByIdAsync_WithEmptyId_ThrowsArgumentException()
        {
            // Arrange
            var discoveryService = new MockDeviceDiscoveryService();
            var client = new DeviceManagementClient(discoveryService, new MockDeviceControlService(),
                new MockHealthMonitoringService(), new MockLocationManagementService());

            // Act & Assert
            _ = await Assert.ThrowsAsync<ArgumentException>(() => client.GetDeviceByIdAsync(""));
        }

        [Fact]
        public async Task GetDeviceByIdAsync_WithNullId_ThrowsArgumentException()
        {
            // Arrange
            var discoveryService = new MockDeviceDiscoveryService();
            var client = new DeviceManagementClient(discoveryService, new MockDeviceControlService(),
                new MockHealthMonitoringService(), new MockLocationManagementService());

            // Act & Assert
            _ = await Assert.ThrowsAsync<ArgumentException>(() => client.GetDeviceByIdAsync(null));
        }

        [Fact]
        public async Task GetDeviceByIdAsync_WithNonExistentId_ThrowsKeyNotFoundException()
        {
            // Arrange
            var devices = new List<Doorbot>
            {
                new() { DeviceId = "device123", Description = "Front Door" }
            };
            var discoveryService = new MockDeviceDiscoveryService { DevicesToReturn = devices };
            var client = new DeviceManagementClient(discoveryService, new MockDeviceControlService(),
                new MockHealthMonitoringService(), new MockLocationManagementService());

            // Act & Assert
            _ = await Assert.ThrowsAsync<KeyNotFoundException>(() => client.GetDeviceByIdAsync("nonexistent"));
        }

        [Fact]
        public async Task GetDeviceByIdAsync_ForwardsCancellationToken()
        {
            // Arrange
            var devices = new List<Doorbot>
            {
                new() { DeviceId = "device123", Description = "Front Door" }
            };
            var discoveryService = new MockDeviceDiscoveryService { DevicesToReturn = devices };
            var client = new DeviceManagementClient(discoveryService, new MockDeviceControlService(),
                new MockHealthMonitoringService(), new MockLocationManagementService());
            var cts = new CancellationTokenSource();

            // Act
            _ = await client.GetDeviceByIdAsync("device123", cts.Token);

            // Assert
            Assert.Equal(cts.Token, discoveryService.LastCancellationToken);
        }
        #endregion

        #region ControlDevice Tests
        [Fact]
        public async Task ControlDeviceAsync_WithLightOnAction_CallsSetLight()
        {
            // Arrange
            var controlService = new MockDeviceControlService();
            var client = new DeviceManagementClient(new MockDeviceDiscoveryService(), controlService,
                new MockHealthMonitoringService(), new MockLocationManagementService());
            var action = new DeviceAction { ActionType = "light_on" };

            // Act
            _ = await client.ControlDeviceAsync("device123", action);

            // Assert
            Assert.True(controlService.SetLightCalled);
        }

        [Fact]
        public async Task ControlDeviceAsync_WithLightOffAction_CallsSetLight()
        {
            // Arrange
            var controlService = new MockDeviceControlService();
            var client = new DeviceManagementClient(new MockDeviceDiscoveryService(), controlService,
                new MockHealthMonitoringService(), new MockLocationManagementService());
            var action = new DeviceAction { ActionType = "light_off" };

            // Act
            _ = await client.ControlDeviceAsync("device123", action);

            // Assert
            Assert.True(controlService.SetLightCalled);
        }

        [Fact]
        public async Task ControlDeviceAsync_WithSirenOnAction_CallsSetSiren()
        {
            // Arrange
            var controlService = new MockDeviceControlService();
            var client = new DeviceManagementClient(new MockDeviceDiscoveryService(), controlService,
                new MockHealthMonitoringService(), new MockLocationManagementService());
            var action = new DeviceAction { ActionType = "siren_on", Parameters = new() { { "duration", 60 } } };

            // Act
            _ = await client.ControlDeviceAsync("device123", action);

            // Assert
            Assert.True(controlService.SetSirenCalled);
        }

        [Fact]
        public async Task ControlDeviceAsync_WithSirenOffAction_CallsSetSiren()
        {
            // Arrange
            var controlService = new MockDeviceControlService();
            var client = new DeviceManagementClient(new MockDeviceDiscoveryService(), controlService,
                new MockHealthMonitoringService(), new MockLocationManagementService());
            var action = new DeviceAction { ActionType = "siren_off" };

            // Act
            _ = await client.ControlDeviceAsync("device123", action);

            // Assert
            Assert.True(controlService.SetSirenCalled);
        }

        [Fact]
        public async Task ControlDeviceAsync_WithNightModeOnAction_CallsSetNightMode()
        {
            // Arrange
            var controlService = new MockDeviceControlService();
            var client = new DeviceManagementClient(new MockDeviceDiscoveryService(), controlService,
                new MockHealthMonitoringService(), new MockLocationManagementService());
            var action = new DeviceAction { ActionType = "night_mode_on" };

            // Act
            _ = await client.ControlDeviceAsync("device123", action);

            // Assert
            Assert.True(controlService.SetNightModeCalled);
        }

        [Fact]
        public async Task ControlDeviceAsync_WithNightModeOffAction_CallsSetNightMode()
        {
            // Arrange
            var controlService = new MockDeviceControlService();
            var client = new DeviceManagementClient(new MockDeviceDiscoveryService(), controlService,
                new MockHealthMonitoringService(), new MockLocationManagementService());
            var action = new DeviceAction { ActionType = "night_mode_off" };

            // Act
            _ = await client.ControlDeviceAsync("device123", action);

            // Assert
            Assert.True(controlService.SetNightModeCalled);
        }

        [Fact]
        public async Task ControlDeviceAsync_WithMotionDetectionOnAction_CallsSetMotionDetection()
        {
            // Arrange
            var controlService = new MockDeviceControlService();
            var client = new DeviceManagementClient(new MockDeviceDiscoveryService(), controlService,
                new MockHealthMonitoringService(), new MockLocationManagementService());
            var action = new DeviceAction { ActionType = "motion_detection_on" };

            // Act
            _ = await client.ControlDeviceAsync("device123", action);

            // Assert
            Assert.True(controlService.SetMotionDetectionCalled);
        }

        [Fact]
        public async Task ControlDeviceAsync_WithMotionDetectionOffAction_CallsSetMotionDetection()
        {
            // Arrange
            var controlService = new MockDeviceControlService();
            var client = new DeviceManagementClient(new MockDeviceDiscoveryService(), controlService,
                new MockHealthMonitoringService(), new MockLocationManagementService());
            var action = new DeviceAction { ActionType = "motion_detection_off" };

            // Act
            _ = await client.ControlDeviceAsync("device123", action);

            // Assert
            Assert.True(controlService.SetMotionDetectionCalled);
        }

        [Fact]
        public async Task ControlDeviceAsync_WithEmptyDeviceId_ThrowsArgumentException()
        {
            // Arrange
            var client = new DeviceManagementClient(new MockDeviceDiscoveryService(), new MockDeviceControlService(),
                new MockHealthMonitoringService(), new MockLocationManagementService());

            // Act & Assert
            _ = await Assert.ThrowsAsync<ArgumentException>(() =>
                client.ControlDeviceAsync("", new DeviceAction { ActionType = "light_on" }));
        }

        [Fact]
        public async Task ControlDeviceAsync_WithNullDeviceId_ThrowsArgumentException()
        {
            // Arrange
            var client = new DeviceManagementClient(new MockDeviceDiscoveryService(), new MockDeviceControlService(),
                new MockHealthMonitoringService(), new MockLocationManagementService());

            // Act & Assert
            _ = await Assert.ThrowsAsync<ArgumentException>(() =>
                client.ControlDeviceAsync(null, new DeviceAction { ActionType = "light_on" }));
        }

        [Fact]
        public async Task ControlDeviceAsync_WithNullAction_ThrowsArgumentNullException()
        {
            // Arrange
            var client = new DeviceManagementClient(new MockDeviceDiscoveryService(), new MockDeviceControlService(),
                new MockHealthMonitoringService(), new MockLocationManagementService());

            // Act & Assert
            _ = await Assert.ThrowsAsync<ArgumentNullException>(() =>
                client.ControlDeviceAsync("device123", null));
        }

        [Fact]
        public async Task ControlDeviceAsync_WithUnsupportedAction_ReturnsFalse()
        {
            // Arrange
            var client = new DeviceManagementClient(new MockDeviceDiscoveryService(), new MockDeviceControlService(),
                new MockHealthMonitoringService(), new MockLocationManagementService());
            var action = new DeviceAction { ActionType = "unsupported_action" };

            // Act
            bool result = await client.ControlDeviceAsync("device123", action);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task ControlDeviceAsync_WhenControlServiceThrows_ReturnsFalse()
        {
            // Arrange
            var controlService = new MockDeviceControlService
            {
                SetLightException = new Exception("Control failed")
            };
            var client = new DeviceManagementClient(new MockDeviceDiscoveryService(), controlService,
                new MockHealthMonitoringService(), new MockLocationManagementService());
            var action = new DeviceAction { ActionType = "light_on" };

            // Act
            bool result = await client.ControlDeviceAsync("device123", action);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task ControlDeviceAsync_ForwardsCancellationToken()
        {
            // Arrange
            var controlService = new MockDeviceControlService();
            var client = new DeviceManagementClient(new MockDeviceDiscoveryService(), controlService,
                new MockHealthMonitoringService(), new MockLocationManagementService());
            var action = new DeviceAction { ActionType = "light_on" };
            var cts = new CancellationTokenSource();

            // Act
            _ = await client.ControlDeviceAsync("device123", action, cts.Token);

            // Assert
            Assert.Equal(cts.Token, controlService.LastCancellationToken);
        }
        #endregion

        #region GetDeviceStatus Tests
        [Fact]
        public async Task GetDeviceStatusAsync_ReturnsValidStatus()
        {
            // Arrange
            var devices = new List<Doorbot>
            {
                new() { DeviceId = "device123", Description = "Front Door", ExternalConnection = true, BatteryLife = 100 }
            };
            var discoveryService = new MockDeviceDiscoveryService { DevicesToReturn = devices };
            var client = new DeviceManagementClient(discoveryService, new MockDeviceControlService(),
                new MockHealthMonitoringService(), new MockLocationManagementService());

            // Act
            DeviceStatusInfo result = await client.GetDeviceStatusAsync("device123");

            // Assert
            Assert.NotNull(result);
            Assert.Equal("device123", result.DeviceId);
            Assert.True(result.IsOnline);
            Assert.Equal(100, result.BatteryLevel);
        }

        [Fact]
        public async Task GetDeviceStatusAsync_WithEmptyDeviceId_ThrowsArgumentException()
        {
            // Arrange
            var client = new DeviceManagementClient(new MockDeviceDiscoveryService(), new MockDeviceControlService(),
                new MockHealthMonitoringService(), new MockLocationManagementService());

            // Act & Assert
            _ = await Assert.ThrowsAsync<ArgumentException>(() => client.GetDeviceStatusAsync(""));
        }

        [Fact]
        public async Task GetDeviceStatusAsync_WithNullDeviceId_ThrowsArgumentException()
        {
            // Arrange
            var client = new DeviceManagementClient(new MockDeviceDiscoveryService(), new MockDeviceControlService(),
                new MockHealthMonitoringService(), new MockLocationManagementService());

            // Act & Assert
            _ = await Assert.ThrowsAsync<ArgumentException>(() => client.GetDeviceStatusAsync(null));
        }

        [Fact]
        public async Task GetDeviceStatusAsync_WithNonExistentDeviceId_ThrowsKeyNotFoundException()
        {
            // Arrange
            var discoveryService = new MockDeviceDiscoveryService { DevicesToReturn = [] };
            var client = new DeviceManagementClient(discoveryService, new MockDeviceControlService(),
                new MockHealthMonitoringService(), new MockLocationManagementService());

            // Act & Assert
            _ = await Assert.ThrowsAsync<KeyNotFoundException>(() => client.GetDeviceStatusAsync("nonexistent"));
        }

        [Fact]
        public async Task GetDeviceStatusAsync_ForwardsCancellationToken()
        {
            // Arrange
            var devices = new List<Doorbot>
            {
                new() { DeviceId = "device123", Description = "Front Door", ExternalConnection = true }
            };
            var discoveryService = new MockDeviceDiscoveryService { DevicesToReturn = devices };
            var client = new DeviceManagementClient(discoveryService, new MockDeviceControlService(),
                new MockHealthMonitoringService(), new MockLocationManagementService());
            var cts = new CancellationTokenSource();

            // Act
            _ = await client.GetDeviceStatusAsync("device123", cts.Token);

            // Assert
            Assert.Equal(cts.Token, discoveryService.LastCancellationToken);
        }
        #endregion

        #region GetAllLocations Tests
        [Fact]
        public async Task GetAllLocationsAsync_ReturnsLocations()
        {
            // Arrange
            var locations = new List<Location>
            {
                new() { Id = Guid.NewGuid(), Name = "Home" }
            };
            var discoveryService = new MockDeviceDiscoveryService { LocationsToReturn = locations };
            var client = new DeviceManagementClient(discoveryService, new MockDeviceControlService(),
                new MockHealthMonitoringService(), new MockLocationManagementService());

            // Act
            List<Location> result = await client.GetAllLocationsAsync();

            // Assert
            Assert.Equal(locations, result);
        }

        [Fact]
        public async Task GetAllLocationsAsync_ReturnsEmptyListWhenNoLocations()
        {
            // Arrange
            var discoveryService = new MockDeviceDiscoveryService { LocationsToReturn = [] };
            var client = new DeviceManagementClient(discoveryService, new MockDeviceControlService(),
                new MockHealthMonitoringService(), new MockLocationManagementService());

            // Act
            List<Location> result = await client.GetAllLocationsAsync();

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetAllLocationsAsync_ForwardsCancellationToken()
        {
            // Arrange
            var discoveryService = new MockDeviceDiscoveryService();
            var client = new DeviceManagementClient(discoveryService, new MockDeviceControlService(),
                new MockHealthMonitoringService(), new MockLocationManagementService());
            var cts = new CancellationTokenSource();

            // Act
            _ = await client.GetAllLocationsAsync(cts.Token);

            // Assert
            Assert.Equal(cts.Token, discoveryService.LastCancellationToken);
        }
        #endregion

        #region GetDevicesByLocation Tests
        [Fact]
        public async Task GetDevicesByLocationAsync_WithValidLocationId_ReturnsDevices()
        {
            // Arrange
            var locationId = Guid.NewGuid();
            var devices = new List<Doorbot>
            {
                new() { DeviceId = "device1", Description = "Front Door", LocationId = locationId }
            };
            var discoveryService = new MockDeviceDiscoveryService { DevicesToReturn = devices };
            var client = new DeviceManagementClient(discoveryService, new MockDeviceControlService(),
                new MockHealthMonitoringService(), new MockLocationManagementService());

            // Act
            List<Doorbot> result = await client.GetDevicesByLocationAsync(locationId);

            // Assert
            Assert.Equal(devices, result);
        }

        [Fact]
        public async Task GetDevicesByLocationAsync_WithEmptyLocationId_ThrowsArgumentException()
        {
            // Arrange
            var client = new DeviceManagementClient(new MockDeviceDiscoveryService(), new MockDeviceControlService(),
                new MockHealthMonitoringService(), new MockLocationManagementService());

            // Act & Assert
            _ = await Assert.ThrowsAsync<ArgumentException>(() =>
                client.GetDevicesByLocationAsync(Guid.Empty));
        }

        [Fact]
        public async Task GetDevicesByLocationAsync_ForwardsCancellationToken()
        {
            // Arrange
            var locationId = Guid.NewGuid();
            var discoveryService = new MockDeviceDiscoveryService();
            var client = new DeviceManagementClient(discoveryService, new MockDeviceControlService(),
                new MockHealthMonitoringService(), new MockLocationManagementService());
            var cts = new CancellationTokenSource();

            // Act
            _ = await client.GetDevicesByLocationAsync(locationId, cts.Token);

            // Assert
            Assert.Equal(cts.Token, discoveryService.LastCancellationToken);
        }
        #endregion

        #region SetLocationMode Tests
        [Fact]
        public async Task SetLocationModeAsync_WithValidParams_CallsLocationService()
        {
            // Arrange
            var locationService = new MockLocationManagementService();
            var client = new DeviceManagementClient(new MockDeviceDiscoveryService(), new MockDeviceControlService(),
                new MockHealthMonitoringService(), locationService);
            var locationId = Guid.NewGuid();

            // Act
            _ = await client.SetLocationModeAsync(locationId, "Home");

            // Assert
            Assert.True(locationService.SetLocationModeCalled);
        }

        [Fact]
        public async Task SetLocationModeAsync_WithEmptyLocationId_ThrowsArgumentException()
        {
            // Arrange
            var client = new DeviceManagementClient(new MockDeviceDiscoveryService(), new MockDeviceControlService(),
                new MockHealthMonitoringService(), new MockLocationManagementService());

            // Act & Assert
            _ = await Assert.ThrowsAsync<ArgumentException>(() =>
                client.SetLocationModeAsync(Guid.Empty, "Home"));
        }

        [Fact]
        public async Task SetLocationModeAsync_WithEmptyMode_ThrowsArgumentException()
        {
            // Arrange
            var client = new DeviceManagementClient(new MockDeviceDiscoveryService(), new MockDeviceControlService(),
                new MockHealthMonitoringService(), new MockLocationManagementService());
            var locationId = Guid.NewGuid();

            // Act & Assert
            _ = await Assert.ThrowsAsync<ArgumentException>(() =>
                client.SetLocationModeAsync(locationId, ""));
        }

        [Fact]
        public async Task SetLocationModeAsync_WithNullMode_ThrowsArgumentException()
        {
            // Arrange
            var client = new DeviceManagementClient(new MockDeviceDiscoveryService(), new MockDeviceControlService(),
                new MockHealthMonitoringService(), new MockLocationManagementService());
            var locationId = Guid.NewGuid();

            // Act & Assert
            _ = await Assert.ThrowsAsync<ArgumentException>(() =>
                client.SetLocationModeAsync(locationId, null));
        }

        [Fact]
        public async Task SetLocationModeAsync_ForwardsCancellationToken()
        {
            // Arrange
            var locationService = new MockLocationManagementService();
            var client = new DeviceManagementClient(new MockDeviceDiscoveryService(), new MockDeviceControlService(),
                new MockHealthMonitoringService(), locationService);
            var locationId = Guid.NewGuid();
            var cts = new CancellationTokenSource();

            // Act
            _ = await client.SetLocationModeAsync(locationId, "Home", cts.Token);

            // Assert
            Assert.Equal(cts.Token, locationService.LastCancellationToken);
        }

        [Fact]
        public async Task SetLocationModeAsync_ReturnsTrueOnSuccess()
        {
            // Arrange
            var locationService = new MockLocationManagementService();
            var client = new DeviceManagementClient(new MockDeviceDiscoveryService(), new MockDeviceControlService(),
                new MockHealthMonitoringService(), locationService);
            var locationId = Guid.NewGuid();

            // Act
            bool result = await client.SetLocationModeAsync(locationId, "Home");

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task SetLocationModeAsync_WhenServiceThrows_PropagatesException()
        {
            // Arrange
            var locationService = new MockLocationManagementService
            {
                SetLocationModeException = new Exception("Mode change failed")
            };
            var client = new DeviceManagementClient(new MockDeviceDiscoveryService(), new MockDeviceControlService(),
                new MockHealthMonitoringService(), locationService);
            var locationId = Guid.NewGuid();

            // Act & Assert
            Exception ex = await Assert.ThrowsAsync<Exception>(() =>
                client.SetLocationModeAsync(locationId, "Home"));
            Assert.Equal("Mode change failed", ex.Message);
        }
        #endregion
    }
}
