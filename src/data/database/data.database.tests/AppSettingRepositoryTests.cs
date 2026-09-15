using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests
{
    public class AppSettingRepositoryTests : IClassFixture<SqliteInMemoryFixture>
    {
        private readonly SqliteInMemoryFixture _fixture;

        public AppSettingRepositoryTests(SqliteInMemoryFixture fixture)
        {
            _fixture = fixture;
        }

        private async Task ClearSettingsAsync()
        {
            var repository = new AppSettingRepository(_fixture.Factory);
            await repository.ClearAllAsync(CancellationToken.None);
        }

        [Fact]
        public async Task SetAsync_CreatesNewSetting()
        {
            await ClearSettingsAsync();
            var repository = new AppSettingRepository(_fixture.Factory);

            await repository.SetAsync("TestKey", "TestValue", CancellationToken.None);

            string? value = await repository.GetAsync("TestKey", CancellationToken.None);
            Assert.Equal("TestValue", value);
        }

        [Fact]
        public async Task SetAsync_UpdatesExistingSetting()
        {
            await ClearSettingsAsync();
            var repository = new AppSettingRepository(_fixture.Factory);

            await repository.SetAsync("TestKey", "Value1", CancellationToken.None);
            await repository.SetAsync("TestKey", "Value2", CancellationToken.None);

            string? value = await repository.GetAsync("TestKey", CancellationToken.None);
            Assert.Equal("Value2", value);
        }

        [Fact]
        public async Task GetAsync_ReturnsNullForMissingSetting()
        {
            await ClearSettingsAsync();
            var repository = new AppSettingRepository(_fixture.Factory);

            string? value = await repository.GetAsync("NonExistent", CancellationToken.None);
            Assert.Null(value);
        }

        [Fact]
        public async Task ListAsync_ReturnsAllSettings()
        {
            await ClearSettingsAsync();
            var repository = new AppSettingRepository(_fixture.Factory);

            await repository.SetAsync("Key1", "Value1", CancellationToken.None);
            await repository.SetAsync("Key2", "Value2", CancellationToken.None);
            await repository.SetAsync("Key3", "Value3", CancellationToken.None);

            IReadOnlyList<AppSetting> settings = await repository.ListAsync(CancellationToken.None);
            Assert.Equal(3, settings.Count);
            Assert.Contains(settings, s => s.Key == "Key1" && s.Value == "Value1");
            Assert.Contains(settings, s => s.Key == "Key2" && s.Value == "Value2");
            Assert.Contains(settings, s => s.Key == "Key3" && s.Value == "Value3");
        }

        [Fact]
        public async Task DeleteAsync_RemovesSetting()
        {
            await ClearSettingsAsync();
            var repository = new AppSettingRepository(_fixture.Factory);

            await repository.SetAsync("TestKey", "TestValue", CancellationToken.None);
            await repository.DeleteAsync("TestKey", CancellationToken.None);

            string? value = await repository.GetAsync("TestKey", CancellationToken.None);
            Assert.Null(value);
        }

        [Fact]
        public async Task DeleteAsync_IgnoresMissingSettings()
        {
            await ClearSettingsAsync();
            var repository = new AppSettingRepository(_fixture.Factory);

            await repository.DeleteAsync("NonExistent", CancellationToken.None);
            // Should not throw
        }

        [Fact]
        public async Task ClearAllAsync_RemovesAllSettings()
        {
            await ClearSettingsAsync();
            var repository = new AppSettingRepository(_fixture.Factory);

            await repository.SetAsync("Key1", "Value1", CancellationToken.None);
            await repository.SetAsync("Key2", "Value2", CancellationToken.None);
            await repository.ClearAllAsync(CancellationToken.None);

            IReadOnlyList<AppSetting> settings = await repository.ListAsync(CancellationToken.None);
            Assert.Empty(settings);
        }

        [Fact]
        public async Task UpdatedAtUtc_IsSetOnCreate()
        {
            await ClearSettingsAsync();
            var repository = new AppSettingRepository(_fixture.Factory);
            DateTime beforeTime = DateTime.UtcNow;

            await repository.SetAsync("TestKey", "TestValue", CancellationToken.None);

            IReadOnlyList<AppSetting> settings = await repository.ListAsync(CancellationToken.None);
            AppSetting setting = Assert.Single(settings);
            Assert.True(setting.UpdatedAtUtc >= beforeTime);
            Assert.True(setting.UpdatedAtUtc <= DateTime.UtcNow.AddSeconds(1));
        }

        [Fact]
        public async Task UpdatedAtUtc_IsUpdatedOnModify()
        {
            await ClearSettingsAsync();
            var repository = new AppSettingRepository(_fixture.Factory);

            await repository.SetAsync("TestKey", "Value1", CancellationToken.None);
            IReadOnlyList<AppSetting> settings1 = await repository.ListAsync(CancellationToken.None);
            DateTime updatedAt1 = settings1.First().UpdatedAtUtc;

            await Task.Delay(100); // Ensure time passes

            await repository.SetAsync("TestKey", "Value2", CancellationToken.None);
            IReadOnlyList<AppSetting> settings2 = await repository.ListAsync(CancellationToken.None);
            DateTime updatedAt2 = settings2.First().UpdatedAtUtc;

            Assert.True(updatedAt2 > updatedAt1);
        }

        [Fact]
        public async Task SetAsync_SameValue_DoesNotBumpUpdatedAtUtc()
        {
            await ClearSettingsAsync();
            var repository = new AppSettingRepository(_fixture.Factory);

            await repository.SetAsync("TestKey", "Value1", CancellationToken.None);
            IReadOnlyList<AppSetting> settings1 = await repository.ListAsync(CancellationToken.None);
            DateTime updatedAt1 = settings1.First().UpdatedAtUtc;

            await Task.Delay(50);

            // Re-setting the same value should be a no-op - no write, no UpdatedAtUtc change.
            await repository.SetAsync("TestKey", "Value1", CancellationToken.None);
            IReadOnlyList<AppSetting> settings2 = await repository.ListAsync(CancellationToken.None);

            _ = Assert.Single(settings2);
            Assert.Equal(updatedAt1, settings2.First().UpdatedAtUtc);
        }
    }
}
