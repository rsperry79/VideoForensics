using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using VideoForensics.Utils.Testing;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests
{
    /// <summary>Changes to typed settings rows are written to the setting-change log, as the older child tables are.</summary>
    public class DeviceTypedSettingsChangeTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private DeviceDetailsRepository _repository = null!;
        private Device _device = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            ILoggerFactory loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });
            _repository = new DeviceDetailsRepository(_fixture.Factory, loggerFactory.CreateLogger<DeviceDetailsRepository>());
            var locationRepository = new LocationRepository(_fixture.Factory, loggerFactory.CreateLogger<LocationRepository>());
            var deviceRepository = new DeviceRepository(_fixture.Factory, loggerFactory.CreateLogger<DeviceRepository>());
            Location location = TestDataBuilder.BuildLocation();
            await locationRepository.AddAsync(location, CancellationToken.None);
            _device = TestDataBuilder.BuildDevice(locationId: location.Id);
            await deviceRepository.AddAsync(_device, CancellationToken.None);
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        [Fact]
        public async Task UpsertAsync_DetectionTypeFlagChanged_RecordsChangeRow()
        {
            await WriteAsync(types: [Type("human", enabled: true)]);

            await WriteAsync(types: [Type("human", enabled: false)]);

            DeviceSettingChange change = await SingleChangeAsync("DetectionType[human]");
            Assert.Contains("enabled=True", change.OldValue);
            Assert.Contains("enabled=False", change.NewValue);
        }

        [Fact]
        public async Task UpsertAsync_ThresholdChanged_RecordsChangeRow()
        {
            await WriteAsync(thresholds: [Threshold("loitering", 10.0)]);

            await WriteAsync(thresholds: [Threshold("loitering", 12.5)]);

            DeviceSettingChange change = await SingleChangeAsync("DetectionThreshold[loitering]");
            Assert.Equal("10", change.OldValue);
            Assert.Equal("12.5", change.NewValue);
        }

        [Fact]
        public async Task UpsertAsync_OfflineMotionChanged_RecordsChangeRow()
        {
            await WriteAsync(offline: [new DeviceOfflineMotionSettings { Id = Guid.NewGuid(), MaxUploadKb = 5000 }]);

            await WriteAsync(offline: [new DeviceOfflineMotionSettings { Id = Guid.NewGuid(), MaxUploadKb = 8000 }]);

            DeviceSettingChange change = await SingleChangeAsync("OfflineMotion[settings]");
            Assert.Contains("maxUploadKb=5000", change.OldValue);
            Assert.Contains("maxUploadKb=8000", change.NewValue);
        }

        [Fact]
        public async Task UpsertAsync_FirstPopulation_RecordsNoChangeRows()
        {
            await WriteAsync(types: [Type("human", enabled: true)], thresholds: [Threshold("loitering", 10.0)]);

            await using VideoForensicsDbContext db = _fixture.Factory.CreateDbContext();
            Assert.Empty(await db.DeviceSettingChanges.Where(c => c.FieldName.StartsWith("DetectionType") || c.FieldName.StartsWith("DetectionThreshold")).ToListAsync());
        }

        private Task WriteAsync(
            IReadOnlyList<DeviceOfflineMotionSettings>? offline = null,
            IReadOnlyList<DeviceDetectionType>? types = null,
            IReadOnlyList<DeviceDetectionThreshold>? thresholds = null)
        {
            return _repository.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None,
                new DeviceSettingsChildren(MotionSnoozePresets: [], LiveViewPresets: [], AdvancedMotionZones: [], OfflineMotion: offline, DetectionTypes: types, DetectionThresholds: thresholds));
        }

        private async Task<DeviceSettingChange> SingleChangeAsync(string fieldName)
        {
            await using VideoForensicsDbContext db = _fixture.Factory.CreateDbContext();
            return await db.DeviceSettingChanges.SingleAsync(c => c.FieldName == fieldName);
        }

        private static DeviceDetectionType Type(string name, bool enabled)
        {
            return new() { Id = Guid.NewGuid(), DetectionType = name, Enabled = enabled, Mode = "cloud" };
        }

        private static DeviceDetectionThreshold Threshold(string name, double value)
        {
            return new() { Id = Guid.NewGuid(), DetectionType = name, Threshold = value };
        }
    }
}
