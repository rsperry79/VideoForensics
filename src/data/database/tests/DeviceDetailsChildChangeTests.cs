using Microsoft.EntityFrameworkCore;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests
{
    /// <summary>Records change history for settings child collections when UpsertAsync receives the settings children.</summary>
    public class DeviceDetailsChildChangeTests : RepositoryTestBase
    {
        private DeviceDetailsRepository _repository = null!;
        private Device _device = null!;

        public override async ValueTask InitializeAsync()
        {
            await base.InitializeAsync();
            _repository = new DeviceDetailsRepository(Fixture.Factory, CreateLogger<DeviceDetailsRepository>());

            var locationRepository = new LocationRepository(Fixture.Factory, CreateLogger<LocationRepository>());
            var deviceRepository = new DeviceRepository(Fixture.Factory, CreateLogger<DeviceRepository>());
            Location location = TestDataBuilder.BuildLocation();
            await locationRepository.AddAsync(location, CancellationToken.None);
            _device = TestDataBuilder.BuildDevice(locationId: location.Id);
            await deviceRepository.AddAsync(_device, CancellationToken.None);
        }

        private static DeviceSettingsChildren Children(
            Guid deviceId,
            IReadOnlyList<string>? snooze = null,
            IReadOnlyList<DeviceZone>? zones = null,
            IReadOnlyList<DeviceAdvancedObjectSetting>? objects = null)
        {
            var snoozeRows = (snooze ?? [])
                .Select((p, i) => new DeviceMotionSnoozePreset { Id = Guid.NewGuid(), DeviceId = deviceId, Position = i, Preset = p })
                .ToList();
            return new DeviceSettingsChildren(snoozeRows, [], [], zones, objects);
        }

        private static DeviceZone Zone(Guid deviceId, string zoneId, string name, params string[] detectionTypes)
        {
            return new()
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                Source = "ring",
                ZoneId = zoneId,
                Name = name,
                State = 1,
                Style = "solid",
                DetectionTypes = detectionTypes
                .Select((t, i) => new DeviceZoneDetectionType { Id = Guid.NewGuid(), Position = i, DetectionType = t })
                .ToList(),
                Vertices = [new DeviceZoneVertex { Id = Guid.NewGuid(), Position = 0, X = 0.1, Y = 0.2 }]
            };
        }

        private async Task<List<DeviceSettingChange>> ChangesAsync()
        {
            await using VideoForensicsDbContext db = await Fixture.Factory.CreateDbContextAsync();
            return await db.DeviceSettingChanges.Where(c => c.DeviceId == _device.Id).ToListAsync();
        }

        [Fact]
        public async Task DeviceDetailsChildChange_SnoozePresetChanged_RecordsOneRow()
        {
            await _repository.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None, Children(_device.Id, ["5 min", "1 hour"]));
            await _repository.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None, Children(_device.Id, ["5 min", "2 hours"]));

            DeviceSettingChange change = Assert.Single(await ChangesAsync());
            Assert.Equal("MotionSnoozePreset[1]", change.FieldName);
            Assert.Equal("1 hour", change.OldValue);
            Assert.Equal("2 hours", change.NewValue);
        }

        [Fact]
        public async Task DeviceDetailsChildChange_ZoneRemoved_RecordsRemovalRow()
        {
            await _repository.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None,
                Children(_device.Id, zones: [Zone(_device.Id, "z1", "Front Door", "person")]));
            await _repository.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None,
                Children(_device.Id, zones: []));

            DeviceSettingChange change = Assert.Single(await ChangesAsync());
            Assert.Equal("Zone[z1]", change.FieldName);
            Assert.Contains("Front Door", change.OldValue);
            Assert.Null(change.NewValue);
        }

        [Fact]
        public async Task DeviceDetailsChildChange_FirstPopulation_RecordsNothing()
        {
            await _repository.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None,
                Children(_device.Id, ["5 min"], zones: [Zone(_device.Id, "z1", "Front Door", "person")],
                    objects: [new DeviceAdvancedObjectSetting { Id = Guid.NewGuid(), DeviceId = _device.Id, SettingKey = "sensitivity", Day = 0.5, Night = 0.7 }]));

            Assert.Empty(await ChangesAsync());
        }

        [Fact]
        public async Task DeviceDetailsChildChange_NullSettings_RecordsNothing()
        {
            await _repository.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None, Children(_device.Id, ["5 min"]));
            await _repository.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None, settings: null);

            Assert.Empty(await ChangesAsync());
            await using VideoForensicsDbContext db = await Fixture.Factory.CreateDbContextAsync();
            _ = Assert.Single(await db.DeviceMotionSnoozePresets.Where(p => p.DeviceId == _device.Id).ToListAsync());
        }

        [Fact]
        public async Task DeviceDetailsChildChange_UnchangedChildren_RecordsNothing()
        {
            await _repository.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None,
                Children(_device.Id, ["5 min"], zones: [Zone(_device.Id, "z1", "Front Door", "person")]));
            await _repository.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None,
                Children(_device.Id, ["5 min"], zones: [Zone(_device.Id, "z1", "Front Door", "person")]));

            Assert.Empty(await ChangesAsync());
        }
        [Fact]
        public async Task DeviceDetailsChildChange_ZoneDetectionTypesChanged_RecordsPropertyRow()
        {
            await _repository.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None,
                Children(_device.Id, zones: [Zone(_device.Id, "z1", "Front Door", "person")]));
            await _repository.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None,
                Children(_device.Id, zones: [Zone(_device.Id, "z1", "Front Door", "person", "vehicle")]));

            DeviceSettingChange change = Assert.Single(await ChangesAsync());
            Assert.Equal("Zone[z1].DetectionTypes", change.FieldName);
            Assert.Equal("person", change.OldValue);
            Assert.Equal("person,vehicle", change.NewValue);
        }

        [Fact]
        public async Task DeviceDetailsChildChange_ObjectSettingChanged_RecordsDayNightText()
        {
            await _repository.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None,
                Children(_device.Id, objects: [new DeviceAdvancedObjectSetting { Id = Guid.NewGuid(), DeviceId = _device.Id, SettingKey = "sensitivity", Day = 0.5, Night = 0.7 }]));
            await _repository.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None,
                Children(_device.Id, objects: [new DeviceAdvancedObjectSetting { Id = Guid.NewGuid(), DeviceId = _device.Id, SettingKey = "sensitivity", Day = 0.5, Night = 0.9 }]));

            DeviceSettingChange change = Assert.Single(await ChangesAsync());
            Assert.Equal("AdvancedObjectSetting[sensitivity]", change.FieldName);
            Assert.Equal("day=0.5|night=0.7", change.OldValue);
            Assert.Equal("day=0.5|night=0.9", change.NewValue);
        }
    }
}
