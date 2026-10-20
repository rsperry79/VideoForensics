namespace VideoForensics.Ui.Shared.Tests;

using System.Reactive.Subjects;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using Moq;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Ui.Shared.Components;
using VideoForensics.Ui.Shared.Contracts;
using VideoForensics.Ui.Shared.Pages;
using VideoForensics.Ui.Shared.Services;
using Xunit;
using ProviderAccountEntity = VideoForensics.Data.Common.Entities.ProviderAccount;

/// <summary>Records every account id the add-account wizard publishes.</summary>
internal sealed class RecordingAccountLinkedNotifier : IAccountLinkedNotifier
{
    public List<Guid> Published { get; } = new();

    public void Publish(Guid providerAccountId) => Published.Add(providerAccountId);
}

/// <summary>Exposes a test-controlled stream as the account-linked source the accounts list subscribes to.</summary>
internal sealed class FakeAccountLinkedSource : IAccountLinkedSource
{
    private readonly Subject<Guid> _subject;

    public FakeAccountLinkedSource(Subject<Guid> subject)
    {
        _subject = subject;
    }

    public IObservable<Guid> AccountLinked => _subject;
}

public class AccountLinkedWiringTests_Wizard : BunitContext
{
    private readonly Mock<IMultiProviderAuthService> _multiProvider = new();
    private readonly Mock<IProviderAuthService> _providerAuth = new();
    private readonly RecordingAccountLinkedNotifier _notifier = new();

    public AccountLinkedWiringTests_Wizard()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddLocalization();
        Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();

        _multiProvider
            .Setup(m => m.GetAvailableProvidersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "Ring" });
        _multiProvider
            .Setup(m => m.GetService("Ring"))
            .Returns(_providerAuth.Object);

        Services.AddScoped(_ => _multiProvider.Object);
        Services.AddScoped(_ => _providerAuth.Object);
        Services.AddScoped(_ => new Mock<IForensicsConfiguration>().Object);
        Services.AddScoped(_ => new Mock<IForensicsConfigurationService>().Object);
        Services.AddScoped(sp => new PairedSessionState(sp.GetRequiredService<IJSRuntime>()));
        Services.AddScoped<IAccountLinkedNotifier>(_ => _notifier);
    }

    private IRenderedComponent<AddAccountWizard> RenderOnAuthStep()
    {
        var cut = Render<AddAccountWizard>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("button")));

        var strings = Services.GetRequiredService<IStringLocalizer<AddAccountWizard>>();
        cut.WaitForAssertion(() =>
        {
            var next = cut.FindAll("button").First(b => b.TextContent.Trim() == strings["Next"].Value);
            next.Click();
        });
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindComponents<AuthForm>()));
        return cut;
    }

    [Fact]
    public void AddAccountWizard_HandleAuthenticationSuccessWithAccountId_PublishesAccountId()
    {
        var accountId = Guid.NewGuid();
        var cut = RenderOnAuthStep();
        var form = cut.FindComponent<AuthForm>();

        cut.InvokeAsync(() => form.Instance.OnAuthenticated.InvokeAsync(
            new AuthResult(Success: true, ProviderAccountId: accountId)));

        Assert.Equal(new[] { accountId }, _notifier.Published);
    }

    [Fact]
    public void AddAccountWizard_HandleAuthenticationFailure_DoesNotPublish()
    {
        var cut = RenderOnAuthStep();
        var form = cut.FindComponent<AuthForm>();

        cut.InvokeAsync(() => form.Instance.OnAuthenticated.InvokeAsync(
            new AuthResult(Success: false, ErrorMessage: "bad password")));

        Assert.Empty(_notifier.Published);
    }

    [Fact]
    public void AddAccountWizard_UniviewAuthSuccessWithAccountId_PublishesAccountId()
    {
        var accountId = Guid.NewGuid();
        var univiewAuth = new Mock<IProviderAuthService>();
        univiewAuth
            .Setup(a => a.AuthenticateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthResult(Success: true, ProviderAccountId: accountId));
        _multiProvider
            .Setup(m => m.GetAvailableProvidersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "Uniview" });
        _multiProvider
            .Setup(m => m.GetService("Uniview"))
            .Returns(univiewAuth.Object);

        var cut = Render<AddAccountWizard>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("button")));

        var strings = Services.GetRequiredService<IStringLocalizer<AddAccountWizard>>();
        cut.WaitForAssertion(() =>
        {
            var next = cut.FindAll("button").First(b => b.TextContent.Trim() == strings["Next"].Value);
            next.Click();
        });
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("form")));

        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Equal(new[] { accountId }, _notifier.Published));
    }
}

public class AccountLinkedWiringTests_Accounts : AccountsPageTestBase
{
    private readonly Subject<Guid> _linked = new();

    public AccountLinkedWiringTests_Accounts()
    {
        Services.AddSingleton<IAccountLinkedSource>(new FakeAccountLinkedSource(_linked));
    }

    private int ListActiveCalls() =>
        AccountRepositoryMock.Invocations.Count(i => i.Method.Name == nameof(IProviderAccountRepository.ListActiveAsync));

    [Fact]
    public void Accounts_AccountLinkedEmitted_ReloadsRowsFromRepository()
    {
        var cut = RenderPage();
        Assert.DoesNotContain("Newly Linked Owner", cut.Markup);

        var newId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000003");
        var newUserId = Guid.NewGuid();
        AccountRepositoryMock
            .Setup(r => r.ListActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProviderAccountEntity>
            {
                new() { Id = RingAccountId, UserId = Guid.NewGuid(), ProviderName = "Ring" },
                new() { Id = newId, UserId = newUserId, ProviderName = "Ring" },
            });
        UserRepositoryMock
            .Setup(r => r.GetAsync(newUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { ProviderUserKey = "new-key", DisplayName = "Newly Linked Owner" });

        int before = ListActiveCalls();
        cut.InvokeAsync(() => _linked.OnNext(newId));

        cut.WaitForAssertion(() => Assert.Contains("Newly Linked Owner", cut.Markup));
        Assert.True(ListActiveCalls() > before);
    }

    [Fact]
    public void Accounts_Disposed_UnsubscribesFromAccountLinked()
    {
        var cut = RenderPage();

        // Precondition: while the page is alive, an emitted link triggers a reload.
        int beforeLive = ListActiveCalls();
        cut.InvokeAsync(() => _linked.OnNext(Guid.NewGuid()));
        cut.WaitForAssertion(() => Assert.True(ListActiveCalls() > beforeLive));

        cut.InvokeAsync(() => ((IDisposable)cut.Instance).Dispose());

        int afterDispose = ListActiveCalls();
        cut.InvokeAsync(() => _linked.OnNext(Guid.NewGuid()));

        Assert.Equal(afterDispose, ListActiveCalls());
    }
}
