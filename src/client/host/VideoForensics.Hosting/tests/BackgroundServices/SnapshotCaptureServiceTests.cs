using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using System.Globalization;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Services;
using VideoForensics.Client.Hosting.BackgroundServices;
using VideoForensics.Providers.Ring.Exceptions;
using VideoForensics.Providers.Ring.Services;

using Device = VideoForensics.Data.Common.Entities.Device;
using Location = VideoForensics.Data.Common.Entities.Location;

namespace VideoForensics.Client.Hosting.Tests.BackgroundServices
{
    public class SnapshotCaptureServiceTests : IDisposable
    {
        private static readonly DateTimeOffset T0 = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);
        private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x01, 0x02, 0x03];

        private readonly string _root = Path.Combine(Path.GetTempPath(), "vf-snapshot-capture-" + Guid.NewGuid().ToString("N"));
        private readonly Guid _accountId = Guid.NewGuid();
        private readonly Guid _locationId = Guid.NewGuid();
        private readonly MutableTimeProvider _time = new(T0);
        private readonly Mock<IProviderAccountRepository> _accounts = new();
        private readonly Mock<ISyncScheduleRepository> _schedules = new();
        private readonly Mock<ILocationRepository> _locations = new();
        private readonly Mock<IDeviceRepository> _devices = new();
        private readonly Mock<ISnapshotCaptureRepository> _captureRows = new();
        private readonly Mock<IMediaItemRepository> _media = new();
        private readonly Mock<IProviderSnapshotCapture> _capture = new();
        private readonly Mock<IProviderApiBudgetGuard> _budget = new();
        private readonly Mock<IProviderAuthService> _auth = new();
        private readonly Mock<ISessionProvider> _sessions = new();
        private readonly List<SnapshotCapture> _rows = [];

        // The provider's offline rule, resolved keyed by provider name. Tests can swap it before building the provider.
        private IProviderDeviceStatus _deviceStatus = new RingDeviceOnlineStatus();

        public SnapshotCaptureServiceTests()
        {
            _ = _accounts.Setup(r => r.ListActiveAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([new ProviderAccount { Id = _accountId, ProviderName = "Ring", IsActive = true }]);
            _ = _schedules.Setup(r => r.GetByProviderAccountIdAsync(_accountId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((SyncSchedule?)null);
            _ = _locations.Setup(r => r.ListByProviderAccountIdAsync(_accountId, It.IsAny<CancellationToken>()))
                .ReturnsAsync([new Location { Id = _locationId, ProviderLocationId = "loc", Name = "Home" }]);
            _ = _auth.Setup(a => a.RestoreFromSavedCredentialsAsync(_accountId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _ = _budget.Setup(b => b.TryConsumeAsync("Ring", It.IsAny<CancellationToken>())).ReturnsAsync(true);

            _ = _captureRows.Setup(r => r.AddAsync(It.IsAny<SnapshotCapture>(), It.IsAny<CancellationToken>()))
                .Callback<SnapshotCapture, CancellationToken>((c, _) => _rows.Add(c))
                .ReturnsAsync((SnapshotCapture c, CancellationToken _) => c);
            _ = _captureRows.Setup(r => r.GetAttemptsForDeviceAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid deviceId, DateTimeOffset from, DateTimeOffset to, CancellationToken _) =>
                    _rows.Where(r => r.DeviceId == deviceId && r.CapturedAtUtc >= from && r.CapturedAtUtc <= to).ToList());
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }

        private sealed class MutableTimeProvider(DateTimeOffset start) : TimeProvider
        {
            public DateTimeOffset Now { get; set; } = start;

            public override DateTimeOffset GetUtcNow()
            {
                return Now;
            }
        }

        private Device AddDevice(string providerDeviceId, bool captureEnabled = true, string type = "doorbot", bool? isOnline = null)
        {
            var device = new Device
            {
                Id = Guid.NewGuid(),
                LocationId = _locationId,
                ProviderDeviceId = providerDeviceId,
                Name = "Front Door " + providerDeviceId,
                Type = type,
                IsOnline = isOnline,
                SnapshotCaptureEnabled = captureEnabled,
            };
            _devicesList.Add(device);
            _ = _devices.Setup(r => r.GetByLocationIdAsync(_locationId, It.IsAny<CancellationToken>())).ReturnsAsync(_devicesList.ToList());
            return device;
        }

        private readonly List<Device> _devicesList = [];

        private async Task RunTickAsync()
        {
            ServiceProvider provider = BuildProvider();
            try
            {
                var service = new SnapshotCaptureService(
                    provider.GetRequiredService<IServiceScopeFactory>(),
                    Options.Create(new SnapshotTimelineOptions { SnapshotRoot = _root }),
                    NullLogger<SnapshotCaptureService>.Instance,
                    _time);
                await service.RunOneTickAsync(CancellationToken.None);
            }
            finally
            {
                await provider.DisposeAsync();
            }
        }

        private ServiceProvider BuildProvider()
        {
            var services = new ServiceCollection();
            _ = services.AddSingleton(_accounts.Object);
            _ = services.AddSingleton(_schedules.Object);
            _ = services.AddSingleton(_locations.Object);
            _ = services.AddSingleton(_devices.Object);
            _ = services.AddSingleton(_captureRows.Object);
            _ = services.AddSingleton(_media.Object);
            _ = services.AddSingleton(_sessions.Object);
            _ = services.AddKeyedSingleton<IProviderSnapshotCapture>("Ring", _capture.Object);
            _ = services.AddKeyedSingleton<IProviderAuthService>("Ring", _auth.Object);
            _ = services.AddKeyedSingleton<IProviderDeviceStatus>("Ring", _deviceStatus);
            _ = services.AddSingleton(_budget.Object);
            return services.BuildServiceProvider();
        }

        private static string ExpectedFileName(DateTimeOffset at)
        {
            return at.UtcDateTime.ToString("yyyyMMdd'T'HHmmssfff'Z'", CultureInfo.InvariantCulture) + ".jpg";
        }

        [Fact]
        public async Task RunOneTickAsync_DisabledDevice_SkipsDevice()
        {
            _ = AddDevice("12345", captureEnabled: false);
            _ = _capture.Setup(c => c.CaptureAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SnapshotCaptureAttempt(200, Jpeg, null));

            await RunTickAsync();

            _capture.Verify(c => c.CaptureAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Never);
            Assert.Empty(_rows);
        }

        [Fact]
        public async Task RunOneTickAsync_RingThrottled_PausesAccountUntilTheBlockLiftsWithoutErrorLogs()
        {
            // A hard ban stops every request until BlockedUntilUtc. The account must stay paused across ticks and the
            // ban must be logged as a warning, not an error with a stack trace on every device every tick.
            _ = AddDevice("12345");
            _ = _capture.Setup(c => c.CaptureAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ThrottledException("Ring has rate-limited this account.", isHardBan: true)
                {
                    BlockedUntilUtc = T0.AddHours(2).UtcDateTime,
                });
            ServiceProvider provider = BuildProvider();
            var logger = new Mock<ILogger<SnapshotCaptureService>>();
            try
            {
                var service = new SnapshotCaptureService(
                    provider.GetRequiredService<IServiceScopeFactory>(),
                    Options.Create(new SnapshotTimelineOptions { SnapshotRoot = _root }),
                    logger.Object,
                    _time);

                await service.RunOneTickAsync(CancellationToken.None);
                _time.Now = T0.AddMinutes(5);
                await service.RunOneTickAsync(CancellationToken.None);
            }
            finally
            {
                await provider.DisposeAsync();
            }

            _capture.Verify(c => c.CaptureAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
            Assert.DoesNotContain(logger.Invocations, i => i.Method.Name == nameof(ILogger.Log) && Equals(i.Arguments[0], LogLevel.Error));
        }


        [Fact]
        public async Task RunOneTickAsync_OfflineDevice_SkipsDevice()
        {
            _ = AddDevice("12345", isOnline: false);
            _ = _capture.Setup(c => c.CaptureAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SnapshotCaptureAttempt(200, Jpeg, null));

            await RunTickAsync();

            _capture.Verify(c => c.CaptureAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Never);
            Assert.Empty(_rows);
        }

        [Fact]
        public async Task RunOneTickAsync_ProviderRuleSaysOffline_SkipsDeviceEvenWhenConnectionReadsOnline()
        {
            // The capture service must ask the provider rather than trust the stored IsOnline column.
            var status = new Mock<IProviderDeviceStatus>();
            _ = status.Setup(s => s.IsOffline(It.IsAny<string>(), It.IsAny<bool?>())).Returns(true);
            _deviceStatus = status.Object;
            _ = AddDevice("12345", isOnline: true);
            _ = _capture.Setup(c => c.CaptureAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SnapshotCaptureAttempt(200, Jpeg, null));

            await RunTickAsync();

            _capture.Verify(c => c.CaptureAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Never);
            status.Verify(s => s.IsOffline("doorbot", true), Times.Once);
        }
        [Fact]
        public async Task RunOneTickAsync_SuccessfulCapture_RegistersSnapshotMediaRow()
        {
            _ = AddDevice("12345", isOnline: true);
            _ = _capture.Setup(c => c.CaptureAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SnapshotCaptureAttempt(200, Jpeg, null));

            await RunTickAsync();

            _media.Verify(m => m.AddAsync(
                It.Is<MediaItem>(i => i.MediaFormat == "jpg" && i.MetadataJson == SnapshotCaptureService.CaptureSnapshotMetadata && i.FilePath.EndsWith(".jpg")),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RunOneTickAsync_UnknownOnlineState_StillCaptures()
        {
            // Null means the provider gave no connection state; it is not evidence the device is offline.
            _ = AddDevice("12345", isOnline: null);
            _ = _capture.Setup(c => c.CaptureAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SnapshotCaptureAttempt(200, Jpeg, null));

            await RunTickAsync();

            _capture.Verify(c => c.CaptureAsync(It.IsAny<Guid>(), "12345", It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Theory]
        [InlineData("chime")]
        [InlineData("base_station")]
        [InlineData("range_extender")]
        [InlineData("")]
        public async Task RunOneTickAsync_NonCameraDevice_SkipsDevice(string type)
        {
            _ = AddDevice("12345", type: type, isOnline: true);
            _ = _capture.Setup(c => c.CaptureAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SnapshotCaptureAttempt(200, Jpeg, null));

            await RunTickAsync();

            _capture.Verify(c => c.CaptureAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Never);
            Assert.Empty(_rows);
        }

        [Theory]
        [InlineData("doorbot")]
        [InlineData("authorized_doorbot")]
        [InlineData("stickup_cam")]
        public async Task RunOneTickAsync_OnlineCameraType_Captures(string type)
        {
            _ = AddDevice("12345", type: type, isOnline: true);
            _ = _capture.Setup(c => c.CaptureAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SnapshotCaptureAttempt(200, Jpeg, null));

            await RunTickAsync();

            _capture.Verify(c => c.CaptureAsync(It.IsAny<Guid>(), "12345", It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RunOneTickAsync_DefaultInterval_CapturesOncePerSixtySeconds()
        {
            _ = AddDevice("12345");
            _ = _capture.Setup(c => c.CaptureAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SnapshotCaptureAttempt(200, Jpeg, null));

            await RunTickAsync();
            _time.Now = T0.AddSeconds(30);
            await RunTickAsync();
            _time.Now = T0.AddSeconds(61);
            await RunTickAsync();

            _capture.Verify(c => c.CaptureAsync(It.IsAny<Guid>(), "12345", It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        }
        [Fact]
        public async Task RunOneTickAsync_ScheduleSnapshotInterval_SetsCaptureSpacing()
        {
            _ = _schedules.Setup(r => r.GetByProviderAccountIdAsync(_accountId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SyncSchedule { ProviderAccountId = _accountId, SnapshotRssiIntervalSeconds = 30 });
            _ = AddDevice("12345");
            _ = _capture.Setup(c => c.CaptureAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SnapshotCaptureAttempt(200, Jpeg, null));

            await RunTickAsync();
            _time.Now = T0.AddSeconds(20);
            await RunTickAsync();
            _time.Now = T0.AddSeconds(31);
            await RunTickAsync();

            _capture.Verify(c => c.CaptureAsync(It.IsAny<Guid>(), "12345", It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [Fact]
        public async Task RunOneTickAsync_StoredIntervalSeconds_UsesStoredInterval()
        {
            _ = _schedules.Setup(r => r.GetByProviderAccountIdAsync(_accountId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SyncSchedule { ProviderAccountId = _accountId, SnapshotIntervalSeconds = 120 });
            _ = AddDevice("12345");
            _ = _capture.Setup(c => c.CaptureAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SnapshotCaptureAttempt(200, Jpeg, null));

            await RunTickAsync();
            _time.Now = T0.AddSeconds(61);
            await RunTickAsync();

            _capture.Verify(c => c.CaptureAsync(It.IsAny<Guid>(), "12345", It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RunOneTickAsync_Success_WritesImageUnderAccountAndDeviceFoldersAndRecords200()
        {
            _ = AddDevice("12345");
            _ = _capture.Setup(c => c.CaptureAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SnapshotCaptureAttempt(200, Jpeg, null));

            await RunTickAsync();

            string accountFolder = _accountId.ToString("N");
            string expectedRelative = Path.Combine(accountFolder, "12345", ExpectedFileName(T0));
            Assert.True(File.Exists(Path.Combine(_root, expectedRelative)));
            SnapshotCapture row = Assert.Single(_rows);
            Assert.Equal(200, row.HttpStatus);
            Assert.True(row.IsCurrent);
            Assert.Equal(expectedRelative, row.MediaFilePath);
            Assert.Equal(T0, row.CapturedAtUtc);
        }

        [Fact]
        public async Task RunOneTickAsync_FailedAttempt_RecordsNon200AndWritesNoFile()
        {
            _ = AddDevice("12345");
            _ = _capture.Setup(c => c.CaptureAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SnapshotCaptureAttempt(404, null, null));

            await RunTickAsync();

            SnapshotCapture row = Assert.Single(_rows);
            Assert.Equal(404, row.HttpStatus);
            Assert.False(row.IsCurrent);
            Assert.Null(row.MediaFilePath);
            Assert.False(Directory.Exists(Path.Combine(_root, _accountId.ToString("N"))));
        }

        [Fact]
        public async Task RunOneTickAsync_Throttled_RecordsA429AndPausesAccountUntilPauseEnds()
        {
            _ = AddDevice("12345");
            _ = _capture.SetupSequence(c => c.CaptureAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SnapshotCaptureAttempt(429, null, TimeSpan.FromSeconds(60)))
                .ReturnsAsync(new SnapshotCaptureAttempt(200, Jpeg, null));

            await RunTickAsync();
            _time.Now = T0.AddSeconds(30);
            await RunTickAsync();
            _time.Now = T0.AddSeconds(61);
            await RunTickAsync();

            _capture.Verify(c => c.CaptureAsync(It.IsAny<Guid>(), "12345", It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
            Assert.Equal(2, _rows.Count);
            Assert.Equal(429, _rows[0].HttpStatus);
            Assert.Null(_rows[0].MediaFilePath);
            Assert.Equal(200, _rows[1].HttpStatus);
        }

        [Fact]
        public async Task RunOneTickAsync_DeviceCaptureThrows_ContinuesWithNextDevice()
        {
            Device first = AddDevice("111");
            Device second = AddDevice("222");
            _ = _capture.Setup(c => c.CaptureAsync(It.IsAny<Guid>(), "111", It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("network down"));
            _ = _capture.Setup(c => c.CaptureAsync(It.IsAny<Guid>(), "222", It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SnapshotCaptureAttempt(200, Jpeg, null));

            await RunTickAsync();

            SnapshotCapture row = Assert.Single(_rows);
            Assert.Equal(second.Id, row.DeviceId);
            Assert.NotEqual(first.Id, row.DeviceId);
        }

        [Fact]
        public async Task RunOneTickAsync_BudgetExhausted_DefersCaptureWithoutCallingRing()
        {
            _ = AddDevice("12345");
            _ = _budget.Setup(b => b.TryConsumeAsync("Ring", It.IsAny<CancellationToken>())).ReturnsAsync(false);
            _ = _capture.Setup(c => c.CaptureAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SnapshotCaptureAttempt(200, Jpeg, null));

            await RunTickAsync();

            _capture.Verify(c => c.CaptureAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Never);
            Assert.Empty(_rows);
        }

        /// <summary>A capture is two Ring calls (the update and the image read), so the budget records both.</summary>
        [Fact]
        public async Task RunOneTickAsync_CaptureSucceeds_RecordsTwoCallsAgainstBudget()
        {
            _ = AddDevice("12345");
            _ = _capture.Setup(c => c.CaptureAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SnapshotCaptureAttempt(200, Jpeg, null));

            await RunTickAsync();

            _budget.Verify(b => b.RecordCallAsync("Ring", It.IsAny<CancellationToken>()), Times.Exactly(2));
        }
    }
}
