using System.Collections.Concurrent;

using VideoForensics.Core.Providers.Common.Contracts;

using ProviderAccountEntity = VideoForensics.Data.Common.Entities.ProviderAccount;

namespace VideoForensics.Client.Hosting.Tests.Services
{
    /// <summary>Integration tests for EventPullService with multi-account scenarios.</summary>
    public class EventPullServiceIntegrationTests
    {
        /// <summary>Tests that concurrent pulls of two different accounts don't cross-contaminate data.</summary>
        [Fact]
        public async Task PullAccountEventsAsync_TwoAccountsConcurrent_NoDataCrossContamination()
        {
            // Arrange: Setup two Ring accounts with different sessions and devices
            var account1Id = Guid.NewGuid();
            var account2Id = Guid.NewGuid();
            var location1Id = Guid.NewGuid();
            var location2Id = Guid.NewGuid();
            var device1Id = Guid.NewGuid();
            var device2Id = Guid.NewGuid();
            CancellationToken cancellationToken = CancellationToken.None;

            var account1 = new ProviderAccountEntity
            {
                Id = account1Id,
                UserId = Guid.NewGuid(),
                ProviderName = "Ring",
                LinkedUtc = DateTime.UtcNow.AddDays(-5),
                IsActive = true
            };

            var account2 = new ProviderAccountEntity
            {
                Id = account2Id,
                UserId = Guid.NewGuid(),
                ProviderName = "Ring",
                LinkedUtc = DateTime.UtcNow.AddDays(-3),
                IsActive = true
            };

            var location1 = new LocationEntity
            {
                Id = location1Id,
                ProviderLocationId = "location-account1",
                Name = "Home A"
            };

            var location2 = new LocationEntity
            {
                Id = location2Id,
                ProviderLocationId = "location-account2",
                Name = "Home B"
            };

            var device1 = new DeviceEntity
            {
                Id = device1Id,
                LocationId = location1Id,
                ProviderDeviceId = "device-account1",
                Name = "Front Door A",
                Type = "doorbell"
            };

            var device2 = new DeviceEntity
            {
                Id = device2Id,
                LocationId = location2Id,
                ProviderDeviceId = "device-account2",
                Name = "Front Door B",
                Type = "doorbell"
            };

            var events1 = new List<DeviceEvent>
            {
                new(
                    Id: "event-account1-1",
                    DeviceId: "device-account1",
                    EventType: "motion",
                    Timestamp: DateTime.UtcNow.AddMinutes(-10)
                ),
                new(
                    Id: "event-account1-2",
                    DeviceId: "device-account1",
                    EventType: "person",
                    Timestamp: DateTime.UtcNow.AddMinutes(-5)
                )
            };

            var events2 = new List<DeviceEvent>
            {
                new(
                    Id: "event-account2-1",
                    DeviceId: "device-account2",
                    EventType: "motion",
                    Timestamp: DateTime.UtcNow.AddMinutes(-15)
                )
            };

            // Track which account was accessed with which accountId
            var accountIdCallTracker = new ConcurrentDictionary<Guid, List<string>>();
            _ = accountIdCallTracker.TryAdd(account1Id, []);
            _ = accountIdCallTracker.TryAdd(account2Id, []);

            // Setup mocks with different behavior per account
            var mockEventAndConfigService = new Mock<IEventAndConfigService>();
            var mockDeviceRepository = new Mock<IDeviceRepository>();
            var mockLocationRepository = new Mock<ILocationRepository>();
            var mockProviderAccountRepository = new Mock<IProviderAccountRepository>();
            var mockDiscovery = new Mock<IDeviceDiscoveryService>();

            // Account 1 setup
            _ = mockProviderAccountRepository
                .Setup(r => r.GetAsync(account1Id, cancellationToken))
                .ReturnsAsync(account1);

            DiscoveryStub.Wire(account1Id, location1, new[] { device1 }, mockDiscovery, mockLocationRepository, mockDeviceRepository);

            _ = mockEventAndConfigService
                .Setup(s => s.GetEventsAsync(
                    account1Id,
                    "device-account1",
                    It.IsAny<DateTime>(),
                    It.IsAny<DateTime>(),
                    null,
                    cancellationToken
                ))
                .Callback(() => accountIdCallTracker[account1Id].Add("GetEventsAsync"))
                .ReturnsAsync(events1);

            _ = mockEventAndConfigService
                .Setup(s => s.GetDeviceConfigAsync(account1Id, "device-account1", cancellationToken))
                .Callback(() => accountIdCallTracker[account1Id].Add("GetDeviceConfigAsync"))
                .ReturnsAsync(new DeviceConfig("device-account1", true, 80, "always"));

            // Account 2 setup
            _ = mockProviderAccountRepository
                .Setup(r => r.GetAsync(account2Id, cancellationToken))
                .ReturnsAsync(account2);

            DiscoveryStub.Wire(account2Id, location2, new[] { device2 }, mockDiscovery, mockLocationRepository, mockDeviceRepository);

            _ = mockEventAndConfigService
                .Setup(s => s.GetEventsAsync(
                    account2Id,
                    "device-account2",
                    It.IsAny<DateTime>(),
                    It.IsAny<DateTime>(),
                    null,
                    cancellationToken
                ))
                .Callback(() => accountIdCallTracker[account2Id].Add("GetEventsAsync"))
                .ReturnsAsync(events2);

            _ = mockEventAndConfigService
                .Setup(s => s.GetDeviceConfigAsync(account2Id, "device-account2", cancellationToken))
                .Callback(() => accountIdCallTracker[account2Id].Add("GetDeviceConfigAsync"))
                .ReturnsAsync(new DeviceConfig("device-account2", true, 75, "motion"));

            var sut = new EventPullService(
                mockEventAndConfigService.Object,
                mockDeviceRepository.Object,
                mockLocationRepository.Object,
                mockProviderAccountRepository.Object,
                mockDiscovery.Object
            );

            // Act: Pull both accounts concurrently
            Task<EventPullResult> task1 = sut.PullAccountEventsAsync(account1Id, null, null, cancellationToken);
            Task<EventPullResult> task2 = sut.PullAccountEventsAsync(account2Id, null, null, cancellationToken);
            _ = await Task.WhenAll(task1, task2);

            // Assert: Each account's calls were made with correct accountId
            Assert.NotEmpty(accountIdCallTracker[account1Id]);
            Assert.NotEmpty(accountIdCallTracker[account2Id]);

            // Verify GetEventsAsync was called with correct accountId for each account
            mockEventAndConfigService.Verify(s => s.GetEventsAsync(
                account1Id,
                "device-account1",
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                null,
                cancellationToken
            ), Times.Once);

            mockEventAndConfigService.Verify(s => s.GetEventsAsync(
                account2Id,
                "device-account2",
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                null,
                cancellationToken
            ), Times.Once);

            // Verify GetDeviceConfigAsync was called with correct accountId for each account
            mockEventAndConfigService.Verify(s => s.GetDeviceConfigAsync(
                account1Id,
                "device-account1",
                cancellationToken
            ), Times.Once);

            mockEventAndConfigService.Verify(s => s.GetDeviceConfigAsync(
                account2Id,
                "device-account2",
                cancellationToken
            ), Times.Once);
        }

