using Bunit;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

using VideoForensics.Client.Ui.Shared.Services;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Services;

public class PairedSessionStateReplaceTokenTests : BunitContext
{
    public PairedSessionStateReplaceTokenTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _ = Services.AddScoped(sp => new PairedSessionState(sp.GetRequiredService<IJSRuntime>()));
    }

    [Fact]
    public async Task ReplaceSessionTokenAsync_SignedIn_SwapsTokenAndKeepsOperator()
    {
        PairedSessionState session = Services.GetRequiredService<PairedSessionState>();
        Guid operatorId = Guid.NewGuid();
        await session.SetAsync("old-token", operatorId, "Admin");

        await session.ReplaceSessionTokenAsync("renewed-token");

        Assert.Equal("renewed-token", session.SessionToken);
        Assert.Equal(operatorId, session.OperatorId);
        Assert.Equal("Admin", session.Role);
    }

    [Fact]
    public async Task ReplaceSessionTokenAsync_NotSignedIn_LeavesSessionEmpty()
    {
        PairedSessionState session = Services.GetRequiredService<PairedSessionState>();

        await session.ReplaceSessionTokenAsync("renewed-token");

        Assert.Null(session.SessionToken);
    }

    [Fact]
    public async Task ReplaceSessionTokenAsync_EmptyToken_KeepsOldToken()
    {
        PairedSessionState session = Services.GetRequiredService<PairedSessionState>();
        await session.SetAsync("old-token", Guid.NewGuid(), "Admin");

        await session.ReplaceSessionTokenAsync(string.Empty);

        Assert.Equal("old-token", session.SessionToken);
    }
}
