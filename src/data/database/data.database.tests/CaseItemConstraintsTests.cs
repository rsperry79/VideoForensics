using Microsoft.EntityFrameworkCore;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

using Xunit;

namespace VideoForensics.Data.Database.Tests
{
    /// <summary>
    /// Tests that hit the database constraints on CaseItems directly through the DbContext,
    /// bypassing CaseRepository, to confirm the EF model (not hand-written migration SQL)
    /// is what enforces the "one active pin per (Case, Event/MediaItem)" uniqueness and the
    /// "exactly one of EventId/MediaItemId, matching Kind" CHECK constraint.
    /// </summary>
    public class CaseItemConstraintsTests : IAsyncLifetime
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

        private async Task<ForensicCase> CreateCaseAsync(string caseNumber)
        {
            await using VideoForensicsDbContext db = _fixture.Factory.CreateDbContext();
            var forensicCase = new ForensicCase
            {
                Id = Guid.NewGuid(),
                CaseNumber = caseNumber,
                Title = "Title",
                CreatedBy = "tester@example.com",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
            };
            db.Cases.Add(forensicCase);
            await db.SaveChangesAsync();
            return forensicCase;
        }

        private async Task<Event> CreateEventAsync(string suffix)
        {
            await using VideoForensicsDbContext db = _fixture.Factory.CreateDbContext();
            var location = new Location
            {
                Id = Guid.NewGuid(),
                ProviderLocationId = $"loc-{suffix}",
                Name = "Test Location",
            };
            db.Locations.Add(location);

            var device = new Device
            {
                Id = Guid.NewGuid(),
                LocationId = location.Id,
                ProviderDeviceId = $"dev-{suffix}",
                Name = "Test Device",
                Type = "Camera",
            };
            db.Devices.Add(device);

            var evt = new Event
            {
                Id = Guid.NewGuid(),
                DeviceId = device.Id,
                ProviderEventId = $"evt-{suffix}",
                EventType = "Motion",
                OccurredAtUtc = DateTime.UtcNow,
                DiscoveredAtUtc = DateTime.UtcNow,
            };
            db.Events.Add(evt);
            await db.SaveChangesAsync();
            return evt;
        }

        private async Task<MediaItem> CreateMediaItemAsync(string suffix)
        {
            await using VideoForensicsDbContext db = _fixture.Factory.CreateDbContext();
            var location = new Location
            {
                Id = Guid.NewGuid(),
                ProviderLocationId = $"loc-{suffix}",
                Name = "Test Location",
            };
            db.Locations.Add(location);

            var device = new Device
            {
                Id = Guid.NewGuid(),
                LocationId = location.Id,
                ProviderDeviceId = $"dev-{suffix}",
                Name = "Test Device",
                Type = "Camera",
            };
            db.Devices.Add(device);

            var mediaItem = new MediaItem
            {
                Id = Guid.NewGuid(),
                DeviceId = device.Id,
                FileName = "clip.mp4",
                FilePath = "/tmp/clip.mp4",
                MediaFormat = "mp4",
                FileSizeBytes = 1024,
                RecordedAtUtc = DateTime.UtcNow,
                DownloadedAtUtc = DateTime.UtcNow,
                Sha256Hash = new string('a', 64),
            };
            db.MediaItems.Add(mediaItem);
            await db.SaveChangesAsync();
            return mediaItem;
        }

        [Fact]
        public async Task CaseItems_TwoActivePinsForSameCaseAndEvent_ThrowsDbUpdateException()
        {
            // Arrange
            ForensicCase forensicCase = await CreateCaseAsync("CONSTRAINT-001");
            Event evt = await CreateEventAsync("constraint-001");
            Guid eventId = evt.Id;

            await using VideoForensicsDbContext db1 = _fixture.Factory.CreateDbContext();
            db1.CaseItems.Add(new CaseItem
            {
                Id = Guid.NewGuid(),
                CaseId = forensicCase.Id,
                Kind = CaseItemKind.Event,
                EventId = eventId,
                Reason = "First pin",
                AddedBy = "tester@example.com",
                AddedAtUtc = DateTime.UtcNow,
            });
            await db1.SaveChangesAsync();

            await using VideoForensicsDbContext db2 = _fixture.Factory.CreateDbContext();
            db2.CaseItems.Add(new CaseItem
            {
                Id = Guid.NewGuid(),
                CaseId = forensicCase.Id,
                Kind = CaseItemKind.Event,
                EventId = eventId,
                Reason = "Duplicate active pin",
                AddedBy = "tester@example.com",
                AddedAtUtc = DateTime.UtcNow,
            });

            // Act & Assert - the unique filtered index IX_CaseItems_CaseId_EventId_Active rejects this
            await Assert.ThrowsAsync<DbUpdateException>(() => db2.SaveChangesAsync());
        }