        /// <summary>Tests that GetSession(accountId) returns different sessions for different accounts (mock-based test).</summary>
        [Fact]
        public async Task PullAccountEventsAsync_DifferentAccounts_ReceiveDifferentDevices()
        {
            // Arrange: Two accounts with completely different device sets
            var account1Id = Guid.NewGuid();
            var account2Id = Guid.NewGuid();
            var location1Id = Guid.NewGuid();
            var location2Id = Guid.NewGuid();
            var device1Id = Guid.NewGuid();
            var device2Id = Guid.NewGuid();
            var device3Id = Guid.NewGuid();
            CancellationToken cancellationToken = CancellationToken.None;

            var account1 = new ProviderAccountEntity
            {
                Id = account1Id,
                UserId = Guid.NewGuid(),
                ProviderName = "Ring",
                LinkedUtc = DateTime.UtcNow,
                IsActive = true
            };

            var account2 = new ProviderAccountEntity
            {
                Id = account2Id,
                UserId = Guid.NewGuid(),
                ProviderName = "Ring",
                LinkedUtc = DateTime.UtcNow,
                IsActive = true
            };

            var location1 = new LocationEntity { Id = location1Id, ProviderLocationId = "loc1", Name = "Loc 1" };
            var location2 = new LocationEntity { Id = location2Id, ProviderLocationId = "loc2", Name = "Loc 2" };

            // Account 1 has 2 devices
            var device1 = new DeviceEntity { Id = device1Id, LocationId = location1Id, ProviderDeviceId = "dev1", Name = "Dev 1", Type = "doorbell" };
            var device2 = new DeviceEntity { Id = device2Id, LocationId = location1Id, ProviderDeviceId = "dev2", Name = "Dev 2", Type = "camera" };

            // Account 2 has 1 different device
            var device3 = new DeviceEntity { Id = device3Id, LocationId = location2Id, ProviderDeviceId = "dev3", Name = "Dev 3", Type = "camera" };

            var mockEventAndConfigService = new Mock<IEventAndConfigService>();
            var mockDeviceRepository = new Mock<IDeviceRepository>();
            var mockLocationRepository = new Mock<ILocationRepository>();
            var mockProviderAccountRepository = new Mock<IProviderAccountRepository>();
            var mockDiscovery = new Mock<IDeviceDiscoveryService>();

            // Account 1 setup (2 devices)
            _ = mockProviderAccountRepository
                .Setup(r => r.GetAsync(account1Id, cancellationToken))
                .ReturnsAsync(account1);

            DiscoveryStub.Wire(account1Id, location1, new[] { device1, device2 }, mockDiscovery, mockLocationRepository, mockDeviceRepository);

            _ = mockEventAndConfigService
                .Setup(s => s.GetEventsAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), null, cancellationToken))
                .ReturnsAsync([]);

