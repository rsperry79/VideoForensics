
using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

using Moq;

using VideoForensics.Ui.Shared.Services;

using Xunit;

namespace VideoForensics.Ui.Shared.Tests;
/// <summary>
/// Covers which step-up a destructive action gets: a passkey challenge for passkey sessions, and a password
/// re-entry for password sessions. Passkey sessions never see a password prompt.
/// </summary>
public class StepUpServiceTests : BunitContext
{
    public StepUpServiceTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private static Mock<WebAuthnClient> CreateWebAuthn()
    {
        var webAuthn = new Mock<WebAuthnClient>(Mock.Of<IJSRuntime>(), Mock.Of<ISelfApiHttpClientFactory>());
        _ = webAuthn.Setup(w => w.StepUpAsync(It.IsAny<string>())).ReturnsAsync("tok-passkey");
        _ = webAuthn.Setup(w => w.StepUpWithPasswordAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync("tok-password");
        return webAuthn;
    }

    private async Task<PairedSessionState> SignedInSessionAsync(bool passkeySession, bool passwordSession, bool twoFactorRequired = false)
    {
        var session = new PairedSessionState(JSInterop.JSRuntime);
        await session.SetAsync("session-token", Guid.NewGuid(), "SuperAdmin");
        // Set after SetAsync: the server's answer is what the service routes on.
        session.SetServerSessionKind(localTier: true, passkeySession, passwordSession, twoFactorRequired);
        return session;
    }

    [Fact]
    public async Task StepUpService_PasskeySession_UsesPasskeyChallengeWithoutPasswordPrompt()
    {
        Mock<WebAuthnClient> webAuthn = CreateWebAuthn();
        PairedSessionState session = await SignedInSessionAsync(passkeySession: true, passwordSession: false);
        var service = new StepUpService(webAuthn.Object, session, Services.GetRequiredService<NavigationManager>());
        bool prompted = false;
        service.RegisterPasswordPrompt(() => { prompted = true; return Task.FromResult<string?>("unused"); });

        string token = await service.GetStepUpTokenAsync();

        Assert.Equal("tok-passkey", token);
        Assert.False(prompted);
        webAuthn.Verify(w => w.StepUpWithPasswordAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task StepUpService_PasswordSession_AsksForPasswordAndUsesIt()
    {
        Mock<WebAuthnClient> webAuthn = CreateWebAuthn();
        PairedSessionState session = await SignedInSessionAsync(passkeySession: false, passwordSession: true);
        var service = new StepUpService(webAuthn.Object, session, Services.GetRequiredService<NavigationManager>());
        service.RegisterPasswordPrompt(() => Task.FromResult<string?>("correct horse"));

        string token = await service.GetStepUpTokenAsync();

        Assert.Equal("tok-password", token);
        webAuthn.Verify(w => w.StepUpWithPasswordAsync("session-token", "correct horse"), Times.Once);
        webAuthn.Verify(w => w.StepUpAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task StepUpService_PasswordSession_CancelledPrompt_ThrowsAndSendsNothing()
    {
        Mock<WebAuthnClient> webAuthn = CreateWebAuthn();
        PairedSessionState session = await SignedInSessionAsync(passkeySession: false, passwordSession: true);
        var service = new StepUpService(webAuthn.Object, session, Services.GetRequiredService<NavigationManager>());
        service.RegisterPasswordPrompt(() => Task.FromResult<string?>(null));

        _ = await Assert.ThrowsAsync<InvalidOperationException>(service.GetStepUpTokenAsync);

        webAuthn.Verify(w => w.StepUpWithPasswordAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task StepUpService_PasswordSessionWhenTwoFactorRequired_SendsToPasskeySetupWithoutPrompt()
    {
        // The role's policy needs a passkey, so a password confirmation is not accepted; the operator is sent to upgrade.
        Mock<WebAuthnClient> webAuthn = CreateWebAuthn();
        PairedSessionState session = await SignedInSessionAsync(passkeySession: false, passwordSession: true, twoFactorRequired: true);
        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        var service = new StepUpService(webAuthn.Object, session, nav);
        bool prompted = false;
        service.RegisterPasswordPrompt(() => { prompted = true; return Task.FromResult<string?>("secret"); });

        _ = await Assert.ThrowsAsync<PasskeyUpgradeRequiredException>(service.GetStepUpTokenAsync);

        Assert.False(prompted);
        Assert.EndsWith("/settings/passkeys?setup=true", nav.Uri);
        webAuthn.Verify(w => w.StepUpWithPasswordAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task StepUpService_PasswordSessionWithoutDialog_Throws()
    {
        Mock<WebAuthnClient> webAuthn = CreateWebAuthn();
        PairedSessionState session = await SignedInSessionAsync(passkeySession: false, passwordSession: true);
        var service = new StepUpService(webAuthn.Object, session, Services.GetRequiredService<NavigationManager>());

        _ = await Assert.ThrowsAsync<InvalidOperationException>(service.GetStepUpTokenAsync);
    }
}
