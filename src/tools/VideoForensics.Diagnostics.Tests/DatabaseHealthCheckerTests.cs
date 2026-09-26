using System;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;
using VideoForensics.Diagnostics.Contracts;
using Xunit;

namespace VideoForensics.Diagnostics.Tests
{
    public class DatabaseHealthCheckerTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        private DatabaseHealthChecker CreateChecker()
        {
            return new DatabaseHealthChecker(_fixture.Factory);
        }

        [Fact]
        public async Task DatabaseHealthChecker_FindDuplicateDevicesAsync_ReturnsGroupWithBothIds()
        {
            // Arrange - Drop the unique constraint to allow duplicate (LocationId, ProviderDeviceId) pairs
            // This simulates a scenario where duplicates got into the database before constraints existed
            await using var db = _fixture.Factory.CreateDbContext();
            var locationId = Guid.NewGuid();
            var device1Id = Guid.NewGuid();
            var device2Id = Guid.NewGuid();

            // Drop the unique index to allow inserting duplicates
            var connection = db.Database.GetDbConnection();
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "DROP INDEX IF EXISTS IX_Devices_LocationId_ProviderDeviceId";
            await cmd.ExecuteNonQueryAsync();

            var location = new Location
            {
                Id = locationId,
                ProviderLocationId = "loc-1",
                Name = "Test Location"
            };
            db.Locations.Add(location);

            // Insert two devices with the same LocationId and ProviderDeviceId (duplicate key)
            var device1 = new Device
            {
                Id = device1Id,
                LocationId = locationId,
                ProviderDeviceId = "device-1",
                Name = "Device 1",
                Type = "Camera"
            };
            db.Devices.Add(device1);

            var device2 = new Device
            {
                Id = device2Id,
                LocationId = locationId,
                ProviderDeviceId = "device-1",
                Name = "Device 2",
                Type = "Camera"
            };
            db.Devices.Add(device2);
            await db.SaveChangesAsync();

            // Act
            var checker = CreateChecker();
            var result = await checker.FindDuplicateDevicesAsync(CancellationToken.None);

            // Assert - Should find exactly one duplicate group with both device IDs
            Assert.Single(result);
            var group = result.First();
            Assert.Equal(2, group.Count);
            Assert.Equal(locationId, group.LocationId);
            Assert.Equal("device-1", group.ProviderKey);
            Assert.Contains(device1Id, group.RecordIds);
            Assert.Contains(device2Id, group.RecordIds);
        }