        [Fact]
        public async Task CaseItems_ReactivePinAllowedAfterFirstPinSoftRemoved()
        {
            // Arrange
            ForensicCase forensicCase = await CreateCaseAsync("CONSTRAINT-002");
            Event evt = await CreateEventAsync("constraint-002");
            Guid eventId = evt.Id;

            await using VideoForensicsDbContext db1 = _fixture.Factory.CreateDbContext();
            db1.CaseItems.Add(new CaseItem
            {
                Id = Guid.NewGuid(),
                CaseId = forensicCase.Id,
                Kind = CaseItemKind.Event,
                EventId = eventId,
                Reason = "First pin",
                AddedBy = "tester@example.com",
                AddedAtUtc = DateTime.UtcNow,
                RemovedAtUtc = DateTime.UtcNow,
                RemovedBy = "tester@example.com",
                RemovalReason = "No longer needed",
            });
            await db1.SaveChangesAsync();

            // Act - a second, active pin for the same (CaseId, EventId) is allowed because the
            // filtered unique index only covers rows where RemovedAtUtc IS NULL.
            await using VideoForensicsDbContext db2 = _fixture.Factory.CreateDbContext();
            db2.CaseItems.Add(new CaseItem
            {
                Id = Guid.NewGuid(),
                CaseId = forensicCase.Id,
                Kind = CaseItemKind.Event,
                EventId = eventId,
                Reason = "Re-pinned",
                AddedBy = "tester@example.com",
                AddedAtUtc = DateTime.UtcNow,
            });
            await db2.SaveChangesAsync();

            // Assert
            await using VideoForensicsDbContext verifyDb = _fixture.Factory.CreateDbContext();
            int activeCount = await verifyDb.CaseItems.CountAsync(
                ci => ci.CaseId == forensicCase.Id && ci.EventId == eventId && ci.RemovedAtUtc == null);
            Assert.Equal(1, activeCount);
        }

        [Fact]
        public async Task CaseItems_KindEventWithNeitherTargetSet_ThrowsCheckConstraintViolation()
        {
            // Arrange
            ForensicCase forensicCase = await CreateCaseAsync("CONSTRAINT-003");

            await using VideoForensicsDbContext db = _fixture.Factory.CreateDbContext();
            db.CaseItems.Add(new CaseItem
            {
                Id = Guid.NewGuid(),
                CaseId = forensicCase.Id,
                Kind = CaseItemKind.Event,
                EventId = null,
                MediaItemId = null,
                Reason = "Invalid: neither target set",
                AddedBy = "tester@example.com",
                AddedAtUtc = DateTime.UtcNow,
            });

            // Act & Assert - CK_CaseItems_ExactlyOneTarget requires EventId set when Kind is Event
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        [Fact]
        public async Task CaseItems_KindEventWithMediaItemIdSetAndEventIdNull_ThrowsCheckConstraintViolation()
        {
            // Arrange - a real MediaItem so the MediaItemId foreign key is satisfied and only
            // the CHECK constraint (not a foreign key violation) fails.
            ForensicCase forensicCase = await CreateCaseAsync("CONSTRAINT-004");
            MediaItem mediaItem = await CreateMediaItemAsync("constraint-004");

            await using VideoForensicsDbContext db = _fixture.Factory.CreateDbContext();
            db.CaseItems.Add(new CaseItem
            {
                Id = Guid.NewGuid(),
                CaseId = forensicCase.Id,
                Kind = CaseItemKind.Event,
                EventId = null,
                MediaItemId = mediaItem.Id,
                Reason = "Invalid: Kind is Event but MediaItemId is set instead of EventId",
                AddedBy = "tester@example.com",
                AddedAtUtc = DateTime.UtcNow,
            });

            // Act & Assert - CK_CaseItems_ExactlyOneTarget rejects Kind=Event with MediaItemId set
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
    }
}
