namespace VideoForensics.Ui.Shared.Tests;

using Bunit;
using Microsoft.JSInterop;
using VideoForensics.Ui.Shared.Services;
using Xunit;

/// <summary>
/// Covers the passkey-setup-only flag on PairedSessionState: set by a create-passkey sign-in, persisted
/// alongside the other session fields, and cleared with the session.
/// </summary>
public class PairedSessionStateTests : BunitContext
{
    public PairedSessionStateTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public async Task PairedSessionState_SetAsyncWithPasskeySetupOnly_PersistsFlag()
    {
        // Arrange
        var state = new PairedSessionState(JSInterop.JSRuntime);

        // Act
        await state.SetAsync("token", Guid.NewGuid(), "ReadOnly", mustChangePassword: false, passkeySetupOnly: true);

        // Assert - in memory, and in the JSON handed to vfWebAuthn.saveSession so a reload restores it
        Assert.True(state.PasskeySetupOnly);
        string saved = JSInterop.Invocations.Single(i => i.Identifier == "vfWebAuthn.saveSession").Arguments[0]!.ToString()!;

        JSInterop.Setup<string?>("vfWebAuthn.loadSession").SetResult(saved);
        var reloaded = new PairedSessionState(JSInterop.JSRuntime);
        await reloaded.EnsureLoadedAsync();
        Assert.True(reloaded.PasskeySetupOnly);
    }

    [Fact]
    public async Task PairedSessionState_ClearAsync_ClearsPasskeySetupOnly()
    {
        // Arrange
        var state = new PairedSessionState(JSInterop.JSRuntime);
        await state.SetAsync("token", Guid.NewGuid(), "ReadOnly", passkeySetupOnly: true);

        // Act
        await state.ClearAsync();

        // Assert
        Assert.False(state.PasskeySetupOnly);
    }
}
