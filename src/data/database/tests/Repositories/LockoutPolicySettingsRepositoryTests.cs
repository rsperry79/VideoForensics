using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests.Repositories
{
    public class LockoutPolicySettingsRepositoryTests : RepositoryTestBase
    {
        private LockoutPolicySettingsRepository _repository = null!;

        public override async ValueTask InitializeAsync()
        {
            await base.InitializeAsync();
            _repository = new LockoutPolicySettingsRepository(Fixture.Factory, CreateLogger<LockoutPolicySettingsRepository>());
        }

        [Fact]
        public async Task GetAsync_WhenEmpty_ReturnsDefaultInstance()
        {
            // Act: Get settings when none exist
            LockoutPolicySettings settings = await _repository.GetAsync(CancellationToken.None);

            // Assert: Should return defaults without persisting
            Assert.NotNull(settings);
            Assert.Equal(Guid.Empty, settings.Id); // Sentinel indicating not-yet-persisted
            Assert.Equal(5, settings.MaxFailedAttempts);
            Assert.Equal(15, settings.LockoutDurationMinutes);
            Assert.Null(settings.BlockedCountryCodes);
            Assert.False(settings.FailClosedOnLookupError);
        }

        [Fact]
        public async Task UpsertAsync_WhenEmpty_InsertsNewRow()
        {
            // Arrange: Create a settings instance with custom values
            var settings = new LockoutPolicySettings
            {
                Id = Guid.NewGuid(),
                MaxFailedAttempts = 10,
                LockoutDurationMinutes = 30,
                BlockedCountryCodes = "KP,IR",
                FailClosedOnLookupError = true,
                UpdatedAtUtc = DateTime.UtcNow,
                UpdatedByOperatorId = null
            };

            // Act: Upsert the settings
            await _repository.UpsertAsync(settings, CancellationToken.None);

            // Assert: Verify it was persisted
            LockoutPolicySettings retrieved = await _repository.GetAsync(CancellationToken.None);
            Assert.NotNull(retrieved);
            Assert.Equal(10, retrieved.MaxFailedAttempts);
            Assert.Equal(30, retrieved.LockoutDurationMinutes);
            Assert.Equal("KP,IR", retrieved.BlockedCountryCodes);
            Assert.True(retrieved.FailClosedOnLookupError);
        }

        [Fact]
        public async Task UpsertAsync_WhenExists_UpdatesExistingRow()
        {
            // Arrange: Insert initial settings
            var initialSettings = new LockoutPolicySettings
            {
                Id = Guid.NewGuid(),
                MaxFailedAttempts = 5,
                LockoutDurationMinutes = 15,
                BlockedCountryCodes = null,
                FailClosedOnLookupError = false,
                UpdatedAtUtc = DateTime.UtcNow,
                UpdatedByOperatorId = null
            };
            await _repository.UpsertAsync(initialSettings, CancellationToken.None);

            // Act: Upsert with updated values
            var updatedSettings = new LockoutPolicySettings
            {
                Id = initialSettings.Id, // Same Id
                MaxFailedAttempts = 3,
                LockoutDurationMinutes = 10,
                BlockedCountryCodes = "KP,IR,SY",
                FailClosedOnLookupError = true,
                UpdatedAtUtc = DateTime.UtcNow,
                UpdatedByOperatorId = Guid.NewGuid()
            };
            await _repository.UpsertAsync(updatedSettings, CancellationToken.None);

            // Assert: Verify the row was updated (still one row, not two)
            LockoutPolicySettings retrieved = await _repository.GetAsync(CancellationToken.None);
            Assert.NotNull(retrieved);
            Assert.Equal(3, retrieved.MaxFailedAttempts);
            Assert.Equal(10, retrieved.LockoutDurationMinutes);
            Assert.Equal("KP,IR,SY", retrieved.BlockedCountryCodes);
            Assert.True(retrieved.FailClosedOnLookupError);
            _ = Assert.NotNull(retrieved.UpdatedByOperatorId);
        }

        [Fact]
        public async Task UpsertAsync_GeneratesIdWhenEmpty()
        {
            // Arrange: Create a settings instance with Id = Guid.Empty (sentinel)
            var settings = new LockoutPolicySettings
            {
                Id = Guid.Empty,
                MaxFailedAttempts = 5,
                LockoutDurationMinutes = 15,
                BlockedCountryCodes = null,
                FailClosedOnLookupError = false,
                UpdatedAtUtc = DateTime.UtcNow,
                UpdatedByOperatorId = null
            };

            // Act: Upsert
            await _repository.UpsertAsync(settings, CancellationToken.None);

            // Assert: Verify a non-empty Id was assigned and the row was persisted
            LockoutPolicySettings retrieved = await _repository.GetAsync(CancellationToken.None);
            Assert.NotNull(retrieved);
            Assert.NotEqual(Guid.Empty, retrieved.Id);
        }

        [Fact]
        public async Task GetAsync_WhenEmpty_ReturnsAutoBanDefaults()
        {
            LockoutPolicySettings settings = await _repository.GetAsync(CancellationToken.None);

            Assert.False(settings.AutoBanEnabled);
            Assert.Equal(3, settings.AutoBanDistinctAccountThreshold);
            Assert.Equal(60, settings.AutoBanWindowMinutes);
            Assert.Equal(1440, settings.AutoBanDurationMinutes);
        }

        [Fact]
        public async Task UpsertAsync_WhenEmptyWithDefaultAutoBanFields_PersistsAutoBanDefaults()
        {
            var settings = new LockoutPolicySettings
            {
                Id = Guid.NewGuid(),
                MaxFailedAttempts = 5,
                LockoutDurationMinutes = 15,
                UpdatedAtUtc = DateTime.UtcNow
            };

            await _repository.UpsertAsync(settings, CancellationToken.None);

            LockoutPolicySettings retrieved = await _repository.GetAsync(CancellationToken.None);
            Assert.False(retrieved.AutoBanEnabled);
            Assert.Equal(3, retrieved.AutoBanDistinctAccountThreshold);
            Assert.Equal(60, retrieved.AutoBanWindowMinutes);
            Assert.Equal(1440, retrieved.AutoBanDurationMinutes);
        }

        [Fact]
        public async Task UpsertAsync_WhenEmpty_AutoBanFieldsRoundTrip()
        {
            var settings = new LockoutPolicySettings
            {
                Id = Guid.NewGuid(),
                MaxFailedAttempts = 5,
                LockoutDurationMinutes = 15,
                AutoBanEnabled = true,
                AutoBanDistinctAccountThreshold = 7,
                AutoBanWindowMinutes = 30,
                AutoBanDurationMinutes = 720,
                UpdatedAtUtc = DateTime.UtcNow
            };

            await _repository.UpsertAsync(settings, CancellationToken.None);

            LockoutPolicySettings retrieved = await _repository.GetAsync(CancellationToken.None);
            Assert.True(retrieved.AutoBanEnabled);
            Assert.Equal(7, retrieved.AutoBanDistinctAccountThreshold);
            Assert.Equal(30, retrieved.AutoBanWindowMinutes);
            Assert.Equal(720, retrieved.AutoBanDurationMinutes);
        }

        [Fact]
        public async Task UpsertAsync_WhenExists_CopiesAllAutoBanFields()
        {
            var initialSettings = new LockoutPolicySettings
            {
                Id = Guid.NewGuid(),
                MaxFailedAttempts = 5,
                LockoutDurationMinutes = 15,
                UpdatedAtUtc = DateTime.UtcNow
            };
            await _repository.UpsertAsync(initialSettings, CancellationToken.None);

            var updatedSettings = new LockoutPolicySettings
            {
                Id = initialSettings.Id,
                MaxFailedAttempts = 5,
                LockoutDurationMinutes = 15,
                AutoBanEnabled = true,
                AutoBanDistinctAccountThreshold = 4,
                AutoBanWindowMinutes = 90,
                AutoBanDurationMinutes = 2880,
                UpdatedAtUtc = DateTime.UtcNow
            };
            await _repository.UpsertAsync(updatedSettings, CancellationToken.None);

            LockoutPolicySettings retrieved = await _repository.GetAsync(CancellationToken.None);
            Assert.True(retrieved.AutoBanEnabled);
            Assert.Equal(4, retrieved.AutoBanDistinctAccountThreshold);
            Assert.Equal(90, retrieved.AutoBanWindowMinutes);
            Assert.Equal(2880, retrieved.AutoBanDurationMinutes);
        }
    }
}
