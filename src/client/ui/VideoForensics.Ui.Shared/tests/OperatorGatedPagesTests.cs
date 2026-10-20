namespace VideoForensics.Ui.Shared.Tests;

using System.Reactive.Subjects;

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Moq;
using Xunit;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Contracts;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Ui.Shared.Contracts;
using VideoForensics.Ui.Shared.Pages;
using VideoForensics.Ui.Shared.Services;
using Device = VideoForensics.Data.Common.Entities.Device;
using DeviceConfigPage = VideoForensics.Ui.Shared.Pages.DeviceConfig;

/// <summary>
/// Shared gate setup for the CollectVideos, CollectSnapshots and DeviceConfig pages. Each
/// needs an operator session (<see cref="PairedSessionState"/>) AND a linked provider (Ring)
/// account (<see cref="IProviderAuthService.IsAuthenticatedAsync"/>). The two gates are driven
/// independently so each page's three states can be tested: signed out, operator without a
/// provider account, and both present.
/// </summary>
public abstract class OperatorGatedPageTestBase : BunitContext
{
    protected Mock<IProviderAuthService> AuthServiceMock { get; } = new();
    protected Mock<IVideoDownloadService> DownloadServiceMock { get; } = new();
    protected Mock<IProviderAccountRepository> ProviderAccountRepositoryMock { get; } = new();
    protected Mock<IDeviceRepository> DeviceRepositoryMock { get; } = new();
    protected Mock<IDeviceConfigRepository> DeviceConfigRepositoryMock { get; } = new();
    protected Mock<IDeviceTypedSettingsReader> TypedSettingsReaderMock { get; } = new();

    protected OperatorGatedPageTestBase()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        Services.AddLocalization();
        Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();
        Services.AddSingleton<IDownloadProgressSource>(new IdleProgressSource());

        // Default: a Ring account is linked, no devices, no provider accounts to pick from.
        AuthServiceMock
            .Setup(m => m.IsAuthenticatedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        ProviderAccountRepositoryMock
            .Setup(m => m.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProviderAccount>());
        DeviceRepositoryMock
            .Setup(m => m.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Device>());
        TypedSettingsReaderMock
            .Setup(m => m.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeviceTypedSettings(null, [], []));

        Services.AddScoped(_ => AuthServiceMock.Object);
        Services.AddScoped(_ => TypedSettingsReaderMock.Object);
        Services.AddScoped(_ => DownloadServiceMock.Object);
        Services.AddScoped(_ => ProviderAccountRepositoryMock.Object);
        Services.AddScoped(_ => DeviceRepositoryMock.Object);
        Services.AddScoped(_ => DeviceConfigRepositoryMock.Object);
        Services.AddScoped<RightPanelContentService>();
        Services.AddScoped(sp => new PairedSessionState(sp.GetRequiredService<IJSRuntime>()));
        Services.AddScoped(sp => new AccountSelectionState(sp.GetRequiredService<IJSRuntime>()));

        SignInOperatorAsync().GetAwaiter().GetResult();
    }

    /// <summary>Download progress that never emits, so the panel renders its idle state.</summary>
    private sealed class IdleProgressSource : IDownloadProgressSource
    {
        public IObservable<DownloadProgressSnapshot> Progress { get; } = new Subject<DownloadProgressSnapshot>();
    }

    protected Task SignInOperatorAsync() =>
        Services.GetRequiredService<PairedSessionState>().SetAsync("test-token", Guid.NewGuid(), "Review");

    protected Task SignOutOperatorAsync() => Services.GetRequiredService<PairedSessionState>().ClearAsync();

    /// <summary>Signs in a SuperAdmin on the Local tier - the only operator who may open the account wizard.</summary>
    protected async Task SignInAsSuperAdminLocalAsync()
    {
        var session = Services.GetRequiredService<PairedSessionState>();
        await session.SetAsync("test-token", Guid.NewGuid(), "SuperAdmin");
        session.SetServerSessionKind(localTier: true, passkeySession: false);
    }

    protected void ProviderAccountLinked(bool linked) =>
        AuthServiceMock
            .Setup(m => m.IsAuthenticatedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(linked);

    /// <summary>Markup of the "operator signed in, no provider account" message (its data-testid is the contract).</summary>
    protected const string NoProviderAccountTestId = "no-provider-account";
}

public class CollectVideos_OperatorGate_Tests : OperatorGatedPageTestBase
{
    [Fact]
    public async Task CollectVideos_NoRingAccountAndNotLocalSuperAdmin_ShowsAdminMessage_NotWizardLink()
    {
        await SignInOperatorAsync();
        ProviderAccountLinked(false);

        var component = Render<CollectVideos>();

        Assert.Single(component.FindAll($"[data-testid='{NoProviderAccountTestId}']"));
        Assert.Empty(component.FindAll("a[href='/accounts/add']"));
        Assert.Contains("Ask an administrator to link a provider account.", component.Markup);
    }

    [Fact]
    public async Task CollectVideos_OperatorSignedOut_ShowsSignInPrompt()
    {
        await SignOutOperatorAsync();
        ProviderAccountLinked(true);

        var component = Render<CollectVideos>();

        Assert.Contains("You are not signed in.", component.Markup);
        Assert.Empty(component.FindAll($"[data-testid='{NoProviderAccountTestId}']"));
    }

