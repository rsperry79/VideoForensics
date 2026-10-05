namespace VideoForensics.Ui.Shared.Tests;

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Moq;
using Xunit;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Ui.Shared.Components;
using VideoForensics.Ui.Shared.Layout;
using VideoForensics.Ui.Shared.Layout.Mobile;
using VideoForensics.Ui.Shared.Layout.Simple;
using VideoForensics.Ui.Shared.Services;

/// <summary>
/// Tests for SimpleLayout component rendering and mode switching behavior.
/// Covers simple-mode specific layout, responsiveness, and component composition.
/// </summary>
public abstract class SimpleModeLayoutTestBase : BunitContext
{
    protected SimpleModeLayoutTestBase()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        Services.AddLocalization();
        Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();
    }

    protected PairedSessionState RegisterSignedInSession()
    {
        var session = new PairedSessionState(JSInterop.JSRuntime);
        Services.AddScoped(_ => session);
        return session;
    }

    protected void RegisterUiModeService(string mode = "Standard")
    {
        var mockService = new Mock<UiModeService>(
            Mock.Of<IOperatorPreferencesRepository>(),
            RegisterSignedInSession()
        );
        mockService.SetupGet(s => s.Mode).Returns(mode);
        mockService.SetupAdd(s => s.OnChange += It.IsAny<Action>());
        Services.AddScoped(_ => mockService.Object);
    }

    protected void RegisterViewportService()
    {
        var mockViewport = new Mock<IViewportService>();
        mockViewport.SetupGet(v => v.IsMobile).Returns(false);
        mockViewport.SetupAdd(v => v.OnChange += It.IsAny<Action>());
        Services.AddScoped(_ => mockViewport.Object);
    }
}

public class ResponsiveLayout_ModeSelection_Tests : SimpleModeLayoutTestBase
{
    [Fact]
    public void ResponsiveLayout_SimpleMode_RendersSimpleLayout()
    {
        // Arrange
        RegisterViewportService();
        RegisterUiModeService("Simple");

        // Act
        var component = Render<ResponsiveLayout>();

        // Assert
        var simpleLayout = component.FindComponent<SimpleLayout>();
        Assert.NotNull(simpleLayout);
    }

    [Fact]
    public void ResponsiveLayout_StandardMode_RendersMainLayout()
    {
        // Arrange
        RegisterViewportService();
        RegisterUiModeService("Standard");

        // Act
        var component = Render<ResponsiveLayout>();

        // Assert
        var mainLayout = component.FindComponent<MainLayout>();
        Assert.NotNull(mainLayout);
    }

    [Fact]
    public void ResponsiveLayout_StandardModeOnDesktop_RendersMainLayout()
    {
        // Arrange
        var mockViewport = new Mock<IViewportService>();
        mockViewport.SetupGet(v => v.IsMobile).Returns(false);
        mockViewport.SetupAdd(v => v.OnChange += It.IsAny<Action>());
        Services.AddScoped(_ => mockViewport.Object);

        RegisterUiModeService("Standard");

        // Act
        var component = Render<ResponsiveLayout>();

        // Assert
        var mainLayout = component.FindComponent<MainLayout>();
        Assert.NotNull(mainLayout);
    }

    [Fact]
    public void ResponsiveLayout_StandardModeOnMobile_RendersMobileLayout()
    {
        // Arrange
        var mockViewport = new Mock<IViewportService>();
        mockViewport.SetupGet(v => v.IsMobile).Returns(true);
        mockViewport.SetupAdd(v => v.OnChange += It.IsAny<Action>());
        Services.AddScoped(_ => mockViewport.Object);

        RegisterUiModeService("Standard");

        // Act
        var component = Render<ResponsiveLayout>();

        // Assert
        var mobileLayout = component.FindComponent<MobileLayout>();
        Assert.NotNull(mobileLayout);
    }
}

public class SimpleLayout_Rendering_Tests : SimpleModeLayoutTestBase
{
    [Fact]
    public void SimpleLayout_Renders_WithChatPanel()
    {
        // Arrange
        RegisterSignedInSession();
        RegisterViewportService();
        RegisterUiModeService("Simple");

        // Act
        var component = Render<SimpleLayout>();

        // Assert
        var chatPanel = component.FindComponent<ChatPanel>();
        Assert.NotNull(chatPanel);
    }

    [Fact]
    public void SimpleLayout_Renders_WithHeaderTitle()
    {
        // Arrange
        RegisterSignedInSession();
        RegisterViewportService();
        RegisterUiModeService("Simple");

        // Act
        var component = Render<SimpleLayout>();

        // Assert
        var header = component.Find(".simple-layout-header");
        Assert.NotNull(header);
        var title = component.Find(".simple-layout-title");
        Assert.NotNull(title);
        Assert.Contains("VideoForensics", title.TextContent);
    }

    [Fact]
    public void SimpleLayout_Renders_WithSignOutButton()
    {
        // Arrange
        RegisterSignedInSession();
        RegisterViewportService();
        RegisterUiModeService("Simple");

        // Act
        var component = Render<SimpleLayout>();

        // Assert
        var buttons = component.FindAll("button");
        var signOutButton = buttons.FirstOrDefault(b => b.GetAttribute("class")?.Contains("simple-layout-signout") ?? false);
        Assert.NotNull(signOutButton);
    }

