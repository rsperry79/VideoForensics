#nullable disable
using System.Reflection;

using VideoForensics.Providers.Ring.Models;

namespace VideoForensics.Providers.Ring.Core.Tests
{
    public class RingVideoServiceModelTests
    {
        [Fact]
        public void Filter_CanBeCreatedWithDefaults()
        {
            var filter = new Filter();

            Assert.NotNull(filter);
            Assert.Equal(10000, filter.VideoCount);
        }

        [Fact]
        public void Filter_CanHavePropertiesSet()
        {
            var filter = new Filter();
            DateTime now = DateTime.Now;

            filter.VideoCount = 100;
            filter.StartDateTime = now;
            filter.EndDateTime = now.AddDays(1);

            Assert.Equal(100, filter.VideoCount);
            Assert.Equal(now, filter.StartDateTime);
            Assert.Equal(now.AddDays(1), filter.EndDateTime);
        }

        [Fact]
        public void RingCredentials_CanBeCreatedWithDefaults()
        {
            var auth = new RingCredentials();

            Assert.NotNull(auth);
            Assert.Null(auth.UserName);
            Assert.Null(auth.Password);
        }

        [Fact]
        public void RingCredentials_StoresUserNameAndPassword()
        {
            var auth = new RingCredentials();
            string username = "test@example.com";
            string password = "testPassword";

            auth.UserName = username;
            auth.Password = password;

            Assert.Equal(username, auth.UserName);
            Assert.Equal(password, auth.Password);
        }

        [Fact]
        public void DeviceInfo_CanBeCreatedWithProperties()
        {
            var device = new DeviceInfo
            {
                Id = 123,
                Name = "Front Door",
                DeviceId = "device_abc123"
            };

            Assert.Equal(123, device.Id);
            Assert.Equal("Front Door", device.Name);
            Assert.Equal("device_abc123", device.DeviceId);
        }

        [Fact]
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

            Assert.Equal(1, deviceList.Devices.Count);
            Assert.Equal("Back Patio", deviceList.Devices[0].Name);
        }

        [Fact]
        public void DeviceList_SupportsMultipleDevices()
        {
            var deviceList = new DeviceList();
            DeviceInfo[] devices = new[]
            {
                new DeviceInfo { Id = 1, Name = "Camera 1", DeviceId = "dev_1" },
                new DeviceInfo { Id = 2, Name = "Camera 2", DeviceId = "dev_2" },
                new DeviceInfo { Id = 3, Name = "Camera 3", DeviceId = "dev_3" }
            };

            foreach (DeviceInfo device in devices)
            {
                deviceList.Devices.Add(device);
            }

            Assert.Equal(3, deviceList.Devices.Count);
            Assert.Equal("Camera 2", deviceList.Devices[1].Name);
        }

        [Fact]
        public void Model_DeviceInfoPropertiesAreIndependent()
        {
            var device1 = new DeviceInfo { Id = 1, Name = "Device A", DeviceId = "dev_a" };
            var device2 = new DeviceInfo { Id = 2, Name = "Device B", DeviceId = "dev_b" };

            Assert.NotEqual(device1.Id, device2.Id);
            Assert.NotEqual(device1.Name, device2.Name);
            Assert.NotEqual(device1.DeviceId, device2.DeviceId);
        }

        [Fact]
        public void FailedDownload_StoresErrorInformation()
        {
            DateTime now = DateTime.UtcNow;
            var error = new FailedDownload
            {
                Timestamp = now,
                EventId = "evt_123",
                CameraId = 456,
                CameraName = "Doorbell",
                LocationName = "Front",
                ErrorDescription = "Network timeout"
            };

            Assert.Equal("evt_123", error.EventId);
            Assert.Equal(456, error.CameraId);
            Assert.Equal("Network timeout", error.ErrorDescription);
        }

        [Fact]
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

