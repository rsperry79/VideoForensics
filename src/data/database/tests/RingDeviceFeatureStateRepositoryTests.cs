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
    /// <summary>Round-trips device feature scalars, feature states and their reason rows through SQLite.</summary>
    public class RingDeviceFeatureStateRepositoryTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private DeviceDetailsRepository _repository = null!;
        private Device _device = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            ILoggerFactory loggerFactory = LoggerFactory.Create(b => { });
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

        private static DeviceSettingsChildren Children(params RingDeviceFeatureState[] states)
        {
            return new DeviceSettingsChildren([], [], [], FeatureStates: states);
        }

        private static RingDeviceFeatureState State(string key, bool enabled, params string[] disallow)
        {
            return new RingDeviceFeatureState
            {
                FeatureKey = key,
                Enabled = enabled,
                Allowed = false,
                MaxDigitalZoomLevel = 6.0,
                Score = "best",
                RecordingState = "active",
                ContinuousSupported = false,
                Reasons = disallow.Select((r, i) => new RingDeviceFeatureReason { Kind = RingDeviceFeatureReason.Disallow, Position = i, Reason = r }).ToList()
            };
        }

        [Fact]
        public async Task UpsertAsync_FeatureStates_WritesStatesAndReasons()
        {
            await _repository.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None,
                Children(State("smart_video_search", false, "not_subscribed", "ineligible")));

            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync();
            RingDeviceFeatureState stored = await db.RingDeviceFeatureStates.Include(s => s.Reasons).SingleAsync(s => s.DeviceId == _device.Id);
            Assert.Equal("smart_video_search", stored.FeatureKey);
            Assert.False(stored.Enabled);
            Assert.Equal(6.0, stored.MaxDigitalZoomLevel);
            Assert.Equal("best", stored.Score);
            Assert.Equal("active", stored.RecordingState);
            Assert.Equal(["not_subscribed", "ineligible"], stored.Reasons.OrderBy(r => r.Position).Select(r => r.Reason));
            Assert.All(stored.Reasons, r => Assert.Equal(stored.Id, r.FeatureStateId));
        }

        [Fact]
        public async Task UpsertAsync_FeatureStatesSecondCall_ReplacesRowsAndReasonsWithoutDuplicates()
        {
            await _repository.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None,
                Children(State("smart_video_search", false, "a"), State("auto_track", false)));
            await _repository.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None,
                Children(State("smart_video_search", true, "b")));

            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync();
            RingDeviceFeatureState stored = await db.RingDeviceFeatureStates.Include(s => s.Reasons).SingleAsync(s => s.DeviceId == _device.Id);
            Assert.True(stored.Enabled);
            Assert.Equal(["b"], stored.Reasons.Select(r => r.Reason));
            Assert.Equal(1, await db.RingDeviceFeatureReasons.CountAsync());
        }

        [Fact]
        public async Task UpsertAsync_NullFeatureStates_LeavesStoredRowsUntouched()
        {
            await _repository.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None, Children(State("auto_track", false)));
            await _repository.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None, new DeviceSettingsChildren([], [], []));

            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync();
            Assert.Equal(1, await db.RingDeviceFeatureStates.CountAsync());
        }

        [Fact]
        public async Task UpsertAsync_EmptyFeatureStates_ClearsStoredRows()
        {
            await _repository.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None, Children(State("auto_track", false, "x")));
            await _repository.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None, Children());

            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync();
            Assert.Equal(0, await db.RingDeviceFeatureStates.CountAsync());
            Assert.Equal(0, await db.RingDeviceFeatureReasons.CountAsync());
        }

        [Fact]
        public async Task UpsertAsync_FeatureScalars_InsertAndUpdateColumns()
        {
            var first = new DeviceFeatures { Id = Guid.NewGuid(), DeviceId = _device.Id, CfesEligible = true, Show24x7Lite = true, PackageWarning = null };
            await _repository.UpsertAsync(_device.Id, null, null, first, null, CancellationToken.None);
            var second = new DeviceFeatures { Id = Guid.NewGuid(), DeviceId = _device.Id, CfesEligible = false, VehicleDetectionEligible = true };
            await _repository.UpsertAsync(_device.Id, null, null, second, null, CancellationToken.None);

            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync();
            DeviceFeatures stored = await db.DeviceFeatures.SingleAsync(f => f.DeviceId == _device.Id);
            Assert.False(stored.CfesEligible);
            Assert.Null(stored.Show24x7Lite);
            Assert.True(stored.VehicleDetectionEligible);
        }
    }
}
