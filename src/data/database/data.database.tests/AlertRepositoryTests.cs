using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests
{
    /// <summary>Tests for AlertRepository.</summary>
    public class AlertRepositoryTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private IAlertRepository _repository = null!;
        private ILoggerFactory _loggerFactory = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            _loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });
            _repository = new AlertRepository(_fixture.Factory, _loggerFactory.CreateLogger<AlertRepository>());
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
            _loggerFactory.Dispose();
        }

        [Fact]
        public async Task CreateAsync_CreatesAlertWithAllRequiredFields()
        {
            // Arrange
            var alert = new Alert
            {
                Id = Guid.NewGuid(),
                Title = "Jamming Detected",
                Description = "Signal interference detected on Device: Front Door",
                RelatedCaseId = Guid.NewGuid(),
                Status = "Open",
                CreatedBy = "System",
                AlertType = "JammingDetection",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = null
            };

            // Act
            Alert createdAlert = await _repository.CreateAsync(alert, CancellationToken.None);

            // Assert
            Assert.NotNull(createdAlert);
            Assert.Equal(alert.Id, createdAlert.Id);
            Assert.Equal("Jamming Detected", createdAlert.Title);
            Assert.Equal("Signal interference detected on Device: Front Door", createdAlert.Description);
            Assert.Equal(alert.RelatedCaseId, createdAlert.RelatedCaseId);
            Assert.Equal("Open", createdAlert.Status);
            Assert.Equal("System", createdAlert.CreatedBy);
            Assert.Equal("JammingDetection", createdAlert.AlertType);
        }

        [Fact]
        public async Task CreateAsync_PersistsAlertToDatabase()
        {
            // Arrange
            var alert = new Alert
            {
                Id = Guid.NewGuid(),
                Title = "Jamming Suspected",
                Description = "Possible signal interference",
                RelatedCaseId = null,
                Status = "Open",
                CreatedBy = "System",
                AlertType = "JammingDetection",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = null
            };

            // Act
            Alert createdAlert = await _repository.CreateAsync(alert, CancellationToken.None);

            // Assert - verify we can retrieve it
            Alert? retrieved = await _repository.GetAsync(createdAlert.Id, CancellationToken.None);
            Assert.NotNull(retrieved);
            Assert.Equal("Jamming Suspected", retrieved.Title);
        }

        [Fact]
        public async Task CreateAsync_WithNullRelatedCaseId()
        {
            // Arrange
            var alert = new Alert
            {
                Id = Guid.NewGuid(),
                Title = "Generic Alert",
                Description = null,
                RelatedCaseId = null,
                Status = "Open",
                CreatedBy = "System",
                AlertType = "JammingDetection",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = null
            };

            // Act
            Alert createdAlert = await _repository.CreateAsync(alert, CancellationToken.None);

            // Assert
            Assert.NotNull(createdAlert);
            Assert.Null(createdAlert.RelatedCaseId);
        }
    }
}