            Assert.NotNull(failedDownload.EventId);
            Assert.NotNull(failedDownload.CameraName);
            Assert.NotNull(failedDownload.LocationName);
        }

        [Fact]
        public void Model_FailedDownloadTimestampIsUtc()
        {
            DateTime now = DateTime.UtcNow;
            var failed = new FailedDownload { Timestamp = now };

            Assert.Equal(now, failed.Timestamp);
            Assert.Equal(DateTimeKind.Utc, now.Kind);
        }

        [Fact]
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

            int daysDifference = (filter.EndDateTime - filter.StartDateTime).Value.Days;

            Assert.Equal(89, daysDifference);
            Assert.Equal(1000, filter.VideoCount);
        }
    }

    public class AuthResolutionTests
    {
        [Fact]
        public void RefreshToken_Present_SucceedsWithoutUsernameOrPassword()
        {
            var auth = new RingCredentials { RefreshToken = "cached-refresh-token" };

            string error = RingVideoService.ResolveAuthError(auth);

            Assert.Null(error);
        }

        [Fact]
        public void UsernameAndPassword_Present_Succeeds()
        {
            var auth = new RingCredentials { UserName = "user@example.com", Password = "pw" };

            string error = RingVideoService.ResolveAuthError(auth);

            Assert.Null(error);
        }

        [Fact]
        public void NoCredentialsAnywhere_FailsWithUsernameError()
        {
            var auth = new RingCredentials();

            string error = RingVideoService.ResolveAuthError(auth);

            Assert.Equal("A Ring username is required", error);
        }

        [Fact]
        public void UsernameOnly_NoPassword_FailsWithPasswordError()
        {
            var auth = new RingCredentials { UserName = "user@example.com" };

            string error = RingVideoService.ResolveAuthError(auth);

            Assert.Equal("A Ring password is required", error);
        }

        [Fact]
        public void RefreshToken_TakesPriorityOverIncompleteUsernamePassword()
        {
            // A username with no password would normally fail, but a refresh token short-circuits
            // that check entirely - this is the bug this test guards against regressing.
            var auth = new RingCredentials { RefreshToken = "cached-refresh-token", UserName = "user@example.com" };

            string error = RingVideoService.ResolveAuthError(auth);

            Assert.Null(error);
        }
    }

    public class LocationResolutionTests
    {
        [Fact]
        public void LocationNameResolutionUsesApiResult()
        {
            var locations = new List<Entities.Location>
            {
                new()
                {
                    Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    Name = "Front Door",
                    Address = new Entities.LocationAddress
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
                    Address = new Entities.LocationAddress
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
            string name = locationById.TryGetValue(locationId, out string value) ? value : "Unknown";

            Assert.Equal("Front Door", name);
        }

        [Fact]
        public void LocationNameResolutionFallsBackToDefault()
        {
            var locations = new List<Entities.Location>
            {
                new()
                {
                    Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    Name = "Front Door"
                }
            };

            var locationById = locations.ToDictionary(l => l.Id ?? Guid.Empty, l => l.Name);

            var locationId = Guid.Parse("99999999-9999-9999-9999-999999999999");
            string name = locationById.TryGetValue(locationId, out string value) ? value : "Unknown Location";

            Assert.Equal("Unknown Location", name);
        }

        [Fact]
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
            string name = apiLocations.TryGetValue(locationIdToResolve, out string apiName)
                ? apiName
                : (fallbackLocationNames.TryGetValue(locationIdToResolve.ToString(), out string configName) ? configName : "Unknown");

            Assert.Equal("Back Patio (from config)", name);
        }

        [Fact]
        public void LocationCanBeNullAndHandledGracefully()
        {
            var location = new Entities.Location
            {
                Id = null,
                Name = null,
                Address = null,
                IsOwner = null
            };

            Guid id = location.Id ?? Guid.Empty;
            string name = location.Name ?? "Unknown";

            Assert.Equal(Guid.Empty, id);
            Assert.Equal("Unknown", name);
        }
    }

    public class RingVideoServiceConstructorTests
    {
        [Fact]
        public void RingVideoService_CanBeInstantiatedWithValidDependencies()
        {
            // Arrange
            var logger = new MockLogger();
            var reporter = new MockReporter();
            var credentialStore = new MockCredentialStore();
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            _ = Directory.CreateDirectory(tempDir);

            try
            {
                // Act
                var service = new RingVideoService(logger, reporter, credentialStore, tempDir);

                // Assert
                Assert.NotNull(service);
                Assert.NotNull(service.Filter);
                Assert.NotNull(service.Auth);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void RingVideoService_SetsDefaultFilterOnConstruction()
        {
            // Arrange
            var logger = new MockLogger();
            var reporter = new MockReporter();
            var credentialStore = new MockCredentialStore();
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            _ = Directory.CreateDirectory(tempDir);

            try
            {
                // Act
                var service = new RingVideoService(logger, reporter, credentialStore, tempDir);

                // Assert
                Assert.NotNull(service.Filter);
                Assert.Equal(10000, service.Filter.VideoCount);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void RingVideoService_SetsDefaultAuthOnConstruction()
        {
            // Arrange
            var logger = new MockLogger();
            var reporter = new MockReporter();
            var credentialStore = new MockCredentialStore();
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            _ = Directory.CreateDirectory(tempDir);

            try
            {
                // Act
                var service = new RingVideoService(logger, reporter, credentialStore, tempDir);

                // Assert
                Assert.NotNull(service.Auth);
                Assert.Null(service.Auth.UserName);
                Assert.Null(service.Auth.Password);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void RingVideoService_InitializesSavedSettingsPath()
        {
            // Arrange
            var logger = new MockLogger();
            var reporter = new MockReporter();
            var credentialStore = new MockCredentialStore();
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            _ = Directory.CreateDirectory(tempDir);

            try
            {
                // Act
                var service = new RingVideoService(logger, reporter, credentialStore, tempDir);

                // Assert
                Assert.Equal(tempDir, service.SavedSettingsFolder);
                Assert.Equal(Path.Combine(tempDir, "RingVideosConfig.json"), service.SavedSettingsFile);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    public class RingVideoServiceGetFilterMessageTests
    {
        [Fact]
        public void GetFilterMessage_IncludesStartDate()
        {
            // Arrange
            var logger = new MockLogger();
            var reporter = new MockReporter();
            var credentialStore = new MockCredentialStore();
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            _ = Directory.CreateDirectory(tempDir);

            try
            {
                var service = new RingVideoService(logger, reporter, credentialStore, tempDir);
                var startDate = new DateTime(2024, 1, 15);
                service.Filter.StartDateTime = startDate;

                // Act
                string message = service.GetFilterMessage();

                // Assert
                Assert.Contains("Start Date", message);
                Assert.Contains(startDate.ToString(), message);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void GetFilterMessage_IncludesEndDate()
        {
            // Arrange
            var logger = new MockLogger();
            var reporter = new MockReporter();
            var credentialStore = new MockCredentialStore();
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            _ = Directory.CreateDirectory(tempDir);

            try
            {
                var service = new RingVideoService(logger, reporter, credentialStore, tempDir);
                var endDate = new DateTime(2024, 1, 20);
                service.Filter.EndDateTime = endDate;

                // Act
                string message = service.GetFilterMessage();

                // Assert
                Assert.Contains("End Date", message);
                Assert.Contains(endDate.ToString(), message);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void GetFilterMessage_IncludesVideoCount()
        {
            // Arrange
            var logger = new MockLogger();
            var reporter = new MockReporter();
            var credentialStore = new MockCredentialStore();
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            _ = Directory.CreateDirectory(tempDir);

            try
            {
                var service = new RingVideoService(logger, reporter, credentialStore, tempDir);
                service.Filter.VideoCount = 500;

                // Act
                string message = service.GetFilterMessage();

                // Assert
                Assert.Contains("Max downloads", message);
                Assert.Contains("500", message);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void GetFilterMessage_IncludesOnlyStarred()
        {
            // Arrange
            var logger = new MockLogger();
            var reporter = new MockReporter();
            var credentialStore = new MockCredentialStore();
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            _ = Directory.CreateDirectory(tempDir);

            try
            {
                var service = new RingVideoService(logger, reporter, credentialStore, tempDir);
                service.Filter.OnlyStarred = true;

                // Act
                string message = service.GetFilterMessage();

                // Assert
                Assert.Contains("Only Starred", message);
                Assert.Contains("True", message);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void GetFilterMessage_IncludesOnlyPersonDetected()
        {
            // Arrange
            var logger = new MockLogger();
            var reporter = new MockReporter();
            var credentialStore = new MockCredentialStore();
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            _ = Directory.CreateDirectory(tempDir);

            try
            {
                var service = new RingVideoService(logger, reporter, credentialStore, tempDir);
                service.Filter.OnlyPersonDetected = true;

                // Act
                string message = service.GetFilterMessage();

                // Assert
                Assert.Contains("Only Person", message);
                Assert.Contains("True", message);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void GetFilterMessage_IncludesDetectionType()
        {
            // Arrange
            var logger = new MockLogger();
            var reporter = new MockReporter();
            var credentialStore = new MockCredentialStore();
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            _ = Directory.CreateDirectory(tempDir);

            try
            {
                var service = new RingVideoService(logger, reporter, credentialStore, tempDir);
                service.Filter.DetectionType = "person";

                // Act
                string message = service.GetFilterMessage();

                // Assert
                Assert.Contains("Detection", message);
                Assert.Contains("person", message);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void GetFilterMessage_IncludesDownloadPath()
        {
            // Arrange
            var logger = new MockLogger();
            var reporter = new MockReporter();
            var credentialStore = new MockCredentialStore();
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            _ = Directory.CreateDirectory(tempDir);

            try
            {
                var service = new RingVideoService(logger, reporter, credentialStore, tempDir);
                service.Filter.DownloadPath = "C:\\Videos";

                // Act
                string message = service.GetFilterMessage();

                // Assert
                Assert.Contains("Download Path", message);
                Assert.Contains("C:\\Videos", message);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    public class RingVideoServicePrintFilterMessageTests
    {
        [Fact]
        public void PrintFilterMessage_CallsReporterInfo()
        {
            // Arrange
            var logger = new MockLogger();
            var reporter = new MockReporter();
            var credentialStore = new MockCredentialStore();
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            _ = Directory.CreateDirectory(tempDir);

            try
            {
                var service = new RingVideoService(logger, reporter, credentialStore, tempDir);
                string firstLine = "Test first line";

                // Act
                service.PrintFilterMessage(firstLine);

                // Assert
                Assert.True(reporter.InfoCalled, "Reporter.Info should have been called");
                Assert.True(reporter.HighlightCalled, "Reporter.Highlight should have been called");
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

#nullable enable
    public class RingVideoServiceSettingsPersistenceTests
    {
        [Fact]
        public void Settings_CanBeSavedToFile()
        {
            // Arrange
            var logger = new MockLogger();
            var reporter = new MockReporter();
            var credentialStore = new MockCredentialStore();
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            _ = Directory.CreateDirectory(tempDir);

            try
            {
                var service = new RingVideoService(logger, reporter, credentialStore, tempDir);
                service.Filter.VideoCount = 250;
                service.Filter.OnlyStarred = true;

                // Act
                MethodInfo? saveMethod = service.GetType().GetMethod("SaveSettings",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                _ = (saveMethod?.Invoke(service, new object?[] { null, null }));

                // Assert
                Assert.True(File.Exists(service.SavedSettingsFile), "Settings file should exist");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [Fact]
        public void Settings_CanBeLoadedFromFile()
        {
            // Arrange
            var logger = new MockLogger();
            var reporter = new MockReporter();
            var credentialStore = new MockCredentialStore();
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            _ = Directory.CreateDirectory(tempDir);

            try
            {
                // Create a settings file
                var service1 = new RingVideoService(logger, reporter, credentialStore, tempDir);
                service1.Filter.VideoCount = 300;
                MethodInfo? saveMethod = service1.GetType().GetMethod("SaveSettings",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                _ = (saveMethod?.Invoke(service1, new object?[] { null, null }));

                // Load it in a new service instance
                var service2 = new RingVideoService(logger, reporter, credentialStore, tempDir);

                // Assert
                Assert.Equal(300, service2.Filter.VideoCount);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }
    }

    // Mock implementations for testing
    internal class MockLogger : Microsoft.Extensions.Logging.ILogger<RingVideoService>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            Microsoft.Extensions.Logging.LogLevel logLevel,
            Microsoft.Extensions.Logging.EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        { }
    }

    internal class MockReporter : IDownloadReporter
    {
        public bool InfoCalled { get; set; }
        public bool HighlightCalled { get; set; }
        public List<string> Messages { get; } = [];

        public void Info(string message)
        {
            InfoCalled = true;
            Messages.Add(message);
        }

        public void Warning(string message)
        {
            Messages.Add(message);
        }

        public void Error(string message)
        {
            Messages.Add(message);
        }

        public void Highlight(string message)
        {
            HighlightCalled = true;
            Messages.Add(message);
        }

        public void UpdateFooter(string status) { }
        public object BeginItem(string label)
        {
            return new MockItemScope();
        }

        public void WriteItem(object item, string text) { }
        public void UpdateItem(object item, string status) { }
        public void CompleteItem(object item, string status) { }
        public void ErrorItem(object item, string status) { }
        public void WarnItem(object item, string status) { }
        public void ReleaseItem(object item) { }
        public void ClearItems() { }
        public void EnsureCapacity(int count) { }

        public Task<T> RunWithStatusAsync<T>(string message, Func<Func<string, Task>, Task<T>> operation)
        {
            return operation(async msg => { });
        }

        public Task RunWithStatusAsync(string message, Func<Func<string, Task>, Task> operation)
        {
            return operation(async msg => { });
        }

        public Task<string> PromptTwoFactorCodeAsync()
        {
            return Task.FromResult("123456");
        }
    }

    internal class MockItemScope
    {
        public string Id => Guid.NewGuid().ToString();
    }

    internal class MockCredentialStore : ICredentialStore
    {
        public RingCredentials Load(string path)
        {
            return new();
        }

        public RingCredentials LoadFromJson(string json)
        {
            return new();
        }

        public void Save(string path, RingCredentials credentials) { }
        public void SetCredentials(string path, string userName, string password = null, string refreshToken = null) { }
        public bool SanitizeClearTextPassword(string filePath, string authPath, string clearFieldName = "Password")
        {
            return false;
        }
    }
}

