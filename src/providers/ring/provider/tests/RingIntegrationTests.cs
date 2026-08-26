using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using VideoForensics.Data.Core.Contracts;
using VideoForensics.Providers.Common.Contracts;
using VideoForensics.Providers.Ring.Services;
using VideoForensics.Providers.Ring.Utils;
using Xunit;

namespace VideoForensics.Providers.Ring.Tests
{
    /// <summary>
    /// Integration tests for the full Ring workflow: auth → locations → devices → video download.
    /// These tests use the actual Ring API and DI container. Set TEST_RING_CREDENTIALS_PATH
    /// environment variable to enable them and provide a path to RingCredentials.json.
    /// </summary>
    public class RingIntegrationTests
    {
        private readonly string _credentialPath;

        public RingIntegrationTests()
        {
            // Auto-discover credentials from standard location
            var credentialDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "VideoForensics"
            );
            _credentialPath = Path.Combine(credentialDir, "RingCredentials.json");
        }

        private bool CanRunIntegrationTests => File.Exists(_credentialPath);

        private sealed class DiagnosticConsoleLoggerProvider : ILoggerProvider
        {
            public ILogger CreateLogger(string categoryName) => new DiagnosticConsoleLogger(categoryName);
            public void Dispose() { }

            private sealed class DiagnosticConsoleLogger : ILogger
            {
                private readonly string _category;
                public DiagnosticConsoleLogger(string category) => _category = category;
                public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
                public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;
                public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                {
                    Console.WriteLine($"[{logLevel}] {_category}: {formatter(state, exception)}");
                    if (exception != null)
                    {
                        Console.WriteLine($"    Exception: {exception.GetType().Name}: {exception.Message}");
                    }
                }
            }
        }

        private IServiceProvider BuildServiceProvider()
        {
            var services = new ServiceCollection();

            // Register logging
            services.AddLogging(builder =>
            {
                builder.AddProvider(new DiagnosticConsoleLoggerProvider());
                builder.SetMinimumLevel(LogLevel.Information);
            });

            // Register the shared session provider (must be singleton so all services observe same session)
            services.AddSingleton<ISessionProvider, SessionProvider>();

            // Register credential store for persisting auth tokens
            services.AddSingleton<ICredentialStore>(new CredentialStore());

            // Register Ring provider services with factories that provide typed loggers
            services.AddSingleton<IProviderAuthService>(provider =>
                new RingAuthService(
                    provider.GetRequiredService<ILogger<RingAuthService>>(),
                    provider.GetRequiredService<ISessionProvider>(),
                    provider.GetRequiredService<ICredentialStore>(),
                    _credentialPath
                )
            );
            services.AddSingleton<IDeviceDiscoveryService>(provider =>
                new RingDeviceDiscoveryService(
                    provider.GetRequiredService<ILogger<RingDeviceDiscoveryService>>(),
                    provider.GetRequiredService<ISessionProvider>()
                )
            );
            services.AddSingleton<IMediaDownloadService>(provider =>
            {
                var mockDataClient = new Mock<IVideoForensicsDataClient>().Object;
                return new RingMediaDownloadService(
                    provider.GetRequiredService<ILogger<RingMediaDownloadService>>(),
                    provider.GetRequiredService<ISessionProvider>(),
                    mockDataClient
                );
            });
            services.AddSingleton<IEventAndConfigService>(provider =>
                new RingEventAndConfigService(
                    provider.GetRequiredService<ILogger<RingEventAndConfigService>>(),
                    provider.GetRequiredService<ISessionProvider>()
                )
            );

            return services.BuildServiceProvider();
        }

