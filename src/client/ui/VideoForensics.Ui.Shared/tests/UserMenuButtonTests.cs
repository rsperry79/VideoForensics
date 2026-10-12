namespace VideoForensics.Ui.Shared.Tests;

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Xunit;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Ui.Shared.Components;
using VideoForensics.Ui.Shared.Services;
using Moq;

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

        // Register default IUiModeService (will be overridden if needed)
        var defaultUiModeMock = new Mock<IUiModeService>();
        defaultUiModeMock.SetupGet(s => s.Mode).Returns("Standard");
        defaultUiModeMock.SetupGet(s => s.IsLocked).Returns(false);
        defaultUiModeMock.Setup(s => s.InitializeAsync()).Returns(Task.CompletedTask);
        defaultUiModeMock.Setup(s => s.SetModeAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        defaultUiModeMock.SetupAdd(s => s.OnChange += It.IsAny<Action>());
        Services.AddScoped(_ => defaultUiModeMock.Object);
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
        Assert.DoesNotContain(items, i => i.GetAttribute("data-path") == "/signin");
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
    public async Task ClickingSignOut_ClearsSession_CallsJsClear_AndNavigatesToSignIn()
    {
        var session = await SignInAsync(OperatorRole.Admin);

        var component = RenderAndOpen();
        component.Find("[data-testid='user-menu-signout']").Click();

        Assert.False(session.IsSignedIn);
        Assert.Contains(JSInterop.Invocations, i => i.Identifier == "vfWebAuthn.clearSession");

        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.Equal("/signin", new Uri(nav.Uri).AbsolutePath);
        Assert.Equal(string.Empty, new Uri(nav.Uri).Query);
    }

    [Fact]
    public async Task OpeningMenu_WhenStandardModeUnlocked_ShowsSimpleViewAction()
    {
        // Arrange
        await SignInAsync(OperatorRole.Admin);
        var mockUiMode = new Mock<IUiModeService>();
        mockUiMode.SetupGet(s => s.Mode).Returns("Standard");
        mockUiMode.SetupGet(s => s.IsLocked).Returns(false);
        mockUiMode.Setup(s => s.InitializeAsync()).Returns(Task.CompletedTask);
        mockUiMode.Setup(s => s.SetModeAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        mockUiMode.SetupAdd(s => s.OnChange += It.IsAny<Action>());
        Services.AddScoped(_ => mockUiMode.Object);

        // Act
        var component = RenderAndOpen();

        // Assert
        var items = component.FindAll("[data-testid='user-menu-item']");
        var modeItem = items.FirstOrDefault(i => i.GetAttribute("data-action") == "set-ui-mode:Simple");
        Assert.NotNull(modeItem);
        Assert.Equal("Switch to Simple view", modeItem.TextContent.Trim());
    }

    [Fact]
    public async Task ClickingSimpleModeAction_CallsSetModeAsync()
    {
        // Arrange
        await SignInAsync(OperatorRole.Admin);
        var mockUiMode = new Mock<IUiModeService>();
        mockUiMode.SetupGet(s => s.Mode).Returns("Standard");
        mockUiMode.SetupGet(s => s.IsLocked).Returns(false);
        mockUiMode.Setup(s => s.InitializeAsync()).Returns(Task.CompletedTask);
        mockUiMode.Setup(s => s.SetModeAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        mockUiMode.SetupAdd(s => s.OnChange += It.IsAny<Action>());
        Services.AddScoped(_ => mockUiMode.Object);

        var component = RenderAndOpen();
        var modeItem = component.Find("[data-action='set-ui-mode:Simple']");

        // Act
        await component.InvokeAsync(() => modeItem.Click());

        // Assert
        mockUiMode.Verify(s => s.SetModeAsync("Simple"), Times.Once);
    }

    [Fact]
    public async Task OpeningMenu_WhenSimpleModeUnlocked_ShowsStandardViewAction()
    {
        // Arrange
        await SignInAsync(OperatorRole.Admin);
        var mockUiMode = new Mock<IUiModeService>();
        mockUiMode.SetupGet(s => s.Mode).Returns("Simple");
        mockUiMode.SetupGet(s => s.IsLocked).Returns(false);
        mockUiMode.Setup(s => s.InitializeAsync()).Returns(Task.CompletedTask);
        mockUiMode.Setup(s => s.SetModeAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        mockUiMode.SetupAdd(s => s.OnChange += It.IsAny<Action>());
        Services.AddScoped(_ => mockUiMode.Object);

        // Act
        var component = RenderAndOpen();

        // Assert
        var items = component.FindAll("[data-testid='user-menu-item']");
        var modeItem = items.FirstOrDefault(i => i.GetAttribute("data-action") == "set-ui-mode:Standard");
        Assert.NotNull(modeItem);
        Assert.Equal("Switch to Standard view", modeItem.TextContent.Trim());
    }

    [Fact]
    public async Task OpeningMenu_WhenUiModeLocked_HidesToggleAction()
    {
        // Arrange
        await SignInAsync(OperatorRole.Admin);
        var mockUiMode = new Mock<IUiModeService>();
        mockUiMode.SetupGet(s => s.Mode).Returns("Standard");
        mockUiMode.SetupGet(s => s.IsLocked).Returns(true);
        mockUiMode.Setup(s => s.InitializeAsync()).Returns(Task.CompletedTask);
        mockUiMode.SetupAdd(s => s.OnChange += It.IsAny<Action>());
        Services.AddScoped(_ => mockUiMode.Object);

        // Act
        var component = RenderAndOpen();

        // Assert
        var items = component.FindAll("[data-testid='user-menu-item']");
        var modeItem = items.FirstOrDefault(i => i.GetAttribute("data-action")?.StartsWith("set-ui-mode:") ?? false);
        Assert.Null(modeItem);
    }

    [Fact]
    public async Task ClickingModeAction_WhenSetModeThrows_DoesNotCrash()
    {
        // Arrange
        await SignInAsync(OperatorRole.Admin);
        var mockUiMode = new Mock<IUiModeService>();
        mockUiMode.SetupGet(s => s.Mode).Returns("Standard");
        mockUiMode.SetupGet(s => s.IsLocked).Returns(false);
        mockUiMode.Setup(s => s.InitializeAsync()).Returns(Task.CompletedTask);
        mockUiMode.Setup(s => s.SetModeAsync(It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("Mode is locked"));
        mockUiMode.SetupAdd(s => s.OnChange += It.IsAny<Action>());
        Services.AddScoped(_ => mockUiMode.Object);

        var component = RenderAndOpen();
        var modeItem = component.Find("[data-action='set-ui-mode:Simple']");

        // Act - should not throw
        await component.InvokeAsync(() => modeItem.Click());

        // Assert - component is still rendered
        Assert.NotNull(component.Instance);
    }
}

public class UserMenuButton_SignedOut_Tests : UserMenuButtonTestBase
{
    [Fact]
    public void OpeningMenu_ShowsSignIn_NotChangePasswordOrPasskeys()
    {
        RegisterSignedOut();

        var component = RenderAndOpen();

        var items = component.FindAll("[data-testid='user-menu-item']");
        Assert.Contains(items, i => i.GetAttribute("data-path") == "/signin");
        Assert.DoesNotContain(items, i => i.GetAttribute("data-path") == "/change-password");
        Assert.DoesNotContain(items, i => i.GetAttribute("data-path") == "/settings/passkeys");
        Assert.Empty(component.FindAll("[data-testid='user-menu-signout']"));
    }
}