using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Moq;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Hosting.Maintenance;
using VideoForensics.Client.WebApp.Services;
using VideoForensics.Core.Helpers.Platform;
using VideoForensics.Data.Common.Contracts;

using Xunit;

namespace VideoForensics.Client.WebApp.Tests.Services
{
    public class MaintainedStorageSettingsService_Tests
    {
        private readonly Mock<IMaintenanceGate> _gate = new();
        private readonly Mock<IDatabaseMaintenanceService> _lifecycle = new();
        private readonly Mock<IServiceScopeFactory> _scopes = new();
        private readonly Mock<IStorageSettingsService> _inner = new();
        private readonly Mock<IAppRestarter> _restarter = new();

        public MaintainedStorageSettingsService_Tests()
        {
            _ = _gate.Setup(g => g.BeginDrainAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _ = _lifecycle.Setup(l => l.ReleaseAllConnectionsAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var scope = new Mock<IServiceScope>();
            _ = scope.Setup(s => s.ServiceProvider).Returns(Mock.Of<IServiceProvider>());
            _ = _scopes.Setup(f => f.CreateScope()).Returns(scope.Object);
        }

        private MaintainedStorageSettingsService CreateService()
        {
            var coordinator = new MaintenanceCoordinator(_gate.Object, _lifecycle.Object, _scopes.Object,
                Mock.Of<ILogger<MaintenanceCoordinator>>(), new MaintenanceHoldTimer(MaintenanceHoldTimer.DefaultTimeout));
            return new MaintainedStorageSettingsService(_inner.Object, coordinator, _restarter.Object);
        }

        [Fact]
        public async Task RelocateAsync_LogsMove_RunsUnderMaintenanceAndStaysHeldForRestart()
        {
            _ = _inner.Setup(s => s.RelocateAsync(It.IsAny<RelocateStorageCategoryRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RelocateStorageCategoryResult(Succeeded: true, ErrorMessage: null, RestartRequired: true));

            _ = await CreateService().RelocateAsync(new RelocateStorageCategoryRequest(StorageCategory.Logs, @"D:\Logs"), CancellationToken.None);

            _gate.Verify(g => g.BeginDrainAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
            _gate.Verify(g => g.End(), Times.Never);
            _restarter.Verify(r => r.Restart(), Times.Once);
        }

        [Fact]
        public async Task RelocateAsync_MediaMove_EndsMaintenanceWithoutRestart()
        {
            _ = _inner.Setup(s => s.RelocateAsync(It.IsAny<RelocateStorageCategoryRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RelocateStorageCategoryResult(Succeeded: true, ErrorMessage: null, RestartRequired: false));

            _ = await CreateService().RelocateAsync(new RelocateStorageCategoryRequest(StorageCategory.Media, @"D:\Media"), CancellationToken.None);

            _gate.Verify(g => g.End(), Times.Once);
            _restarter.Verify(r => r.Restart(), Times.Never);
        }

        [Fact]
        public async Task ChangeDefaultRootAsync_RunsUnderMaintenanceAndStaysHeldForRestart()
        {
            _ = _inner.Setup(s => s.ChangeDefaultRootAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RelocateStorageCategoryResult(Succeeded: true, ErrorMessage: null, RestartRequired: true));

            _ = await CreateService().ChangeDefaultRootAsync(@"G:\VideoForensics", CancellationToken.None);

            _gate.Verify(g => g.End(), Times.Never);
            _restarter.Verify(r => r.Restart(), Times.Once);
        }
    }
}