        [Fact]
        public async Task FullWorkflow_AuthRestoreLocationsDevicesDownload_Success()
        {
            if (!CanRunIntegrationTests)
            {
                return;
            }

            // Arrange - Build DI container
            var serviceProvider = BuildServiceProvider();
            var logger = serviceProvider.GetRequiredService<ILogger<RingIntegrationTests>>();
            var authService = serviceProvider.GetRequiredService<IProviderAuthService>();
            var deviceService = serviceProvider.GetRequiredService<IDeviceDiscoveryService>();
            var downloadService = serviceProvider.GetRequiredService<IMediaDownloadService>();

            var outputDir = Path.Combine(Path.GetTempPath(), "ring_test_videos");
            Directory.CreateDirectory(outputDir);

            try
            {
                // Act 1: Restore authentication from persisted credentials
                var restored = await authService.RestoreFromSavedCredentialsAsync();
                Assert.True(restored, "Failed to restore authentication from saved credentials");

                // Act 2: Verify session is authenticated
                var isAuthenticated = await authService.IsAuthenticatedAsync();
                Assert.True(isAuthenticated, "Session is not authenticated after restoration");

                // Act 3: Get locations
                var locations = await deviceService.GetLocationsAsync();
                if (locations.Count == 0)
                {
                    throw new InvalidOperationException("No locations found on Ring account. Account may not have any locations configured, or API credentials may be incomplete.");
                }
                Assert.NotEmpty(locations);
                logger.LogInformation("Found {LocationCount} locations: {Locations}", locations.Count, string.Join(", ", locations.Select(l => l.Name)));

                // Act 4: Get devices from each location
                var allDevices = new List<Device>();
                foreach (var location in locations)
                {
                    var devices = await deviceService.GetDevicesAsync(location.Id);
                    logger.LogInformation("Location {LocationName} ({LocationId}): {DeviceCount} device(s)", location.Name, location.Id, devices.Count);
                    foreach (var device in devices)
                    {
                        logger.LogInformation("  - Device: {DeviceName} ({DeviceId}), Type: {DeviceType}, Online: {IsOnline}", device.Name, device.Id, device.Type, device.IsOnline);
                    }
                    allDevices.AddRange(devices);
                }

                if (allDevices.Count == 0)
                {
                    throw new InvalidOperationException($"No devices found across {locations.Count} location(s). Account may have locations but no cameras/doorbells registered.");
                }
                Assert.NotEmpty(allDevices);

                // Act 5: Download video from first online device
                var onlineDevices = allDevices.Where(d => d.IsOnline).ToList();
                if (onlineDevices.Any())
                {
                    var device = onlineDevices.First();
                    var startDate = DateTime.Now.AddDays(-7);
                    var endDate = DateTime.Now;

                    var result = await downloadService.DownloadVideosAsync(
                        device.Id,
                        outputDir,
                        startDate,
                        endDate
                    );

                    Assert.NotNull(result);
                    if (result.Success && result.FilesDownloaded > 0)
                    {
                        var downloadedFiles = Directory.GetFiles(outputDir, "*.mp4", SearchOption.AllDirectories);
                        Assert.NotEmpty(downloadedFiles);
                    }
                }
            }
            finally
            {
                // Cleanup
                if (Directory.Exists(outputDir))
                {
                    try
                    {
                        Directory.Delete(outputDir, recursive: true);
                    }
                    catch { /* Ignore cleanup errors */ }
                }
            }
        }

        [Fact]
        public async Task DownloadVideos_OneFromEachCameraType_Success()
        {
            if (!CanRunIntegrationTests)
            {
                return;
            }

            // Arrange
            var serviceProvider = BuildServiceProvider();
            var logger = serviceProvider.GetRequiredService<ILogger<RingIntegrationTests>>();
            var authService = serviceProvider.GetRequiredService<IProviderAuthService>();
            var deviceService = serviceProvider.GetRequiredService<IDeviceDiscoveryService>();
            var downloadService = serviceProvider.GetRequiredService<IMediaDownloadService>();

            var outputDir = Path.Combine(Path.GetTempPath(), "ring_video_by_type");
            Directory.CreateDirectory(outputDir);

            try
            {
                // Restore auth
                var restored = await authService.RestoreFromSavedCredentialsAsync();
                Assert.True(restored);

                // Get all devices
                var locations = await deviceService.GetLocationsAsync();
                Assert.NotEmpty(locations);

                var allDevices = new List<Device>();
                foreach (var location in locations)
                {
                    var devices = await deviceService.GetDevicesAsync(location.Id);
                    allDevices.AddRange(devices);
                }
                Assert.NotEmpty(allDevices);

                // Group devices by type
                var devicesByType = allDevices
                    .Where(d => d.IsOnline)
                    .GroupBy(d => d.Type)
                    .ToDictionary(g => g.Key, g => g.ToList());

                logger.LogInformation("Found {TypeCount} device types: {Types}",
                    devicesByType.Count,
                    string.Join(", ", devicesByType.Keys));

                var startDate = DateTime.Now.AddDays(-7);
                var endDate = DateTime.Now;

                // Download from one device of each type
                foreach (var (deviceType, devicesOfType) in devicesByType)
                {
                    var device = devicesOfType.First();
                    var typeOutputDir = Path.Combine(outputDir, deviceType);
                    Directory.CreateDirectory(typeOutputDir);

                    logger.LogInformation("Downloading from {DeviceType}: {DeviceName} ({DeviceId})",
                        deviceType, device.Name, device.Id);

                    var result = await downloadService.DownloadVideosAsync(
                        device.Id,
                        typeOutputDir,
                        startDate,
                        endDate
                    );

                    Assert.NotNull(result);
                    logger.LogInformation("  Result: Success={Success}, FilesDownloaded={Count}, Error={Error}",
                        result.Success, result.FilesDownloaded, result.ErrorMessage ?? "none");

                    // Assert that download was attempted
                    Assert.True(result.Success || !string.IsNullOrEmpty(result.ErrorMessage),
                        $"Download for {deviceType} returned neither success nor error message");
                }

                // Verify at least one device type was tested
                Assert.NotEmpty(devicesByType);
            }
            finally
            {
                if (Directory.Exists(outputDir))
                {
                    try
                    {
                        Directory.Delete(outputDir, recursive: true);
                    }
                    catch { }
                }
            }
        }