            _ = mockEventAndConfigService
                .Setup(s => s.GetDeviceConfigAsync(It.IsAny<Guid>(), It.IsAny<string>(), cancellationToken))
                .ReturnsAsync(new DeviceConfig("dummy", true, 80, "always"));

            // Account 2 setup (1 device)
            _ = mockProviderAccountRepository
                .Setup(r => r.GetAsync(account2Id, cancellationToken))
                .ReturnsAsync(account2);

            DiscoveryStub.Wire(account2Id, location2, new[] { device3 }, mockDiscovery, mockLocationRepository, mockDeviceRepository);

            var sut = new EventPullService(
                mockEventAndConfigService.Object,
                mockDeviceRepository.Object,
                mockLocationRepository.Object,
                mockProviderAccountRepository.Object,
                mockDiscovery.Object
            );

            // Act
            _ = await sut.PullAccountEventsAsync(account1Id, null, null, cancellationToken);
            _ = await sut.PullAccountEventsAsync(account2Id, null, null, cancellationToken);

            // Assert: Verify Account 1 pulled from 2 devices
            mockEventAndConfigService.Verify(s => s.GetEventsAsync(
                account1Id,
                "dev1",
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                null,
                cancellationToken
            ), Times.Once);

            mockEventAndConfigService.Verify(s => s.GetEventsAsync(
                account1Id,
                "dev2",
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                null,
                cancellationToken
            ), Times.Once);

            // Assert: Verify Account 2 pulled from 1 device
            mockEventAndConfigService.Verify(s => s.GetEventsAsync(
                account2Id,
                "dev3",
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                null,
                cancellationToken
            ), Times.Once);

            // Verify no cross-contamination (account1 never accessed account2's devices and vice versa)
            mockEventAndConfigService.Verify(s => s.GetEventsAsync(
                account1Id,
                "dev3",
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                null,
                cancellationToken
            ), Times.Never);

