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
    /// <summary>Light events: an Observed row is written only on an off-to-on transition; Command rows are appended.</summary>
    public class DeviceLightEventRepositoryTests : IAsyncLifetime
    {
        private static readonly DateTime T0 = new(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);

        private SqliteInMemoryFixture _fixture = null!;
        private DeviceDetailsRepository _details = null!;
        private DeviceLightEventRepository _events = null!;
        private Device _device = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            ILoggerFactory loggerFactory = LoggerFactory.Create(b => { });
            _details = new DeviceDetailsRepository(_fixture.Factory, loggerFactory.CreateLogger<DeviceDetailsRepository>());
            _events = new DeviceLightEventRepository(_fixture.Factory, loggerFactory.CreateLogger<DeviceLightEventRepository>());
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

        private Task<bool> Observe(int? seconds, DateTime at) => _events.RecordObservedAsync(_device.Id, seconds, at, CancellationToken.None);

        [Fact]
        public async Task RecordObservedAsync_OffToOn_InsertsObservedEvent()
        {
            Assert.False(await Observe(0, T0));
            Assert.True(await Observe(20, T0.AddMinutes(1)));

            DeviceLightEvent e = Assert.Single(await _events.ListByDeviceAsync(_device.Id, CancellationToken.None));
            Assert.Equal(DeviceLightEventSource.Observed, e.Source);
            Assert.True(e.IsOn);
            Assert.Equal(20, e.SecondsRemaining);
            Assert.Equal(T0.AddMinutes(1), e.OccurredAtUtc);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(null)]
        public async Task RecordObservedAsync_OffOrUnknown_InsertsNothing(int? seconds)
        {
            Assert.False(await Observe(seconds, T0));

            Assert.Empty(await _events.ListByDeviceAsync(_device.Id, CancellationToken.None));
        }

        [Fact]
        public async Task RecordObservedAsync_StillOn_DoesNotInsertAgain()
        {
            _ = await Observe(60, T0);
            Assert.False(await Observe(40, T0.AddSeconds(20)));
            Assert.False(await Observe(10, T0.AddSeconds(50)));

            Assert.Single(await _events.ListByDeviceAsync(_device.Id, CancellationToken.None));
        }

        [Fact]
        public async Task RecordObservedAsync_OnThenOffThenOn_InsertsAgainAfterWindowEnds()
        {
            _ = await Observe(20, T0);
            Assert.False(await Observe(0, T0.AddSeconds(30)));
            Assert.True(await Observe(20, T0.AddSeconds(60)));

            Assert.Equal(2, (await _events.ListByDeviceAsync(_device.Id, CancellationToken.None)).Count);
        }

        [Fact]
        public async Task RecordObservedAsync_CommandWithUnknownDuration_DoesNotMaskLaterObservedOn()
        {
            await _events.AddCommandAsync(_device.Id, T0, null, CancellationToken.None);

            Assert.True(await Observe(30, T0.AddSeconds(5)));
        }

        private Task<bool> ObserveState(bool on, DateTime at) => _events.RecordObservedStateAsync(_device.Id, on, at, CancellationToken.None);

        [Fact]
        public async Task RecordObservedStateAsync_OffToOn_InsertsOneEventWithNullSeconds()
        {
            Assert.False(await ObserveState(false, T0));
            Assert.True(await ObserveState(true, T0.AddMinutes(1)));

            DeviceLightEvent e = Assert.Single(await _events.ListByDeviceAsync(_device.Id, CancellationToken.None));
            Assert.Equal(DeviceLightEventSource.Observed, e.Source);
            Assert.Null(e.SecondsRemaining);
        }

        [Fact]
        public async Task RecordObservedStateAsync_OnTwice_InsertsOnlyOnce()
        {
            Assert.True(await ObserveState(true, T0));
            Assert.False(await ObserveState(true, T0.AddMinutes(5)));

            Assert.Single(await _events.ListByDeviceAsync(_device.Id, CancellationToken.None));
        }

        [Fact]
        public async Task RecordObservedStateAsync_OnOffOn_InsertsOnAgainAfterOff()
        {
            Assert.True(await ObserveState(true, T0));
            Assert.True(await ObserveState(false, T0.AddMinutes(1)));
            Assert.True(await ObserveState(true, T0.AddMinutes(2)));

            List<DeviceLightEvent> all = [.. await _events.ListByDeviceAsync(_device.Id, CancellationToken.None)];
            Assert.Equal(3, all.Count);
            Assert.Equal(2, all.Count(e => e.IsOn));
            Assert.Equal(1, all.Count(e => !e.IsOn && e.SecondsRemaining == null));
            Assert.Equal(2, (await _events.ListByDeviceAsync(_device.Id, CancellationToken.None, onEventsOnly: true)).Count);
        }

        [Fact]
        public async Task RecordObservedStateAsync_OffWhenAlreadyOff_InsertsNothing()
        {
            Assert.False(await ObserveState(false, T0));

            Assert.Empty(await _events.ListByDeviceAsync(_device.Id, CancellationToken.None));
        }

        [Fact]
        public async Task RecordObservedStateAsync_AfterCommand_StillInsertsObservedOn()
        {
            await _events.AddCommandAsync(_device.Id, T0, null, CancellationToken.None);

            Assert.True(await ObserveState(true, T0.AddSeconds(5)));
        }

        [Fact]
        public async Task UpsertAsync_LightIsOnInChildren_FollowsTransitionRules()
        {
            DeviceSettingsChildren With(bool on) => new([], [], [], LightIsOn: on);

            await _details.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None, With(true));
            await _details.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None, With(true));

            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync();
            Assert.Equal(1, await db.DeviceLightEvents.CountAsync(x => x.DeviceId == _device.Id));
        }

        [Fact]
        public async Task AddCommandAsync_StoresCommandEventWithNullSeconds()
        {
            await _events.AddCommandAsync(_device.Id, T0, null, CancellationToken.None);

            DeviceLightEvent e = Assert.Single(await _events.ListByDeviceAsync(_device.Id, CancellationToken.None));
            Assert.Equal(DeviceLightEventSource.Command, e.Source);
            Assert.True(e.IsOn);
            Assert.Null(e.SecondsRemaining);
        }

        [Fact]
        public async Task UpsertAsync_LightSecondsInChildren_FollowTransitionRules()
        {
            DeviceSettingsChildren With(int seconds) => new([], [], [], LightSecondsRemaining: seconds);

            await _details.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None, With(0));
            await _details.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None, With(20));
            await _details.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None, With(15));
            await _details.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None, new DeviceSettingsChildren([], [], []));

            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync();
            DeviceLightEvent e = await db.DeviceLightEvents.SingleAsync(x => x.DeviceId == _device.Id);
            Assert.Equal(20, e.SecondsRemaining);
        }

        [Fact]
        public async Task DeleteDevice_CascadesToLightEvents()
        {
            _ = await Observe(20, T0);
            await using (VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync())
            {
                _ = await db.Devices.Where(d => d.Id == _device.Id).ExecuteDeleteAsync();
            }

            await using VideoForensicsDbContext check = await _fixture.Factory.CreateDbContextAsync();
            Assert.Equal(0, await check.DeviceLightEvents.CountAsync());
        }
    }
}
