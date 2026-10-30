using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Client.Hosting.Contracts;

using ProviderAccountEntity = VideoForensics.Data.Common.Entities.ProviderAccount;

namespace VideoForensics.Client.Hosting.Tests.Services
{
    /// <summary>A custom range is pulled in batches of at most seven days, and reaches back at most one year.</summary>
    public class AccountSyncRunnerCustomRangeTests
    {
        private static readonly DateTime End = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);

        private readonly Mock<IProviderAuthService> _auth = new();
        private readonly Mock<IEventPullService> _pull = new();
        private readonly Mock<IProviderAccountRepository> _accounts = new();
        private readonly Mock<IProviderSyncRunRepository> _runs = new();
        private readonly Mock<ISnapshotSyncService> _snapshots = new();
        private readonly SyncActivityLog _activity = new();
        private readonly Guid _id = Guid.NewGuid();
        private readonly List<(DateTime? From, DateTime? To)> _pulled = [];

        public AccountSyncRunnerCustomRangeTests()
        {
            var account = new ProviderAccountEntity
            {
                Id = _id,
                UserId = Guid.NewGuid(),
                ProviderName = "Ring",
                LinkedUtc = DateTime.UtcNow,
                IsActive = true
            };
            _ = _accounts.Setup(r => r.GetAsync(_id, It.IsAny<CancellationToken>())).ReturnsAsync(account);
            _ = _auth.Setup(a => a.RestoreFromSavedCredentialsAsync(_id, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _ = _runs.Setup(r => r.AddAsync(It.IsAny<ProviderSyncRun>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _ = _pull.Setup(p => p.PullAccountEventsAsync(_id, It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
                .Callback<Guid, DateTime?, DateTime?, CancellationToken>((_, from, to, _) => _pulled.Add((from, to)))
                .ReturnsAsync(new EventPullResult(DevicesSynced: 1, LocationsSynced: 1, EventsSynced: 1, ConfigAreasSynced: 1, FeedsFailed: 0));
        }

        private AccountSyncRunner Sut()
        {
            var services = new ServiceCollection().AddScoped(_ => _auth.Object).AddScoped(_ => _pull.Object).BuildServiceProvider();
            return new(services, _accounts.Object, _runs.Object, _snapshots.Object, Mock.Of<ILogger<AccountSyncRunner>>(), _activity);
        }

        [Fact]
        public void WeeklyWindows_TwentyDays_SplitsIntoSevenSevenSixDayWindows()
        {
            DateTime from = End.AddDays(-20);

            IReadOnlyList<(DateTime From, DateTime To)> windows = AccountSyncRunner.WeeklyWindows(from, End);

            Assert.Equal(3, windows.Count);
            Assert.Equal((from, from.AddDays(7)), windows[0]);
            Assert.Equal((from.AddDays(7), from.AddDays(14)), windows[1]);
            Assert.Equal((from.AddDays(14), End), windows[2]);
        }

        [Fact]
        public void WeeklyWindows_ExactlySevenDays_ReturnsOneWindow()
        {
            DateTime from = End.AddDays(-7);

            IReadOnlyList<(DateTime From, DateTime To)> windows = AccountSyncRunner.WeeklyWindows(from, End);

            Assert.Equal((from, End), Assert.Single(windows));
        }

        [Fact]
        public void WeeklyWindows_FromNotBeforeTo_ReturnsEmpty()
        {
            Assert.Empty(AccountSyncRunner.WeeklyWindows(End, End));
            Assert.Empty(AccountSyncRunner.WeeklyWindows(End.AddDays(1), End));
        }

        [Fact]
        public async Task RunAsync_CustomTwentyDayRange_PullsThreeWindowsOldestFirst()
        {
            DateTime from = End.AddDays(-20);

            await Sut().RunAsync(_id, new AccountSyncRequest(AccountSyncType.Events, from, End, false), CancellationToken.None);

            Assert.Equal(
                new (DateTime?, DateTime?)[]
                {
                    (from, from.AddDays(7)),
                    (from.AddDays(7), from.AddDays(14)),
                    (from.AddDays(14), End),
                },
                _pulled);
        }

        [Fact]
        public async Task RunAsync_CustomThreeDayRange_PullsOnceWithExactRange()
        {
            DateTime from = End.AddDays(-3);

            await Sut().RunAsync(_id, new AccountSyncRequest(AccountSyncType.Events, from, End, false), CancellationToken.None);

            Assert.Equal(new (DateTime?, DateTime?)[] { (from, End) }, _pulled);
        }

        [Fact]
        public async Task RunAsync_CustomRangeOlderThanOneYear_ClampsFirstWindowToOneYearBackAndLogsIt()
        {
            DateTime from = End.AddYears(-3);

            await Sut().RunAsync(_id, new AccountSyncRequest(AccountSyncType.Events, from, End, false), CancellationToken.None);

            Assert.Equal(End.AddYears(-1), _pulled[0].From);
            Assert.Equal(End, _pulled[^1].To);
            Assert.Contains("Ring: history limited to one year per request", _activity.Drain());
        }

        [Fact]
        public async Task RunAsync_NoExplicitStart_PullsOnceWithOneWeekFloor()
        {
            await Sut().RunAsync(_id, CancellationToken.None);

            (DateTime? from, DateTime? to) = Assert.Single(_pulled);
            Assert.True(from.HasValue && Math.Abs((DateTime.UtcNow.AddDays(-7) - from.Value).TotalMinutes) < 5);
            Assert.Null(to);
        }
    }
}