    [Fact]
    public async Task CollectVideos_OperatorSignedInNoRingAccount_ShowsNoAccountMessage_NotSignInPrompt()
    {
        await SignInAsSuperAdminLocalAsync();
        ProviderAccountLinked(false);

        var component = Render<CollectVideos>();

        Assert.DoesNotContain("You are not signed in.", component.Markup);
        Assert.Single(component.FindAll($"[data-testid='{NoProviderAccountTestId}']"));
        Assert.NotEmpty(component.FindAll("a[href='/accounts/add']"));
    }

    [Fact]
    public async Task CollectVideos_RingAccountLinked_ShowsContent()
    {
        await SignInOperatorAsync();
        ProviderAccountLinked(true);

        var component = Render<CollectVideos>();

        Assert.DoesNotContain("You are not signed in.", component.Markup);
        Assert.Empty(component.FindAll($"[data-testid='{NoProviderAccountTestId}']"));
        Assert.Contains("Start Download", component.Markup);
    }
}

public class CollectSnapshots_OperatorGate_Tests : OperatorGatedPageTestBase
{
    [Fact]
    public async Task CollectSnapshots_NoRingAccountAndNotLocalSuperAdmin_ShowsAdminMessage_NotWizardLink()
    {
        await SignInOperatorAsync();
        ProviderAccountLinked(false);

        var component = Render<CollectSnapshots>();

        Assert.Single(component.FindAll($"[data-testid='{NoProviderAccountTestId}']"));
        Assert.Empty(component.FindAll("a[href='/accounts/add']"));
        Assert.Contains("Ask an administrator to link a provider account.", component.Markup);
    }

    [Fact]
    public async Task CollectSnapshots_OperatorSignedOut_ShowsSignInPrompt()
    {
        await SignOutOperatorAsync();
        ProviderAccountLinked(true);

        var component = Render<CollectSnapshots>();

        Assert.Contains("You are not signed in.", component.Markup);
        Assert.Empty(component.FindAll($"[data-testid='{NoProviderAccountTestId}']"));
    }

    [Fact]
    public async Task CollectSnapshots_OperatorSignedInNoRingAccount_ShowsNoAccountMessage_NotSignInPrompt()
    {
        await SignInAsSuperAdminLocalAsync();
        ProviderAccountLinked(false);

        var component = Render<CollectSnapshots>();

        Assert.DoesNotContain("You are not signed in.", component.Markup);
        Assert.Single(component.FindAll($"[data-testid='{NoProviderAccountTestId}']"));
        Assert.NotEmpty(component.FindAll("a[href='/accounts/add']"));
    }

    [Fact]
    public async Task CollectSnapshots_RingAccountLinked_ShowsContent()
    {
        await SignInOperatorAsync();
        ProviderAccountLinked(true);

        var component = Render<CollectSnapshots>();

        Assert.DoesNotContain("You are not signed in.", component.Markup);
        Assert.Empty(component.FindAll($"[data-testid='{NoProviderAccountTestId}']"));
        Assert.Contains("Start Download", component.Markup);
    }
}

public class DeviceConfig_OperatorGate_Tests : OperatorGatedPageTestBase
{
    [Fact]
    public async Task DeviceConfigPage_NoRingAccountAndNotLocalSuperAdmin_ShowsAdminMessage_NotWizardLink()
    {
        await SignInOperatorAsync();
        ProviderAccountLinked(false);

        var component = Render<DeviceConfigPage>();

        Assert.Single(component.FindAll($"[data-testid='{NoProviderAccountTestId}']"));
        Assert.Empty(component.FindAll("a[href='/accounts/add']"));
        Assert.Contains("Ask an administrator to link a provider account.", component.Markup);
    }

    [Fact]
    public async Task DeviceConfig_OperatorSignedOut_ShowsSignInPrompt()
    {
        await SignOutOperatorAsync();
        ProviderAccountLinked(true);

        var component = Render<DeviceConfigPage>();

        Assert.Contains("You are not signed in.", component.Markup);
        Assert.Empty(component.FindAll($"[data-testid='{NoProviderAccountTestId}']"));
    }

    [Fact]
    public async Task DeviceConfig_OperatorSignedInNoRingAccount_ShowsNoAccountMessage_NotSignInPrompt()
    {
        await SignInAsSuperAdminLocalAsync();
        ProviderAccountLinked(false);

        var component = Render<DeviceConfigPage>();

        Assert.DoesNotContain("You are not signed in.", component.Markup);
        Assert.Single(component.FindAll($"[data-testid='{NoProviderAccountTestId}']"));
        Assert.NotEmpty(component.FindAll("a[href='/accounts/add']"));
    }

    [Fact]
    public async Task DeviceConfig_RingAccountLinked_ShowsContent()
    {
        await SignInOperatorAsync();
        ProviderAccountLinked(true);

        var component = Render<DeviceConfigPage>();

        Assert.DoesNotContain("You are not signed in.", component.Markup);
        Assert.Empty(component.FindAll($"[data-testid='{NoProviderAccountTestId}']"));
    }
}
