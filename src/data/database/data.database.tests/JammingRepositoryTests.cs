using Microsoft.Extensions.Logging;
using Xunit;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.Repositories;

namespace VideoForensics.Data.Database.Tests
{
    public class JammingRepositoryTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private JammingRepository _repository = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });
            _repository = new JammingRepository(_fixture.Factory, loggerFactory.CreateLogger<JammingRepository>());
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        private static JammingIncidentRecord BuildJammingIncident(
            Guid deviceId,
            DateTime startUtc,
            DateTime? endUtc = null,
            int affectedEventCount = 5,
            double averageDegradationDb = 10.0,
            JammingConfidenceLevel confidence = JammingConfidenceLevel.Medium,
            JammingIncidentSource source = JammingIncidentSource.AutoDetected)
        {
            return new JammingIncidentRecord
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                StartUtc = startUtc,
                EndUtc = endUtc ?? startUtc.AddMinutes(15),
                AffectedEventCount = affectedEventCount,
                AverageDegradationDb = averageDegradationDb,
                Confidence = confidence,
                DetectedAtUtc = DateTime.UtcNow,
                Notes = "Test incident",
                Source = source
            };
        }

        [Fact]
        public async Task UpsertIncidentAsync_CreatesNewIncident_WhenIdIsEmpty()
        {
            var deviceId = Guid.NewGuid();
            var startTime = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
            var incident = BuildJammingIncident(deviceId, startTime);
            incident.Id = Guid.Empty;

            var result = await _repository.UpsertIncidentAsync(incident, CancellationToken.None);

            Assert.NotEqual(Guid.Empty, result.Id);
            Assert.Equal(deviceId, result.DeviceId);
            Assert.Equal(startTime, result.StartUtc);
        }

        [Fact]
        public async Task UpsertIncidentAsync_UpdatesExistingIncident_WhenIdMatches()
        {
            var deviceId = Guid.NewGuid();
            var startTime = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
            var incident = BuildJammingIncident(deviceId, startTime);

            var created = await _repository.UpsertIncidentAsync(incident, CancellationToken.None);
            var createdId = created.Id;

            // Update the incident
            created.AffectedEventCount = 10;
            created.AverageDegradationDb = 15.5;
            created.Confidence = JammingConfidenceLevel.High;
            created.Notes = "Updated test incident";

            var updated = await _repository.UpsertIncidentAsync(created, CancellationToken.None);

            Assert.Equal(createdId, updated.Id);
            Assert.Equal(10, updated.AffectedEventCount);
            Assert.Equal(15.5, updated.AverageDegradationDb);
            Assert.Equal(JammingConfidenceLevel.High, updated.Confidence);
            Assert.Equal("Updated test incident", updated.Notes);
        }

        [Fact]
        public async Task ListIncidentsAsync_ReturnsAllIncidents_WhenNoFilters()
        {
            var deviceId1 = Guid.NewGuid();
            var deviceId2 = Guid.NewGuid();
            var now = DateTime.UtcNow;

            var incident1 = BuildJammingIncident(deviceId1, now.AddHours(-1));
            var incident2 = BuildJammingIncident(deviceId1, now);
            var incident3 = BuildJammingIncident(deviceId2, now.AddHours(1));

            await _repository.UpsertIncidentAsync(incident1, CancellationToken.None);
            await _repository.UpsertIncidentAsync(incident2, CancellationToken.None);
            await _repository.UpsertIncidentAsync(incident3, CancellationToken.None);

            var results = await _repository.ListIncidentsAsync(null, null, null, CancellationToken.None);

            Assert.Equal(3, results.Count);
        }

        [Fact]
        public async Task ListIncidentsAsync_FiltersBy_DeviceId()
        {
            var deviceId1 = Guid.NewGuid();
            var deviceId2 = Guid.NewGuid();
            var now = DateTime.UtcNow;

            await _repository.UpsertIncidentAsync(BuildJammingIncident(deviceId1, now), CancellationToken.None);
            await _repository.UpsertIncidentAsync(BuildJammingIncident(deviceId1, now.AddMinutes(15)), CancellationToken.None);
            await _repository.UpsertIncidentAsync(BuildJammingIncident(deviceId2, now.AddHours(1)), CancellationToken.None);

            var results = await _repository.ListIncidentsAsync(deviceId1, null, null, CancellationToken.None);

            Assert.Equal(2, results.Count);
            Assert.All(results, r => Assert.Equal(deviceId1, r.DeviceId));
        }

        [Fact]
        public async Task ListIncidentsAsync_FiltersBy_DateRange()
        {
            var deviceId = Guid.NewGuid();
            var now = DateTime.UtcNow;

            var before = BuildJammingIncident(deviceId, now.AddHours(-2));
            var during1 = BuildJammingIncident(deviceId, now.AddHours(-1));
            var during2 = BuildJammingIncident(deviceId, now);
            var after = BuildJammingIncident(deviceId, now.AddHours(2));

            await _repository.UpsertIncidentAsync(before, CancellationToken.None);
            await _repository.UpsertIncidentAsync(during1, CancellationToken.None);
            await _repository.UpsertIncidentAsync(during2, CancellationToken.None);
            await _repository.UpsertIncidentAsync(after, CancellationToken.None);

            var results = await _repository.ListIncidentsAsync(
                deviceId,
                now.AddHours(-1.5),
                now.AddHours(1),
                CancellationToken.None);

            Assert.Equal(2, results.Count);
            Assert.All(results, r =>
            {
                Assert.True(r.StartUtc >= now.AddHours(-1.5));
                Assert.True(r.StartUtc <= now.AddHours(1));
            });
        }

        [Fact]
        public async Task ListIncidentsAsync_AppliesMultipleFilters_Together()
        {
            var deviceId1 = Guid.NewGuid();
            var deviceId2 = Guid.NewGuid();
            var now = DateTime.UtcNow;

            var device1Before = BuildJammingIncident(deviceId1, now.AddHours(-2));
            var device1During = BuildJammingIncident(deviceId1, now);
            var device1After = BuildJammingIncident(deviceId1, now.AddHours(2));
            var device2During = BuildJammingIncident(deviceId2, now);

            await _repository.UpsertIncidentAsync(device1Before, CancellationToken.None);
            await _repository.UpsertIncidentAsync(device1During, CancellationToken.None);
            await _repository.UpsertIncidentAsync(device1After, CancellationToken.None);
            await _repository.UpsertIncidentAsync(device2During, CancellationToken.None);

            var results = await _repository.ListIncidentsAsync(
                deviceId1,
                now.AddHours(-1),
                now.AddHours(1),
                CancellationToken.None);

            Assert.Single(results);
            Assert.Equal(deviceId1, results[0].DeviceId);
            Assert.True(results[0].StartUtc >= now.AddHours(-1));
            Assert.True(results[0].StartUtc <= now.AddHours(1));
        }

        [Fact]
        public async Task GetStatsAsync_ReturnsNull_WhenNoStatsExist()
        {
            var deviceId = Guid.NewGuid();

            var result = await _repository.GetStatsAsync(deviceId, CancellationToken.None);

            Assert.Null(result);
        }

        [Fact]
        public async Task GetStatsAsync_ReturnsStats_WhenStatsExist()
        {
            var deviceId = Guid.NewGuid();
            var now = DateTime.UtcNow;

            var incident = BuildJammingIncident(deviceId, now);
            await _repository.UpsertIncidentAsync(incident, CancellationToken.None);
            await _repository.RecomputeStatsAsync(deviceId, CancellationToken.None);

            var result = await _repository.GetStatsAsync(deviceId, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(deviceId, result.DeviceId);
            Assert.Equal(1, result.IncidentCount);
        }

        [Fact]
        public async Task ListStatsAsync_ReturnsAllStats()
        {
            var deviceId1 = Guid.NewGuid();
            var deviceId2 = Guid.NewGuid();
            var now = DateTime.UtcNow;

            var incident1 = BuildJammingIncident(deviceId1, now);
            var incident2 = BuildJammingIncident(deviceId2, now.AddHours(1));

            await _repository.UpsertIncidentAsync(incident1, CancellationToken.None);
            await _repository.UpsertIncidentAsync(incident2, CancellationToken.None);
            await _repository.RecomputeStatsAsync(deviceId1, CancellationToken.None);
            await _repository.RecomputeStatsAsync(deviceId2, CancellationToken.None);

            var results = await _repository.ListStatsAsync(CancellationToken.None);

            Assert.Equal(2, results.Count);
        }

        [Fact]
        public async Task RecomputeStatsAsync_ComputesCorrectStats_FromIncidents()
        {
            var deviceId = Guid.NewGuid();
            var now = DateTime.UtcNow;

            var incident1 = BuildJammingIncident(
                deviceId,
                now,
                now.AddMinutes(10),
                affectedEventCount: 3,
                averageDegradationDb: 8.0,
                confidence: JammingConfidenceLevel.Low);

            var incident2 = BuildJammingIncident(
                deviceId,
                now.AddMinutes(30),
                now.AddMinutes(50),
                affectedEventCount: 5,
                averageDegradationDb: 12.0,
                confidence: JammingConfidenceLevel.Medium);

            var incident3 = BuildJammingIncident(
                deviceId,
                now.AddMinutes(60),
                now.AddMinutes(65),
                affectedEventCount: 2,
                averageDegradationDb: 16.0,
                confidence: JammingConfidenceLevel.High);

            await _repository.UpsertIncidentAsync(incident1, CancellationToken.None);
            await _repository.UpsertIncidentAsync(incident2, CancellationToken.None);
            await _repository.UpsertIncidentAsync(incident3, CancellationToken.None);

            var summary = await _repository.RecomputeStatsAsync(deviceId, CancellationToken.None);

            Assert.Equal(3, summary.IncidentCount);
            Assert.Equal(35, summary.TotalJammedDurationMinutes); // 10 + 20 + 5
            Assert.Equal(12.0, summary.AverageDegradationDb); // (8 + 12 + 16) / 3
            Assert.Equal(16.0, summary.MaxDegradationDb);
            Assert.Equal(1, summary.LowConfidenceCount);
            Assert.Equal(1, summary.MediumConfidenceCount);
            Assert.Equal(1, summary.HighConfidenceCount);
            Assert.Equal(0, summary.DefiniteConfidenceCount);
            Assert.Equal(now, summary.FirstIncidentUtc);
            Assert.Equal(now.AddMinutes(60), summary.LastIncidentUtc);
        }

        [Fact]
        public async Task RecomputeStatsAsync_HandlesEmptyIncidents_Gracefully()
        {
            var deviceId = Guid.NewGuid();

            var summary = await _repository.RecomputeStatsAsync(deviceId, CancellationToken.None);

            Assert.Equal(deviceId, summary.DeviceId);
            Assert.Equal(0, summary.IncidentCount);
            Assert.Equal(0, summary.TotalJammedDurationMinutes);
            Assert.Equal(0, summary.AverageDegradationDb);
            Assert.Equal(0, summary.MaxDegradationDb);
            Assert.Null(summary.FirstIncidentUtc);
            Assert.Null(summary.LastIncidentUtc);
        }

        [Fact]
        public async Task RecomputeStatsAsync_UpdatesExistingStats()
        {
            var deviceId = Guid.NewGuid();
            var now = DateTime.UtcNow;

            var incident1 = BuildJammingIncident(deviceId, now);
            await _repository.UpsertIncidentAsync(incident1, CancellationToken.None);
            var summary1 = await _repository.RecomputeStatsAsync(deviceId, CancellationToken.None);

            Assert.Equal(1, summary1.IncidentCount);

            var incident2 = BuildJammingIncident(deviceId, now.AddHours(1));
            await _repository.UpsertIncidentAsync(incident2, CancellationToken.None);
            var summary2 = await _repository.RecomputeStatsAsync(deviceId, CancellationToken.None);

            Assert.Equal(summary1.Id, summary2.Id); // Same summary row
            Assert.Equal(2, summary2.IncidentCount);
        }

        [Fact]
        public async Task RecomputeStatsAsync_CountsConfidenceByLevel()
        {
            var deviceId = Guid.NewGuid();
            var now = DateTime.UtcNow;

            await _repository.UpsertIncidentAsync(
                BuildJammingIncident(deviceId, now, confidence: JammingConfidenceLevel.Low),
                CancellationToken.None);
            await _repository.UpsertIncidentAsync(
                BuildJammingIncident(deviceId, now.AddMinutes(20), confidence: JammingConfidenceLevel.Low),
                CancellationToken.None);
            await _repository.UpsertIncidentAsync(
                BuildJammingIncident(deviceId, now.AddMinutes(40), confidence: JammingConfidenceLevel.Medium),
                CancellationToken.None);
            await _repository.UpsertIncidentAsync(
                BuildJammingIncident(deviceId, now.AddMinutes(60), confidence: JammingConfidenceLevel.High),
                CancellationToken.None);
            await _repository.UpsertIncidentAsync(
                BuildJammingIncident(deviceId, now.AddMinutes(80), confidence: JammingConfidenceLevel.Definite),
                CancellationToken.None);

            var summary = await _repository.RecomputeStatsAsync(deviceId, CancellationToken.None);

            Assert.Equal(2, summary.LowConfidenceCount);
            Assert.Equal(1, summary.MediumConfidenceCount);
            Assert.Equal(1, summary.HighConfidenceCount);
            Assert.Equal(1, summary.DefiniteConfidenceCount);
        }
    }
}
