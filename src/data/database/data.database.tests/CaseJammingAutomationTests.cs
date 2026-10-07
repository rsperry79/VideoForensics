using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests
{
    /// <summary>
    /// Tests for the case + alert + incident-link automation in CaseRepository.CreateFromJammingDetectionAsync.
    /// </summary>
    public class CaseJammingAutomationTests : IAsyncLifetime
    {
        private static readonly DateTime Start = new(2026, 10, 7, 10, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime End = new(2026, 10, 7, 10, 30, 0, DateTimeKind.Utc);

        private SqliteInMemoryFixture _fixture = null!;
        private CaseRepository _cases = null!;
        private AlertRepository _alerts = null!;
        private ILoggerFactory _loggerFactory = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            _loggerFactory = LoggerFactory.Create(b => { });
            _cases = BuildRepo(_fixture.Factory);
            _alerts = new AlertRepository(_fixture.Factory, _loggerFactory.CreateLogger<AlertRepository>());
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
            _loggerFactory.Dispose();
        }

        private CaseRepository BuildRepo(IDbContextFactory<VideoForensicsDbContext> factory) =>
            new(factory,
                new ActionLogRepository(factory, _loggerFactory.CreateLogger<ActionLogRepository>()),
                _loggerFactory.CreateLogger<CaseRepository>(),
                new FakeTimeProvider(new DateTime(2026, 10, 7, 14, 35, 42, DateTimeKind.Utc)));

        private static async Task<Guid> SeedDeviceAsync(IDbContextFactory<VideoForensicsDbContext> factory, ILoggerFactory lf)
        {
            var loc = TestDataBuilder.BuildLocation(name: "Jam Location");
            await new LocationRepository(factory, lf.CreateLogger<LocationRepository>()).AddAsync(loc, CancellationToken.None);
            var device = TestDataBuilder.BuildDevice(locationId: loc.Id, name: "Jam Device");
            await new DeviceRepository(factory, lf.CreateLogger<DeviceRepository>()).AddAsync(device, CancellationToken.None);
            return device.Id;
        }

        private static JammingIncidentRecord NewIncident(Guid deviceId, JammingConfidenceLevel level, string? notes = null) => new()
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            StartUtc = Start,
            EndUtc = End,
            AffectedEventCount = 7,
            AverageDegradationDb = 8.5,
            Confidence = level,
            DetectedAtUtc = End,
            Notes = notes,
            Source = JammingIncidentSource.AutoDetected
        };

        private static async Task PersistIncidentAsync(IDbContextFactory<VideoForensicsDbContext> factory, JammingIncidentRecord incident)
        {
            await using VideoForensicsDbContext db = factory.CreateDbContext();
            _ = db.JammingIncidentRecords.Add(incident);
            _ = await db.SaveChangesAsync();
        }

        [Fact]
        public async Task CreateFromJamming_Detected_CreatesOpenSystemAlertWithDetectedTitle()
        {
            Guid deviceId = await SeedDeviceAsync(_fixture.Factory, _loggerFactory);
            JammingIncidentRecord incident = NewIncident(deviceId, JammingConfidenceLevel.High);
            await PersistIncidentAsync(_fixture.Factory, incident);

            ForensicCase created = await _cases.CreateFromJammingDetectionAsync(incident, CancellationToken.None);

            Alert? alert = await _alerts.GetByCaseIdAsync(created.Id, CancellationToken.None);
            Assert.NotNull(alert);
            Assert.Equal("Jamming Detected", alert.Title);
            Assert.Equal("Open", alert.Status);
            Assert.Equal("System", alert.CreatedBy);
            Assert.Equal("JammingDetection", alert.AlertType);
        }

        [Fact]
        public async Task CreateFromJamming_Suspected_CreatesAlertWithSuspectedTitle()
        {
            Guid deviceId = await SeedDeviceAsync(_fixture.Factory, _loggerFactory);
            JammingIncidentRecord incident = NewIncident(deviceId, JammingConfidenceLevel.Low);
            await PersistIncidentAsync(_fixture.Factory, incident);

            ForensicCase created = await _cases.CreateFromJammingDetectionAsync(incident, CancellationToken.None);

            Alert? alert = await _alerts.GetByCaseIdAsync(created.Id, CancellationToken.None);
            Assert.NotNull(alert);
            Assert.Equal("Jamming Suspected", alert.Title);
            Assert.Equal("JammingDetection", alert.AlertType);
        }

        [Fact]
        public async Task CreateFromJamming_PersistsIncidentCaseId()
        {
            Guid deviceId = await SeedDeviceAsync(_fixture.Factory, _loggerFactory);
            JammingIncidentRecord incident = NewIncident(deviceId, JammingConfidenceLevel.High);
            await PersistIncidentAsync(_fixture.Factory, incident);

            ForensicCase created = await _cases.CreateFromJammingDetectionAsync(incident, CancellationToken.None);

            await using VideoForensicsDbContext db = _fixture.Factory.CreateDbContext();
            JammingIncidentRecord stored = await db.JammingIncidentRecords.AsNoTracking().SingleAsync(i => i.Id == incident.Id);
            Assert.Equal(created.Id, stored.CaseId);
        }

        [Fact]
        public async Task CreateFromJamming_CalledTwice_ReturnsSameCaseAndSingleAlert()
        {
            Guid deviceId = await SeedDeviceAsync(_fixture.Factory, _loggerFactory);
            JammingIncidentRecord incident = NewIncident(deviceId, JammingConfidenceLevel.High);
            await PersistIncidentAsync(_fixture.Factory, incident);

            ForensicCase first = await _cases.CreateFromJammingDetectionAsync(incident, CancellationToken.None);

            // Second call with a stale copy that does not know about the link: idempotency must come from the DB row.
            JammingIncidentRecord stale = NewIncident(deviceId, JammingConfidenceLevel.High);
            stale.Id = incident.Id;
            ForensicCase second = await _cases.CreateFromJammingDetectionAsync(stale, CancellationToken.None);

            Assert.Equal(first.Id, second.Id);
            Assert.Single(await _cases.ListAsync(null, CancellationToken.None));
            Assert.Single(await _alerts.ListAsync(null, CancellationToken.None));
        }

        [Fact]
        public async Task CreateFromJamming_AlertDescription_ContainsCaseNumberDeviceAndWindow()
        {
            Guid deviceId = await SeedDeviceAsync(_fixture.Factory, _loggerFactory);
            JammingIncidentRecord incident = NewIncident(deviceId, JammingConfidenceLevel.High);
            await PersistIncidentAsync(_fixture.Factory, incident);

            ForensicCase created = await _cases.CreateFromJammingDetectionAsync(incident, CancellationToken.None);

            Alert alert = (await _alerts.GetByCaseIdAsync(created.Id, CancellationToken.None))!;
            Assert.Contains(created.CaseNumber, alert.Description);
            Assert.Contains(deviceId.ToString(), alert.Description);
            Assert.Contains($"{Start:O}", alert.Description);
            Assert.Contains($"{End:O}", alert.Description);
            Assert.Contains("8.50 dB", alert.Description);
            Assert.Contains("Affected events: 7", alert.Description);
            Assert.Contains("High", alert.Description);
        }

        [Fact]
        public async Task CreateFromJamming_LongNotes_AlertDescriptionCappedAt4000()
        {
            Guid deviceId = await SeedDeviceAsync(_fixture.Factory, _loggerFactory);
            JammingIncidentRecord incident = NewIncident(deviceId, JammingConfidenceLevel.High, new string('x', 6000));
            await PersistIncidentAsync(_fixture.Factory, incident);

            ForensicCase created = await _cases.CreateFromJammingDetectionAsync(incident, CancellationToken.None);

            Alert alert = (await _alerts.GetByCaseIdAsync(created.Id, CancellationToken.None))!;
            Assert.Equal(4000, alert.Description!.Length);
        }

        [Fact]
        public async Task CreateFromJamming_IncidentRowMissing_StillCreatesCaseAndAlert()
        {
            Guid deviceId = await SeedDeviceAsync(_fixture.Factory, _loggerFactory);
            JammingIncidentRecord incident = NewIncident(deviceId, JammingConfidenceLevel.High); // never persisted

            ForensicCase created = await _cases.CreateFromJammingDetectionAsync(incident, CancellationToken.None);

            Assert.NotNull(await _cases.GetAsync(created.Id, CancellationToken.None));
            Assert.NotNull(await _alerts.GetByCaseIdAsync(created.Id, CancellationToken.None));
        }

        [Fact]
        public async Task CreateFromJamming_SaveFails_NothingPersisted()
        {
            await using var conn = new SqliteConnection("DataSource=:memory:");
            await conn.OpenAsync();
            var plain = new DbContextOptionsBuilder<VideoForensicsDbContext>().UseSqlite(conn).Options;
            var plainFactory = new SimpleFactory(plain);
            await using (VideoForensicsDbContext init = plainFactory.CreateDbContext())
            {
                _ = await init.Database.EnsureCreatedAsync();
            }

            Guid deviceId = await SeedDeviceAsync(plainFactory, _loggerFactory);
            JammingIncidentRecord incident = NewIncident(deviceId, JammingConfidenceLevel.High);
            await PersistIncidentAsync(plainFactory, incident);

            var throwing = new DbContextOptionsBuilder<VideoForensicsDbContext>()
                .UseSqlite(conn).AddInterceptors(new ThrowOnSaveInterceptor()).Options;
            CaseRepository failing = BuildRepo(new SimpleFactory(throwing));

            _ = await Assert.ThrowsAsync<InvalidOperationException>(
                () => failing.CreateFromJammingDetectionAsync(incident, CancellationToken.None));

            await using VideoForensicsDbContext db = plainFactory.CreateDbContext();
            Assert.Equal(0, await db.Cases.CountAsync());
            Assert.Equal(0, await db.Alerts.CountAsync());
            Assert.Equal(0, await db.CaseDevices.CountAsync());
            Assert.Null((await db.JammingIncidentRecords.AsNoTracking().SingleAsync(i => i.Id == incident.Id)).CaseId);
        }

        private sealed class SimpleFactory(DbContextOptions<VideoForensicsDbContext> options) : IDbContextFactory<VideoForensicsDbContext>
        {
            public VideoForensicsDbContext CreateDbContext() => new(options);
        }

        private sealed class ThrowOnSaveInterceptor : SaveChangesInterceptor
        {
            public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
                DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
                => throw new InvalidOperationException("simulated save failure");
        }
    }
}
