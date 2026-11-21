using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Hosting.BackgroundServices;
using VideoForensics.Client.Hosting.Services;
using VideoForensics.Core.Providers.Common.Contracts;
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

            var scheduleRepository = new Mock<ISyncScheduleWriteRepository>();
            _ = scheduleRepository.Setup(r => r.ListEnabledAsync(It.IsAny<CancellationToken>())).ReturnsAsync(schedules);

            var accounts = new Mock<IProviderAccountWriteRepository>();
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

        [Fact]
        public async Task RefreshOnceAsync_SessionNotRestored_SkipsTheSyncInsteadOfDiscoveringWithoutASession()
        {
            // Why: after a restart the provider session is in memory only. Discovery without it fails with "Session is null".
            var account = new ProviderAccount { Id = Guid.NewGuid(), ProviderName = "Ring", IsActive = true };
            IReadOnlyList<SyncSchedule> schedules = [new SyncSchedule { Id = Guid.NewGuid(), ProviderAccountId = account.Id, IsEnabled = true }];

            var scheduleRepository = new Mock<ISyncScheduleWriteRepository>();
            _ = scheduleRepository.Setup(r => r.ListEnabledAsync(It.IsAny<CancellationToken>())).ReturnsAsync(schedules);

            var accounts = new Mock<IProviderAccountWriteRepository>();
            _ = accounts.Setup(r => r.GetAsync(account.Id, It.IsAny<CancellationToken>())).ReturnsAsync(account);

            var budget = new Mock<IProviderApiBudgetGuard>();
            _ = budget.Setup(b => b.TryConsumeAsync("Ring", It.IsAny<CancellationToken>())).ReturnsAsync(true);

            var auth = new Mock<IProviderAuthService>();
            _ = auth.Setup(a => a.RestoreFromSavedCredentialsAsync(account.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);

            var inventory = new Mock<IInventorySyncService>();

            ServiceProvider provider = new ServiceCollection()
                .AddSingleton(scheduleRepository.Object)
                .AddSingleton(accounts.Object)
                .AddSingleton(budget.Object)
                .AddKeyedSingleton<IProviderAuthService>("Ring", auth.Object)
                .AddKeyedSingleton<IInventorySyncService>("Ring", inventory.Object)
                .BuildServiceProvider();

            var options = new Mock<IOptionsMonitor<ScheduledTasksOptions>>();
            _ = options.Setup(o => o.CurrentValue).Returns(() => new ScheduledTasksOptions { EnabledProviders = ["Ring"] });

            var service = new InventoryRefreshService(
                provider.GetRequiredService<IServiceScopeFactory>(),
                options.Object,
                Mock.Of<ILogger<InventoryRefreshService>>(),
                TimeProvider.System);

            _ = await service.RefreshOnceAsync(CancellationToken.None);

            inventory.Verify(i => i.SyncAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
