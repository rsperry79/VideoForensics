using Microsoft.Extensions.Logging;

using System.Collections.Concurrent;

using VideoForensics.Core.Providers.Common.Contracts;

using ProviderAccountEntity = VideoForensics.Data.Common.Entities.ProviderAccount;

namespace VideoForensics.Hosting.Tests.Services
{
    /// <summary>
    /// Verifies the Information-level progress lines a pull writes so a long sync is visible in the console.
    /// </summary>
    public class EventPullServiceProgressLogTests
    {
        private sealed class CapturingLoggerProvider : ILoggerProvider
        {
            public ConcurrentQueue<string> Lines { get; } = new();

            public ILogger CreateLogger(string categoryName)
            {
                return new CapturingLogger(this);
            }

            public void Dispose()
            {
            }

            private sealed class CapturingLogger(CapturingLoggerProvider owner) : ILogger
            {
                public IDisposable? BeginScope<TState>(TState state) where TState : notnull
                {
                    return null;
                }

                public bool IsEnabled(LogLevel logLevel)
                {
                    return true;
                }

                public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                {
                    owner.Lines.Enqueue($"{logLevel}: {formatter(state, exception)}");
                }
            }
        }

        private readonly Guid _accountId = Guid.NewGuid();
        private readonly Guid _locationId = Guid.NewGuid();
        private readonly Mock<IEventAndConfigService> _events = new();
        private readonly Mock<IDeviceRepository> _devices = new();
        private readonly Mock<ILocationRepository> _locations = new();
        private readonly Mock<IProviderAccountRepository> _accounts = new();
        private readonly Mock<IDeviceDiscoveryService> _discovery = new();
        private readonly CapturingLoggerProvider _logs = new();

        private static DeviceEntity Device(Guid locationId, string providerDeviceId, string name)
        {
            return new()
            {
                Id = Guid.NewGuid(),
                LocationId = locationId,
                ProviderDeviceId = providerDeviceId,
                Name = name,
                Type = "doorbell"
            };
        }

        private EventPullService BuildSut(params DeviceEntity[] devices)
        {
            var account = new ProviderAccountEntity { Id = _accountId, UserId = Guid.NewGuid(), ProviderName = "Ring", LinkedUtc = DateTime.UtcNow, IsActive = true };
            var location = new LocationEntity { Id = _locationId, ProviderLocationId = "location-123", Name = "Home" };

            _ = _accounts.Setup(r => r.GetAsync(_accountId, It.IsAny<CancellationToken>())).ReturnsAsync(account);
            DiscoveryStub.Wire(_accountId, location, devices, _discovery, _locations, _devices);

            ILoggerFactory factory = LoggerFactory.Create(b => b.SetMinimumLevel(LogLevel.Information).AddProvider(_logs));
            return new EventPullService(
                _events.Object,
                _devices.Object,
                _locations.Object,
                _accounts.Object,
                _discovery.Object,
                factory.CreateLogger<EventPullService>());
        }

        [Fact]
        public async Task PullAccountEventsAsync_TwoDevices_LogsDeviceCountAtInformation()
        {
            EventPullService sut = BuildSut(Device(_locationId, "dev-1", "Front Door"), Device(_locationId, "dev-2", "Back Yard"));

            _ = await sut.PullAccountEventsAsync(_accountId, null, null, CancellationToken.None);

            Assert.Contains("Information: Syncing events for 2 device(s)", _logs.Lines);
        }

        [Fact]
        public async Task PullAccountEventsAsync_TwoDevices_LogsEachDeviceNameWithIndexAtInformation()
        {
            EventPullService sut = BuildSut(Device(_locationId, "dev-1", "Front Door"), Device(_locationId, "dev-2", "Back Yard"));

            _ = await sut.PullAccountEventsAsync(_accountId, null, null, CancellationToken.None);

            Assert.Contains("Information: Syncing events for device Front Door (1/2)", _logs.Lines);
            Assert.Contains("Information: Syncing events for device Back Yard (2/2)", _logs.Lines);
        }

        [Fact]
        public async Task PullAccountEventsAsync_DeviceWithEventsInAndOutsideWindow_LogsOnlyInWindowCountForThatDevice()
        {
            DateTime from = DateTime.UtcNow.AddDays(-1);
            EventPullService sut = BuildSut(Device(_locationId, "dev-1", "Front Door"), Device(_locationId, "dev-2", "Back Yard"));

            _ = _events.Setup(s => s.GetEventsAsync(_accountId, "dev-1", from, It.IsAny<DateTime>(), null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(
                [
                    new("e-1", "dev-1", "motion", DateTime.UtcNow.AddHours(-1)),
                    new("e-2", "dev-1", "ding", DateTime.UtcNow.AddHours(-2)),
                    new("e-3", "dev-1", "motion", DateTime.UtcNow.AddDays(-2))
                ]);
            _ = _events.Setup(s => s.GetEventsAsync(_accountId, "dev-2", from, It.IsAny<DateTime>(), null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(
                [
                    new("e-4", "dev-2", "motion", DateTime.UtcNow.AddHours(-3))
                ]);

            _ = await sut.PullAccountEventsAsync(_accountId, from, null, CancellationToken.None);

            Assert.Contains("Information: Synced device Front Door: 2 event(s)", _logs.Lines);
            Assert.Contains("Information: Synced device Back Yard: 1 event(s)", _logs.Lines);
        }

        [Fact]
        public async Task PullAccountEventsAsync_TwoDevices_ProgressLogsNeverContainAccountGuid()
        {
            EventPullService sut = BuildSut(Device(_locationId, "dev-1", "Front Door"), Device(_locationId, "dev-2", "Back Yard"));

            _ = await sut.PullAccountEventsAsync(_accountId, null, null, CancellationToken.None);

            Assert.DoesNotContain(_logs.Lines, line => line.Contains(_accountId.ToString(), StringComparison.OrdinalIgnoreCase));
        }
    }
}