    [Fact]
    public void SimpleLayout_Renders_WithMainContent()
    {
        // Arrange
        RegisterSignedInSession();
        RegisterViewportService();
        RegisterUiModeService("Simple");

        // Act
        var component = Render<SimpleLayout>();

        // Assert
        var mainContent = component.Find(".simple-layout-content");
        Assert.NotNull(mainContent);
    }

    [Fact]
    public void SimpleLayout_Renders_WithChatContainer()
    {
        // Arrange
        RegisterSignedInSession();
        RegisterViewportService();
        RegisterUiModeService("Simple");

        // Act
        var component = Render<SimpleLayout>();

        // Assert
        var chatContainer = component.Find(".simple-layout-chat");
        Assert.NotNull(chatContainer);
    }
}

public class SimpleLayout_SignOut_Tests : SimpleModeLayoutTestBase
{
    [Fact]
    public async Task SimpleLayout_ClickingSignOut_ClearsSessionAndNavigates()
    {
        // Arrange
        var session = RegisterSignedInSession();
        RegisterViewportService();
        RegisterUiModeService("Simple");

        var component = Render<SimpleLayout>();

        // Act
        var signOutButton = component.Find("button.simple-layout-signout");
        await component.InvokeAsync(() => signOutButton.Click());

        // Assert
        // Session should be cleared and navigation should occur
        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.EndsWith("/device-signin", nav.Uri);
    }

    [Fact]
    public void SimpleLayout_SignOutButton_IsEnabled()
    {
        // Arrange
        RegisterSignedInSession();
        RegisterViewportService();
        RegisterUiModeService("Simple");

        // Act
        var component = Render<SimpleLayout>();

        // Assert
        var signOutButton = component.Find("button.simple-layout-signout");
        var isDisabled = signOutButton.HasAttribute("disabled");
        Assert.False(isDisabled);
    }
}

public class ResponsiveLayout_ModeChangeHandling_Tests : SimpleModeLayoutTestBase
{
    [Fact]
    public void ResponsiveLayout_RegistersForModeChangeNotifications()
    {
        // Arrange
        var mockUiMode = new Mock<UiModeService>(
            Mock.Of<IOperatorPreferencesRepository>(),
            RegisterSignedInSession()
        );
        mockUiMode.SetupGet(s => s.Mode).Returns("Standard");
        var modeChangeHandled = false;
        mockUiMode.SetupAdd(s => s.OnChange += It.IsAny<Action>()).Callback<Action>(action =>
        {
            modeChangeHandled = true;
        });
        Services.AddScoped(_ => mockUiMode.Object);

        RegisterViewportService();

        // Act
        var component = Render<ResponsiveLayout>();

        // Assert
        // Verify the handler was registered
        Assert.True(modeChangeHandled);
    }

    [Fact]
    public void ResponsiveLayout_RegistersForViewportChangeNotifications()
    {
        // Arrange
        var mockViewport = new Mock<IViewportService>();
        mockViewport.SetupGet(v => v.IsMobile).Returns(false);
        var viewportChangeHandled = false;
        mockViewport.SetupAdd(v => v.OnChange += It.IsAny<Action>()).Callback<Action>(action =>
        {
            viewportChangeHandled = true;
        });
        Services.AddScoped(_ => mockViewport.Object);

        RegisterUiModeService("Standard");

        // Act
        var component = Render<ResponsiveLayout>();

        // Assert
        // Verify the handler was registered
        Assert.True(viewportChangeHandled);
    }
}

public class SimpleLayout_LayoutStructure_Tests : SimpleModeLayoutTestBase
{
    [Fact]
    public void SimpleLayout_HasCorrectDomStructure()
    {
        // Arrange
        RegisterSignedInSession();
        RegisterViewportService();
        RegisterUiModeService("Simple");

        // Act
        var component = Render<SimpleLayout>();

        // Assert - check for presence of main structural elements
        Assert.NotNull(component.Find(".simple-layout"));
        Assert.NotNull(component.Find(".simple-layout-header"));
        Assert.NotNull(component.Find("main.simple-layout-body"));
        Assert.NotNull(component.Find(".simple-layout-content"));
        Assert.NotNull(component.Find(".simple-layout-chat"));
    }

    [Fact]
    public void SimpleLayout_ContentAndChatAreSiblingContainers()
    {
        // Arrange
        RegisterSignedInSession();
        RegisterViewportService();
        RegisterUiModeService("Simple");

        // Act
        var component = Render<SimpleLayout>();

        // Assert
        var main = component.Find("main.simple-layout-body");
        var children = main.Children;
        var contentIndex = -1;
        var chatIndex = -1;

        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].ClassList.Contains("simple-layout-content"))
                contentIndex = i;
            if (children[i].ClassList.Contains("simple-layout-chat"))
                chatIndex = i;
        }

        Assert.True(contentIndex >= 0, "Content container not found");
        Assert.True(chatIndex >= 0, "Chat container not found");
    }
}
