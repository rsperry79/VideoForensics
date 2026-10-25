using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using VideoForensics.Client.Hosting.BackgroundServices;
using VideoForensics.Data.Common.Contracts;

using Xunit;

namespace VideoForensics.Client.Hosting.Tests
{
    /// <summary>The daily prune trims operational tables by their own retention, and leaves audit tables alone.</summary>
    public class OperationalLogPruneServiceTests
    {
        [Fact]
        public async Task PruneOnce_UsesEachTablesRetention()
        {
            var errors = new Mock<IProviderApiErrorLogRepository>();
            var calls = new Mock<IProviderApiCallLogRepository>();
            var telemetry = new Mock<ILiveViewTelemetryRepository>();
            _ = errors.Setup(r => r.PruneOlderThanAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);

            ServiceProvider provider = new ServiceCollection()
                .AddSingleton(errors.Object)
                .AddSingleton(calls.Object)
                .AddSingleton(telemetry.Object)
                .AddScoped<IProviderApiErrorLogRepository>(_ => errors.Object)
                .BuildServiceProvider();
            var service = new OperationalLogPruneService(provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<OperationalLogPruneService>.Instance, TimeProvider.System);

            await service.PruneOnceAsync(CancellationToken.None);

            errors.Verify(r => r.PruneOlderThanAsync(OperationalLogPruneService.ErrorRetention, It.IsAny<CancellationToken>()), Times.Once);
            calls.Verify(r => r.PruneOlderThanAsync(OperationalLogPruneService.CallLogRetention, It.IsAny<CancellationToken>()), Times.Once);
            telemetry.Verify(r => r.PruneOlderThanAsync(OperationalLogPruneService.TelemetryRetention, It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