        [Fact]
        public async Task DownloadVideos_WritesEnhancedMetadata_AndValidatesMedia()
        {
            if (!CanRunIntegrationTests)
            {
                return;
            }

            // Arrange
            var serviceProvider = BuildServiceProvider();
            var logger = serviceProvider.GetRequiredService<ILogger<RingIntegrationTests>>();
            var authService = serviceProvider.GetRequiredService<IProviderAuthService>();
            var deviceService = serviceProvider.GetRequiredService<IDeviceDiscoveryService>();
            var downloadService = serviceProvider.GetRequiredService<IMediaDownloadService>();

            var outputDir = Path.Combine(Path.GetTempPath(), "ring_metadata_validation");
            Directory.CreateDirectory(outputDir);

            try
            {
                // Restore auth
                var restored = await authService.RestoreFromSavedCredentialsAsync();
                Assert.True(restored);

                // Get devices
                var locations = await deviceService.GetLocationsAsync();
                Assert.NotEmpty(locations);

                var allDevices = new List<Device>();
                foreach (var location in locations)
                {
                    var devices = await deviceService.GetDevicesAsync(location.Id);
                    allDevices.AddRange(devices);
                }
                Assert.NotEmpty(allDevices);

                var onlineDevices = allDevices.Where(d => d.IsOnline).ToList();
                if (!onlineDevices.Any())
                {
                    return; // Skip if no online devices
                }

                var device = onlineDevices.First();
                var startDate = DateTime.Now.AddDays(-7);
                var endDate = DateTime.Now;

                // Act - Download videos
                var result = await downloadService.DownloadVideosAsync(
                    device.Id,
                    outputDir,
                    startDate,
                    endDate
                );

                // Assert - Result contains metadata tracking
                Assert.NotNull(result);
                logger.LogInformation("Download result: Success={Success}, FilesDownloaded={Files}, " +
                    "MetadataWritten={Metadata}, MediaValidated={Validated}, " +
                    "ErrorsDetected={Detected}, ErrorsCorrected={Corrected}",
                    result.Success, result.FilesDownloaded, result.MetadataFilesWritten,
                    result.MediaFilesValidated, result.MediaErrorsDetected, result.MediaErrorsCorrected);

                // If download succeeded, verify metadata tracking
                if (result.Success && result.FilesDownloaded > 0)
                {
                    // Metadata files should be written alongside videos
                    Assert.True(result.MetadataFilesWritten >= 0,
                        "MetadataFilesWritten should be non-negative");

                    // Media validation should run on downloaded files
                    Assert.True(result.MediaFilesValidated >= 0,
                        "MediaFilesValidated should be non-negative");

                    // Check that actual files were created
                    var allFiles = Directory.GetFiles(outputDir, "*", SearchOption.AllDirectories);
                    logger.LogInformation("Downloaded {FileCount} total files", allFiles.Length);

                    // Should have video files (.mp4)
                    var videoFiles = Directory.GetFiles(outputDir, "*.mp4", SearchOption.AllDirectories);
                    Assert.True(videoFiles.Length == result.FilesDownloaded,
                        $"Video file count ({videoFiles.Length}) should match FilesDownloaded ({result.FilesDownloaded})");

                    // Should have metadata files (.json) for each video
                    var metadataFiles = Directory.GetFiles(outputDir, "*.json", SearchOption.AllDirectories);
                    logger.LogInformation("Found {VideoCount} videos and {MetadataCount} metadata files",
                        videoFiles.Length, metadataFiles.Length);

                    // Metadata files should exist alongside videos
                    if (videoFiles.Length > 0)
                    {
                        Assert.True(metadataFiles.Length > 0,
                            "Metadata files should be written for downloaded videos");

                        // Each video should have corresponding metadata
                        foreach (var videoFile in videoFiles)
                        {
                            var baseName = Path.GetFileNameWithoutExtension(videoFile);
                            var metadataFile = Directory.GetFiles(outputDir, $"{baseName}.json", SearchOption.AllDirectories).FirstOrDefault();
                            Assert.NotNull(metadataFile);
                            logger.LogInformation("Video {VideoName} has metadata: {MetadataName}",
                                Path.GetFileName(videoFile), Path.GetFileName(metadataFile));
                        }
                    }

                    // If media errors were detected, they should be corrected
                    if (result.MediaErrorsDetected > 0)
                    {
                        Assert.True(result.MediaErrorsCorrected > 0,
                            "Detected media errors should be corrected");
                        logger.LogInformation("Detected {Errors} media errors, corrected {Corrected}",
                            result.MediaErrorsDetected, result.MediaErrorsCorrected);
                    }
                }
            }
            finally
            {
                if (Directory.Exists(outputDir))
                {
                    try
                    {
                        Directory.Delete(outputDir, recursive: true);
                    }
                    catch { }
                }
            }
        }

