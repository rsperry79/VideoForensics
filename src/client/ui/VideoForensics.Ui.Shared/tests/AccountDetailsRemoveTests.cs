namespace VideoForensics.Ui.Shared.Tests;

using System.Net;
using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Moq;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Ui.Shared.Contracts;
using VideoForensics.Ui.Shared.Pages;
using VideoForensics.Ui.Shared.Services;
using Xunit;
using ProviderAccountEntity = VideoForensics.Data.Common.Entities.ProviderAccount;

/// <summary>
/// Shared setup for the /accounts/{id} remove flow. The Accounts grid no longer has a Remove button, so the
/// remove panel is reached from the details page's "Remove Account" button.
/// </summary>
public abstract class AccountDetailsRemoveTestBase : BunitContext
{
    protected Mock<IProviderAccountRepository> AccountRepositoryMock { get; } = new();
    protected Mock<IUserRepository> UserRepositoryMock { get; } = new();
    protected Mock<IAccountExportService> ExportServiceMock { get; } = new();
    protected Mock<WebAuthnClient> WebAuthnMock { get; } = new(Mock.Of<IJSRuntime>(), Mock.Of<ISelfApiHttpClientFactory>());

    protected static readonly Guid RingAccountId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    protected AccountDetailsRemoveTestBase()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddLocalization();
        ScopeOwnershipTestDoubles.RegisterEmpty(Services);
        Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();
        Services.AddSingleton<IDownloadProgressSource>(new SilentProgressSource());

        var ringUserId = Guid.NewGuid();
        AccountRepositoryMock
            .Setup(r => r.GetAsync(RingAccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderAccountEntity { Id = RingAccountId, UserId = ringUserId, ProviderName = "Ring", LinkedUtc = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc) });
        UserRepositoryMock
            .Setup(r => r.GetAsync(ringUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { ProviderUserKey = "ring-key", DisplayName = "Ring Owner" });

        Services.AddScoped(_ => AccountRepositoryMock.Object);
        Services.AddScoped(_ => ExportServiceMock.Object);
        Services.AddScoped(_ => UserRepositoryMock.Object);
        Services.AddScoped(_ => new Mock<IProviderAuthService>().Object);
        Services.AddScoped(_ => new Mock<IMultiProviderAuthService>().Object);
        Services.AddScoped(_ => new Mock<IForensicsConfiguration>().Object);
        Services.AddScoped(_ => new Mock<IForensicsConfigurationService>().Object);
        Services.AddScoped(sp => new AccountSelectionState(sp.GetRequiredService<IJSRuntime>()));
        // Signed-in passkey session so StepUpService can fetch a step-up token for remove/retire/export.
        Services.AddScoped(sp =>
        {
            var session = new PairedSessionState(sp.GetRequiredService<IJSRuntime>());
            session.SetAsync("session-token", Guid.NewGuid(), "SuperAdmin").GetAwaiter().GetResult();
            session.SetServerSessionKind(localTier: true, passkeySession: true, passwordSession: false);
            return session;
        });
        // The sync-run and case lookups on the details page are not under test; a 404 makes them show their error state.
        Services.AddScoped(_ => new HttpClient(new NotFoundHandler()));
        WebAuthnMock.Setup(w => w.StepUpAsync(It.IsAny<string>())).ReturnsAsync("step-token");
        Services.AddScoped(_ => WebAuthnMock.Object);
        Services.AddScoped<StepUpService>();
        SetRendererInfo(new RendererInfo("Server", true));
    }

    protected IRenderedComponent<AccountDetails> RenderPage() =>
        Render<AccountDetails>(parameters => parameters.Add(p => p.Id, RingAccountId));

    /// <summary>Clicks the first button with exactly this text, waiting for it to render first.</summary>
    protected static void ClickButton(IRenderedComponent<AccountDetails> cut, string text)
    {
        IElement? target = null;
        cut.WaitForAssertion(() => target = cut.FindAll("button").First(b => b.TextContent.Trim() == text));
        target!.Click();
    }

    private sealed class NotFoundHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}