        [Fact]
        public async Task DatabaseHealthChecker_FindDuplicateDevicesAsync_NoDuplicates_ReturnsEmpty()
        {
            // Arrange
            await using var db = _fixture.Factory.CreateDbContext();
            var locationId = Guid.NewGuid();

            var location = new Location
            {
                Id = locationId,
                ProviderLocationId = "loc-1",
                Name = "Test Location"
            };
            db.Locations.Add(location);

            var device = new Device
            {
                Id = Guid.NewGuid(),
                LocationId = locationId,
                ProviderDeviceId = "device-1",
                Name = "Device 1",
                Type = "Camera"
            };
            db.Devices.Add(device);
            await db.SaveChangesAsync();

            // Act
            var checker = CreateChecker();
            var result = await checker.FindDuplicateDevicesAsync(CancellationToken.None);

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public async Task DatabaseHealthChecker_FindDuplicateEventsAsync_ReturnsGroupWithBothIds()
        {
            // Arrange - Drop the unique constraint to allow duplicate (DeviceId, ProviderEventId) pairs
            // This simulates a scenario where duplicates got into the database before constraints existed
            await using var db = _fixture.Factory.CreateDbContext();
            var deviceId = Guid.NewGuid();
            var event1Id = Guid.NewGuid();
            var event2Id = Guid.NewGuid();
            var locationId = Guid.NewGuid();

            // Drop the unique index to allow inserting duplicates
            var connection = db.Database.GetDbConnection();
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "DROP INDEX IF EXISTS IX_Events_DeviceId_ProviderEventId";
            await cmd.ExecuteNonQueryAsync();

            var location = new Location
            {
                Id = locationId,
                ProviderLocationId = "loc-1",
                Name = "Test Location"
            };
            db.Locations.Add(location);

            var device = new Device
            {
                Id = deviceId,
                LocationId = locationId,
                ProviderDeviceId = "device-1",
                Name = "Device 1",
                Type = "Camera"
            };
            db.Devices.Add(device);

            // Insert two events with the same DeviceId and ProviderEventId (duplicate key)
            var event1 = new Event
            {
                Id = event1Id,
                DeviceId = deviceId,
                ProviderEventId = "event-1",
                EventType = "Motion",
                OccurredAtUtc = DateTime.UtcNow,
                DiscoveredAtUtc = DateTime.UtcNow
            };
            db.Events.Add(event1);

            var event2 = new Event
            {
                Id = event2Id,
                DeviceId = deviceId,
                ProviderEventId = "event-1",
                EventType = "Motion",
                OccurredAtUtc = DateTime.UtcNow,
                DiscoveredAtUtc = DateTime.UtcNow
            };
            db.Events.Add(event2);
            await db.SaveChangesAsync();

            // Act
            var checker = CreateChecker();
            var result = await checker.FindDuplicateEventsAsync(CancellationToken.None);

            // Assert - Should find exactly one duplicate group with both event IDs
            Assert.Single(result);
            var group = result.First();
            Assert.Equal(2, group.Count);
            Assert.Equal(deviceId, group.DeviceId);
            Assert.Equal("event-1", group.ProviderKey);
            Assert.Contains(event1Id, group.RecordIds);
            Assert.Contains(event2Id, group.RecordIds);
        }

        [Fact]
        public async Task DatabaseHealthChecker_FindOrphanedRecordsAsync_DetectsOrphanedEvent()
        {
            // Arrange
            await using var db = _fixture.Factory.CreateDbContext();
            var nonExistentDeviceId = Guid.NewGuid();
            var orphanedEventId = Guid.NewGuid();

            var orphanedEvent = new Event
            {
                Id = orphanedEventId,
                DeviceId = nonExistentDeviceId,
                ProviderEventId = "orphaned-event",
                EventType = "Motion",
                OccurredAtUtc = DateTime.UtcNow,
                DiscoveredAtUtc = DateTime.UtcNow
            };
            db.Events.Add(orphanedEvent);
            await db.SaveChangesAsync();

            // Act
            var checker = CreateChecker();
            var result = await checker.FindOrphanedRecordsAsync(CancellationToken.None);

            // Assert
            Assert.Equal(1, result.OrphanedEventCount);
            Assert.Contains(orphanedEventId, result.OrphanedEventIds);
            Assert.Empty(result.OrphanedMediaItemIds);
            Assert.Empty(result.OrphanedMediaItemDetectionIds);
            Assert.Empty(result.OrphanedEventDetectionIds);
            Assert.Empty(result.OrphanedDeviceIds);
            Assert.Empty(result.OrphanedDownloadEventIds);
        }

        [Fact]
        public async Task DatabaseHealthChecker_FindOrphanedRecordsAsync_DetectsOrphanedMediaItem()
        {
            // Arrange
            await using var db = _fixture.Factory.CreateDbContext();
            var nonExistentDeviceId = Guid.NewGuid();
            var orphanedMediaItemId = Guid.NewGuid();

            var orphanedMediaItem = new MediaItem
            {
                Id = orphanedMediaItemId,
                DeviceId = nonExistentDeviceId,
                FileName = "test.mp4",
                FilePath = "/path/to/test.mp4",
                MediaFormat = "video/mp4",
                Sha256Hash = "abc123",
                RecordedAtUtc = DateTime.UtcNow,
                DownloadedAtUtc = DateTime.UtcNow
            };
            db.MediaItems.Add(orphanedMediaItem);
            await db.SaveChangesAsync();

            // Act
            var checker = CreateChecker();
            var result = await checker.FindOrphanedRecordsAsync(CancellationToken.None);

            // Assert
            Assert.Equal(1, result.OrphanedMediaItemCount);
            Assert.Contains(orphanedMediaItemId, result.OrphanedMediaItemIds);
            Assert.Empty(result.OrphanedEventIds);
            Assert.Empty(result.OrphanedMediaItemDetectionIds);
            Assert.Empty(result.OrphanedEventDetectionIds);
            Assert.Empty(result.OrphanedDeviceIds);
            Assert.Empty(result.OrphanedDownloadEventIds);
        }

        [Fact]
        public async Task DatabaseHealthChecker_FindOrphanedRecordsAsync_NoOrphans_ReturnsAllZero()
        {
            // Arrange
            await using var db = _fixture.Factory.CreateDbContext();
            var locationId = Guid.NewGuid();
            var deviceId = Guid.NewGuid();
            var eventId = Guid.NewGuid();
            var mediaItemId = Guid.NewGuid();

            var location = new Location
            {
                Id = locationId,
                ProviderLocationId = "loc-1",
                Name = "Test Location"
            };
            db.Locations.Add(location);

            var device = new Device
            {
                Id = deviceId,
                LocationId = locationId,
                ProviderDeviceId = "device-1",
                Name = "Device 1",
                Type = "Camera"
            };
            db.Devices.Add(device);

            var @event = new Event
            {
                Id = eventId,
                DeviceId = deviceId,
                ProviderEventId = "event-1",
                EventType = "Motion",
                OccurredAtUtc = DateTime.UtcNow,
                DiscoveredAtUtc = DateTime.UtcNow
            };
            db.Events.Add(@event);

            var mediaItem = new MediaItem
            {
                Id = mediaItemId,
                DeviceId = deviceId,
                FileName = "test.mp4",
                FilePath = "/path/to/test.mp4",
                MediaFormat = "video/mp4",
                Sha256Hash = "abc123",
                RecordedAtUtc = DateTime.UtcNow,
                DownloadedAtUtc = DateTime.UtcNow
            };
            db.MediaItems.Add(mediaItem);

            await db.SaveChangesAsync();

            // Act
            var checker = CreateChecker();
            var result = await checker.FindOrphanedRecordsAsync(CancellationToken.None);

            // Assert
            Assert.Equal(0, result.OrphanedEventCount);
            Assert.Empty(result.OrphanedEventIds);
            Assert.Equal(0, result.OrphanedMediaItemCount);
            Assert.Empty(result.OrphanedMediaItemIds);
            Assert.Equal(0, result.OrphanedMediaItemDetectionCount);
            Assert.Empty(result.OrphanedMediaItemDetectionIds);
            Assert.Equal(0, result.OrphanedEventDetectionCount);
            Assert.Empty(result.OrphanedEventDetectionIds);
            Assert.Equal(0, result.OrphanedDeviceCount);
            Assert.Empty(result.OrphanedDeviceIds);
            Assert.Equal(0, result.OrphanedDownloadEventCount);
            Assert.Empty(result.OrphanedDownloadEventIds);
        }

        [Fact]
        public async Task DatabaseHealthChecker_GetDetectionRedundancySummaryAsync_ReturnsCorrectCounts()
        {
            // Arrange
            await using var db = _fixture.Factory.CreateDbContext();
            var locationId = Guid.NewGuid();
            var deviceId = Guid.NewGuid();
            var eventId = Guid.NewGuid();
            var mediaItemId = Guid.NewGuid();

            var location = new Location
            {
                Id = locationId,
                ProviderLocationId = "loc-1",
                Name = "Test Location"
            };
            db.Locations.Add(location);

            var device = new Device
            {
                Id = deviceId,
                LocationId = locationId,
                ProviderDeviceId = "device-1",
                Name = "Device 1",
                Type = "Camera"
            };
            db.Devices.Add(device);

            var @event = new Event
            {
                Id = eventId,
                DeviceId = deviceId,
                ProviderEventId = "event-1",
                EventType = "Motion",
                OccurredAtUtc = DateTime.UtcNow,
                DiscoveredAtUtc = DateTime.UtcNow
            };
            db.Events.Add(@event);

            var mediaItem = new MediaItem
            {
                Id = mediaItemId,
                DeviceId = deviceId,
                FileName = "test.mp4",
                FilePath = "/path/to/test.mp4",
                MediaFormat = "video/mp4",
                Sha256Hash = "abc123",
                RecordedAtUtc = DateTime.UtcNow,
                DownloadedAtUtc = DateTime.UtcNow
            };
            db.MediaItems.Add(mediaItem);

            var mediaDetection = new MediaItemDetection
            {
                Id = Guid.NewGuid(),
                MediaItemId = mediaItemId,
                PersonDetected = true
            };
            db.MediaItemDetections.Add(mediaDetection);

            var eventDetection = new EventDetection
            {
                Id = Guid.NewGuid(),
                EventId = eventId,
                PersonDetected = true
            };
            db.EventDetections.Add(eventDetection);

            await db.SaveChangesAsync();

            // Act
            var checker = CreateChecker();
            var result = await checker.GetDetectionRedundancySummaryAsync(CancellationToken.None);

            // Assert
            Assert.Equal(1, result.MediaItemDetectionCount);
            Assert.Equal(1, result.EventDetectionCount);
            Assert.NotNull(result.AvgDetectionsPerMediaItem);
            Assert.Equal(1.0, result.AvgDetectionsPerMediaItem);
            Assert.NotNull(result.AvgDetectionsPerEvent);
            Assert.Equal(1.0, result.AvgDetectionsPerEvent);
        }

        [Fact]
        public async Task DatabaseHealthChecker_GetDeviceHealthSummaryAsync_ReturnsCorrectCounts()
        {
            // Arrange
            await using var db = _fixture.Factory.CreateDbContext();

            var deviceHealth1 = new DeviceHealth
            {
                Id = Guid.NewGuid(),
                DeviceId = Guid.NewGuid(),
                CapturedAtUtc = DateTime.UtcNow.AddDays(-2)
            };
            var deviceHealth2 = new DeviceHealth
            {
                Id = Guid.NewGuid(),
                DeviceId = Guid.NewGuid(),
                CapturedAtUtc = DateTime.UtcNow.AddDays(-1)
            };
            var deviceHealth3 = new DeviceHealth
            {
                Id = Guid.NewGuid(),
                DeviceId = Guid.NewGuid(),
                CapturedAtUtc = DateTime.UtcNow
            };

            db.DeviceHealths.AddRange(deviceHealth1, deviceHealth2, deviceHealth3);
            await db.SaveChangesAsync();

            // Act
            var checker = CreateChecker();
            var result = await checker.GetDeviceHealthSummaryAsync(CancellationToken.None);

            // Assert
            Assert.Equal(3, result.DeviceHealthRowCount);
            Assert.NotEmpty(result.RecentDates);
            // At minimum, should have captured the dates
            Assert.True(result.RecentDates.Count > 0);
        }

        [Fact]
        public async Task DatabaseHealthChecker_GetDeviceFeatureOverlapAsync_ReturnsCorrectCounts()
        {
            // Arrange
            await using var db = _fixture.Factory.CreateDbContext();
            var device1Id = Guid.NewGuid();
            var device2Id = Guid.NewGuid();
            var device3Id = Guid.NewGuid();

            var capabilities1 = new DeviceCapabilities { Id = Guid.NewGuid(), DeviceId = device1Id };
            var capabilities2 = new DeviceCapabilities { Id = Guid.NewGuid(), DeviceId = device2Id };
            var features1 = new DeviceFeatures { Id = Guid.NewGuid(), DeviceId = device1Id };
            var features3 = new DeviceFeatures { Id = Guid.NewGuid(), DeviceId = device3Id };

            db.DeviceCapabilities.AddRange(capabilities1, capabilities2);
            db.DeviceFeatures.AddRange(features1, features3);
            await db.SaveChangesAsync();

            // Act
            var checker = CreateChecker();
            var result = await checker.GetDeviceFeatureOverlapAsync(CancellationToken.None);

            // Assert
            Assert.Equal(2, result.DeviceCapabilitiesCount);
            Assert.Equal(2, result.DeviceFeaturesCount);
            Assert.Equal(1, result.DevicesWithBoth); // device1 has both
            Assert.Single(result.DeviceIdsWithBoth);
            Assert.Contains(device1Id, result.DeviceIdsWithBoth);
            Assert.Equal(1, result.CapabilitiesOnlyCount); // device2 has only capabilities
            Assert.Equal(1, result.FeaturesOnlyCount); // device3 has only features
        }

        [Fact]
        public async Task DatabaseHealthChecker_GetTableSizesAsync_ReturnsCorrectCounts()
        {
            // Arrange
            await using var db = _fixture.Factory.CreateDbContext();
            var locationId = Guid.NewGuid();
            var deviceId = Guid.NewGuid();

            var location = new Location
            {
                Id = locationId,
                ProviderLocationId = "loc-1",
                Name = "Test Location"
            };
            db.Locations.Add(location);

            var device = new Device
            {
                Id = deviceId,
                LocationId = locationId,
                ProviderDeviceId = "device-1",
                Name = "Device 1",
                Type = "Camera"
            };
            db.Devices.Add(device);

            var @event = new Event
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                ProviderEventId = "event-1",
                EventType = "Motion",
                OccurredAtUtc = DateTime.UtcNow,
                DiscoveredAtUtc = DateTime.UtcNow
            };
            db.Events.Add(@event);

            await db.SaveChangesAsync();

            // Act
            var checker = CreateChecker();
            var result = await checker.GetTableSizesAsync(CancellationToken.None);

            // Assert
            Assert.NotEmpty(result);
            var locationsEntry = result.FirstOrDefault(x => x.TableName == "Locations");
            Assert.NotNull(locationsEntry);
            Assert.Equal(1, locationsEntry.RowCount);

            var devicesEntry = result.FirstOrDefault(x => x.TableName == "Devices");
            Assert.NotNull(devicesEntry);
            Assert.Equal(1, devicesEntry.RowCount);

            var eventsEntry = result.FirstOrDefault(x => x.TableName == "Events");
            Assert.NotNull(eventsEntry);
            Assert.Equal(1, eventsEntry.RowCount);
        }
    }
}
