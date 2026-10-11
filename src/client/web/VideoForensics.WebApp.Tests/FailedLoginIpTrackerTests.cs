using Microsoft.Extensions.DependencyInjection;

using Moq;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.WebApp.Services;

using Xunit;

namespace VideoForensics.WebApp.Tests
{
    public class FailedLoginIpTrackerTests
    {
        private const string AttackerIp = "203.0.113.10";

        [Fact]
        public async Task FailedLoginIpTracker_ThresholdDistinctAccounts_CreatesTemporaryBan()
        {
            var store = new List<BannedIpRange>();
            var time = new ManualTimeProvider(DateTimeOffset.Parse("2026-10-10T12:00:00Z"));
            var audit = new Mock<ISecurityAuditLogger>();
            var tracker = CreateTracker(time, store, audit);
            var policy = CreatePolicy(threshold: 3);

            bool first = await tracker.RecordLockoutAsync(AttackerIp, Guid.NewGuid(), policy, CancellationToken.None);
            bool second = await tracker.RecordLockoutAsync(AttackerIp, Guid.NewGuid(), policy, CancellationToken.None);
            bool third = await tracker.RecordLockoutAsync(AttackerIp, Guid.NewGuid(), policy, CancellationToken.None);

            Assert.False(first);
            Assert.False(second);
            Assert.True(third);
            Assert.Single(store);
            audit.Verify(a => a.LogAsync(SecurityAuditEventTypes.AutoBanCreated, null, null, AttackerIp,
                It.IsAny<string?>(), true, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task FailedLoginIpTracker_SameAccountRepeated_DoesNotBan()
        {
            var store = new List<BannedIpRange>();
            var time = new ManualTimeProvider(DateTimeOffset.Parse("2026-10-10T12:00:00Z"));
            var tracker = CreateTracker(time, store, new Mock<ISecurityAuditLogger>());
            var policy = CreatePolicy(threshold: 3);
            Guid sameOperator = Guid.NewGuid();

            for (int i = 0; i < 5; i++)
            {
                bool banned = await tracker.RecordLockoutAsync(AttackerIp, sameOperator, policy, CancellationToken.None);
                Assert.False(banned);
            }

            Assert.Empty(store);
        }

        [Fact]
        public async Task FailedLoginIpTracker_BelowThreshold_DoesNotBan()
        {
            var store = new List<BannedIpRange>();
            var time = new ManualTimeProvider(DateTimeOffset.Parse("2026-10-10T12:00:00Z"));
            var tracker = CreateTracker(time, store, new Mock<ISecurityAuditLogger>());
            var policy = CreatePolicy(threshold: 3);

            Assert.False(await tracker.RecordLockoutAsync(AttackerIp, Guid.NewGuid(), policy, CancellationToken.None));
            Assert.False(await tracker.RecordLockoutAsync(AttackerIp, Guid.NewGuid(), policy, CancellationToken.None));

            Assert.Empty(store);
        }

        [Fact]
        public async Task FailedLoginIpTracker_EventsOutsideWindow_AreNotCounted()
        {
            var store = new List<BannedIpRange>();
            var time = new ManualTimeProvider(DateTimeOffset.Parse("2026-10-10T12:00:00Z"));
            var tracker = CreateTracker(time, store, new Mock<ISecurityAuditLogger>());
            var policy = CreatePolicy(threshold: 3, windowMinutes: 60);

            Assert.False(await tracker.RecordLockoutAsync(AttackerIp, Guid.NewGuid(), policy, CancellationToken.None));
            Assert.False(await tracker.RecordLockoutAsync(AttackerIp, Guid.NewGuid(), policy, CancellationToken.None));

            // The first two lockouts fall out of the 60 minute window before the third arrives.
            time.Advance(TimeSpan.FromMinutes(61));

            Assert.False(await tracker.RecordLockoutAsync(AttackerIp, Guid.NewGuid(), policy, CancellationToken.None));
            Assert.False(await tracker.RecordLockoutAsync(AttackerIp, Guid.NewGuid(), policy, CancellationToken.None));
            Assert.Empty(store);

            Assert.True(await tracker.RecordLockoutAsync(AttackerIp, Guid.NewGuid(), policy, CancellationToken.None));
            Assert.Single(store);
        }

        [Fact]
        public async Task FailedLoginIpTracker_AutoBanDisabled_NeverBans()
        {
            var store = new List<BannedIpRange>();
            var time = new ManualTimeProvider(DateTimeOffset.Parse("2026-10-10T12:00:00Z"));
            var audit = new Mock<ISecurityAuditLogger>();
            var tracker = CreateTracker(time, store, audit);
            var policy = CreatePolicy(autoBan: false, threshold: 3);

            for (int i = 0; i < 10; i++)
            {
                Assert.False(await tracker.RecordLockoutAsync(AttackerIp, Guid.NewGuid(), policy, CancellationToken.None));
            }

            Assert.Empty(store);
            audit.Verify(a => a.LogAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Theory]
        [InlineData("127.0.0.1")]
        [InlineData("::1")]
        [InlineData("::ffff:127.0.0.1")]
        [InlineData("0.0.0.0")]
        [InlineData("unknown")]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("not-an-ip")]
        public async Task FailedLoginIpTracker_LoopbackOrUnknownIp_NeverBans(string? clientIp)
        {
            var store = new List<BannedIpRange>();
            var time = new ManualTimeProvider(DateTimeOffset.Parse("2026-10-10T12:00:00Z"));
            var tracker = CreateTracker(time, store, new Mock<ISecurityAuditLogger>());
            var policy = CreatePolicy(threshold: 3);

            for (int i = 0; i < 5; i++)
            {
                Assert.False(await tracker.RecordLockoutAsync(clientIp!, Guid.NewGuid(), policy, CancellationToken.None));
            }

            Assert.Empty(store);
        }

        [Fact]
        public async Task FailedLoginIpTracker_UnresolvedCloudflareEdgeIp_NeverBans()
        {
            // NetworkTierResolver falls back to the Cloudflare edge address itself when no
            // CF-Connecting-IP header is present; banning it would block every Internet-tier user.
            var store = new List<BannedIpRange>();
            var time = new ManualTimeProvider(DateTimeOffset.Parse("2026-10-10T12:00:00Z"));
            var tracker = CreateTracker(time, store, new Mock<ISecurityAuditLogger>());
            var policy = CreatePolicy(threshold: 3);

            for (int i = 0; i < 5; i++)
            {
                Assert.False(await tracker.RecordLockoutAsync("173.245.48.1", Guid.NewGuid(), policy, CancellationToken.None));
            }

            Assert.Empty(store);
        }

        [Fact]
        public async Task FailedLoginIpTracker_BanCreated_SetsExpiryAndSlash32()
        {
            var store = new List<BannedIpRange>();
            var now = DateTimeOffset.Parse("2026-10-10T12:00:00Z");
            var time = new ManualTimeProvider(now);
            var tracker = CreateTracker(time, store, new Mock<ISecurityAuditLogger>());
            var policy = CreatePolicy(threshold: 3, durationMinutes: 1440);

            for (int i = 0; i < 3; i++)
            {
                await tracker.RecordLockoutAsync(AttackerIp, Guid.NewGuid(), policy, CancellationToken.None);
            }

            BannedIpRange ban = Assert.Single(store);
            Assert.Equal("203.0.113.10/32", ban.CidrRange);
            Assert.Equal(now.UtcDateTime.AddMinutes(1440), ban.ExpiresAtUtc);
            Assert.Equal(now.UtcDateTime, ban.CreatedAtUtc);
            Assert.Equal(FailedLoginIpTracker.SystemOperatorId, ban.CreatedByOperatorId);
            Assert.StartsWith("Auto-ban: ", ban.Reason);
            Assert.Contains("3", ban.Reason);
        }

        [Fact]
        public async Task FailedLoginIpTracker_Ipv6Client_BansSlash128()
        {
            var store = new List<BannedIpRange>();
            var time = new ManualTimeProvider(DateTimeOffset.Parse("2026-10-10T12:00:00Z"));
            var tracker = CreateTracker(time, store, new Mock<ISecurityAuditLogger>());
            var policy = CreatePolicy(threshold: 3);

            for (int i = 0; i < 3; i++)
            {
                await tracker.RecordLockoutAsync("2001:db8::25", Guid.NewGuid(), policy, CancellationToken.None);
            }

            BannedIpRange ban = Assert.Single(store);
            Assert.Equal("2001:db8::25/128", ban.CidrRange);
        }

        [Fact]
        public async Task FailedLoginIpTracker_AlreadyBanned_DoesNotDuplicate()
        {
            var store = new List<BannedIpRange>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    CidrRange = "203.0.113.10/32",
                    Reason = "Auto-ban: earlier",
                    CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5),
                    CreatedByOperatorId = FailedLoginIpTracker.SystemOperatorId,
                    ExpiresAtUtc = DateTime.UtcNow.AddHours(1)
                }
            };
            var repository = CreateBannedRangeRepository(store);
            var time = new ManualTimeProvider(DateTimeOffset.Parse("2026-10-10T12:00:00Z"));
            var tracker = CreateTracker(time, repository, new Mock<ISecurityAuditLogger>());
            var policy = CreatePolicy(threshold: 3);

            for (int i = 0; i < 3; i++)
            {
                Assert.False(await tracker.RecordLockoutAsync(AttackerIp, Guid.NewGuid(), policy, CancellationToken.None));
            }

            Assert.Single(store);
            repository.Verify(r => r.AddAsync(It.IsAny<BannedIpRange>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task FailedLoginIpTracker_ConcurrentRecords_CountsCorrectly()
        {
            var store = new List<BannedIpRange>();
            var time = new ManualTimeProvider(DateTimeOffset.Parse("2026-10-10T12:00:00Z"));
            var tracker = CreateTracker(time, store, new Mock<ISecurityAuditLogger>());
            var policy = CreatePolicy(threshold: 50);

            // 49 distinct accounts in parallel: one short of the threshold, so no ban yet.
            bool[] belowThreshold = await Task.WhenAll(Enumerable.Range(0, 49).Select(_ =>
                Task.Run(() => tracker.RecordLockoutAsync(AttackerIp, Guid.NewGuid(), policy, CancellationToken.None))));
            Assert.All(belowThreshold, banned => Assert.False(banned));
            Assert.Empty(store);

            // The 50th distinct account crosses the threshold exactly once.
            Assert.True(await tracker.RecordLockoutAsync(AttackerIp, Guid.NewGuid(), policy, CancellationToken.None));
            Assert.Single(store);
        }

        [Fact]
        public async Task FailedLoginIpTracker_ConcurrentRecordsOverThreshold_BansExactlyOnce()
        {
            var store = new List<BannedIpRange>();
            var repository = CreateBannedRangeRepository(store);
            var time = new ManualTimeProvider(DateTimeOffset.Parse("2026-10-10T12:00:00Z"));
            var tracker = CreateTracker(time, repository, new Mock<ISecurityAuditLogger>());
            var policy = CreatePolicy(threshold: 3);

            bool[] results = await Task.WhenAll(Enumerable.Range(0, 40).Select(_ =>
                Task.Run(() => tracker.RecordLockoutAsync(AttackerIp, Guid.NewGuid(), policy, CancellationToken.None))));

            Assert.Equal(1, results.Count(banned => banned));
            Assert.Single(store);
            repository.Verify(r => r.AddAsync(It.IsAny<BannedIpRange>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        private static LockoutPolicySettings CreatePolicy(
            bool autoBan = true,
            int threshold = 3,
            int windowMinutes = 60,
            int durationMinutes = 1440)
        {
            return new LockoutPolicySettings
            {
                Id = Guid.NewGuid(),
                MaxFailedAttempts = 5,
                LockoutDurationMinutes = 15,
                UpdatedAtUtc = DateTime.UtcNow,
                AutoBanEnabled = autoBan,
                AutoBanDistinctAccountThreshold = threshold,
                AutoBanWindowMinutes = windowMinutes,
                AutoBanDurationMinutes = durationMinutes
            };
        }

        private static Mock<IBannedIpRangeRepository> CreateBannedRangeRepository(List<BannedIpRange> store)
        {
            var repository = new Mock<IBannedIpRangeRepository>();
            repository.Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => store.ToList());
            repository.Setup(r => r.AddAsync(It.IsAny<BannedIpRange>(), It.IsAny<CancellationToken>()))
                .Callback<BannedIpRange, CancellationToken>((range, _) => store.Add(range))
                .Returns(Task.CompletedTask);
            return repository;
        }

        private static FailedLoginIpTracker CreateTracker(
            ManualTimeProvider time,
            List<BannedIpRange> store,
            Mock<ISecurityAuditLogger> audit)
        {
            return CreateTracker(time, CreateBannedRangeRepository(store), audit);
        }

        private static FailedLoginIpTracker CreateTracker(
            ManualTimeProvider time,
            Mock<IBannedIpRangeRepository> repository,
            Mock<ISecurityAuditLogger> audit)
        {
            // The tracker is a singleton that resolves scoped repositories per write, so the tests
            // build a real root provider rather than handing it the mocks directly.
            ServiceProvider services = new ServiceCollection()
                .AddSingleton(repository.Object)
                .AddSingleton(audit.Object)
                .BuildServiceProvider();

            return new FailedLoginIpTracker(time, services.GetRequiredService<IServiceScopeFactory>(),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<FailedLoginIpTracker>.Instance);
        }

        /// <summary>Controllable clock so window expiry is deterministic.</summary>
        private sealed class ManualTimeProvider : TimeProvider
        {
            private DateTimeOffset _now;

            public ManualTimeProvider(DateTimeOffset start) => _now = start;

            public override DateTimeOffset GetUtcNow() => _now;

            public void Advance(TimeSpan by) => _now = _now.Add(by);
        }
    }
}
