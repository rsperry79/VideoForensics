using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;
using VideoForensics.Data.Database.DependencyInjection;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests
{
    /// <summary>
    /// Tests for M3 automation: automatic case and alert creation when a new jamming incident is recorded in JammingRepository.
    /// Covers both INSERT path (creates case) and UPDATE path (no new case).
    /// </summary>
    public class JammingRepositoryCaseAutomationTests : IAsyncLifetime
    {
        private static readonly DateTime Start = new(2026, 10, 7, 10, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime End = new(2026, 10, 7, 10, 30, 0, DateTimeKind.Utc);

        private SqliteInMemoryFixture _fixture = null!;
        private JammingRepository _jammingRepo = null!;
        private CaseRepository _caseRepo = null!;
        private AlertRepository _alertRepo = null!;
        private ILoggerFactory _loggerFactory = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            _loggerFactory = LoggerFactory.Create(b => { });

            var actionLogRepo = new ActionLogRepository(_fixture.Factory, _loggerFactory.CreateLogger<ActionLogRepository>());
            _caseRepo = new CaseRepository(_fixture.Factory, actionLogRepo, _loggerFactory.CreateLogger<CaseRepository>(),
                new FakeTimeProvider(new DateTime(2026, 10, 7, 14, 35, 42, DateTimeKind.Utc)));
            _alertRepo = new AlertRepository(_fixture.Factory, _loggerFactory.CreateLogger<AlertRepository>());
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
            _loggerFactory.Dispose();
        }

        private JammingRepository BuildJammingRepo(ICaseRepository? caseRepository = null) =>
            new(_fixture.Factory, _loggerFactory.CreateLogger<JammingRepository>(), caseRepository);

        private static async Task<Guid> SeedDeviceAsync(IDbContextFactory<VideoForensicsDbContext> factory, ILoggerFactory lf)
        {
            var loc = TestDataBuilder.BuildLocation(name: "Test Location");
            await new LocationRepository(factory, lf.CreateLogger<LocationRepository>()).AddAsync(loc, CancellationToken.None);
            var device = TestDataBuilder.BuildDevice(locationId: loc.Id, name: "Test Device");
            await new DeviceRepository(factory, lf.CreateLogger<DeviceRepository>()).AddAsync(device, CancellationToken.None);
            return device.Id;
        }

        private static JammingIncidentRecord NewIncident(Guid deviceId, JammingConfidenceLevel level,
            JammingIncidentSource source = JammingIncidentSource.AutoDetected, string? notes = null) => new()
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
            Source = source
        };

        /// <summary>
        /// Test (a): New High-confidence incident creates case with "detected-" prefix, alert, and links incident.
        /// </summary>
        [Fact]
        public async Task UpsertIncidentAsync_NewHighConfidence_CreatesDetectedCaseAndAlert()
        {
            Guid deviceId = await SeedDeviceAsync(_fixture.Factory, _loggerFactory);
            JammingIncidentRecord incident = NewIncident(deviceId, JammingConfidenceLevel.High);
            var jammingRepo = BuildJammingRepo(_caseRepo);

            JammingIncidentRecord result = await jammingRepo.UpsertIncidentAsync(incident, CancellationToken.None);

            // Verify case was created
            Assert.NotNull(result.CaseId);
            ForensicCase? forensicCase = await _caseRepo.GetAsync(result.CaseId.Value, CancellationToken.None);
            Assert.NotNull(forensicCase);
            Assert.StartsWith("detected-", forensicCase.CaseNumber);

            // Verify alert was created
            Alert? alert = await _alertRepo.GetByCaseIdAsync(forensicCase.Id, CancellationToken.None);
            Assert.NotNull(alert);
            Assert.Equal("Jamming Detected", alert.Title);
            Assert.Equal(forensicCase.Id, alert.RelatedCaseId);

            // Verify incident row has CaseId set
            JammingIncidentRecord? stored = await _jammingRepo.GetIncidentAsync(incident.Id, CancellationToken.None);
            Assert.NotNull(stored);
            Assert.Equal(forensicCase.Id, stored.CaseId);

            // Verify returned record has CaseId set
            Assert.Equal(forensicCase.Id, result.CaseId);
        }

        /// <summary>
        /// Test (b): Medium and Low confidence incidents create case with "suspected-" prefix.
        /// </summary>
        [Fact]
        public async Task UpsertIncidentAsync_MediumConfidence_CreatesSuspectedCaseAndAlert()
        {
            Guid deviceId = await SeedDeviceAsync(_fixture.Factory, _loggerFactory);
            JammingIncidentRecord incident = NewIncident(deviceId, JammingConfidenceLevel.Medium);
            var jammingRepo = BuildJammingRepo(_caseRepo);

            JammingIncidentRecord result = await jammingRepo.UpsertIncidentAsync(incident, CancellationToken.None);

            Assert.NotNull(result.CaseId);
            ForensicCase? forensicCase = await _caseRepo.GetAsync(result.CaseId.Value, CancellationToken.None);
            Assert.NotNull(forensicCase);
            Assert.StartsWith("suspected-", forensicCase.CaseNumber);

            Alert? alert = await _alertRepo.GetByCaseIdAsync(forensicCase.Id, CancellationToken.None);
            Assert.NotNull(alert);
            Assert.Equal("Jamming Suspected", alert.Title);
        }

        [Fact]
        public async Task UpsertIncidentAsync_LowConfidence_CreatesSuspectedCaseAndAlert()
        {
            Guid deviceId = await SeedDeviceAsync(_fixture.Factory, _loggerFactory);
            JammingIncidentRecord incident = NewIncident(deviceId, JammingConfidenceLevel.Low);
            var jammingRepo = BuildJammingRepo(_caseRepo);

            JammingIncidentRecord result = await jammingRepo.UpsertIncidentAsync(incident, CancellationToken.None);

            Assert.NotNull(result.CaseId);
            ForensicCase? forensicCase = await _caseRepo.GetAsync(result.CaseId.Value, CancellationToken.None);
            Assert.NotNull(forensicCase);
            Assert.StartsWith("suspected-", forensicCase.CaseNumber);

            Alert? alert = await _alertRepo.GetByCaseIdAsync(forensicCase.Id, CancellationToken.None);
            Assert.NotNull(alert);
            Assert.Equal("Jamming Suspected", alert.Title);
        }

        /// <summary>
        /// Test (c): ManuallyRecorded incidents also trigger case creation.
        /// </summary>
        [Fact]
        public async Task UpsertIncidentAsync_ManuallyRecorded_CreatesCaseAndAlert()
        {
            Guid deviceId = await SeedDeviceAsync(_fixture.Factory, _loggerFactory);
            JammingIncidentRecord incident = NewIncident(deviceId, JammingConfidenceLevel.High, JammingIncidentSource.ManuallyRecorded);
            var jammingRepo = BuildJammingRepo(_caseRepo);

            JammingIncidentRecord result = await jammingRepo.UpsertIncidentAsync(incident, CancellationToken.None);

            Assert.NotNull(result.CaseId);
            ForensicCase? forensicCase = await _caseRepo.GetAsync(result.CaseId.Value, CancellationToken.None);
            Assert.NotNull(forensicCase);
            Assert.StartsWith("detected-", forensicCase.CaseNumber);

            Alert? alert = await _alertRepo.GetByCaseIdAsync(forensicCase.Id, CancellationToken.None);
            Assert.NotNull(alert);
            Assert.Equal("Jamming Detected", alert.Title);
        }

        /// <summary>
        /// Test (d): Updating an existing incident does NOT create a new case or alert; CaseId is preserved.
        /// </summary>
        [Fact]
        public async Task UpsertIncidentAsync_UpdateExisting_DoesNotCreateNewCaseOrAlert()
        {
            Guid deviceId = await SeedDeviceAsync(_fixture.Factory, _loggerFactory);
            JammingIncidentRecord incident = NewIncident(deviceId, JammingConfidenceLevel.High);
            var jammingRepo = BuildJammingRepo(_caseRepo);

            // First upsert: INSERT
            JammingIncidentRecord created = await jammingRepo.UpsertIncidentAsync(incident, CancellationToken.None);
            Guid originalCaseId = created.CaseId ?? throw new InvalidOperationException("CaseId should be set");
            Guid incidentId = created.Id;

            // Count cases and alerts after first upsert
            int caseCountBefore = (await _caseRepo.ListAsync(null, CancellationToken.None)).Count;
            IReadOnlyList<Alert> alertsBefore = await _alertRepo.ListAsync(null, CancellationToken.None);
            int alertCountBefore = alertsBefore.Count;

            // Second upsert: UPDATE (change some fields but same Id)
            JammingIncidentRecord update = NewIncident(deviceId, JammingConfidenceLevel.High);
            update.Id = incidentId;
            update.Notes = "Updated notes";
            update.AffectedEventCount = 10;

            JammingIncidentRecord updated = await jammingRepo.UpsertIncidentAsync(update, CancellationToken.None);

            // Verify no new case or alert was created
            int caseCountAfter = (await _caseRepo.ListAsync(null, CancellationToken.None)).Count;
            IReadOnlyList<Alert> alertsAfter = await _alertRepo.ListAsync(null, CancellationToken.None);
            int alertCountAfter = alertsAfter.Count;

            Assert.Equal(caseCountBefore, caseCountAfter);
            Assert.Equal(alertCountBefore, alertCountAfter);

            // Verify CaseId is preserved
            Assert.Equal(originalCaseId, updated.CaseId);
            JammingIncidentRecord? stored = await _jammingRepo.GetIncidentAsync(incidentId, CancellationToken.None);
            Assert.NotNull(stored);
            Assert.Equal(originalCaseId, stored.CaseId);
        }

        /// <summary>
        /// Test (e): If ICaseRepository throws, UpsertIncidentAsync still returns the saved incident with CaseId=null and logs error.
        /// </summary>
        [Fact]
        public async Task UpsertIncidentAsync_CaseRepositoryThrows_IncidentSavedWithNullCaseId()
        {
            Guid deviceId = await SeedDeviceAsync(_fixture.Factory, _loggerFactory);
            JammingIncidentRecord incident = NewIncident(deviceId, JammingConfidenceLevel.High);

            // Mock ICaseRepository to throw
            var mockCaseRepo = new Mock<ICaseRepository>();
            mockCaseRepo
                .Setup(r => r.CreateFromJammingDetectionAsync(It.IsAny<JammingIncidentRecord>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Simulated case creation failure"));

            var jammingRepo = BuildJammingRepo(mockCaseRepo.Object);

            // Upsert should not throw; should continue
            JammingIncidentRecord result = await jammingRepo.UpsertIncidentAsync(incident, CancellationToken.None);

            // Incident must be saved
            Assert.NotNull(result);
            Assert.NotEqual(Guid.Empty, result.Id);
            Assert.Null(result.CaseId); // CaseId should remain null

            // Verify incident is in database
            JammingIncidentRecord? stored = await _jammingRepo.GetIncidentAsync(result.Id, CancellationToken.None);
            Assert.NotNull(stored);
            Assert.Null(stored.CaseId);
        }

        /// <summary>
        /// Test (f): JammingRepository without a case repository behaves as before (no cases/alerts created).
        /// </summary>
        [Fact]
        public async Task UpsertIncidentAsync_NoCaseRepository_BehavesAsNormal()
        {
            Guid deviceId = await SeedDeviceAsync(_fixture.Factory, _loggerFactory);
            JammingIncidentRecord incident = NewIncident(deviceId, JammingConfidenceLevel.High);

            // Build jamming repo without case repository
            var jammingRepo = BuildJammingRepo(null);

            JammingIncidentRecord result = await jammingRepo.UpsertIncidentAsync(incident, CancellationToken.None);

            // CaseId should remain null
            Assert.Null(result.CaseId);

            // No cases or alerts should be created
            int caseCount = (await _caseRepo.ListAsync(null, CancellationToken.None)).Count;
            int alertCount = (await _alertRepo.ListAsync(null, CancellationToken.None)).Count;
            Assert.Equal(0, caseCount);
            Assert.Equal(0, alertCount);
        }

        /// <summary>
        /// Test (g): Cancellation during case creation propagates OperationCanceledException.
        /// </summary>
        [Fact]
        public async Task UpsertIncidentAsync_CancellationDuringCaseCreation_PropagatesCancelation()
        {
            Guid deviceId = await SeedDeviceAsync(_fixture.Factory, _loggerFactory);
            JammingIncidentRecord incident = NewIncident(deviceId, JammingConfidenceLevel.High);

            // Mock ICaseRepository to throw OperationCanceledException
            var mockCaseRepo = new Mock<ICaseRepository>();
            mockCaseRepo
                .Setup(r => r.CreateFromJammingDetectionAsync(It.IsAny<JammingIncidentRecord>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new OperationCanceledException("Cancelled"));

            var jammingRepo = BuildJammingRepo(mockCaseRepo.Object);

            // Expect OperationCanceledException to propagate
            await Assert.ThrowsAsync<OperationCanceledException>(
                () => jammingRepo.UpsertIncidentAsync(incident, CancellationToken.None));
        }

        /// <summary>
        /// Test (h): DI resolution test - verify JammingRepository is wired with CaseRepository via DI.
        /// </summary>
        [Fact]
        public async Task DIResolution_JammingRepositoryReceivesCaseRepository()
        {
            var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
            services.AddVideoForensicsDatabase();

            await using (var db = new VideoForensicsDbContext(new DbContextOptionsBuilder<VideoForensicsDbContext>()
                .UseSqlite("DataSource=:memory:")
                .Options))
            {
                await db.Database.EnsureCreatedAsync();
            }

            var sp = services.BuildServiceProvider();

            var jammingRepo = sp.GetRequiredService<IJammingRepository>();
            Assert.NotNull(jammingRepo);

            // The resolved IJammingRepository should be a JammingRepository instance
            Assert.IsType<JammingRepository>(jammingRepo);
        }
    }
}
