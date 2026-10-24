using VideoForensics.Data.Common.Contracts;
using VideoForensics.Client.Ui.Shared.Services;

using AngleSharp.Dom;

using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

using Moq;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Client.Ui.Shared.Components;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;
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

        _ = Services.AddLocalization();
        _ = Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        _ = Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        _ = Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();

        // Register default IUiModeService (will be overridden if needed)
        var defaultUiModeMock = new Mock<IUiModeService>();
        _ = defaultUiModeMock.SetupGet(s => s.Mode).Returns("Standard");
        _ = defaultUiModeMock.SetupGet(s => s.IsLocked).Returns(false);
        _ = defaultUiModeMock.Setup(s => s.InitializeAsync()).Returns(Task.CompletedTask);
        _ = defaultUiModeMock.Setup(s => s.SetModeAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        _ = defaultUiModeMock.SetupAdd(s => s.OnChange += It.IsAny<Action>());
        _ = Services.AddScoped(_ => defaultUiModeMock.Object);
    }

    protected async Task<PairedSessionState> SignInAsync(OperatorRole role)
    {
        var session = new PairedSessionState(JSInterop.JSRuntime);
        await session.SetAsync("test-token", Guid.NewGuid(), role.ToString());
        _ = Services.AddScoped(_ => session);
        _ = Services.AddScoped(_ => new ThemePreferenceService(JSInterop.JSRuntime, Mock.Of<IOperatorPreferencesRepository>(), session, Mock.Of<ICultureSwitcher>()));
        return session;
    }

    protected PairedSessionState RegisterSignedOut()
    {
        var session = new PairedSessionState(JSInterop.JSRuntime);
        _ = Services.AddScoped(_ => session);
        _ = Services.AddScoped(_ => new ThemePreferenceService(JSInterop.JSRuntime, Mock.Of<IOperatorPreferencesRepository>(), session, Mock.Of<ICultureSwitcher>()));
        return session;
    }

    protected IRenderedComponent<UserMenuButton> RenderAndOpen()
    {
        IRenderedComponent<UserMenuButton> component = Render<UserMenuButton>();
        component.Find("[data-testid='user-menu-toggle']").Click();
        return component;
    }
}

public class UserMenuButton_SignedIn_Tests : UserMenuButtonTestBase
{
    [Fact]
    public async Task Renders_SignedInRole()
    {
        _ = await SignInAsync(OperatorRole.Admin);

        IRenderedComponent<UserMenuButton> component = Render<UserMenuButton>();

        Assert.Equal("Admin", component.Find("[data-testid='user-menu']").GetAttribute("data-role-label"));
    }

    [Fact]
    public async Task OpeningMenu_ShowsChangePasswordPasskeysAndSignOut_NotDeviceSignIn()
    {
        _ = await SignInAsync(OperatorRole.Admin);

        IRenderedComponent<UserMenuButton> component = RenderAndOpen();

        IReadOnlyList<IElement> items = component.FindAll("[data-testid='user-menu-item']");
        Assert.Contains(items, i => i.GetAttribute("data-path") == "/change-password");
        Assert.Contains(items, i => i.GetAttribute("data-path") == "/settings/passkeys");
        Assert.DoesNotContain(items, i => i.GetAttribute("data-path") == "/signin");
        Assert.DoesNotContain(items, i => i.GetAttribute("data-path") == "/device-signin");
        Assert.NotEmpty(component.FindAll("[data-testid='user-menu-signout']"));
    }

    [Fact]
    public async Task ClickingPathItem_Navigates()
    {
        _ = await SignInAsync(OperatorRole.Admin);

        IRenderedComponent<UserMenuButton> component = RenderAndOpen();
        component.Find("[data-path='/settings/passkeys']").Click();

        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        Assert.EndsWith("/settings/passkeys", nav.Uri);
    }

    [Fact]
    public async Task ClickingSignOut_ClearsSession_CallsJsClear_AndNavigatesToSignIn()
    {
        PairedSessionState session = await SignInAsync(OperatorRole.Admin);

        IRenderedComponent<UserMenuButton> component = RenderAndOpen();
        component.Find("[data-testid='user-menu-signout']").Click();

        Assert.False(session.IsSignedIn);
        Assert.Contains(JSInterop.Invocations, i => i.Identifier == "vfWebAuthn.clearSession");

        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        Assert.Equal("/signin", new Uri(nav.Uri).AbsolutePath);
        Assert.Equal(string.Empty, new Uri(nav.Uri).Query);
    }

