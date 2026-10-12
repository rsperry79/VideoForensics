using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests
{
    /// <summary>Tests for DetectionEntityProvider, which manages adding detection-related entities within transactions.</summary>
    public class DetectionEntityProviderTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private UnitOfWork _unitOfWork = null!;
        private ILoggerFactory _loggerFactory = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            _loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });

            var serviceProvider = new TestServiceProvider(_fixture, _loggerFactory);
            _unitOfWork = new UnitOfWork(
                _fixture.Factory,
                serviceProvider,
                _loggerFactory.CreateLogger<UnitOfWork>());
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
            _loggerFactory.Dispose();
        }

        [Fact]
        public async Task DetectionEntityProvider_AddMediaItemDetectionAsync_AddsDetectionToContext()
        {
            var mediaItemId = Guid.NewGuid();
            var detectionId = Guid.NewGuid();

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                var detection = new MediaItemDetection
                {
                    Id = detectionId,
                    MediaItemId = mediaItemId,
                    DetectionType = "Person",
                    Confidence = 0.95m,
                    FullDescription = "Person detected",
                    PersonDetected = true
                };

                await context.DetectionEntities.AddMediaItemDetectionAsync(detection, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            VideoForensicsDbContext ctx = _fixture.Factory.CreateDbContext();
            MediaItemDetection? retrieved = await ctx.MediaItemDetections.FirstOrDefaultAsync(d => d.Id == detectionId);

            Assert.NotNull(retrieved);
            Assert.Equal(detectionId, retrieved.Id);
            Assert.Equal(mediaItemId, retrieved.MediaItemId);
            Assert.Equal("Person", retrieved.DetectionType);
            Assert.Equal(0.95m, retrieved.Confidence);
        }

        [Fact]
        public async Task DetectionEntityProvider_AddMediaItemDetectionAsync_WithNullDetection_NoErrorAndNothingAdded()
        {
            int countBefore = 0;
            int countAfter = 0;

            VideoForensicsDbContext ctxBefore = _fixture.Factory.CreateDbContext();
            countBefore = await ctxBefore.MediaItemDetections.CountAsync();

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                await context.DetectionEntities.AddMediaItemDetectionAsync(null!, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            VideoForensicsDbContext ctxAfter = _fixture.Factory.CreateDbContext();
            countAfter = await ctxAfter.MediaItemDetections.CountAsync();

            Assert.Equal(countBefore, countAfter);
        }

        [Fact]
        public async Task DetectionEntityProvider_AddDetectedPersonsAsync_AddsMultiplePersonsToContext()
        {
            var mediaItemId = Guid.NewGuid();
            var person1Id = Guid.NewGuid();
            var person2Id = Guid.NewGuid();

            var persons = new List<DetectedPerson>
            {
                new() {
                    Id = person1Id,
                    MediaItemId = mediaItemId,
                    ProfileId = "profile-1",
                    ProfileName = "Person 1",
                    Confidence = 0.85m
                },
                new() {
                    Id = person2Id,
                    MediaItemId = mediaItemId,
                    ProfileId = "profile-2",
                    ProfileName = "Person 2",
                    Confidence = 0.75m
                }
            };

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                await context.DetectionEntities.AddDetectedPersonsAsync(persons, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            VideoForensicsDbContext ctx = _fixture.Factory.CreateDbContext();
            int count = await ctx.DetectedPersons.CountAsync(p => p.MediaItemId == mediaItemId);
            List<DetectedPerson> retrieved = await ctx.DetectedPersons.Where(p => p.MediaItemId == mediaItemId).ToListAsync();

            Assert.Equal(2, count);
            Assert.Contains(retrieved, p => p.Id == person1Id && p.Confidence == 0.85m);
            Assert.Contains(retrieved, p => p.Id == person2Id && p.Confidence == 0.75m);
        }

        [Fact]
        public async Task DetectionEntityProvider_AddDetectedPersonsAsync_WithEmptyList_NoErrorAndNothingAdded()
        {
            int countBefore = await _fixture.Factory.CreateDbContext().DetectedPersons.CountAsync();

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                await context.DetectionEntities.AddDetectedPersonsAsync([], CancellationToken.None);
                return null;
            }, CancellationToken.None);

            int countAfter = await _fixture.Factory.CreateDbContext().DetectedPersons.CountAsync();
            Assert.Equal(countBefore, countAfter);
        }

        [Fact]
        public async Task DetectionEntityProvider_AddDetectedPersonsAsync_WithNullList_NoErrorAndNothingAdded()
        {
            int countBefore = await _fixture.Factory.CreateDbContext().DetectedPersons.CountAsync();

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                await context.DetectionEntities.AddDetectedPersonsAsync(null!, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            int countAfter = await _fixture.Factory.CreateDbContext().DetectedPersons.CountAsync();
            Assert.Equal(countBefore, countAfter);
        }

        [Fact]
        public async Task DetectionEntityProvider_AddDetectionTypeOccurrencesAsync_AddMultipleOccurrences()
        {
            var mediaItemDetectionId = Guid.NewGuid();
            var occurrence1Id = Guid.NewGuid();
            var occurrence2Id = Guid.NewGuid();

            var occurrences = new List<DetectionTypeOccurrence>
            {
                new() {
                    Id = occurrence1Id,
                    MediaItemDetectionId = mediaItemDetectionId,
                    DetectionType = "Vehicle",
                    DetectedAtUtc = DateTime.UtcNow
                },
                new() {
                    Id = occurrence2Id,
                    MediaItemDetectionId = mediaItemDetectionId,
                    DetectionType = "Animal",
                    DetectedAtUtc = DateTime.UtcNow
                }
            };

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                await context.DetectionEntities.AddDetectionTypeOccurrencesAsync(occurrences, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            VideoForensicsDbContext ctx = _fixture.Factory.CreateDbContext();
            int count = await ctx.DetectionTypeOccurrences.CountAsync(o => o.MediaItemDetectionId == mediaItemDetectionId);
            List<DetectionTypeOccurrence> retrieved = await ctx.DetectionTypeOccurrences.Where(o => o.MediaItemDetectionId == mediaItemDetectionId).ToListAsync();

            Assert.Equal(2, count);
            Assert.Contains(retrieved, o => o.Id == occurrence1Id && o.DetectionType == "Vehicle");
            Assert.Contains(retrieved, o => o.Id == occurrence2Id && o.DetectionType == "Animal");
        }

        [Fact]
        public async Task DetectionEntityProvider_AddEventDetectionAsync_AddsEventDetectionToContext()
        {
            var eventId = Guid.NewGuid();
            var detectionId = Guid.NewGuid();

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                var detection = new EventDetection
                {
                    Id = detectionId,
                    EventId = eventId,
                    DetectionType = "Motion",
                    Confidence = 0.88m,
                    FullDescription = "Motion detected"
                };

                await context.DetectionEntities.AddEventDetectionAsync(detection, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            VideoForensicsDbContext ctx = _fixture.Factory.CreateDbContext();
            EventDetection? retrieved = await ctx.EventDetections.FirstOrDefaultAsync(d => d.Id == detectionId);

            Assert.NotNull(retrieved);
            Assert.Equal(detectionId, retrieved.Id);
            Assert.Equal(eventId, retrieved.EventId);
            Assert.Equal("Motion", retrieved.DetectionType);
            Assert.Equal(0.88m, retrieved.Confidence);
        }

        [Fact]
        public async Task DetectionEntityProvider_AddEventDetectionAsync_WithNullDetection_NoErrorAndNothingAdded()
        {
            int countBefore = await _fixture.Factory.CreateDbContext().EventDetections.CountAsync();

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                await context.DetectionEntities.AddEventDetectionAsync(null!, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            int countAfter = await _fixture.Factory.CreateDbContext().EventDetections.CountAsync();
            Assert.Equal(countBefore, countAfter);
        }

        [Fact]
        public async Task DetectionEntityProvider_AddEventDetectionZonesAsync_AddsMultipleZonesToContext()
        {
            var eventDetectionId = Guid.NewGuid();
            var zone1Id = Guid.NewGuid();
            var zone2Id = Guid.NewGuid();

            var zones = new List<EventDetectionZone>
            {
                new() {
                    Id = zone1Id,
                    EventDetectionId = eventDetectionId,
                    ZoneId = "zone-a",
                    ZoneName = "Zone A",
                    Confidence = 0.90m
                },
                new() {
                    Id = zone2Id,
                    EventDetectionId = eventDetectionId,
                    ZoneId = "zone-b",
                    ZoneName = "Zone B",
                    Confidence = 0.85m
                }
            };

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                await context.DetectionEntities.AddEventDetectionZonesAsync(zones, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            VideoForensicsDbContext ctx = _fixture.Factory.CreateDbContext();
            int count = await ctx.EventDetectionZones.CountAsync(z => z.EventDetectionId == eventDetectionId);
            List<EventDetectionZone> retrieved = await ctx.EventDetectionZones.Where(z => z.EventDetectionId == eventDetectionId).ToListAsync();

            Assert.Equal(2, count);
            Assert.Contains(retrieved, z => z.Id == zone1Id && z.ZoneName == "Zone A");
            Assert.Contains(retrieved, z => z.Id == zone2Id && z.ZoneName == "Zone B");
        }

        [Fact]
        public async Task DetectionEntityProvider_AddEventSecurityAlertsAsync_AddsMultipleAlertsToContext()
        {
            var eventId = Guid.NewGuid();
            var alert1Id = Guid.NewGuid();
            var alert2Id = Guid.NewGuid();

            var alerts = new List<EventSecurityAlert>
            {
                new() {
                    Id = alert1Id,
                    EventId = eventId,
                    Severity = "Low",
                    AlertText = "Low confidence alert"
                },
                new() {
                    Id = alert2Id,
                    EventId = eventId,
                    Severity = "High",
                    AlertText = "High severity alert"
                }
            };

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                await context.DetectionEntities.AddEventSecurityAlertsAsync(alerts, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            VideoForensicsDbContext ctx = _fixture.Factory.CreateDbContext();
            int count = await ctx.EventSecurityAlerts.CountAsync(a => a.EventId == eventId);
            List<EventSecurityAlert> retrieved = await ctx.EventSecurityAlerts.Where(a => a.EventId == eventId).ToListAsync();

            Assert.Equal(2, count);
            Assert.Contains(retrieved, a => a.Id == alert1Id && a.Severity == "Low");
            Assert.Contains(retrieved, a => a.Id == alert2Id && a.Severity == "High");
        }

        [Fact]
        public async Task DetectionEntityProvider_AddEventDetectedPersonsAsync_AddsMultiplePersonsToContext()
        {
            var eventId = Guid.NewGuid();
            var person1Id = Guid.NewGuid();
            var person2Id = Guid.NewGuid();

            var persons = new List<EventDetectedPerson>
            {
                new() {
                    Id = person1Id,
                    EventId = eventId,
                    ProfileId = "profile-suspect-1",
                    ProfileName = "Suspect 1",
                    Confidence = 0.92m
                },
                new() {
                    Id = person2Id,
                    EventId = eventId,
                    ProfileId = "profile-witness-1",
                    ProfileName = "Witness 1",
                    Confidence = 0.78m
                }
            };

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                await context.DetectionEntities.AddEventDetectedPersonsAsync(persons, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            VideoForensicsDbContext ctx = _fixture.Factory.CreateDbContext();
            int count = await ctx.EventDetectedPersons.CountAsync(p => p.EventId == eventId);
            List<EventDetectedPerson> retrieved = await ctx.EventDetectedPersons.Where(p => p.EventId == eventId).ToListAsync();

            Assert.Equal(2, count);
            Assert.Contains(retrieved, p => p.Id == person1Id && p.Confidence == 0.92m);
            Assert.Contains(retrieved, p => p.Id == person2Id && p.Confidence == 0.78m);
        }

        [Fact]
        public async Task DetectionEntityProvider_AddEventDetectionTypeOccurrencesAsync_AddsMultipleOccurrences()
        {
            var eventDetectionId = Guid.NewGuid();
            var occurrence1Id = Guid.NewGuid();
            var occurrence2Id = Guid.NewGuid();

            var occurrences = new List<EventDetectionTypeOccurrence>
            {
                new() {
                    Id = occurrence1Id,
                    EventDetectionId = eventDetectionId,
                    DetectionType = "Package",
                    DetectedAtUtc = DateTime.UtcNow
                },
                new() {
                    Id = occurrence2Id,
                    EventDetectionId = eventDetectionId,
                    DetectionType = "Intrusion",
                    DetectedAtUtc = DateTime.UtcNow
                }
            };

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                await context.DetectionEntities.AddEventDetectionTypeOccurrencesAsync(occurrences, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            VideoForensicsDbContext ctx = _fixture.Factory.CreateDbContext();
            int count = await ctx.EventDetectionTypeOccurrences.CountAsync(o => o.EventDetectionId == eventDetectionId);
            List<EventDetectionTypeOccurrence> retrieved = await ctx.EventDetectionTypeOccurrences.Where(o => o.EventDetectionId == eventDetectionId).ToListAsync();

            Assert.Equal(2, count);
            Assert.Contains(retrieved, o => o.Id == occurrence1Id && o.DetectionType == "Package");
            Assert.Contains(retrieved, o => o.Id == occurrence2Id && o.DetectionType == "Intrusion");
        }

        [Fact]
        public async Task DetectionEntityProvider_MultipleAdds_InSingleTransaction_AllPersist()
        {
            var mediaItemId = Guid.NewGuid();
            var eventId = Guid.NewGuid();

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                var mediaDetection = new MediaItemDetection
                {
                    Id = Guid.NewGuid(),
                    MediaItemId = mediaItemId,
                    DetectionType = "Person",
                    Confidence = 0.90m,
                    PersonDetected = true
                };

                var eventDetection = new EventDetection
                {
                    Id = Guid.NewGuid(),
                    EventId = eventId,
                    DetectionType = "Motion",
                    Confidence = 0.85m,
                    FullDescription = "Motion detected"
                };

                await context.DetectionEntities.AddMediaItemDetectionAsync(mediaDetection, CancellationToken.None);
                await context.DetectionEntities.AddEventDetectionAsync(eventDetection, CancellationToken.None);

                return null;
            }, CancellationToken.None);

            VideoForensicsDbContext ctx = _fixture.Factory.CreateDbContext();
            int mediaCount = await ctx.MediaItemDetections.CountAsync(d => d.MediaItemId == mediaItemId);
            int eventCount = await ctx.EventDetections.CountAsync(d => d.EventId == eventId);

            Assert.Equal(1, mediaCount);
            Assert.Equal(1, eventCount);
        }

        private class TestServiceProvider : IServiceProvider
        {
            private readonly SqliteInMemoryFixture _fixture;
            private readonly Microsoft.Extensions.Logging.ILoggerFactory _loggerFactory;

            public TestServiceProvider(SqliteInMemoryFixture fixture, Microsoft.Extensions.Logging.ILoggerFactory loggerFactory)
            {
                _fixture = fixture;
                _loggerFactory = loggerFactory;
            }

            public object? GetService(Type serviceType)
            {
                return serviceType == typeof(ICredentialEncryptionProvider)
                    ? _fixture.EncryptionProvider
                    : serviceType == typeof(Microsoft.Extensions.Logging.ILogger<ICredentialRepository>)
                    ? _loggerFactory.CreateLogger<ICredentialRepository>()
                    : serviceType == typeof(Microsoft.Extensions.Logging.ILogger<UnitOfWork>) ? _loggerFactory.CreateLogger<UnitOfWork>() : (object?)null;
            }
        }
    }
}
