using Moq;

using VideoForensics.Client.WebApp.Services;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.WebApp.Tests.Services;

public class InitialSyncResumerTests
{
    [Fact]
    public async Task ResumeIncompleteAsync_RunsOnlyAccountsWhoseFirstSyncDidNotFinish()
    {
        var finished = new ProviderAccount { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), ProviderName = "Ring", IsActive = true, InitialSyncCompleted = true };
        var interrupted = new ProviderAccount { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), ProviderName = "Ring", IsActive = true, InitialSyncCompleted = false };

        var accounts = new Mock<IProviderAccountRepository>();
        _ = accounts.Setup(a => a.ListActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([finished, interrupted]);
        var coordinator = new Mock<IInitialSyncCoordinator>();

        await new InitialSyncResumer(accounts.Object, coordinator.Object, Microsoft.Extensions.Logging.Abstractions.NullLogger<InitialSyncResumer>.Instance)
            .ResumeIncompleteAsync(CancellationToken.None);

        coordinator.Verify(c => c.RunAsync(interrupted.Id, It.IsAny<CancellationToken>()), Times.Once);
        coordinator.Verify(c => c.RunAsync(finished.Id, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResumeIncompleteAsync_OneAccountThrows_StillResumesTheRest()
    {
        var first = new ProviderAccount { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), ProviderName = "Ring", IsActive = true, InitialSyncCompleted = false };
        var second = new ProviderAccount { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), ProviderName = "Ring", IsActive = true, InitialSyncCompleted = false };

        var accounts = new Mock<IProviderAccountRepository>();
        _ = accounts.Setup(a => a.ListActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([first, second]);
        var coordinator = new Mock<IInitialSyncCoordinator>();
        _ = coordinator.Setup(c => c.RunAsync(first.Id, It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("boom"));

        await new InitialSyncResumer(accounts.Object, coordinator.Object, Microsoft.Extensions.Logging.Abstractions.NullLogger<InitialSyncResumer>.Instance)
            .ResumeIncompleteAsync(CancellationToken.None);

        coordinator.Verify(c => c.RunAsync(second.Id, It.IsAny<CancellationToken>()), Times.Once);
    }
}