    [Fact]
    public async Task OpeningMenu_WhenStandardModeUnlocked_ShowsSimpleViewAction()
    {
        // Arrange
        _ = await SignInAsync(OperatorRole.Admin);
        var mockUiMode = new Mock<IUiModeService>();
        _ = mockUiMode.SetupGet(s => s.Mode).Returns("Standard");
        _ = mockUiMode.SetupGet(s => s.IsLocked).Returns(false);
        _ = mockUiMode.Setup(s => s.InitializeAsync()).Returns(Task.CompletedTask);
        _ = mockUiMode.Setup(s => s.SetModeAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        _ = mockUiMode.SetupAdd(s => s.OnChange += It.IsAny<Action>());
        _ = Services.AddScoped(_ => mockUiMode.Object);

        // Act
        IRenderedComponent<UserMenuButton> component = RenderAndOpen();

        // Assert
        IReadOnlyList<IElement> items = component.FindAll("[data-testid='user-menu-item']");
        IElement? modeItem = items.FirstOrDefault(i => i.GetAttribute("data-action") == "set-ui-mode:Simple");
        Assert.NotNull(modeItem);
        Assert.Equal("Switch to Simple view", modeItem.TextContent.Trim());
    }

    [Fact]
    public async Task ClickingSimpleModeAction_CallsSetModeAsync()
    {
        // Arrange
        _ = await SignInAsync(OperatorRole.Admin);
        var mockUiMode = new Mock<IUiModeService>();
        _ = mockUiMode.SetupGet(s => s.Mode).Returns("Standard");
        _ = mockUiMode.SetupGet(s => s.IsLocked).Returns(false);
        _ = mockUiMode.Setup(s => s.InitializeAsync()).Returns(Task.CompletedTask);
        _ = mockUiMode.Setup(s => s.SetModeAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        _ = mockUiMode.SetupAdd(s => s.OnChange += It.IsAny<Action>());
        _ = Services.AddScoped(_ => mockUiMode.Object);

        IRenderedComponent<UserMenuButton> component = RenderAndOpen();
        IElement modeItem = component.Find("[data-action='set-ui-mode:Simple']");

        // Act
        await component.InvokeAsync(() => modeItem.Click());

        // Assert
        mockUiMode.Verify(s => s.SetModeAsync("Simple"), Times.Once);
    }

    [Fact]
    public async Task OpeningMenu_WhenSimpleModeUnlocked_ShowsStandardViewAction()
    {
        // Arrange
        _ = await SignInAsync(OperatorRole.Admin);
        var mockUiMode = new Mock<IUiModeService>();
        _ = mockUiMode.SetupGet(s => s.Mode).Returns("Simple");
        _ = mockUiMode.SetupGet(s => s.IsLocked).Returns(false);
        _ = mockUiMode.Setup(s => s.InitializeAsync()).Returns(Task.CompletedTask);
        _ = mockUiMode.Setup(s => s.SetModeAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        _ = mockUiMode.SetupAdd(s => s.OnChange += It.IsAny<Action>());
        _ = Services.AddScoped(_ => mockUiMode.Object);

        // Act
        IRenderedComponent<UserMenuButton> component = RenderAndOpen();

        // Assert
        IReadOnlyList<IElement> items = component.FindAll("[data-testid='user-menu-item']");
        IElement? modeItem = items.FirstOrDefault(i => i.GetAttribute("data-action") == "set-ui-mode:Standard");
        Assert.NotNull(modeItem);
        Assert.Equal("Switch to Standard view", modeItem.TextContent.Trim());
    }

    [Fact]
    public async Task OpeningMenu_WhenUiModeLocked_HidesToggleAction()
    {
        // Arrange
        _ = await SignInAsync(OperatorRole.Admin);
        var mockUiMode = new Mock<IUiModeService>();
        _ = mockUiMode.SetupGet(s => s.Mode).Returns("Standard");
        _ = mockUiMode.SetupGet(s => s.IsLocked).Returns(true);
        _ = mockUiMode.Setup(s => s.InitializeAsync()).Returns(Task.CompletedTask);
        _ = mockUiMode.SetupAdd(s => s.OnChange += It.IsAny<Action>());
        _ = Services.AddScoped(_ => mockUiMode.Object);

        // Act
        IRenderedComponent<UserMenuButton> component = RenderAndOpen();

        // Assert
        IReadOnlyList<IElement> items = component.FindAll("[data-testid='user-menu-item']");
        IElement? modeItem = items.FirstOrDefault(i => i.GetAttribute("data-action")?.StartsWith("set-ui-mode:") ?? false);
        Assert.Null(modeItem);
    }

    [Fact]
    public async Task ClickingModeAction_WhenSetModeThrows_DoesNotCrash()
    {
        // Arrange
        _ = await SignInAsync(OperatorRole.Admin);
        var mockUiMode = new Mock<IUiModeService>();
        _ = mockUiMode.SetupGet(s => s.Mode).Returns("Standard");
        _ = mockUiMode.SetupGet(s => s.IsLocked).Returns(false);
        _ = mockUiMode.Setup(s => s.InitializeAsync()).Returns(Task.CompletedTask);
        _ = mockUiMode.Setup(s => s.SetModeAsync(It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("Mode is locked"));
        _ = mockUiMode.SetupAdd(s => s.OnChange += It.IsAny<Action>());
        _ = Services.AddScoped(_ => mockUiMode.Object);

        IRenderedComponent<UserMenuButton> component = RenderAndOpen();
        IElement modeItem = component.Find("[data-action='set-ui-mode:Simple']");

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
        _ = RegisterSignedOut();

        IRenderedComponent<UserMenuButton> component = RenderAndOpen();

        IReadOnlyList<IElement> items = component.FindAll("[data-testid='user-menu-item']");
        Assert.Contains(items, i => i.GetAttribute("data-path") == "/signin");
        Assert.DoesNotContain(items, i => i.GetAttribute("data-path") == "/change-password");
        Assert.DoesNotContain(items, i => i.GetAttribute("data-path") == "/settings/passkeys");
        Assert.Empty(component.FindAll("[data-testid='user-menu-signout']"));
    }
}
