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
    /// Tests for case number generation and auto-numbering behavior.
    /// Verifies that case numbers are generated correctly with retry logic for collisions,
    /// and that CreateFromJammingDetectionAsync produces the correct prefix and title.
    /// </summary>
    public class CaseRepositoryNumberingTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private ICaseRepository _repository = null!;
        private ActionLogRepository _actionLogRepository = null!;
        private LocationRepository _locationRepository = null!;
        private DeviceRepository _deviceRepository = null!;
        private ILoggerFactory _loggerFactory = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            _loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });

            _actionLogRepository = new ActionLogRepository(_fixture.Factory, _loggerFactory.CreateLogger<ActionLogRepository>());
            _locationRepository = new LocationRepository(_fixture.Factory, _loggerFactory.CreateLogger<LocationRepository>());
            _deviceRepository = new DeviceRepository(_fixture.Factory, _loggerFactory.CreateLogger<DeviceRepository>());
            _repository = new CaseRepository(_fixture.Factory, _actionLogRepository, _loggerFactory.CreateLogger<CaseRepository>());
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
            _loggerFactory.Dispose();
        }

        #region GenerateCaseNumber Tests

        [Fact]
        public async Task GenerateCaseNumber_Manual_ProducesCorrectFormat()
        {
            // Arrange
            var fakeTime = new FakeTimeProvider(new DateTime(2026, 10, 7, 14, 35, 42, DateTimeKind.Utc));
            var repo = new CaseRepository(_fixture.Factory, _actionLogRepository, _loggerFactory.CreateLogger<CaseRepository>(), fakeTime);

            // Act
            string caseNumber = await repo.GenerateCaseNumber("manual", CancellationToken.None);

            // Assert
            Assert.Equal("manual-2026-10-07-14-35-0", caseNumber);
        }

        [Fact]
        public async Task GenerateCaseNumber_Detected_ProducesCorrectFormat()
        {
            // Arrange
            var fakeTime = new FakeTimeProvider(new DateTime(2026, 10, 7, 14, 35, 42, DateTimeKind.Utc));
            var repo = new CaseRepository(_fixture.Factory, _actionLogRepository, _loggerFactory.CreateLogger<CaseRepository>(), fakeTime);

            // Act
            string caseNumber = await repo.GenerateCaseNumber("detected", CancellationToken.None);

            // Assert
            Assert.Equal("detected-2026-10-07-14-35-0", caseNumber);
        }

        [Fact]
        public async Task GenerateCaseNumber_Suspected_ProducesCorrectFormat()
        {
            // Arrange
            var fakeTime = new FakeTimeProvider(new DateTime(2026, 10, 7, 14, 35, 42, DateTimeKind.Utc));
            var repo = new CaseRepository(_fixture.Factory, _actionLogRepository, _loggerFactory.CreateLogger<CaseRepository>(), fakeTime);

            // Act
            string caseNumber = await repo.GenerateCaseNumber("suspected", CancellationToken.None);

            // Assert
            Assert.Equal("suspected-2026-10-07-14-35-0", caseNumber);
        }

        [Fact]
        public async Task GenerateCaseNumber_WithExistingCase_IncrementsIndex()
        {
            // Arrange
            var fakeTime = new FakeTimeProvider(new DateTime(2026, 10, 7, 14, 35, 42, DateTimeKind.Utc));
            var repo = new CaseRepository(_fixture.Factory, _actionLogRepository, _loggerFactory.CreateLogger<CaseRepository>(), fakeTime);

            // Create a case with the first generated number
            string firstNumber = await repo.GenerateCaseNumber("manual", CancellationToken.None);
            await repo.CreateAsync(firstNumber, "Title 1", null, null, null, null, [], "user@example.com", CancellationToken.None);

            // Act - Generate another number in the same minute
            string secondNumber = await repo.GenerateCaseNumber("manual", CancellationToken.None);

            // Assert
            Assert.Equal("manual-2026-10-07-14-35-0", firstNumber);
            Assert.Equal("manual-2026-10-07-14-35-1", secondNumber);
        }

        [Fact]
        public async Task GenerateCaseNumber_DifferentPrefixSameMinute_StartsAtZero()
        {
            // Arrange
            var fakeTime = new FakeTimeProvider(new DateTime(2026, 10, 7, 14, 35, 42, DateTimeKind.Utc));
            var repo = new CaseRepository(_fixture.Factory, _actionLogRepository, _loggerFactory.CreateLogger<CaseRepository>(), fakeTime);

            // Create a manual case
            string manualNumber = await repo.GenerateCaseNumber("manual", CancellationToken.None);
            await repo.CreateAsync(manualNumber, "Title 1", null, null, null, null, [], "user@example.com", CancellationToken.None);

            // Act - Generate a detected number in the same minute (should NOT count the manual case)
            string detectedNumber = await repo.GenerateCaseNumber("detected", CancellationToken.None);

            // Assert - Different prefixes should have independent counts
            Assert.Equal("manual-2026-10-07-14-35-0", manualNumber);
            Assert.Equal("detected-2026-10-07-14-35-0", detectedNumber);
        }

        [Fact]
        public async Task GenerateCaseNumber_WithGaps_UsesMaxPlusOne()
        {
            // Arrange
            var fakeTime = new FakeTimeProvider(new DateTime(2026, 10, 7, 14, 35, 42, DateTimeKind.Utc));
            var repo = new CaseRepository(_fixture.Factory, _actionLogRepository, _loggerFactory.CreateLogger<CaseRepository>(), fakeTime);

            // Create cases with indices 0 and 2 (skip 1)
            await repo.CreateAsync("manual-2026-10-07-14-35-0", "Title 0", null, null, null, null, [], "user@example.com", CancellationToken.None);
            await repo.CreateAsync("manual-2026-10-07-14-35-2", "Title 2", null, null, null, null, [], "user@example.com", CancellationToken.None);

            // Act - Generate a new number
            string newNumber = await repo.GenerateCaseNumber("manual", CancellationToken.None);

            // Assert - Should use max+1, not the first gap
            Assert.Equal("manual-2026-10-07-14-35-3", newNumber);
        }

        [Fact]
        public async Task GenerateCaseNumber_NewMinute_StartsAtZero()
        {
            // Arrange
            var fakeTime1 = new FakeTimeProvider(new DateTime(2026, 10, 7, 14, 35, 42, DateTimeKind.Utc));
            var repo1 = new CaseRepository(_fixture.Factory, _actionLogRepository, _loggerFactory.CreateLogger<CaseRepository>(), fakeTime1);

            // Create a case at 14:35
            string num1 = await repo1.GenerateCaseNumber("manual", CancellationToken.None);
            await repo1.CreateAsync(num1, "Title", null, null, null, null, [], "user@example.com", CancellationToken.None);

            // Act - Move to next minute and generate
            var fakeTime2 = new FakeTimeProvider(new DateTime(2026, 10, 7, 14, 36, 10, DateTimeKind.Utc));
            var repo2 = new CaseRepository(_fixture.Factory, _actionLogRepository, _loggerFactory.CreateLogger<CaseRepository>(), fakeTime2);
            string num2 = await repo2.GenerateCaseNumber("manual", CancellationToken.None);

            // Assert
            Assert.Equal("manual-2026-10-07-14-35-0", num1);
            Assert.Equal("manual-2026-10-07-14-36-0", num2);
        }

        #endregion

        #region CreateAsync with null (auto-generation) Tests

        [Fact]
        public async Task CreateAsync_WithNullCaseNumber_GeneratesManualNumber()
        {
            // Arrange
            var fakeTime = new FakeTimeProvider(new DateTime(2026, 10, 7, 14, 35, 42, DateTimeKind.Utc));
            var repo = new CaseRepository(_fixture.Factory, _actionLogRepository, _loggerFactory.CreateLogger<CaseRepository>(), fakeTime);
            string title = "Test Case";

            // Act
            ForensicCase created = await repo.CreateAsync(null, title, null, null, null, null, [], "user@example.com", CancellationToken.None);

            // Assert
            Assert.NotNull(created.CaseNumber);
            Assert.StartsWith("manual-2026-10-07-14-35-", created.CaseNumber);
            Assert.Equal(title, created.Title);
        }

        [Fact]
        public async Task CreateAsync_WithNullCaseNumber_StoresGeneratedNumber()
        {
            // Arrange
            var fakeTime = new FakeTimeProvider(new DateTime(2026, 10, 7, 14, 35, 42, DateTimeKind.Utc));
            var repo = new CaseRepository(_fixture.Factory, _actionLogRepository, _loggerFactory.CreateLogger<CaseRepository>(), fakeTime);

            // Act
            ForensicCase created = await repo.CreateAsync(null, "Title", null, null, null, null, [], "user@example.com", CancellationToken.None);

            // Assert - Verify the number is actually in the database
            ForensicCase? retrieved = await repo.GetByNumberAsync(created.CaseNumber, CancellationToken.None);
            Assert.NotNull(retrieved);
            Assert.Equal(created.Id, retrieved.Id);
        }

        [Fact]
        public async Task CreateAsync_WithEmptyCaseNumber_GeneratesManualNumber()
        {
            // Arrange
            var fakeTime = new FakeTimeProvider(new DateTime(2026, 10, 7, 14, 35, 42, DateTimeKind.Utc));
            var repo = new CaseRepository(_fixture.Factory, _actionLogRepository, _loggerFactory.CreateLogger<CaseRepository>(), fakeTime);

            // Act
            ForensicCase created = await repo.CreateAsync("", "Title", null, null, null, null, [], "user@example.com", CancellationToken.None);

            // Assert
            Assert.NotNull(created.CaseNumber);
            Assert.StartsWith("manual-2026-10-07-14-35-", created.CaseNumber);
        }

        [Fact]
        public async Task CreateAsync_WithWhitespaceCaseNumber_GeneratesManualNumber()
        {
            // Arrange
            var fakeTime = new FakeTimeProvider(new DateTime(2026, 10, 7, 14, 35, 42, DateTimeKind.Utc));
            var repo = new CaseRepository(_fixture.Factory, _actionLogRepository, _loggerFactory.CreateLogger<CaseRepository>(), fakeTime);

            // Act
            ForensicCase created = await repo.CreateAsync("   ", "Title", null, null, null, null, [], "user@example.com", CancellationToken.None);

            // Assert
            Assert.NotNull(created.CaseNumber);
            Assert.StartsWith("manual-2026-10-07-14-35-", created.CaseNumber);
        }

        #endregion

        #region CreateAsync with explicit number Tests

        [Fact]
        public async Task CreateAsync_WithExplicitNumber_UsesProvidedNumber()
        {
            // Arrange
            string caseNumber = "MANUAL-2026-EXPLICIT-001";

            // Act
            ForensicCase created = await _repository.CreateAsync(
                caseNumber, "Title", null, null, null, null, [], "user@example.com", CancellationToken.None);

            // Assert
            Assert.Equal(caseNumber, created.CaseNumber);
        }

        [Fact]
        public async Task CreateAsync_WithDuplicateExplicitNumber_ThrowsExactMessage()
        {
            // Arrange
            string caseNumber = "DUP-2026-001";
            await _repository.CreateAsync(caseNumber, "Title 1", null, null, null, null, [], "user1@example.com", CancellationToken.None);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _repository.CreateAsync(caseNumber, "Title 2", null, null, null, null, [], "user2@example.com", CancellationToken.None)
            );
            Assert.Equal($"A case with number '{caseNumber}' already exists.", ex.Message);
        }

        #endregion

        #region Concurrent CreateAsync with auto-generation Tests

        [Fact]
        public async Task CreateAsync_ConcurrentAutoGeneration_BothSucceed()
        {
            // Arrange
            var fakeTime = new FakeTimeProvider(new DateTime(2026, 10, 7, 14, 35, 42, DateTimeKind.Utc));
            var repo = new CaseRepository(_fixture.Factory, _actionLogRepository, _loggerFactory.CreateLogger<CaseRepository>(), fakeTime);

            // Act - Two concurrent CreateAsync calls with null case number
            var task1 = repo.CreateAsync(null, "Title 1", null, null, null, null, [], "user1@example.com", CancellationToken.None);
            var task2 = repo.CreateAsync(null, "Title 2", null, null, null, null, [], "user2@example.com", CancellationToken.None);
            var results = await Task.WhenAll(task1, task2);

            // Assert - Both should succeed with different case numbers
            Assert.Equal(2, results.Length);
            Assert.NotEqual(results[0].CaseNumber, results[1].CaseNumber);
            Assert.StartsWith("manual-2026-10-07-14-35-", results[0].CaseNumber);
            Assert.StartsWith("manual-2026-10-07-14-35-", results[1].CaseNumber);
        }

        #endregion

        #region CreateFromJammingDetectionAsync Tests

        private async Task<Guid> SeedDevice()
        {
            Location location = TestDataBuilder.BuildLocation(name: "Test Location");
            await _locationRepository.AddAsync(location, CancellationToken.None);
            
            Device device = TestDataBuilder.BuildDevice(locationId: location.Id, name: "Test Device");
            await _deviceRepository.AddAsync(device, CancellationToken.None);
            return device.Id;
        }

        [Fact]
        public async Task CreateFromJammingDetectionAsync_HighConfidence_CreatesDetectedCase()
        {
            // Arrange
            var fakeTime = new FakeTimeProvider(new DateTime(2026, 10, 7, 14, 35, 42, DateTimeKind.Utc));
            var repo = new CaseRepository(_fixture.Factory, _actionLogRepository, _loggerFactory.CreateLogger<CaseRepository>(), fakeTime);
            var deviceId = await SeedDevice();
            var jammingIncident = new JammingIncidentRecord
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                StartUtc = new DateTime(2026, 10, 7, 14, 30, 0, DateTimeKind.Utc),
                EndUtc = new DateTime(2026, 10, 7, 14, 40, 0, DateTimeKind.Utc),
                AffectedEventCount = 5,
                AverageDegradationDb = 12.5,
                Confidence = JammingConfidenceLevel.High,
                DetectedAtUtc = DateTime.UtcNow,
                Notes = null,
                Source = JammingIncidentSource.AutoDetected
            };

            // Act
            ForensicCase created = await repo.CreateFromJammingDetectionAsync(jammingIncident, CancellationToken.None);

            // Assert
            Assert.NotNull(created);
            Assert.StartsWith("detected-2026-10-07-14-35-", created.CaseNumber);
            Assert.Equal("Jamming Detected", created.Title);
            Assert.Contains("Jamming incident detected", created.Description);
        }

        [Fact]
        public async Task CreateFromJammingDetectionAsync_DefiniteConfidence_CreatesDetectedCase()
        {
            // Arrange
            var fakeTime = new FakeTimeProvider(new DateTime(2026, 10, 7, 14, 35, 42, DateTimeKind.Utc));
            var repo = new CaseRepository(_fixture.Factory, _actionLogRepository, _loggerFactory.CreateLogger<CaseRepository>(), fakeTime);
            var deviceId = await SeedDevice();
            var jammingIncident = new JammingIncidentRecord
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                StartUtc = new DateTime(2026, 10, 7, 14, 30, 0, DateTimeKind.Utc),
                EndUtc = new DateTime(2026, 10, 7, 14, 40, 0, DateTimeKind.Utc),
                AffectedEventCount = 10,
                AverageDegradationDb = 25.0,
                Confidence = JammingConfidenceLevel.Definite,
                DetectedAtUtc = DateTime.UtcNow,
                Notes = null,
                Source = JammingIncidentSource.AutoDetected
            };

            // Act
            ForensicCase created = await repo.CreateFromJammingDetectionAsync(jammingIncident, CancellationToken.None);

            // Assert
            Assert.StartsWith("detected-2026-10-07-14-35-", created.CaseNumber);
            Assert.Equal("Jamming Detected", created.Title);
        }

        [Fact]
        public async Task CreateFromJammingDetectionAsync_MediumConfidence_CreatesSuspectedCase()
        {
            // Arrange
            var fakeTime = new FakeTimeProvider(new DateTime(2026, 10, 7, 14, 35, 42, DateTimeKind.Utc));
            var repo = new CaseRepository(_fixture.Factory, _actionLogRepository, _loggerFactory.CreateLogger<CaseRepository>(), fakeTime);
            var deviceId = await SeedDevice();
            var jammingIncident = new JammingIncidentRecord
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                StartUtc = new DateTime(2026, 10, 7, 14, 30, 0, DateTimeKind.Utc),
                EndUtc = new DateTime(2026, 10, 7, 14, 40, 0, DateTimeKind.Utc),
                AffectedEventCount = 3,
                AverageDegradationDb = 8.0,
                Confidence = JammingConfidenceLevel.Medium,
                DetectedAtUtc = DateTime.UtcNow,
                Notes = null,
                Source = JammingIncidentSource.AutoDetected
            };

            // Act
            ForensicCase created = await repo.CreateFromJammingDetectionAsync(jammingIncident, CancellationToken.None);

            // Assert
            Assert.StartsWith("suspected-2026-10-07-14-35-", created.CaseNumber);
            Assert.Equal("Jamming Suspected", created.Title);
        }

        [Fact]
        public async Task CreateFromJammingDetectionAsync_LowConfidence_CreatesSuspectedCase()
        {
            // Arrange
            var fakeTime = new FakeTimeProvider(new DateTime(2026, 10, 7, 14, 35, 42, DateTimeKind.Utc));
            var repo = new CaseRepository(_fixture.Factory, _actionLogRepository, _loggerFactory.CreateLogger<CaseRepository>(), fakeTime);
            var deviceId = await SeedDevice();
            var jammingIncident = new JammingIncidentRecord
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                StartUtc = new DateTime(2026, 10, 7, 14, 30, 0, DateTimeKind.Utc),
                EndUtc = new DateTime(2026, 10, 7, 14, 40, 0, DateTimeKind.Utc),
                AffectedEventCount = 1,
                AverageDegradationDb = 3.0,
                Confidence = JammingConfidenceLevel.Low,
                DetectedAtUtc = DateTime.UtcNow,
                Notes = null,
                Source = JammingIncidentSource.AutoDetected
            };

            // Act
            ForensicCase created = await repo.CreateFromJammingDetectionAsync(jammingIncident, CancellationToken.None);

            // Assert
            Assert.StartsWith("suspected-2026-10-07-14-35-", created.CaseNumber);
            Assert.Equal("Jamming Suspected", created.Title);
        }

        [Fact]
        public async Task CreateFromJammingDetectionAsync_SetsScope()
        {
            // Arrange
            var fakeTime = new FakeTimeProvider(new DateTime(2026, 10, 7, 14, 35, 42, DateTimeKind.Utc));
            var repo = new CaseRepository(_fixture.Factory, _actionLogRepository, _loggerFactory.CreateLogger<CaseRepository>(), fakeTime);
            var deviceId = await SeedDevice();
            var startTime = new DateTime(2026, 10, 7, 14, 30, 0, DateTimeKind.Utc);
            var endTime = new DateTime(2026, 10, 7, 14, 40, 0, DateTimeKind.Utc);
            var jammingIncident = new JammingIncidentRecord
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                StartUtc = startTime,
                EndUtc = endTime,
                AffectedEventCount = 5,
                AverageDegradationDb = 12.5,
                Confidence = JammingConfidenceLevel.High,
                DetectedAtUtc = DateTime.UtcNow,
                Notes = null,
                Source = JammingIncidentSource.AutoDetected
            };

            // Act
            ForensicCase created = await repo.CreateFromJammingDetectionAsync(jammingIncident, CancellationToken.None);

            // Assert
            Assert.Equal(startTime, created.ScopeFromUtc);
            Assert.Equal(endTime, created.ScopeToUtc);
            IReadOnlyList<Guid> deviceIds = await repo.GetDeviceIdsAsync(created.Id, CancellationToken.None);
            Assert.Single(deviceIds);
            Assert.Equal(deviceId, deviceIds[0]);
        }

        [Fact]
        public async Task CreateFromJammingDetectionAsync_CreatedBySystem()
        {
            // Arrange
            var fakeTime = new FakeTimeProvider(new DateTime(2026, 10, 7, 14, 35, 42, DateTimeKind.Utc));
            var repo = new CaseRepository(_fixture.Factory, _actionLogRepository, _loggerFactory.CreateLogger<CaseRepository>(), fakeTime);
            var deviceId = await SeedDevice();
            var jammingIncident = new JammingIncidentRecord
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                StartUtc = new DateTime(2026, 10, 7, 14, 30, 0, DateTimeKind.Utc),
                EndUtc = new DateTime(2026, 10, 7, 14, 40, 0, DateTimeKind.Utc),
                AffectedEventCount = 5,
                AverageDegradationDb = 12.5,
                Confidence = JammingConfidenceLevel.High,
                DetectedAtUtc = DateTime.UtcNow,
                Notes = null,
                Source = JammingIncidentSource.AutoDetected
            };

            // Act
            ForensicCase created = await repo.CreateFromJammingDetectionAsync(jammingIncident, CancellationToken.None);

            // Assert
            Assert.Equal("System", created.CreatedBy);
        }

        [Fact]
        public async Task CreateFromJammingDetectionAsync_IncludesDetails()
        {
            // Arrange
            var fakeTime = new FakeTimeProvider(new DateTime(2026, 10, 7, 14, 35, 42, DateTimeKind.Utc));
            var repo = new CaseRepository(_fixture.Factory, _actionLogRepository, _loggerFactory.CreateLogger<CaseRepository>(), fakeTime);
            var deviceId = await SeedDevice();
            var jammingIncident = new JammingIncidentRecord
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                StartUtc = new DateTime(2026, 10, 7, 14, 30, 0, DateTimeKind.Utc),
                EndUtc = new DateTime(2026, 10, 7, 14, 40, 0, DateTimeKind.Utc),
                AffectedEventCount = 5,
                AverageDegradationDb = 12.5,
                Confidence = JammingConfidenceLevel.High,
                DetectedAtUtc = DateTime.UtcNow,
                Notes = "Test notes",
                Source = JammingIncidentSource.AutoDetected
            };

            // Act
            ForensicCase created = await repo.CreateFromJammingDetectionAsync(jammingIncident, CancellationToken.None);

            // Assert
            Assert.Contains(deviceId.ToString(), created.Description);
            Assert.Contains("12.50 dB", created.Description); // AverageDegradationDb
            Assert.Contains("5", created.Description); // AffectedEventCount
            Assert.Contains("High", created.Description); // Confidence level
            Assert.Contains("Test notes", created.Description);
        }


        [Fact]
        public async Task CreateFromJammingDetectionAsync_WritesActionLogWithActorTypeSystem()
        {
            // Arrange
            var deviceId = await SeedDevice();
            var jammingIncident = new JammingIncidentRecord
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                StartUtc = DateTime.UtcNow,
                EndUtc = DateTime.UtcNow.AddMinutes(10),
                AffectedEventCount = 5,
                AverageDegradationDb = 12.5,
                Confidence = JammingConfidenceLevel.High,
                DetectedAtUtc = DateTime.UtcNow,
                Notes = null,
                Source = JammingIncidentSource.AutoDetected
            };

            // Act
            ForensicCase created = await _repository.CreateFromJammingDetectionAsync(jammingIncident, CancellationToken.None);

            // Assert - verify action log entry has ActorType.System
            IReadOnlyList<ActionLogEntry> history = await _actionLogRepository.GetHistoryForEntityAsync("Case", created.Id, CancellationToken.None);
            ActionLogEntry? createEntry = history.FirstOrDefault(e => e.Action == "CreateCase");
            Assert.NotNull(createEntry);
            Assert.Equal("System", createEntry.Actor);
            Assert.Equal(ActorType.System, createEntry.ActorType);
        }

        [Fact]
        public async Task CreateAsync_ManualCaseWritesActionLogWithActorTypeHuman()
        {
            // Arrange & Act
            ForensicCase created = await _repository.CreateAsync(null, "Manual Case", null, null, null, null, [], "operator@example.com", CancellationToken.None);

            // Assert - verify action log entry has ActorType.Human (not System)
            IReadOnlyList<ActionLogEntry> history = await _actionLogRepository.GetHistoryForEntityAsync("Case", created.Id, CancellationToken.None);
            ActionLogEntry? createEntry = history.FirstOrDefault(e => e.Action == "CreateCase");
            Assert.NotNull(createEntry);
            Assert.Equal("operator@example.com", createEntry.Actor);
            Assert.Equal(ActorType.Human, createEntry.ActorType);
        }

        [Fact]
        public async Task CreateAsync_GeneratedNumberRetryOnCollision()
        {
            // Arrange: Pre-seed a case that will collide with the FIRST generated candidate
            var fakeTime = new FakeTimeProvider(new DateTime(2026, 10, 7, 14, 35, 42, DateTimeKind.Utc));
            var repo = new CaseRepository(_fixture.Factory, _actionLogRepository, _loggerFactory.CreateLogger<CaseRepository>(), fakeTime);
            
            // Pre-create the case that will collide with the first generated number
            string collidingNumber = "manual-2026-10-07-14-35-0";
            await repo.CreateAsync(collidingNumber, "Colliding Case", null, null, null, null, [], "user@example.com", CancellationToken.None);

            // Act - try to create another case that will first generate the same number
            ForensicCase created = await repo.CreateAsync(null, "Retried Case", null, null, null, null, [], "user@example.com", CancellationToken.None);

            // Assert - should succeed with incremented index (not colliding)
            Assert.NotEqual(collidingNumber, created.CaseNumber);
            Assert.Equal("manual-2026-10-07-14-35-1", created.CaseNumber);
        }

        [Fact]
        public async Task CreateAsync_ConcurrentParallelCreatesAllSucceed()
        {
            // Arrange
            var fakeTime = new FakeTimeProvider(new DateTime(2026, 10, 7, 14, 35, 42, DateTimeKind.Utc));
            var repo = new CaseRepository(_fixture.Factory, _actionLogRepository, _loggerFactory.CreateLogger<CaseRepository>(), fakeTime);

            // Act - 10 parallel creates with null case number
            var tasks = Enumerable.Range(0, 10)
                .Select(i => repo.CreateAsync(null, $"Parallel Case {i}", null, null, null, null, [], "user@example.com", CancellationToken.None))
                .ToList();
            var results = await Task.WhenAll(tasks);

            // Assert - all 10 succeed with distinct numbers
            Assert.Equal(10, results.Length);
            var numbers = results.Select(c => c.CaseNumber).ToHashSet();
            Assert.Equal(10, numbers.Count); // All unique
            Assert.All(numbers, n => Assert.StartsWith("manual-2026-10-07-14-35-", n));
        }

        #endregion

        #region Insert-time race and failure-mode Tests

        private static readonly DateTime RaceNow = new(2026, 10, 7, 14, 35, 42, DateTimeKind.Utc);

        private async Task<(CaseRepository Repo, SqliteConnection Conn, RaceInterceptor Interceptor)> BuildRacingRepoAsync(bool armed = true)
        {
            var conn = new SqliteConnection("DataSource=:memory:");
            await conn.OpenAsync();
            var plain = new DbContextOptionsBuilder<VideoForensicsDbContext>().UseSqlite(conn).Options;
            await using (var setup = new VideoForensicsDbContext(plain))
            {
                _ = await setup.Database.EnsureCreatedAsync();
            }

            var interceptor = new RaceInterceptor(() => new VideoForensicsDbContext(plain), RaceNow) { Armed = armed };
            var racing = new DbContextOptionsBuilder<VideoForensicsDbContext>().UseSqlite(conn).AddInterceptors(interceptor).Options;
            var factory = new SimpleFactory(racing);
            var repo = new CaseRepository(factory, new ActionLogRepository(factory, _loggerFactory.CreateLogger<ActionLogRepository>()),
                _loggerFactory.CreateLogger<CaseRepository>(), new FakeTimeProvider(RaceNow));
            return (repo, conn, interceptor);
        }

        private static async Task<List<string>> CaseNumbersAsync(SqliteConnection conn)
        {
            await using var db = new VideoForensicsDbContext(new DbContextOptionsBuilder<VideoForensicsDbContext>().UseSqlite(conn).Options);
            return await db.Cases.Select(c => c.CaseNumber).OrderBy(n => n).ToListAsync();
        }

        [Fact]
        public async Task CreateAsync_InsertTimeUniqueViolation_RetriesWithNextIndex()
        {
            var (repo, conn, interceptor) = await BuildRacingRepoAsync();
            await using (conn)
            {
                ForensicCase created = await repo.CreateAsync(null, "Raced", null, null, null, null, [], "user@example.com", CancellationToken.None);

                Assert.True(interceptor.Fired);
                Assert.Equal("manual-2026-10-07-14-35-1", created.CaseNumber);
                Assert.Equal(new[] { "manual-2026-10-07-14-35-0", "manual-2026-10-07-14-35-1" }, await CaseNumbersAsync(conn));
            }
        }

        [Fact]
        public async Task CreateFromJammingDetectionAsync_InsertTimeUniqueViolation_KeepsDetectedPrefix()
        {
            var (repo, conn, interceptor) = await BuildRacingRepoAsync(armed: false);
            await using (conn)
            {
                Guid deviceId = await SeedDeviceAsync(conn);
                interceptor.Armed = true;
                var incident = new JammingIncidentRecord
                {
                    Id = Guid.NewGuid(),
                    DeviceId = deviceId,
                    StartUtc = new DateTime(2026, 10, 7, 14, 30, 0, DateTimeKind.Utc),
                    EndUtc = new DateTime(2026, 10, 7, 14, 40, 0, DateTimeKind.Utc),
                    AffectedEventCount = 5,
                    AverageDegradationDb = 12.5,
                    Confidence = JammingConfidenceLevel.High,
                    DetectedAtUtc = RaceNow,
                    Source = JammingIncidentSource.AutoDetected
                };

                ForensicCase created = await repo.CreateFromJammingDetectionAsync(incident, CancellationToken.None);

                Assert.True(interceptor.Fired);
                Assert.Equal("detected-2026-10-07-14-35-1", created.CaseNumber);
                Assert.Equal(new[] { "detected-2026-10-07-14-35-0", "detected-2026-10-07-14-35-1" }, await CaseNumbersAsync(conn));
            }
        }

        [Fact]
        public async Task CreateAsync_ForeignKeyFailure_IsNotRetriedOrSwallowed()
        {
            var (repo, conn, _) = await BuildRacingRepoAsync(armed: false);
            await using (conn)
            {
                await Assert.ThrowsAsync<DbUpdateException>(
                    () => repo.CreateAsync(null, "Bad device", null, null, null, null, [Guid.NewGuid()], "user@example.com", CancellationToken.None));

                Assert.Empty(await CaseNumbersAsync(conn));
            }
        }

        [Fact]
        public async Task CreateAsync_ExplicitDuplicateAtInsertTime_ThrowsDbUpdateWithoutRetry()
        {
            var (repo, conn, interceptor) = await BuildRacingRepoAsync();
            await using (conn)
            {
                await Assert.ThrowsAsync<DbUpdateException>(
                    () => repo.CreateAsync("EXPLICIT-RACE-1", "Explicit", null, null, null, null, [], "user@example.com", CancellationToken.None));

                Assert.True(interceptor.Fired);
                Assert.Equal(new[] { "EXPLICIT-RACE-1" }, await CaseNumbersAsync(conn));
            }
        }

        private async Task<Guid> SeedDeviceAsync(SqliteConnection conn)
        {
            var factory = new SimpleFactory(new DbContextOptionsBuilder<VideoForensicsDbContext>().UseSqlite(conn).Options);
            var loc = TestDataBuilder.BuildLocation(name: "Race Location");
            await new LocationRepository(factory, _loggerFactory.CreateLogger<LocationRepository>()).AddAsync(loc, CancellationToken.None);
            var device = TestDataBuilder.BuildDevice(locationId: loc.Id, name: "Race Device");
            await new DeviceRepository(factory, _loggerFactory.CreateLogger<DeviceRepository>()).AddAsync(device, CancellationToken.None);
            return device.Id;
        }

        private sealed class SimpleFactory(DbContextOptions<VideoForensicsDbContext> options) : IDbContextFactory<VideoForensicsDbContext>
        {
            public VideoForensicsDbContext CreateDbContext() => new(options);
        }

        /// <summary>On the first save that adds a case, inserts a competing case with the same number from another context.</summary>
        private sealed class RaceInterceptor(Func<VideoForensicsDbContext> contextFactory, DateTime now) : SaveChangesInterceptor
        {
            public bool Armed { get; set; }
            public bool Fired { get; private set; }

            public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
                DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
            {
                ForensicCase? added = eventData.Context?.ChangeTracker.Entries<ForensicCase>()
                    .FirstOrDefault(e => e.State == EntityState.Added)?.Entity;
                if (Armed && !Fired && added is not null)
                {
                    Fired = true;
                    await using VideoForensicsDbContext other = contextFactory();
                    _ = other.Cases.Add(new ForensicCase
                    {
                        Id = Guid.NewGuid(),
                        CaseNumber = added.CaseNumber,
                        Title = "Competitor",
                        Status = CaseStatus.Open,
                        CreatedBy = "racer",
                        CreatedAtUtc = now,
                        UpdatedAtUtc = now
                    });
                    _ = await other.SaveChangesAsync(cancellationToken);
                }

                return result;
            }
        }

        #endregion
    }

    /// <summary>
    /// A fake time provider that returns a fixed time for testing.
    /// </summary>
    internal sealed class FakeTimeProvider : TimeProvider
    {
        private readonly DateTime _fixedTime;

        public FakeTimeProvider(DateTime fixedTime)
        {
            _fixedTime = fixedTime;
        }

        public override DateTimeOffset GetUtcNow() => new(_fixedTime, TimeSpan.Zero);
    }
}
