using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests
{
    public class OperatorPreferencesRepositoryTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private OperatorPreferencesRepository _repository = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            _repository = new OperatorPreferencesRepository(_fixture.Factory);
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        [Fact]
        public async Task OperatorPreferencesRepository_Get_NotFound_ReturnsNull()
        {
            var nonexistentOperatorId = Guid.NewGuid();

            OperatorPreferences? retrieved = await _repository.GetAsync(nonexistentOperatorId, CancellationToken.None);

            Assert.Null(retrieved);
        }

        [Fact]
        public async Task OperatorPreferencesRepository_Upsert_Insert_CreatesNewRecord()
        {
            var operatorId = Guid.NewGuid();
            var preferences = new OperatorPreferences
            {
                Id = Guid.NewGuid(),
                OperatorId = operatorId,
                ThemeMode = "Dark",
                CultureName = "en-US",
                UpdatedAtUtc = DateTime.UtcNow
            };

            await _repository.UpsertAsync(preferences, CancellationToken.None);

            OperatorPreferences? retrieved = await _repository.GetAsync(operatorId, CancellationToken.None);

            Assert.NotNull(retrieved);
            Assert.Equal(operatorId, retrieved.OperatorId);
            Assert.Equal("Dark", retrieved.ThemeMode);
            Assert.Equal("en-US", retrieved.CultureName);
        }

        [Fact]
        public async Task OperatorPreferencesRepository_Upsert_Update_ModifiesExisting()
        {
            var operatorId = Guid.NewGuid();
            var preferences = new OperatorPreferences
            {
                Id = Guid.NewGuid(),
                OperatorId = operatorId,
                ThemeMode = "Light",
                CultureName = "en-GB",
                UpdatedAtUtc = DateTime.UtcNow
            };

            await _repository.UpsertAsync(preferences, CancellationToken.None);

            // Update the preferences
            var updatedPreferences = new OperatorPreferences
            {
                Id = preferences.Id,
                OperatorId = operatorId,
                ThemeMode = "Dark",
                CultureName = "fr-FR",
                UpdatedAtUtc = DateTime.UtcNow
            };

            await _repository.UpsertAsync(updatedPreferences, CancellationToken.None);

            OperatorPreferences? retrieved = await _repository.GetAsync(operatorId, CancellationToken.None);

            Assert.NotNull(retrieved);
            Assert.Equal("Dark", retrieved.ThemeMode);
            Assert.Equal("fr-FR", retrieved.CultureName);
        }

        [Fact]
        public async Task OperatorPreferencesRepository_Upsert_MultipleUpdates_OverwritesPrevious()
        {
            var operatorId = Guid.NewGuid();
            var preferences = new OperatorPreferences
            {
                Id = Guid.NewGuid(),
                OperatorId = operatorId,
                ThemeMode = "System",
                CultureName = null,
                UpdatedAtUtc = DateTime.UtcNow
            };

            await _repository.UpsertAsync(preferences, CancellationToken.None);

            // First update
            var update1 = new OperatorPreferences
            {
                Id = preferences.Id,
                OperatorId = operatorId,
                ThemeMode = "Light",
                CultureName = "de-DE",
                UpdatedAtUtc = DateTime.UtcNow
            };
            await _repository.UpsertAsync(update1, CancellationToken.None);

            // Second update
            var update2 = new OperatorPreferences
            {
                Id = preferences.Id,
                OperatorId = operatorId,
                ThemeMode = "Dark",
                CultureName = "es-ES",
                UpdatedAtUtc = DateTime.UtcNow
            };
            await _repository.UpsertAsync(update2, CancellationToken.None);

            OperatorPreferences? retrieved = await _repository.GetAsync(operatorId, CancellationToken.None);

            Assert.NotNull(retrieved);
            Assert.Equal("Dark", retrieved.ThemeMode);
            Assert.Equal("es-ES", retrieved.CultureName);
        }

        [Fact]
        public async Task OperatorPreferencesRepository_Upsert_WithEmptyId_GeneratesId()
        {
            var operatorId = Guid.NewGuid();
            var preferences = new OperatorPreferences
            {
                Id = Guid.Empty, // Empty ID
                OperatorId = operatorId,
                ThemeMode = "Light",
                CultureName = "ja-JP",
                UpdatedAtUtc = DateTime.UtcNow
            };

            await _repository.UpsertAsync(preferences, CancellationToken.None);

            OperatorPreferences? retrieved = await _repository.GetAsync(operatorId, CancellationToken.None);

            Assert.NotNull(retrieved);
            Assert.NotEqual(Guid.Empty, retrieved.Id);
            Assert.Equal("Light", retrieved.ThemeMode);
        }

        [Fact]
        public async Task OperatorPreferencesRepository_Upsert_NullCultureName_IsPreserved()
        {
            var operatorId = Guid.NewGuid();
            var preferences = new OperatorPreferences
            {
                Id = Guid.NewGuid(),
                OperatorId = operatorId,
                ThemeMode = "System",
                CultureName = null,
                UpdatedAtUtc = DateTime.UtcNow
            };

            await _repository.UpsertAsync(preferences, CancellationToken.None);

            OperatorPreferences? retrieved = await _repository.GetAsync(operatorId, CancellationToken.None);

            Assert.NotNull(retrieved);
            Assert.Null(retrieved.CultureName);
        }

        [Fact]
        public async Task OperatorPreferencesRepository_Upsert_UpdatePreservesId()
        {
            var operatorId = Guid.NewGuid();
            var id = Guid.NewGuid();
            var preferences = new OperatorPreferences
            {
                Id = id,
                OperatorId = operatorId,
                ThemeMode = "Light",
                CultureName = "en-US",
                UpdatedAtUtc = DateTime.UtcNow
            };

            await _repository.UpsertAsync(preferences, CancellationToken.None);

            // Update with same ID
            var updated = new OperatorPreferences
            {
                Id = id,
                OperatorId = operatorId,
                ThemeMode = "Dark",
                CultureName = "en-GB",
                UpdatedAtUtc = DateTime.UtcNow
            };

            await _repository.UpsertAsync(updated, CancellationToken.None);

            OperatorPreferences? retrieved = await _repository.GetAsync(operatorId, CancellationToken.None);

            Assert.NotNull(retrieved);
            Assert.Equal(id, retrieved.Id);
        }

        [Fact]
        public async Task OperatorPreferencesRepository_GetAsync_RetrievesCorrectOperator()
        {
            var op1Id = Guid.NewGuid();
            var op2Id = Guid.NewGuid();

            var prefs1 = new OperatorPreferences
            {
                Id = Guid.NewGuid(),
                OperatorId = op1Id,
                ThemeMode = "Dark",
                CultureName = "en-US",
                UpdatedAtUtc = DateTime.UtcNow
            };

            var prefs2 = new OperatorPreferences
            {
                Id = Guid.NewGuid(),
                OperatorId = op2Id,
                ThemeMode = "Light",
                CultureName = "fr-FR",
                UpdatedAtUtc = DateTime.UtcNow
            };

            await _repository.UpsertAsync(prefs1, CancellationToken.None);
            await _repository.UpsertAsync(prefs2, CancellationToken.None);

            OperatorPreferences? retrieved1 = await _repository.GetAsync(op1Id, CancellationToken.None);
            OperatorPreferences? retrieved2 = await _repository.GetAsync(op2Id, CancellationToken.None);

            Assert.NotNull(retrieved1);
            Assert.NotNull(retrieved2);
            Assert.Equal("Dark", retrieved1.ThemeMode);
            Assert.Equal("Light", retrieved2.ThemeMode);
            Assert.Equal("en-US", retrieved1.CultureName);
            Assert.Equal("fr-FR", retrieved2.CultureName);
        }

        [Fact]
        public async Task OperatorPreferencesRepository_Upsert_UpdatesTimestamp()
        {
            var operatorId = Guid.NewGuid();
            DateTime time1 = DateTime.UtcNow;

            var preferences = new OperatorPreferences
            {
                Id = Guid.NewGuid(),
                OperatorId = operatorId,
                ThemeMode = "Light",
                CultureName = "en-US",
                UpdatedAtUtc = time1
            };

            await _repository.UpsertAsync(preferences, CancellationToken.None);

            OperatorPreferences? retrieved1 = await _repository.GetAsync(operatorId, CancellationToken.None);
            Assert.NotNull(retrieved1);
            DateTime firstTimestamp = retrieved1.UpdatedAtUtc;

            // Wait a bit and update
            await Task.Delay(10);

            var updated = new OperatorPreferences
            {
                Id = preferences.Id,
                OperatorId = operatorId,
                ThemeMode = "Dark",
                CultureName = "en-US",
                UpdatedAtUtc = DateTime.UtcNow
            };

            await _repository.UpsertAsync(updated, CancellationToken.None);

            OperatorPreferences? retrieved2 = await _repository.GetAsync(operatorId, CancellationToken.None);
            Assert.NotNull(retrieved2);

            // Timestamp should be updated (though we can't be too strict about timing)
            Assert.NotEqual(firstTimestamp, retrieved2.UpdatedAtUtc);
            Assert.True(retrieved2.UpdatedAtUtc >= firstTimestamp);
        }

        [Fact]
        public void OperatorPreferences_Constructor_UiModeDefaultsToStandard()
        {
            var preferences = new OperatorPreferences();

            Assert.Equal("Standard", preferences.UiMode);
        }

        [Fact]
        public async Task OperatorPreferencesRepository_Upsert_Insert_UiModeRoundTrips()
        {
            var operatorId = Guid.NewGuid();
            var preferences = new OperatorPreferences
            {
                Id = Guid.NewGuid(),
                OperatorId = operatorId,
                ThemeMode = "Dark",
                CultureName = "en-US",
                UiMode = "Simple",
                UpdatedAtUtc = DateTime.UtcNow
            };

            await _repository.UpsertAsync(preferences, CancellationToken.None);

            OperatorPreferences? retrieved = await _repository.GetAsync(operatorId, CancellationToken.None);

            Assert.NotNull(retrieved);
            Assert.Equal("Simple", retrieved.UiMode);
        }

        [Fact]
        public async Task OperatorPreferencesRepository_Upsert_Update_UiModeRoundTrips()
        {
            var operatorId = Guid.NewGuid();
            var preferences = new OperatorPreferences
            {
                Id = Guid.NewGuid(),
                OperatorId = operatorId,
                ThemeMode = "Light",
                CultureName = "en-GB",
                UiMode = "Standard",
                UpdatedAtUtc = DateTime.UtcNow
            };

            await _repository.UpsertAsync(preferences, CancellationToken.None);

            // Update the UiMode
            var updatedPreferences = new OperatorPreferences
            {
                Id = preferences.Id,
                OperatorId = operatorId,
                ThemeMode = "Dark",
                CultureName = "fr-FR",
                UiMode = "Simple",
                UpdatedAtUtc = DateTime.UtcNow
            };

            await _repository.UpsertAsync(updatedPreferences, CancellationToken.None);

            OperatorPreferences? retrieved = await _repository.GetAsync(operatorId, CancellationToken.None);

            Assert.NotNull(retrieved);
            Assert.Equal("Simple", retrieved.UiMode);
        }

        // Tests for SetUiModeAsync and UiModeLocked feature

        [Fact]
        public async Task SetUiModeAsync_CreatesNewRow_WithSimpleAndLocked()
        {
            var operatorId = Guid.NewGuid();

            OperatorPreferences result = await _repository.SetUiModeAsync(operatorId, "Simple", true, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(operatorId, result.OperatorId);
            Assert.Equal("Simple", result.UiMode);
            Assert.True(result.UiModeLocked);
            Assert.Equal("System", result.ThemeMode);

            OperatorPreferences? retrieved = await _repository.GetAsync(operatorId, CancellationToken.None);
            Assert.NotNull(retrieved);
            Assert.Equal("Simple", retrieved.UiMode);
            Assert.True(retrieved.UiModeLocked);
        }

        [Fact]
        public async Task SetUiModeAsync_UpdatesExistingRow_PreservesThemeAndCulture()
        {
            var operatorId = Guid.NewGuid();
            var preferences = new OperatorPreferences
            {
                Id = Guid.NewGuid(),
                OperatorId = operatorId,
                ThemeMode = "Dark",
                CultureName = "de-DE",
                UiMode = "Standard",
                UpdatedAtUtc = DateTime.UtcNow
            };

            await _repository.UpsertAsync(preferences, CancellationToken.None);

            OperatorPreferences result = await _repository.SetUiModeAsync(operatorId, "Simple", true, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal("Simple", result.UiMode);
            Assert.True(result.UiModeLocked);
            Assert.Equal("Dark", result.ThemeMode);
            Assert.Equal("de-DE", result.CultureName);

            OperatorPreferences? retrieved = await _repository.GetAsync(operatorId, CancellationToken.None);
            Assert.NotNull(retrieved);
            Assert.Equal("Dark", retrieved.ThemeMode);
            Assert.Equal("de-DE", retrieved.CultureName);
        }

        [Fact]
        public async Task SetUiModeAsync_InvalidMode_ThrowsArgumentException()
        {
            var operatorId = Guid.NewGuid();

            await Assert.ThrowsAsync<ArgumentException>(() =>
                _repository.SetUiModeAsync(operatorId, "Bogus", false, CancellationToken.None));

            OperatorPreferences? retrieved = await _repository.GetAsync(operatorId, CancellationToken.None);
            Assert.Null(retrieved);
        }

        [Fact]
        public async Task UpsertAsync_OnLockedRow_WithDifferentUiMode_ThrowsInvalidOperationException()
        {
            var operatorId = Guid.NewGuid();

            await _repository.SetUiModeAsync(operatorId, "Standard", true, CancellationToken.None);

            var updated = new OperatorPreferences
            {
                Id = Guid.NewGuid(),
                OperatorId = operatorId,
                ThemeMode = "Light",
                CultureName = "en-US",
                UiMode = "Simple",
                UpdatedAtUtc = DateTime.UtcNow
            };

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _repository.UpsertAsync(updated, CancellationToken.None));

            OperatorPreferences? retrieved = await _repository.GetAsync(operatorId, CancellationToken.None);
            Assert.NotNull(retrieved);
            Assert.Equal("Standard", retrieved.UiMode);
        }

        [Fact]
        public async Task UpsertAsync_OnLockedRow_WithSameUiMode_AllowsThemeChange()
        {
            var operatorId = Guid.NewGuid();

            await _repository.SetUiModeAsync(operatorId, "Standard", true, CancellationToken.None);

            var updated = new OperatorPreferences
            {
                Id = Guid.NewGuid(),
                OperatorId = operatorId,
                ThemeMode = "Light",
                CultureName = "en-US",
                UiMode = "Standard",
                UpdatedAtUtc = DateTime.UtcNow
            };

            await _repository.UpsertAsync(updated, CancellationToken.None);

            OperatorPreferences? retrieved = await _repository.GetAsync(operatorId, CancellationToken.None);
            Assert.NotNull(retrieved);
            Assert.Equal("Standard", retrieved.UiMode);
            Assert.Equal("Light", retrieved.ThemeMode);
            Assert.True(retrieved.UiModeLocked);
        }

        [Fact]
        public async Task UpsertAsync_OnUnlockedRow_IgnoresIncomingLockAttempt()
        {
            var operatorId = Guid.NewGuid();

            var preferences = new OperatorPreferences
            {
                Id = Guid.NewGuid(),
                OperatorId = operatorId,
                ThemeMode = "System",
                CultureName = null,
                UiMode = "Standard",
                UiModeLocked = false,
                UpdatedAtUtc = DateTime.UtcNow
            };

            await _repository.UpsertAsync(preferences, CancellationToken.None);

            var updated = new OperatorPreferences
            {
                Id = preferences.Id,
                OperatorId = operatorId,
                ThemeMode = "Light",
                CultureName = "en-US",
                UiMode = "Simple",
                UiModeLocked = true,
                UpdatedAtUtc = DateTime.UtcNow
            };

            await _repository.UpsertAsync(updated, CancellationToken.None);

            OperatorPreferences? retrieved = await _repository.GetAsync(operatorId, CancellationToken.None);
            Assert.NotNull(retrieved);
            Assert.False(retrieved.UiModeLocked);
        }

        [Fact]
        public async Task UpsertAsync_CreatingNewRow_IgnoresIncomingLock()
        {
            var operatorId = Guid.NewGuid();

            var preferences = new OperatorPreferences
            {
                Id = Guid.NewGuid(),
                OperatorId = operatorId,
                ThemeMode = "System",
                CultureName = null,
                UiMode = "Simple",
                UiModeLocked = true,
                UpdatedAtUtc = DateTime.UtcNow
            };

            await _repository.UpsertAsync(preferences, CancellationToken.None);

            OperatorPreferences? retrieved = await _repository.GetAsync(operatorId, CancellationToken.None);
            Assert.NotNull(retrieved);
            Assert.False(retrieved.UiModeLocked);
        }

        [Fact]
        public async Task SetUiModeAsync_UnlocksRow_AllowsSubsequentUpsert()
        {
            var operatorId = Guid.NewGuid();

            await _repository.SetUiModeAsync(operatorId, "Standard", true, CancellationToken.None);

            await _repository.SetUiModeAsync(operatorId, "Standard", false, CancellationToken.None);

            OperatorPreferences? retrieved = await _repository.GetAsync(operatorId, CancellationToken.None);
            Assert.NotNull(retrieved);
            Assert.False(retrieved.UiModeLocked);

            var updated = new OperatorPreferences
            {
                Id = retrieved.Id,
                OperatorId = operatorId,
                ThemeMode = "Dark",
                CultureName = "fr-FR",
                UiMode = "Simple",
                UpdatedAtUtc = DateTime.UtcNow
            };

            await _repository.UpsertAsync(updated, CancellationToken.None);

            OperatorPreferences? finalRetrieved = await _repository.GetAsync(operatorId, CancellationToken.None);
            Assert.NotNull(finalRetrieved);
            Assert.Equal("Simple", finalRetrieved.UiMode);
        }
    }
}
