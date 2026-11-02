using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Moq;

using VideoForensics.Client.Hosting.Maintenance;
using VideoForensics.Client.WebApp.Api;
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

        private MaintenanceCoordinator CreateCoordinator(MaintenanceHoldTimer? holdTimer = null) =>
            new(_gate.Object, _lifecycle.Object, _scopes.Object, Mock.Of<ILogger<MaintenanceCoordinator>>(),
                holdTimer ?? new MaintenanceHoldTimer(MaintenanceHoldTimer.DefaultTimeout));

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

        [Fact]
        public async Task BeginHoldAsync_WhileIdle_ReturnsTokenMarksRunningAndReleasesConnections()
        {
            MaintenanceCoordinator coordinator = CreateCoordinator();

            Guid token = await coordinator.BeginHoldAsync("Server held for database repair", CancellationToken.None);

            Assert.NotEqual(Guid.Empty, token);
            _gate.Verify(g => g.MarkRunning("Server held for database repair"), Times.Once);
            _lifecycle.Verify(l => l.ReleaseAllConnectionsAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task BeginHoldAsync_WhileAlreadyInMaintenance_ThrowsMaintenanceInProgressException()
        {
            _ = _gate.Setup(g => g.BeginDrainAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Throws(new InvalidOperationException("Server maintenance is already in progress."));

            _ = await Assert.ThrowsAsync<MaintenanceInProgressException>(() =>
                CreateCoordinator().BeginHoldAsync("Server held for database repair", CancellationToken.None));

            _gate.Verify(g => g.MarkRunning(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task BeginHoldAsync_GateBusyWithAnotherRun_DoesNotEndTheOtherRun()
        {
            _ = _gate.Setup(g => g.BeginDrainAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Throws(new InvalidOperationException("Server maintenance is already in progress."));
            MaintenanceCoordinator coordinator = CreateCoordinator();

            _ = await Assert.ThrowsAsync<MaintenanceInProgressException>(() =>
                coordinator.BeginHoldAsync("Server held for database repair", CancellationToken.None));

            _gate.Verify(g => g.End(), Times.Never);
        }

        [Fact]
        public async Task BeginHoldAsync_ReleaseFails_EndsMaintenanceAndRethrows()
        {
            _ = _lifecycle.Setup(l => l.ReleaseAllConnectionsAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new IOException("disk gone"));

            _ = await Assert.ThrowsAsync<IOException>(() =>
                CreateCoordinator().BeginHoldAsync("Server held for database repair", CancellationToken.None));

            _gate.Verify(g => g.End(), Times.Once);
        }

        [Fact]
        public async Task BeginHoldAsync_ReleaseFails_ClearsTheHoldSoANewBeginSucceeds()
        {
            _ = _lifecycle.SetupSequence(l => l.ReleaseAllConnectionsAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new IOException("disk gone"))
                .Returns(Task.CompletedTask);
            MaintenanceCoordinator coordinator = CreateCoordinator();
            _ = await Assert.ThrowsAsync<IOException>(() => coordinator.BeginHoldAsync("Server held for database repair", CancellationToken.None));

            Guid token = await coordinator.BeginHoldAsync("Server held for database repair", CancellationToken.None);

            Assert.NotEqual(Guid.Empty, token);
        }

        [Fact]
        public async Task EndHold_WithActiveToken_EndsMaintenanceAndReturnsTrue()
        {
            MaintenanceCoordinator coordinator = CreateCoordinator();
            Guid token = await coordinator.BeginHoldAsync("Server held for database repair", CancellationToken.None);

            bool ended = coordinator.EndHold(token);

            Assert.True(ended);
            _gate.Verify(g => g.End(), Times.Once);
        }

        [Fact]
        public async Task EndHold_WrongToken_DoesNothingAndKeepsTheHold()
        {
            MaintenanceCoordinator coordinator = CreateCoordinator();
            _ = await coordinator.BeginHoldAsync("Server held for database repair", CancellationToken.None);

            bool ended = coordinator.EndHold(Guid.NewGuid());

            Assert.False(ended);
            _gate.Verify(g => g.End(), Times.Never);
        }

        [Fact]
        public void EndHold_NoActiveHold_ReturnsFalseAndDoesNotEndMaintenance()
        {
            bool ended = CreateCoordinator().EndHold(Guid.NewGuid());

            Assert.False(ended);
            _gate.Verify(g => g.End(), Times.Never);
        }

        [Fact]
        public async Task EndHold_CalledTwiceWithSameToken_EndsOnlyOnce()
        {
            MaintenanceCoordinator coordinator = CreateCoordinator();
            Guid token = await coordinator.BeginHoldAsync("Server held for database repair", CancellationToken.None);

            bool first = coordinator.EndHold(token);
            bool second = coordinator.EndHold(token);

            Assert.True(first);
            Assert.False(second);
            _gate.Verify(g => g.End(), Times.Once);
        }

        [Fact]
        public async Task EndHold_TokenFromAnEarlierHold_CannotEndALaterHold()
        {
            MaintenanceCoordinator coordinator = CreateCoordinator();
            Guid firstToken = await coordinator.BeginHoldAsync("Server held for database repair", CancellationToken.None);
            Assert.True(coordinator.EndHold(firstToken));
            Guid secondToken = await coordinator.BeginHoldAsync("Server held for database repair", CancellationToken.None);

            bool ended = coordinator.EndHold(firstToken);

            Assert.False(ended);
            Assert.NotEqual(firstToken, secondToken);
            _gate.Verify(g => g.End(), Times.Once);
        }

        [Fact]
        public async Task RenewHold_WithActiveToken_ReturnsTrue()
        {
            MaintenanceCoordinator coordinator = CreateCoordinator();
            Guid token = await coordinator.BeginHoldAsync("Server held for database repair", CancellationToken.None);

            Assert.True(coordinator.RenewHold(token));
            _gate.Verify(g => g.End(), Times.Never);
        }

        [Fact]
        public async Task RenewHold_WrongToken_ReturnsFalse()
        {
            MaintenanceCoordinator coordinator = CreateCoordinator();
            _ = await coordinator.BeginHoldAsync("Server held for database repair", CancellationToken.None);

            Assert.False(coordinator.RenewHold(Guid.NewGuid()));
        }

        [Fact]
        public async Task BeginHoldAsync_NoEndCalled_TimeoutEndsMaintenance()
        {
            var ended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _ = _gate.Setup(g => g.End()).Callback(() => ended.TrySetResult());
            MaintenanceCoordinator coordinator = CreateCoordinator(new MaintenanceHoldTimer(TimeSpan.FromMilliseconds(20)));

            Guid token = await coordinator.BeginHoldAsync("Server held for database repair", CancellationToken.None);

            await ended.Task.WaitAsync(TimeSpan.FromSeconds(5));
            _gate.Verify(g => g.End(), Times.Once);
            Assert.False(coordinator.EndHold(token));
        }

        [Fact]
        public async Task BeginHoldAsync_DrainStillRunning_TimerDoesNotEndTheHoldEarly()
        {
            var drain = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _ = _gate.Setup(g => g.BeginDrainAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(drain.Task);
            MaintenanceCoordinator coordinator = CreateCoordinator(new MaintenanceHoldTimer(TimeSpan.FromMilliseconds(20)));

            Task<Guid> begin = coordinator.BeginHoldAsync("Server held for database repair", CancellationToken.None);
            await Task.Delay(150);

            _gate.Verify(g => g.End(), Times.Never);
            _ = await Assert.ThrowsAsync<MaintenanceInProgressException>(() =>
                coordinator.BeginHoldAsync("Server held for database repair", CancellationToken.None));

            drain.SetResult();
            Guid token = await begin;
            Assert.NotEqual(Guid.Empty, token);
            _gate.Verify(g => g.MarkRunning("Server held for database repair"), Times.Once);
        }

        [Fact]
        public async Task EndHold_GateEndThrows_StillReportsEndedAndFreesTheSlot()
        {
            _ = _gate.Setup(g => g.End()).Throws(new InvalidOperationException("gate broke"));
            MaintenanceCoordinator coordinator = CreateCoordinator();
            Guid token = await coordinator.BeginHoldAsync("Server held for database repair", CancellationToken.None);

            bool ended = coordinator.EndHold(token);

            Assert.True(ended);
            _ = await coordinator.BeginHoldAsync("Server held for database repair", CancellationToken.None);
        }

        [Fact]
        public async Task RenewHoldOutcome_PastMaxAge_ReturnsMaxAgeReached()
        {
            var clock = new CoordinatorFakeClock(new DateTimeOffset(2026, 11, 2, 9, 0, 0, TimeSpan.Zero));
            MaintenanceCoordinator coordinator = CreateCoordinator(new MaintenanceHoldTimer(TimeSpan.FromMinutes(30), TimeSpan.FromHours(4), clock));
            Guid token = await coordinator.BeginHoldAsync("Server held for database repair", CancellationToken.None);
            clock.Now = clock.Now.AddHours(4);

            Assert.Equal(HoldRenewResult.MaxAgeReached, coordinator.RenewHoldOutcome(token));
        }

        [Fact]
        public void RenewHoldResponse_MaxAgeReached_Returns409WithReason()
        {
            IResult result = DatabaseEndpoints.RenewHoldResponse(HoldRenewResult.MaxAgeReached);

            Assert.Equal(StatusCodes.Status409Conflict, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        }

        [Fact]
        public void RenewHoldResponse_NotActive_Returns404()
        {
            IResult result = DatabaseEndpoints.RenewHoldResponse(HoldRenewResult.NotActive);

            Assert.Equal(StatusCodes.Status404NotFound, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        }

        private sealed class CoordinatorFakeClock(DateTimeOffset start) : TimeProvider
        {
            public DateTimeOffset Now { get; set; } = start;

            public override DateTimeOffset GetUtcNow() => Now;
        }
    }
}