public class AccountDetailsRemove_Tests : AccountDetailsRemoveTestBase
{
    [Fact]
    public void AccountDetails_RemoveRetain_CallsRetire()
    {
        var cut = RenderPage();

        ClickButton(cut, "Remove Account");
        ClickButton(cut, "Retain data");

        cut.WaitForAssertion(() =>
            AccountRepositoryMock.Verify(r => r.RetireAsync(RingAccountId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()), Times.Once));
        AccountRepositoryMock.Verify(r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public void AccountDetails_RemoveRetain_PassesStepUpTokenToRetire()
    {
        var cut = RenderPage();

        ClickButton(cut, "Remove Account");
        ClickButton(cut, "Retain data");

        cut.WaitForAssertion(() =>
            AccountRepositoryMock.Verify(r => r.RetireAsync(RingAccountId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>(), "step-token"), Times.Once));
    }

    [Fact]
    public void AccountDetails_RemoveRetain_StepUpCancelled_DoesNotCallRetire()
    {
        WebAuthnMock.Setup(w => w.StepUpAsync(It.IsAny<string>())).ThrowsAsync(new InvalidOperationException("cancelled"));
        var cut = RenderPage();

        ClickButton(cut, "Remove Account");
        ClickButton(cut, "Retain data");

        cut.WaitForAssertion(() => WebAuthnMock.Verify(w => w.StepUpAsync(It.IsAny<string>()), Times.Once));
        cut.WaitForAssertion(() => Assert.Contains("text-danger", cut.Markup));
        AccountRepositoryMock.Verify(r => r.RetireAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public void AccountDetails_RemoveDeletePermanently_NoExport_CallsDelete()
    {
        var cut = RenderPage();

        ClickButton(cut, "Remove Account");
        ClickButton(cut, "Delete permanently");
        ClickButton(cut, "Delete without export");

        cut.WaitForAssertion(() =>
            AccountRepositoryMock.Verify(r => r.DeleteAsync(RingAccountId, It.IsAny<CancellationToken>(), It.IsAny<string?>()), Times.Once));
        ExportServiceMock.Verify(e => e.QueueExportAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public void AccountDetails_RemoveDeletePermanently_WithExport_QueuesExportOnlyAndKeepsAccount()
    {
        var jobId = Guid.NewGuid();
        ExportServiceMock
            .Setup(e => e.QueueExportAsync(RingAccountId, false, It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .ReturnsAsync(jobId);
        ExportServiceMock
            .Setup(e => e.GetStatusAsync(jobId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExportJobStatus(jobId, RingAccountId, ExportJobState.Running, null, null, true, DateTime.UtcNow, null));

        var cut = RenderPage();
        ClickButton(cut, "Remove Account");
        ClickButton(cut, "Delete permanently");
        ClickButton(cut, "Export first");

        cut.WaitForAssertion(() =>
            ExportServiceMock.Verify(e => e.QueueExportAsync(RingAccountId, false, It.IsAny<CancellationToken>(), It.IsAny<string?>()), Times.Once));
        AccountRepositoryMock.Verify(r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()), Times.Never);
    }
}

public class AccountDetailsPurge_Tests : AccountDetailsRemoveTestBase
{
    [Fact]
    public void AccountDetails_ExportAndDelete_FirstClick_OnlyAsksForConfirmation()
    {
        var cut = RenderPage();

        ClickButton(cut, "Remove Account");
        ClickButton(cut, "Delete permanently");
        ClickButton(cut, "Export and delete");

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='account-purge-confirm']")));
        ExportServiceMock.Verify(e => e.QueueExportAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()), Times.Never);
        AccountRepositoryMock.Verify(r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public void AccountDetails_ExportAndDelete_Confirmed_QueuesPurgeWithStepUp()
    {
        var cut = RenderPage();

        ClickButton(cut, "Remove Account");
        ClickButton(cut, "Delete permanently");
        ClickButton(cut, "Export and delete");
        ClickButton(cut, "Export and delete now");

        cut.WaitForAssertion(() =>
            ExportServiceMock.Verify(e => e.QueueExportAsync(RingAccountId, true, It.IsAny<CancellationToken>(), "step-token"), Times.Once));
        AccountRepositoryMock.Verify(r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public void AccountDetails_ExportAndDelete_CancelledConfirmation_QueuesNothing()
    {
        var cut = RenderPage();

        ClickButton(cut, "Remove Account");
        ClickButton(cut, "Delete permanently");
        ClickButton(cut, "Export and delete");
        ClickButton(cut, "Cancel");

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid='account-purge-confirm']")));
        ExportServiceMock.Verify(e => e.QueueExportAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()), Times.Never);
    }
}
