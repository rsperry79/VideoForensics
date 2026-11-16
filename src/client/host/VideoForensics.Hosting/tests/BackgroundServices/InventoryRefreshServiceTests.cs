using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Hosting.BackgroundServices;
using VideoForensics.Client.Hosting.Services;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.Hosting.Tests.BackgroundServices
{
    public class InventoryRefreshServiceTests
    {
        [Fact]
        public async Task RefreshOnceAsync_EnabledAccount_RunsInventorySyncOnce()
        {
            var account = new ProviderAccount { Id = Guid.NewGuid(), ProviderName = "Ring", IsActive = true };
            IReadOnlyList<SyncSchedule> schedules = [new SyncSchedule { Id = Guid.NewGuid(), ProviderAccountId = account.Id, IsEnabled = true }];

            var scheduleRepository = new Mock<ISyncScheduleRepository>();
            _ = scheduleRepository.Setup(r => r.ListEnabledAsync(It.IsAny<CancellationToken>())).ReturnsAsync(schedules);

            var accounts = new Mock<IProviderAccountRepository>();
            _ = accounts.Setup(r => r.GetAsync(account.Id, It.IsAny<CancellationToken>())).ReturnsAsync(account);

            var budget = new Mock<IProviderApiBudgetGuard>();
            _ = budget.Setup(b => b.TryConsumeAsync("Ring", It.IsAny<CancellationToken>())).ReturnsAsync(true);

            var inventory = new Mock<IInventorySyncService>();
            _ = inventory.Setup(i => i.SyncAsync(account.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new InventorySyncResult(1, 1, 1, 0));

            ServiceProvider provider = new ServiceCollection()
                .AddSingleton(scheduleRepository.Object)
                .AddSingleton(accounts.Object)
                .AddSingleton(budget.Object)
                .AddKeyedSingleton<IInventorySyncService>("Ring", inventory.Object)
                .BuildServiceProvider();

            var options = new Mock<IOptionsMonitor<ScheduledTasksOptions>>();
            _ = options.Setup(o => o.CurrentValue).Returns(() => new ScheduledTasksOptions { EnabledProviders = ["Ring"] });

            var service = new InventoryRefreshService(
                provider.GetRequiredService<IServiceScopeFactory>(),
                options.Object,
                Mock.Of<ILogger<InventoryRefreshService>>(),
                TimeProvider.System);

            int failed = await service.RefreshOnceAsync(CancellationToken.None);

            Assert.Equal(0, failed);
            inventory.Verify(i => i.SyncAsync(account.Id, It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
