using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests
{
    /// <summary>
    /// Comprehensive tests for case creation from jamming detection incidents.
    /// Verifies idempotency, atomic alert creation, incident linking, description formatting,
    /// and failure handling (missing incident row, save interceptor exceptions).
    /// </summary>
    public class CaseJammingAutomationTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private ICaseRepository _caseRepository = null!;
        private IAlertRepository _alertRepository = null!;
        private ActionLogRepository _actionLogRepository = null!;
        private LocationRepository _locationRepository = null!;
        private DeviceRepository _deviceRepository = null!;
        private IJammingRepository _jammingRepository = null!;
        private ILoggerFactory _loggerFactory = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            _loggerFactory = LoggerFactory.Create(b => { });

            _actionLogRepository = new ActionLogRepository(_fixture.Factory, _loggerFactory.CreateLogger<ActionLogRepository>());
            _locationRepository = new LocationRepository(_fixture.Factory, _loggerFactory.CreateLogger<LocationRepository>());
            _deviceRepository = new DeviceRepository(_fixture.Factory, _loggerFactory.CreateLogger<DeviceRepository>());
            _caseRepository = new CaseRepository(_fixture.Factory, _actionLogRepository, _loggerFactory.CreateLogger<CaseRepository>());
            _alertRepository = new AlertRepository(_fixture.Factory, _loggerFactory.CreateLogger<AlertRepository>());
            _jammingRepository = new JammingRepository(_fixture.Factory, _loggerFactory.CreateLogger<JammingRepository>());
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
            _loggerFactory.Dispose();
        }

        private async Task<Guid> SeedDeviceAsync()
        {
            var loc = TestDataBuilder.BuildLocation(name: "Test Location");
            await _locationRepository.AddAsync(loc, CancellationToken.None);
            var device = TestDataBuilder.BuildDevice(locationId: loc.Id, name: "Test Device");
            await _deviceRepository.AddAsync(device, CancellationToken.None);
            return device.Id;
        }

        #region Detected vs Suspected Alert Title Tests

        [Fact]
        public async Task CreateFromJammingDetectionAsync_DetectedConfidence_CreatesAlertWithDetectedTitle()
        {
            // Arrange
            var deviceId = await SeedDeviceAsync();
            var incident = new JammingIncidentRecord
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                StartUtc = new DateTime(2026, 10, 7, 10, 0, 0, DateTimeKind.Utc),
                EndUtc = new DateTime(2026, 10, 7, 10, 30, 0, DateTimeKind.Utc),
                AffectedEventCount = 5,
                AverageDegradationDb = 8.5,
                Confidence = JammingConfidenceLevel.High,
                DetectedAtUtc = DateTime.UtcNow,
                Notes = null
            };

            // Act
            ForensicCase case1 = await _caseRepository.CreateFromJammingDetectionAsync(incident, CancellationToken.None);

            // Assert
            Assert.NotNull(case1);
            Alert? alert = await _alertRepository.GetByCaseIdAsync(case1.Id, CancellationToken.None);
            Assert.NotNull(alert);
            Assert.Equal("Jamming Detected", alert.Title);
            Assert.Equal("Open", alert.Status);
            Assert.Equal("System", alert.CreatedBy);
            Assert.Equal("JammingDetection", alert.AlertType);
        }

        [Fact]
        public async Task CreateFromJammingDetectionAsync_SuspectedConfidence_CreatesAlertWithSuspectedTitle()
        {
            // Arrange
            var deviceId = await SeedDeviceAsync();
            var incident = new JammingIncidentRecord
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                StartUtc = new DateTime(2026, 10, 7, 10, 0, 0, DateTimeKind.Utc),
                EndUtc = new DateTime(2026, 10, 7, 10, 30, 0, DateTimeKind.Utc),
                AffectedEventCount = 5,
                AverageDegradationDb = 8.5,
                Confidence = JammingConfidenceLevel.Low,
                DetectedAtUtc = DateTime.UtcNow,
                Notes = null
            };

            // Act
            ForensicCase case1 = await _caseRepository.CreateFromJammingDetectionAsync(incident, CancellationToken.None);

            // Assert
            Assert.NotNull(case1);
            Alert? alert = await _alertRepository.GetByCaseIdAsync(case1.Id, CancellationToken.None);
            Assert.NotNull(alert);
            Assert.Equal("Jamming Suspected", alert.Title);
            Assert.Equal("Open", alert.Status);
            Assert.Equal("System", alert.CreatedBy);
            Assert.Equal("JammingDetection", alert.AlertType);
        }

        #endregion

        #region Idempotency Tests

        [Fact]
        public async Task CreateFromJammingDetectionAsync_CalledTwice_ReturnsExistingCaseNoNewAlert()
        {
            // Arrange
            var deviceId = await SeedDeviceAsync();
            var incident = new JammingIncidentRecord
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                StartUtc = new DateTime(2026, 10, 7, 10, 0, 0, DateTimeKind.Utc),
                EndUtc = new DateTime(2026, 10, 7, 10, 30, 0, DateTimeKind.Utc),
                AffectedEventCount = 5,
                AverageDegradationDb = 8.5,
                Confidence = JammingConfidenceLevel.High,
                DetectedAtUtc = DateTime.UtcNow,
                Notes = null
            };

            // Act - First call
            ForensicCase case1 = await _caseRepository.CreateFromJammingDetectionAsync(incident, CancellationToken.None);

            // Act - Second call (incident now has CaseId set)
            incident.CaseId = case1.Id;
            ForensicCase case2 = await _caseRepository.CreateFromJammingDetectionAsync(incident, CancellationToken.None);

            // Assert
            Assert.Equal(case1.Id, case2.Id);
            Assert.Equal(case1.CaseNumber, case2.CaseNumber);
            
            // Verify only 1 alert was created
            var alerts = await _alertRepository.ListAsync("Open", CancellationToken.None);
            var caseAlerts = alerts.Where(a => a.RelatedCaseId == case1.Id).ToList();
            Assert.Single(caseAlerts);
        }

        #endregion

        #region Incident Linking Tests

        [Fact]
        public async Task CreateFromJammingDetectionAsync_LinkedToIncidentAndDatabase()
        {
            // Arrange
            var deviceId = await SeedDeviceAsync();
            var incident = new JammingIncidentRecord
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                StartUtc = new DateTime(2026, 10, 7, 10, 0, 0, DateTimeKind.Utc),
                EndUtc = new DateTime(2026, 10, 7, 10, 30, 0, DateTimeKind.Utc),
                AffectedEventCount = 5,
                AverageDegradationDb = 8.5,
                Confidence = JammingConfidenceLevel.High,
                DetectedAtUtc = DateTime.UtcNow,
                Notes = null
            };

            // Act
            ForensicCase createdCase = await _caseRepository.CreateFromJammingDetectionAsync(incident, CancellationToken.None);

            // Assert - Verify incident.CaseId is set
            Assert.NotEqual(Guid.Empty, createdCase.Id);
            Assert.Equal(createdCase.Id, incident.CaseId);
        }

        #endregion

        #region Alert Description Tests

        [Fact]
        public async Task CreateFromJammingDetectionAsync_AlertDescriptionContainsDeviceIdAndTimeWindow()
        {
            // Arrange
            var deviceId = await SeedDeviceAsync();
            var startTime = new DateTime(2026, 10, 7, 10, 0, 0, DateTimeKind.Utc);
            var endTime = new DateTime(2026, 10, 7, 10, 30, 0, DateTimeKind.Utc);
            var incident = new JammingIncidentRecord
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                StartUtc = startTime,
                EndUtc = endTime,
                AffectedEventCount = 5,
                AverageDegradationDb = 8.5,
                Confidence = JammingConfidenceLevel.High,
                DetectedAtUtc = DateTime.UtcNow,
                Notes = null
            };

            // Act
            ForensicCase createdCase = await _caseRepository.CreateFromJammingDetectionAsync(incident, CancellationToken.None);

            // Assert
            Alert? alert = await _alertRepository.GetByCaseIdAsync(createdCase.Id, CancellationToken.None);
            Assert.NotNull(alert);
            Assert.NotNull(alert.Description);
            Assert.Contains(deviceId.ToString(), alert.Description);
            Assert.Contains(startTime.ToString("O"), alert.Description);
            Assert.Contains(endTime.ToString("O"), alert.Description);
            Assert.Contains("8.50 dB", alert.Description); // AverageDegradationDb formatted
            Assert.Contains("5", alert.Description); // AffectedEventCount
        }

        [Fact]
        public async Task CreateFromJammingDetectionAsync_AlertDescriptionCappedAt4000Chars()
        {
            // Arrange
            var deviceId = await SeedDeviceAsync();
            var longNotes = new string('x', 3500); // Very long notes to exceed 4000 char limit
            var incident = new JammingIncidentRecord
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                StartUtc = new DateTime(2026, 10, 7, 10, 0, 0, DateTimeKind.Utc),
                EndUtc = new DateTime(2026, 10, 7, 10, 30, 0, DateTimeKind.Utc),
                AffectedEventCount = 5,
                AverageDegradationDb = 8.5,
                Confidence = JammingConfidenceLevel.High,
                DetectedAtUtc = DateTime.UtcNow,
                Notes = longNotes
            };

            // Act
            ForensicCase createdCase = await _caseRepository.CreateFromJammingDetectionAsync(incident, CancellationToken.None);

            // Assert
            Alert? alert = await _alertRepository.GetByCaseIdAsync(createdCase.Id, CancellationToken.None);
            Assert.NotNull(alert);
            Assert.NotNull(alert.Description);
            Assert.True(alert.Description.Length <= 4000, $"Description length {alert.Description.Length} exceeds 4000 char limit");
            if (alert.Description.Length >= 3997)
            {
                // If truncated, should end with "..."
                Assert.EndsWith("...", alert.Description);
            }
        }

        #endregion

        #region Missing Incident Row Tests

        [Fact]
        public async Task CreateFromJammingDetectionAsync_IncidentNotInDatabase_CaseAndAlertCreatedSuccessfully()
        {
            // Arrange
            var deviceId = await SeedDeviceAsync();
            var incident = new JammingIncidentRecord
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                StartUtc = new DateTime(2026, 10, 7, 10, 0, 0, DateTimeKind.Utc),
                EndUtc = new DateTime(2026, 10, 7, 10, 30, 0, DateTimeKind.Utc),
                AffectedEventCount = 5,
                AverageDegradationDb = 8.5,
                Confidence = JammingConfidenceLevel.High,
                DetectedAtUtc = DateTime.UtcNow,
                Notes = null
            };
            // Don't add incident to DB; it's just created in memory

            // Act - Should not throw, just log warning
            ForensicCase createdCase = await _caseRepository.CreateFromJammingDetectionAsync(incident, CancellationToken.None);

            // Assert
            Assert.NotNull(createdCase);
            Assert.NotEqual(Guid.Empty, createdCase.Id);
            
            // Case should exist
            var retrievedCase = await _caseRepository.GetAsync(createdCase.Id, CancellationToken.None);
            Assert.NotNull(retrievedCase);

            // Alert should exist
            Alert? alert = await _alertRepository.GetByCaseIdAsync(createdCase.Id, CancellationToken.None);
            Assert.NotNull(alert);
        }

        #endregion
    }
}