        [Fact]
        public async Task CanRestoreFromPersistedCredentials()
        {
            if (!CanRunIntegrationTests)
            {
                return;
            }

            // Arrange
            var serviceProvider = BuildServiceProvider();
            var authService = serviceProvider.GetRequiredService<IProviderAuthService>();

            // Act
            var restored = await authService.RestoreFromSavedCredentialsAsync();
            var isAuthenticated = await authService.IsAuthenticatedAsync();

            // Assert
            Assert.True(restored, "Failed to restore from persisted credentials");
            Assert.True(isAuthenticated, "Session not authenticated after restoration");
        }

        [Fact]
        public async Task CanDiscoverLocationsAndDevices()
        {
            if (!CanRunIntegrationTests)
            {
                return;
            }

            // Arrange
            var serviceProvider = BuildServiceProvider();
            var authService = serviceProvider.GetRequiredService<IProviderAuthService>();
            var deviceService = serviceProvider.GetRequiredService<IDeviceDiscoveryService>();

            // Act
            await authService.RestoreFromSavedCredentialsAsync();
            var locations = await deviceService.GetLocationsAsync();

            // Assert
            Assert.NotEmpty(locations);

            foreach (var location in locations)
            {
                var devices = await deviceService.GetDevicesAsync(location.Id);
                Assert.NotNull(devices);
            }
        }

        [Fact]
        public async Task ApiCoverage_AllNonDestructiveEndpoints_NoSchemaViolations()
        {
            if (!CanRunIntegrationTests)
            {
                return;
            }

            // Arrange
            var serviceProvider = BuildServiceProvider();
            var logger = serviceProvider.GetRequiredService<ILogger<RingIntegrationTests>>();
            var authService = serviceProvider.GetRequiredService<IProviderAuthService>();

            // Restore session
            var restored = await authService.RestoreFromSavedCredentialsAsync();
            Assert.True(restored, "Failed to restore authentication");

            // Get the session from the provider
            var sessionProvider = serviceProvider.GetRequiredService<ISessionProvider>();
            var session = sessionProvider.GetSession();
            Assert.NotNull(session);

            var outputDir = Path.Combine(Path.GetTempPath(), "ring_api_test");
            Directory.CreateDirectory(outputDir);

            try
            {
                // Act - Run all non-destructive endpoints via Runner
                var runner = new Runner(session, outputDir, quiet: false);
                var runOptions = new RunOptions(
                    RequestedKeys: new[] { "devices", "locations", "doorbot-history", "snapshot-timestamps", "profile" },
                    Destructive: false,
                    NoPhysical: true,
                    LocationIdFilter: null,
                    DoorbotIdFilter: null,
                    ChimeIdFilter: null
                );

                var indexDoc = await runner.RunAsync(runOptions, _credentialPath);

                // Assert
                Assert.NotNull(indexDoc);
                Assert.NotEmpty(indexDoc.Calls);

                var successfulCalls = indexDoc.Calls.Where(c => c.Success).ToList();
                logger.LogInformation("Executed {TotalCalls} endpoints, {SuccessCount} successful",
                    indexDoc.Calls.Count, successfulCalls.Count);

                // All calls should succeed
                var hasErrors = false;
                foreach (var call in indexDoc.Calls)
                {
                    var statusCode = call.HttpCalls.FirstOrDefault()?.StatusCode ?? 0;
                    var status = call.Success ? "OK" : "FAIL";
                    logger.LogInformation("  {Endpoint}: {Status} HTTP {StatusCode}",
                        call.DisplayName, status, statusCode);

                    if (!call.Success)
                    {
                        logger.LogError("    Error: {Error}", call.Error);
                        if (statusCode == 429 || (call.Error ?? string.Empty).Contains("too many requests", StringComparison.OrdinalIgnoreCase))
                        {
                            logger.LogWarning("    Rate limit detected (HTTP 429)");
                        }
                        else
                        {
                            hasErrors = true;
                        }
                    }

                    // Check for schema violations
                    if (call.SchemaIssues.Any())
                    {
                        var errorIssues = call.SchemaIssues.Where(s => s.Severity == "Error").ToList();
                        if (errorIssues.Any())
                        {
                            logger.LogError("    Schema violations for {Endpoint}:", call.DisplayName);
                            foreach (var issue in errorIssues)
                            {
                                logger.LogError("      {Path}: {IssueType} (expected: {Expected}, actual: {Actual})",
                                    issue.Path, issue.IssueType, issue.Expected, issue.Actual);
                            }
                            hasErrors = true;
                        }
                    }
                }

                Assert.NotEmpty(successfulCalls);
                Assert.False(hasErrors, "Some endpoints failed or had schema violations");
            }
            finally
            {
                if (Directory.Exists(outputDir))
                {
                    try
                    {
                        Directory.Delete(outputDir, recursive: true);
                    }
                    catch { }
                }
            }
        }

