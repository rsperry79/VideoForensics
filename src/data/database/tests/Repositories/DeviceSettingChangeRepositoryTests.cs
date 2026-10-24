using Microsoft.Extensions.Logging;

using VideoForensics.Utils.Testing;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests
{
    public class DeviceSettingChangeRepositoryTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private DeviceSettingChangeRepository _repository = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            ILoggerFactory loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });
            _repository = new DeviceSettingChangeRepository(_fixture.Factory, loggerFactory.CreateLogger<DeviceSettingChangeRepository>());
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        private static DeviceSettingChange BuildChange(Guid deviceId, string field, DateTime changedAtUtc)
        {
            return new()
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                FieldName = field,
                OldValue = "1",
                NewValue = "2",
                ChangedAtUtc = changedAtUtc
            };
        }

        [Fact]
        public async Task DeviceSettingChangeRepository_ListByDeviceAsync_ReturnsOnlyThatDeviceNewestFirst()
        {
            var deviceA = Guid.NewGuid();
            var deviceB = Guid.NewGuid();
            DateTime now = DateTime.UtcNow;

            await _repository.AddRangeAsync(
            [
                BuildChange(deviceA, "LightBrightness", now.AddMinutes(-5)),
                BuildChange(deviceA, "DoorbellVolume", now),
                BuildChange(deviceB, "LightBrightness", now)
            ], CancellationToken.None);

            IReadOnlyList<DeviceSettingChange> rows = await _repository.ListByDeviceAsync(deviceA, CancellationToken.None);

            Assert.Equal(2, rows.Count);
            Assert.All(rows, r => Assert.Equal(deviceA, r.DeviceId));
            Assert.Equal("DoorbellVolume", rows[0].FieldName);
            Assert.Equal("LightBrightness", rows[1].FieldName);
        }

        [Fact]
        public async Task DeviceSettingChangeRepository_AddRangeAsync_PersistsNullOldValue()
        {
            var deviceId = Guid.NewGuid();
            DeviceSettingChange change = BuildChange(deviceId, "Address", DateTime.UtcNow);
            change.OldValue = null;

            await _repository.AddRangeAsync([change], CancellationToken.None);
            IReadOnlyList<DeviceSettingChange> rows = await _repository.ListByDeviceAsync(deviceId, CancellationToken.None);

            DeviceSettingChange stored = Assert.Single(rows);
            Assert.Null(stored.OldValue);
            Assert.Equal("2", stored.NewValue);
        }
    }
}
