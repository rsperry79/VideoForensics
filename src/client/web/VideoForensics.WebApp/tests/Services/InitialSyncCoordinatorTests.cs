using Moq;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting.Services;
using VideoForensics.WebApp.Services;

using Xunit;

namespace VideoForensics.WebApp.Tests.Services;

public class InitialSyncCoordinatorTests
{
    private readonly Guid _accountId = Guid.NewGuid();
    private readonly Mock<IAccountSyncRunner> _runner = new();
    private readonly Mock<IProviderAccountRepository> _accounts = new();
    private readonly Mock<IAccountDeviceRepository> _accountDevices = new();
    private readonly Mock<IEvidenceValidationService> _validation = new();
    private readonly ProviderAccount _account;

    public InitialSyncCoordinatorTests()
    {
        _account = new ProviderAccount
        {
            Id = _accountId,
            UserId = Guid.NewGuid(),
            ProviderName = "Ring",
            IsActive = true,
            InitialSyncCompleted = false
        };
        _ = _accounts.Setup(a => a.GetAsync(_accountId, It.IsAny<CancellationToken>())).ReturnsAsync(() => _account);
        _ = _accountDevices.Setup(d => d.ListByProviderAccountAsync(_accountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreateDevice("100")]);
    }

    [Fact]
    public async Task RunAsync_AllStagesSucceed_SetsInitialSyncCompleted()
    {
        _ = _validation.Setup(v => v.VerifyLocalIntegrityAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>(), It.IsAny<IProgress<string>?>()))
            .ReturnsAsync([]);
        _ = _validation.Setup(v => v.ReconcileWithProviderAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await CreateSut().RunAsync(_accountId, CancellationToken.None);

        Assert.True(_account.InitialSyncCompleted);
        _accounts.Verify(a => a.UpdateAsync(_account, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunInitialSyncAsync_DispatchedFromScheduler_RunsTheSameFirstSync()
    {
        _ = _validation.Setup(v => v.VerifyLocalIntegrityAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>(), It.IsAny<IProgress<string>?>()))
            .ReturnsAsync([]);
        _ = _validation.Setup(v => v.ReconcileWithProviderAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await CreateSut().RunInitialSyncAsync(_accountId, CancellationToken.None);

        _runner.Verify(r => r.RunInitialAsync(_accountId, It.IsAny<CancellationToken>()), Times.Once);
        Assert.True(_account.InitialSyncCompleted);
    }

    [Fact]
    public async Task RunAsync_RunnerRecordsError_LeavesFlagFalseAndSkipsValidation()
    {
        _account.LastErrorMessage = "Saved credentials could not be restored.";

        await CreateSut().RunAsync(_accountId, CancellationToken.None);

        Assert.False(_account.InitialSyncCompleted);
        _validation.Verify(v => v.VerifyLocalIntegrityAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>(), It.IsAny<IProgress<string>?>()), Times.Never);
        _accounts.Verify(a => a.UpdateAsync(It.IsAny<ProviderAccount>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_ValidationReportsFailedDevice_LeavesFlagFalse()
    {
        _ = _validation.Setup(v => v.VerifyLocalIntegrityAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>(), It.IsAny<IProgress<string>?>()))
            .ThrowsAsync(new InvalidOperationException("disk offline"));

        await CreateSut().RunAsync(_accountId, CancellationToken.None);

        Assert.False(_account.InitialSyncCompleted);
        _accounts.Verify(a => a.UpdateAsync(It.IsAny<ProviderAccount>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_AccountRemovedDuringSync_DoesNotSetFlag()
    {
        _ = _accounts.Setup(a => a.GetAsync(_accountId, It.IsAny<CancellationToken>())).ReturnsAsync((ProviderAccount?)null);

        await CreateSut().RunAsync(_accountId, CancellationToken.None);

        _accounts.Verify(a => a.UpdateAsync(It.IsAny<ProviderAccount>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private InitialSyncCoordinator CreateSut()
    {
        var validation = new BulkValidationService(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<BulkValidationService>.Instance,
            _validation.Object,
            new Mock<IDeviceRepository>().Object);

        return new InitialSyncCoordinator(
            _runner.Object,
            _accounts.Object,
            _accountDevices.Object,
            validation,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<InitialSyncCoordinator>.Instance,
            new SyncActivityLog());
    }

    private static Device CreateDevice(string providerDeviceId)
    {
        return new()
        {
            Id = Guid.NewGuid(),
            LocationId = Guid.NewGuid(),
            ProviderDeviceId = providerDeviceId,
            Name = "Front Door",
            Type = "doorbell"
        };
    }
}