            mockEventAndConfigService.Verify(s => s.GetEventsAsync(
                account2Id,
                "dev1",
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                null,
                cancellationToken
            ), Times.Never);
        }

        /// <summary>Tests that multiple concurrent pulls maintain isolation (stress test).</summary>
        [Fact]
        public async Task PullAccountEventsAsync_MultipleAccountsConcurrent_AllCallsIsolated()
        {
            // Arrange: Setup 3 accounts to be pulled concurrently
            Guid[] accountIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
            CancellationToken cancellationToken = CancellationToken.None;
            var callCounts = new ConcurrentDictionary<Guid, int>();

            foreach (Guid accountId in accountIds)
            {
                _ = callCounts.TryAdd(accountId, 0);
            }

            var mockEventAndConfigService = new Mock<IEventAndConfigService>();
            var mockDeviceRepository = new Mock<IDeviceRepository>();
            var mockLocationRepository = new Mock<ILocationRepository>();
            var mockProviderAccountRepository = new Mock<IProviderAccountRepository>();
            var mockDiscovery = new Mock<IDeviceDiscoveryService>();

            // Setup all 3 accounts with minimal setup (just verify accountId isolation)
            foreach (Guid accountId in accountIds)
            {
                var account = new ProviderAccountEntity
                {
                    Id = accountId,
                    UserId = Guid.NewGuid(),
                    ProviderName = "Ring",
                    LinkedUtc = DateTime.UtcNow,
                    IsActive = true
                };

                var locationId = Guid.NewGuid();
                var deviceId = Guid.NewGuid();

                var location = new LocationEntity { Id = locationId, ProviderLocationId = $"loc-{accountId}", Name = "Location" };
                var device = new DeviceEntity { Id = deviceId, LocationId = locationId, ProviderDeviceId = $"dev-{accountId}", Name = "Device", Type = "doorbell" };

                _ = mockProviderAccountRepository
                    .Setup(r => r.GetAsync(accountId, cancellationToken))
                    .ReturnsAsync(account);

                DiscoveryStub.Wire(accountId, location, new[] { device }, mockDiscovery, mockLocationRepository, mockDeviceRepository);

                _ = mockEventAndConfigService
                    .Setup(s => s.GetEventsAsync(
                        accountId,
                        $"dev-{accountId}",
                        It.IsAny<DateTime>(),
                        It.IsAny<DateTime>(),
                        null,
                        cancellationToken
                    ))
                    .Callback(() => callCounts[accountId]++)
                    .ReturnsAsync([]);

                _ = mockEventAndConfigService
                    .Setup(s => s.GetDeviceConfigAsync(accountId, $"dev-{accountId}", cancellationToken))
                    .ReturnsAsync(new DeviceConfig($"dev-{accountId}", true, 80, "always"));
            }

            var sut = new EventPullService(
                mockEventAndConfigService.Object,
                mockDeviceRepository.Object,
                mockLocationRepository.Object,
                mockProviderAccountRepository.Object,
                mockDiscovery.Object
            );

            // Act: Pull all 3 accounts concurrently
            Task<EventPullResult>[] tasks = accountIds.Select(id => sut.PullAccountEventsAsync(id, null, null, cancellationToken)).ToArray();
            _ = await Task.WhenAll(tasks);

            // Assert: Each account was called exactly once, demonstrating no cross-contamination
            foreach (Guid accountId in accountIds)
            {
                Assert.Equal(1, callCounts[accountId]);
            }
        }

        /// <summary>Tests backward compatibility: single-account workflows still work with new accountId parameter.</summary>
        [Fact]
        public async Task PullAccountEventsAsync_SingleAccount_BackwardCompatible()
        {
            // Arrange: Single account workflow (most common case before multi-account refactor)
            var accountId = Guid.NewGuid();
            var locationId = Guid.NewGuid();
            var deviceId = Guid.NewGuid();
            CancellationToken cancellationToken = CancellationToken.None;

            var account = new ProviderAccountEntity
            {
                Id = accountId,
                UserId = Guid.NewGuid(),
                ProviderName = "Ring",
                LinkedUtc = DateTime.UtcNow,
                IsActive = true
            };

            var location = new LocationEntity { Id = locationId, ProviderLocationId = "location-123", Name = "Home" };
            var device = new DeviceEntity { Id = deviceId, LocationId = locationId, ProviderDeviceId = "device-123", Name = "Front Door", Type = "doorbell" };

            var mockEventAndConfigService = new Mock<IEventAndConfigService>();
            var mockDeviceRepository = new Mock<IDeviceRepository>();
            var mockLocationRepository = new Mock<ILocationRepository>();
            var mockProviderAccountRepository = new Mock<IProviderAccountRepository>();
            var mockDiscovery = new Mock<IDeviceDiscoveryService>();

            _ = mockProviderAccountRepository
                .Setup(r => r.GetAsync(accountId, cancellationToken))
                .ReturnsAsync(account);

            DiscoveryStub.Wire(accountId, location, new[] { device }, mockDiscovery, mockLocationRepository, mockDeviceRepository);

            _ = mockEventAndConfigService
                .Setup(s => s.GetEventsAsync(
                    accountId,
                    "device-123",
                    It.IsAny<DateTime>(),
                    It.IsAny<DateTime>(),
                    null,
                    cancellationToken
                ))
                .ReturnsAsync([]);

            _ = mockEventAndConfigService
                .Setup(s => s.GetDeviceConfigAsync(accountId, "device-123", cancellationToken))
                .ReturnsAsync(new DeviceConfig("device-123", true, 80, "always"));

            var sut = new EventPullService(
                mockEventAndConfigService.Object,
                mockDeviceRepository.Object,
                mockLocationRepository.Object,
                mockProviderAccountRepository.Object,
                mockDiscovery.Object
            );

            // Act: Pull single account (backward compatibility test)
            _ = await sut.PullAccountEventsAsync(accountId, null, null, cancellationToken);

            // Assert: All expected methods were called
            mockProviderAccountRepository.Verify(r => r.GetAsync(accountId, cancellationToken), Times.Once);
            mockDiscovery.Verify(d => d.GetLocationsAsync(accountId, cancellationToken), Times.Once);
            mockDiscovery.Verify(d => d.GetDevicesAsync(accountId, It.IsAny<string>(), cancellationToken), Times.Once);
            mockEventAndConfigService.Verify(s => s.GetEventsAsync(
                accountId,
                "device-123",
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                null,
                cancellationToken
            ), Times.Once);
        }
    }
}
