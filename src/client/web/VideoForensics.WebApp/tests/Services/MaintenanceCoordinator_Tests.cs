using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Moq;

using VideoForensics.Client.Hosting.Maintenance;
using VideoForensics.Client.WebApp.Services;
using VideoForensics.Data.Common.Contracts;

using Xunit;

namespace VideoForensics.Client.WebApp.Tests.Services
{
    public class MaintenanceCoordinator_Tests
    {
        private readonly Mock<IMaintenanceGate> _gate = new();
        private readonly Mock<IDatabaseMaintenanceService> _lifecycle = new();
        private readonly Mock<IServiceScopeFactory> _scopes = new();

        public MaintenanceCoordinator_Tests()
        {
            _ = _gate.Setup(g => g.BeginDrainAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _ = _lifecycle.Setup(l => l.ReleaseAllConnectionsAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var scope = new Mock<IServiceScope>();
            _ = scope.Setup(s => s.ServiceProvider).Returns(Mock.Of<IServiceProvider>());
            _ = _scopes.Setup(f => f.CreateScope()).Returns(scope.Object);
        }

        private MaintenanceCoordinator CreateCoordinator() =>
            new(_gate.Object, _lifecycle.Object, _scopes.Object, Mock.Of<ILogger<MaintenanceCoordinator>>());

        [Fact]
        public async Task RunAsync_Default_EndsMaintenanceAfterSuccess()
        {
            _ = await CreateCoordinator().RunAsync("Test", (sp, ct) => Task.FromResult(1), CancellationToken.None);

            _gate.Verify(g => g.End(), Times.Once);
        }

        [Fact]
        public async Task RunAsync_HoldOnSuccess_LeavesMaintenanceRunning()
        {
            _ = await CreateCoordinator().RunAsync("Moving the database", (sp, ct) => Task.FromResult(1), CancellationToken.None, holdOnSuccess: true);

            _gate.Verify(g => g.End(), Times.Never);
        }

        [Fact]
        public async Task RunAsync_HoldOnSuccess_EndsMaintenanceWhenOperationFails()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateCoordinator().RunAsync<int>("Moving the database", (sp, ct) => throw new InvalidOperationException("boom"), CancellationToken.None, holdOnSuccess: true));

            _gate.Verify(g => g.End(), Times.Once);
        }
    }
}
