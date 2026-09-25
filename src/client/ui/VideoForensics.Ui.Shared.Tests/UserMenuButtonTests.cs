namespace VideoForensics.Ui.Shared.Tests;

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Xunit;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Ui.Shared.Components;
using VideoForensics.Ui.Shared.Services;

/// <summary>
/// Shared setup for UserMenuButton bUnit tests. Registers the minimal Syncfusion services the
/// component's SfButton needs to render for real (the same three services EvidencePageTests
/// registers), plus a JSInterop set to Loose mode so PairedSessionState's
/// EnsureLoadedAsync/SetAsync/ClearAsync JS interop calls no-op instead of throwing.
/// </summary>
public abstract class UserMenuButtonTestBase : BunitContext
{
    protected UserMenuButtonTestBase()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        Services.AddLocalization();
        Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();
    }

    protected async Task<PairedSessionState> SignInAsync(OperatorRole role)
    {
        var session = new PairedSessionState(JSInterop.JSRuntime);
        await session.SetAsync("test-token", Guid.NewGuid(), role.ToString());
        Services.AddScoped(_ => session);
        return session;
    }

    protected PairedSessionState RegisterSignedOut()
    {
        var session = new PairedSessionState(JSInterop.JSRuntime);
        Services.AddScoped(_ => session);
        return session;
    }

    protected IRenderedComponent<UserMenuButton> RenderAndOpen()
    {
        var component = Render<UserMenuButton>();
        component.Find("[data-testid='user-menu-toggle']").Click();
        return component;
    }
}

public class UserMenuButton_SignedIn_Tests : UserMenuButtonTestBase
{
    [Fact]
    public async Task Renders_SignedInRole()
    {
        await SignInAsync(OperatorRole.Admin);

        var component = Render<UserMenuButton>();

        Assert.Equal("Admin", component.Find("[data-testid='user-menu']").GetAttribute("data-role-label"));
    }

    [Fact]
    public async Task OpeningMenu_ShowsChangePasswordPasskeysAndSignOut_NotDeviceSignIn()
    {
        await SignInAsync(OperatorRole.Admin);

        var component = RenderAndOpen();

        var items = component.FindAll("[data-testid='user-menu-item']");
        Assert.Contains(items, i => i.GetAttribute("data-path") == "/change-password");
        Assert.Contains(items, i => i.GetAttribute("data-path") == "/settings/passkeys");
        Assert.DoesNotContain(items, i => i.GetAttribute("data-path") == "/device-signin");
        Assert.NotEmpty(component.FindAll("[data-testid='user-menu-signout']"));
    }

    [Fact]
    public async Task ClickingPathItem_Navigates()
    {
        await SignInAsync(OperatorRole.Admin);

        var component = RenderAndOpen();
        component.Find("[data-path='/settings/passkeys']").Click();

        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.EndsWith("/settings/passkeys", nav.Uri);
    }

    [Fact]
    public async Task ClickingSignOut_ClearsSession_CallsJsClear_AndNavigatesToDeviceSignIn()
    {
        var session = await SignInAsync(OperatorRole.Admin);

        var component = RenderAndOpen();
        component.Find("[data-testid='user-menu-signout']").Click();

        Assert.False(session.IsSignedIn);
        Assert.Contains(JSInterop.Invocations, i => i.Identifier == "vfWebAuthn.clearSession");

        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.EndsWith("/device-signin", nav.Uri);
    }
}

public class UserMenuButton_SignedOut_Tests : UserMenuButtonTestBase
{
    [Fact]
    public void OpeningMenu_ShowsDeviceSignIn_NotChangePasswordOrPasskeys()
    {
        RegisterSignedOut();

        var component = RenderAndOpen();

        var items = component.FindAll("[data-testid='user-menu-item']");
        Assert.Contains(items, i => i.GetAttribute("data-path") == "/device-signin");
        Assert.DoesNotContain(items, i => i.GetAttribute("data-path") == "/change-password");
        Assert.DoesNotContain(items, i => i.GetAttribute("data-path") == "/settings/passkeys");
        Assert.Empty(component.FindAll("[data-testid='user-menu-signout']"));
    }
}