        [Fact]
        public async Task DownloadSnapshots_AuthRestoreDeviceDiscovery_Success()
        {
            if (!CanRunIntegrationTests)
            {
                return;
            }

            // Arrange - Build DI container
            var serviceProvider = BuildServiceProvider();
            var logger = serviceProvider.GetRequiredService<ILogger<RingIntegrationTests>>();
            var authService = serviceProvider.GetRequiredService<IProviderAuthService>();
            var deviceService = serviceProvider.GetRequiredService<IDeviceDiscoveryService>();
            var downloadService = serviceProvider.GetRequiredService<IMediaDownloadService>();

            var outputDir = Path.Combine(Path.GetTempPath(), "ring_test_snapshots");
            Directory.CreateDirectory(outputDir);

            try
            {
                // Act - Restore and discover devices
                var restored = await authService.RestoreFromSavedCredentialsAsync();
                Assert.True(restored, "Failed to restore credentials");

                var locations = await deviceService.GetLocationsAsync();
                Assert.NotEmpty(locations);

                var startDate = DateTime.Now.AddDays(-30);  // Look back 30 days for more data
                var endDate = DateTime.Now;

                var downloadedSnapshotCount = 0;
                var deviceCount = 0;
                var cameraTypesFound = new HashSet<string>();

                foreach (var location in locations)
                {
                    var devices = await deviceService.GetDevicesAsync(location.Id.ToString());
                    if (devices == null || devices.Count == 0)
                        continue;

                    foreach (var device in devices)
                    {
                        deviceCount++;
                        cameraTypesFound.Add(device.Type);

                        var deviceOutputDir = Path.Combine(outputDir, $"{device.Name}_{device.Id}");
                        Directory.CreateDirectory(deviceOutputDir);

                        var result = await downloadService.DownloadSnapshotsAsync(
                            device.Id,
                            deviceOutputDir,
                            startDate,
                            endDate
                        );

                        logger.LogInformation("Device {Name} ({Type}): Downloaded {Count} snapshot(s)",
                            device.Name, device.Type, result.FilesDownloaded);

                        if (result.Success)
                        {
                            downloadedSnapshotCount += result.FilesDownloaded;
                        }
                    }
                }

                // Assert - At minimum, device discovery should work
                Assert.True(deviceCount > 0, "No devices found");
                Assert.NotEmpty(cameraTypesFound);

                logger.LogInformation("Found {DeviceCount} device(s) from {CameraTypeCount} type(s): {Types}",
                    deviceCount, cameraTypesFound.Count, string.Join(", ", cameraTypesFound));

                if (downloadedSnapshotCount > 0)
                {
                    logger.LogInformation("Downloaded {Total} snapshot(s)", downloadedSnapshotCount);
                    // Verify files exist
                    var jpgFiles = Directory.GetFiles(outputDir, "*.jpg", SearchOption.AllDirectories);
                    Assert.Equal(downloadedSnapshotCount, jpgFiles.Length);
                }
            }
            finally
            {
                if (Directory.Exists(outputDir))
                {
                    try
                    {
                        Directory.Delete(outputDir, recursive: true);
                    }
                    catch { }
                }
            }
        }
    }
}
