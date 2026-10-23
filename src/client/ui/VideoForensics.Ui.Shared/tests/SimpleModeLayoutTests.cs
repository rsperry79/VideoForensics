
using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;

using Moq;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Client.Ui.Shared.Components;
using VideoForensics.Client.Ui.Shared.Formatting;
using VideoForensics.Client.Ui.Shared.Layout;
using VideoForensics.Client.Ui.Shared.Layout.Mobile;
using VideoForensics.Client.Ui.Shared.Layout.Simple;
using VideoForensics.Client.Ui.Shared.Resources;
using VideoForensics.Client.Ui.Shared.Services;
using VideoForensics.Client.Ui.Shared.Services.Cases;
using VideoForensics.Client.Ui.Shared.Services.Inspector;
using VideoForensics.Client.Ui.Shared.Services.Scope;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;
/// <summary>
/// Tests for SimpleLayout component rendering and mode switching behavior.
/// Covers simple-mode specific layout, responsiveness, and component composition.
/// </summary>
public abstract class SimpleModeLayoutTestBase : BunitContext
{
    protected SimpleModeLayoutTestBase()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        _ = Services.AddLocalization();
        ScopeOwnershipTestDoubles.RegisterEmpty(Services);
        _ = Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        _ = Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        _ = Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();

        // Register TimeProvider for ScopeRail component
        _ = Services.AddSingleton(TimeProvider.System);

        // Register state classes (no-dependency, real instances)
        _ = Services.AddScoped<InspectorState>();
        _ = Services.AddScoped<ScopeState>();
        _ = Services.AddScoped<RightPanelContentService>();
        _ = Services.AddScoped<LeftPanelContentService>();
        _ = Services.AddScoped(sp => new LayoutPreferencesState(sp.GetRequiredService<IJSRuntime>()));
        // AccountSwitcher in the right panel resolves the account selection on every layout render.
        _ = Services.AddScoped(sp => new AccountSelectionState(sp.GetRequiredService<IJSRuntime>()));

        // Register PairedSessionState for SimpleLayout and other components
        var session = new PairedSessionState(JSInterop.JSRuntime);
        _ = Services.AddScoped(_ => session);

        // Register IChatService mock for ChatPanel component
        var mockChatService = new Mock<IChatService>();
        _ = Services.AddScoped(_ => mockChatService.Object);

        _ = Services.AddScoped<IStringLocalizer<SharedResources>>(_ => TestLocalizer.Create());

        // Register IAppLockPreferencesStore mock
        var appLockMock = new Mock<IAppLockPreferencesStore>();
        _ = appLockMock.SetupGet(a => a.IsSupported).Returns(false);
        _ = Services.AddScoped(_ => appLockMock.Object);

        // Register IServerConnectivityState mock
        var connectivityMock = new Mock<IServerConnectivityState>();
        _ = connectivityMock.SetupGet(c => c.IsUnreachable).Returns(false);
        _ = Services.AddScoped(_ => connectivityMock.Object);

        // Register INoticeRepository mock
        var noticeRepoMock = new Mock<INoticeRepository>();
        _ = noticeRepoMock
            .Setup(r => r.CountUndismissedForOperatorAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _ = noticeRepoMock
            .Setup(r => r.ListForOperatorAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _ = Services.AddScoped(_ => noticeRepoMock.Object);

        // Register INoticeDismissalRepository mock
        var dismissalRepoMock = new Mock<INoticeDismissalRepository>();
        _ = Services.AddScoped(_ => dismissalRepoMock.Object);

        // Register ILegalHoldRepository and IEvidenceValidationService mocks (needed by InspectorPanel)
        _ = Services.AddScoped(_ => new Mock<ILegalHoldRepository>().Object);
        _ = Services.AddScoped(_ => new Mock<IEvidenceValidationService>().Object);

        // Register ICultureSwitcher mock
        var cultureSwitcherMock = new Mock<ICultureSwitcher>();
        _ = Services.AddScoped(_ => cultureSwitcherMock.Object);

        // Register IOperatorPreferencesRepository mock
        var opPrefRepoMock = new Mock<IOperatorPreferencesRepository>();
        _ = opPrefRepoMock
            .Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OperatorPreferences?)null);
        _ = Services.AddScoped(_ => opPrefRepoMock.Object);

        // Register default IUiModeService mock (can be overridden by individual tests)
        var uiModeMock = new Mock<IUiModeService>();
        _ = uiModeMock.SetupGet(u => u.Mode).Returns("Standard");
        _ = uiModeMock.SetupGet(u => u.IsLocked).Returns(false);
        _ = uiModeMock.Setup(u => u.InitializeAsync()).Returns(Task.CompletedTask);
        _ = uiModeMock.Setup(u => u.SetModeAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        _ = uiModeMock.SetupAdd(u => u.OnChange += It.IsAny<Action>());
        _ = Services.AddScoped(_ => uiModeMock.Object);

        // Register ICaseRepository mock
        var caseRepoMock = new Mock<ICaseRepository>();
        _ = caseRepoMock
            .Setup(r => r.ListAsync(It.IsAny<CaseStatus>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _ = Services.AddScoped(_ => caseRepoMock.Object);

        // Defaults for SimpleHome, which SimpleLayout renders at "/" and "/media" (empty data; SimpleHomeTests override these)
        _ = Services.AddScoped(_ => new Mock<IEventRepository>().Object);
        _ = Services.AddScoped(_ => new Mock<IMediaItemRepository>().Object);
        _ = Services.AddScoped(_ => new Mock<IDeviceRepository>().Object);
        _ = Services.AddScoped(_ => new Mock<IJammingRepository>().Object);
        _ = Services.AddScoped(_ => new SimpleHomeBuilder(TimeProvider.System, new Mock<IMediaContentUrlProvider>().Object, TestLocalizer.Create()));

        // Register services needed by RightPanel components (AccountSwitcher, ThemeLanguagePicker)
        _ = Services.AddScoped(_ => new Mock<IProviderAccountRepository>().Object);
        _ = Services.AddScoped(_ => new Mock<IUserRepository>().Object);

        // Note: AccountSwitcher requires IProviderAuthService which violates client/server architecture.
        // This is registered here for testing purposes only.
        // TODO: Refactor AccountSwitcher to use remote service calls instead of direct provider access.
        var authServiceMock = new Mock<IProviderAuthService>();
        _ = Services.AddScoped(_ => authServiceMock.Object);

        // Register IForensicsConfiguration mock
        var forensicsConfigMock = new Mock<IForensicsConfiguration>();
        _ = forensicsConfigMock.SetupGet(f => f.ActiveProviderAccountId).Returns((Guid?)null);
        _ = Services.AddScoped(_ => forensicsConfigMock.Object);

        // Register IForensicsConfigurationService mock
        _ = Services.AddScoped(_ => new Mock<IForensicsConfigurationService>().Object);

        // Register ThemePreferenceService with all its dependencies
        _ = Services.AddScoped(sp =>
            new ThemePreferenceService(
                sp.GetRequiredService<IJSRuntime>(),
                sp.GetRequiredService<IOperatorPreferencesRepository>(),
                sp.GetRequiredService<PairedSessionState>(),
                sp.GetRequiredService<ICultureSwitcher>()));

        // Register CaseState which depends on ICaseRepository and ScopeState
        _ = Services.AddScoped(sp =>
            new CaseState(
                sp.GetRequiredService<ICaseRepository>(),
                sp.GetRequiredService<ScopeState>()));
    }

    protected PairedSessionState RegisterSignedInSession()
    {
        var session = new PairedSessionState(JSInterop.JSRuntime);
        _ = Services.AddScoped(_ => session);
        return session;
    }

    protected void RegisterUiModeService(string mode = "Standard")
    {
        var mockService = new Mock<IUiModeService>();
        _ = mockService.SetupGet(s => s.Mode).Returns(mode);
        _ = mockService.Setup(s => s.InitializeAsync()).Returns(Task.CompletedTask);
        _ = mockService.SetupAdd(s => s.OnChange += It.IsAny<Action>());
        _ = Services.AddScoped(_ => mockService.Object);
    }

    protected void RegisterViewportService()
    {
        var mockViewport = new Mock<IViewportService>();
        _ = mockViewport.SetupGet(v => v.IsMobile).Returns(false);
        _ = mockViewport.SetupAdd(v => v.OnChange += It.IsAny<Action>());
        _ = Services.AddScoped(_ => mockViewport.Object);
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
        IRenderedComponent<ResponsiveLayout> component = Render<ResponsiveLayout>();

        // Assert
        IRenderedComponent<SimpleLayout> simpleLayout = component.FindComponent<SimpleLayout>();
        Assert.NotNull(simpleLayout);
    }

    [Fact]
    public void ResponsiveLayout_StandardMode_RendersMainLayout()
    {
        // Arrange
        RegisterViewportService();
        RegisterUiModeService("Standard");

        // Act
        IRenderedComponent<ResponsiveLayout> component = Render<ResponsiveLayout>();

        // Assert
        IRenderedComponent<MainLayout> mainLayout = component.FindComponent<MainLayout>();
        Assert.NotNull(mainLayout);
    }

    [Fact]
    public void ResponsiveLayout_StandardModeOnDesktop_RendersMainLayout()
    {
        // Arrange
        var mockViewport = new Mock<IViewportService>();
        _ = mockViewport.SetupGet(v => v.IsMobile).Returns(false);
        _ = mockViewport.SetupAdd(v => v.OnChange += It.IsAny<Action>());
        _ = Services.AddScoped(_ => mockViewport.Object);

        RegisterUiModeService("Standard");

        // Act
        IRenderedComponent<ResponsiveLayout> component = Render<ResponsiveLayout>();

        // Assert
        IRenderedComponent<MainLayout> mainLayout = component.FindComponent<MainLayout>();
        Assert.NotNull(mainLayout);
    }

    [Fact]
    public void ResponsiveLayout_StandardModeOnMobile_RendersMobileLayout()
    {
        // Arrange
        var mockViewport = new Mock<IViewportService>();
        _ = mockViewport.SetupGet(v => v.IsMobile).Returns(true);
        _ = mockViewport.SetupAdd(v => v.OnChange += It.IsAny<Action>());
        _ = Services.AddScoped(_ => mockViewport.Object);

        RegisterUiModeService("Standard");

        // Act
        IRenderedComponent<ResponsiveLayout> component = Render<ResponsiveLayout>();

        // Assert
        IRenderedComponent<MobileLayout> mobileLayout = component.FindComponent<MobileLayout>();
        Assert.NotNull(mobileLayout);
    }
}

public class SimpleLayout_Rendering_Tests : SimpleModeLayoutTestBase
{
    [Fact]
    public void SimpleLayout_Renders_WithChatPanel()
    {
        // Arrange
        _ = RegisterSignedInSession();
        RegisterViewportService();
        RegisterUiModeService("Simple");

        // Act
        IRenderedComponent<SimpleLayout> component = Render<SimpleLayout>();

        // Assert
        IRenderedComponent<ChatPanel> chatPanel = component.FindComponent<ChatPanel>();
        Assert.NotNull(chatPanel);
    }

    [Fact]
    public void SimpleLayout_Renders_WithHeaderTitle()
    {
        // Arrange
        _ = RegisterSignedInSession();
        RegisterViewportService();
        RegisterUiModeService("Simple");

        // Act
        IRenderedComponent<SimpleLayout> component = Render<SimpleLayout>();

        // Assert
        AngleSharp.Dom.IElement header = component.Find(".simple-layout-header");
        Assert.NotNull(header);
        AngleSharp.Dom.IElement title = component.Find(".simple-layout-title");
        Assert.NotNull(title);
        Assert.Contains("VideoForensics", title.TextContent);
    }

    [Fact]
    public void SimpleLayout_Renders_WithSignOutButton()
    {
        // Arrange
        _ = RegisterSignedInSession();
        RegisterViewportService();
        RegisterUiModeService("Simple");

        // Act
        IRenderedComponent<SimpleLayout> component = Render<SimpleLayout>();

        // Assert
        IReadOnlyList<AngleSharp.Dom.IElement> buttons = component.FindAll("button");
        AngleSharp.Dom.IElement? signOutButton = buttons.FirstOrDefault(b => b.GetAttribute("class")?.Contains("simple-layout-signout") ?? false);
        Assert.NotNull(signOutButton);
    }

    [Fact]
    public void SimpleLayout_Renders_WithMainContent()
    {
        // Arrange
        _ = RegisterSignedInSession();
        RegisterViewportService();
        RegisterUiModeService("Simple");

        // Act
        IRenderedComponent<SimpleLayout> component = Render<SimpleLayout>();

        // Assert
        AngleSharp.Dom.IElement mainContent = component.Find(".simple-layout-content");
        Assert.NotNull(mainContent);
    }

    [Fact]
    public void SimpleLayout_Renders_WithChatContainer()
    {
        // Arrange
        _ = RegisterSignedInSession();
        RegisterViewportService();
        RegisterUiModeService("Simple");

        // Act
        IRenderedComponent<SimpleLayout> component = Render<SimpleLayout>();

        // Assert
        AngleSharp.Dom.IElement chatContainer = component.Find(".simple-layout-chat");
        Assert.NotNull(chatContainer);
    }
}

public class SimpleLayout_SignOut_Tests : SimpleModeLayoutTestBase
{
    [Fact]
    public async Task SimpleLayout_ClickingSignOut_ClearsSessionAndNavigates()
    {
        // Arrange
        PairedSessionState session = RegisterSignedInSession();
        RegisterViewportService();
        RegisterUiModeService("Simple");

        IRenderedComponent<SimpleLayout> component = Render<SimpleLayout>();

        // Act
        AngleSharp.Dom.IElement signOutButton = component.Find("button.simple-layout-signout");
        await component.InvokeAsync(() => signOutButton.Click());

        // Assert
        // Session should be cleared and navigation should occur
        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        Assert.Equal("/signin", new Uri(nav.Uri).AbsolutePath);
        Assert.False(session.IsSignedIn);
    }

    [Fact]
    public void SimpleLayout_SignOutButton_IsEnabled()
    {
        // Arrange
        _ = RegisterSignedInSession();
        RegisterViewportService();
        RegisterUiModeService("Simple");

        // Act
        IRenderedComponent<SimpleLayout> component = Render<SimpleLayout>();

        // Assert
        AngleSharp.Dom.IElement signOutButton = component.Find("button.simple-layout-signout");
        bool isDisabled = signOutButton.HasAttribute("disabled");
        Assert.False(isDisabled);
    }
}

public class ResponsiveLayout_ModeChangeHandling_Tests : SimpleModeLayoutTestBase
{
    [Fact]
    public void ResponsiveLayout_RegistersForModeChangeNotifications()
    {
        // Arrange
        var mockUiMode = new Mock<IUiModeService>();
        _ = mockUiMode.SetupGet(s => s.Mode).Returns("Standard");
        _ = mockUiMode.Setup(s => s.InitializeAsync()).Returns(Task.CompletedTask);
        bool modeChangeHandled = false;
        _ = mockUiMode.SetupAdd(s => s.OnChange += It.IsAny<Action>()).Callback<Action>(action =>
        {
            modeChangeHandled = true;
        });
        _ = Services.AddScoped(_ => mockUiMode.Object);

        RegisterViewportService();

        // Act
        IRenderedComponent<ResponsiveLayout> component = Render<ResponsiveLayout>();

        // Assert
        // Verify the handler was registered
        Assert.True(modeChangeHandled);
    }

    [Fact]
    public void ResponsiveLayout_RegistersForViewportChangeNotifications()
    {
        // Arrange
        var mockViewport = new Mock<IViewportService>();
        _ = mockViewport.SetupGet(v => v.IsMobile).Returns(false);
        bool viewportChangeHandled = false;
        _ = mockViewport.SetupAdd(v => v.OnChange += It.IsAny<Action>()).Callback<Action>(action =>
        {
            viewportChangeHandled = true;
        });
        _ = Services.AddScoped(_ => mockViewport.Object);

        RegisterUiModeService("Standard");

        // Act
        IRenderedComponent<ResponsiveLayout> component = Render<ResponsiveLayout>();

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
        _ = RegisterSignedInSession();
        RegisterViewportService();
        RegisterUiModeService("Simple");

        // Act
        IRenderedComponent<SimpleLayout> component = Render<SimpleLayout>();

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
        _ = RegisterSignedInSession();
        RegisterViewportService();
        RegisterUiModeService("Simple");

        // Act
        IRenderedComponent<SimpleLayout> component = Render<SimpleLayout>();

        // Assert
        AngleSharp.Dom.IElement main = component.Find("main.simple-layout-body");
        AngleSharp.Dom.IHtmlCollection<AngleSharp.Dom.IElement> children = main.Children;
        int contentIndex = -1;
        int chatIndex = -1;

        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].ClassList.Contains("simple-layout-content"))
            {
                contentIndex = i;
            }

            if (children[i].ClassList.Contains("simple-layout-chat"))
            {
                chatIndex = i;
            }
        }

        Assert.True(contentIndex >= 0, "Content container not found");
        Assert.True(chatIndex >= 0, "Chat container not found");
    }
}
public class SimpleLayout_StandardViewButton_Tests : SimpleModeLayoutTestBase
{
    [Fact]
    public void SimpleLayout_WhenUnlocked_RendersStandardViewButton()
    {
        // Arrange
        _ = RegisterSignedInSession();
        RegisterViewportService();
        RegisterUiModeService("Simple");
        var mockUiMode = new Mock<IUiModeService>();
        _ = mockUiMode.SetupGet(s => s.Mode).Returns("Simple");
        _ = mockUiMode.SetupGet(s => s.IsLocked).Returns(false);
        _ = mockUiMode.Setup(s => s.InitializeAsync()).Returns(Task.CompletedTask);
        _ = mockUiMode.Setup(s => s.SetModeAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        _ = mockUiMode.SetupAdd(s => s.OnChange += It.IsAny<Action>());
        _ = Services.AddScoped(_ => mockUiMode.Object);

        // Act
        IRenderedComponent<SimpleLayout> component = Render<SimpleLayout>();

        // Assert
        AngleSharp.Dom.IElement? button = component.FindAll("button").FirstOrDefault(b =>
            b.ClassList.Contains("simple-layout-standard-button") &&
            b.TextContent.Contains("Standard view"));
        Assert.NotNull(button);
    }

    [Fact]
    public async Task SimpleLayout_ClickingStandardViewButton_CallsSetModeAsync()
    {
        // Arrange
        _ = RegisterSignedInSession();
        RegisterViewportService();
        var mockUiMode = new Mock<IUiModeService>();
        _ = mockUiMode.SetupGet(s => s.Mode).Returns("Simple");
        _ = mockUiMode.SetupGet(s => s.IsLocked).Returns(false);
        _ = mockUiMode.Setup(s => s.InitializeAsync()).Returns(Task.CompletedTask);
        _ = mockUiMode.Setup(s => s.SetModeAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        _ = mockUiMode.SetupAdd(s => s.OnChange += It.IsAny<Action>());
        _ = Services.AddScoped(_ => mockUiMode.Object);

        IRenderedComponent<SimpleLayout> component = Render<SimpleLayout>();
        AngleSharp.Dom.IElement button = component.FindAll("button").First(b => b.ClassList.Contains("simple-layout-standard-button"));

        // Act
        await component.InvokeAsync(() => button.Click());

        // Assert
        mockUiMode.Verify(s => s.SetModeAsync("Standard"), Times.Once);
    }

    [Fact]
    public void SimpleLayout_WhenLocked_HidesStandardViewButton()
    {
        // Arrange
        _ = RegisterSignedInSession();
        RegisterViewportService();
        var mockUiMode = new Mock<IUiModeService>();
        _ = mockUiMode.SetupGet(s => s.Mode).Returns("Simple");
        _ = mockUiMode.SetupGet(s => s.IsLocked).Returns(true);
        _ = mockUiMode.Setup(s => s.InitializeAsync()).Returns(Task.CompletedTask);
        _ = mockUiMode.SetupAdd(s => s.OnChange += It.IsAny<Action>());
        _ = Services.AddScoped(_ => mockUiMode.Object);

        // Act
        IRenderedComponent<SimpleLayout> component = Render<SimpleLayout>();

        // Assert
        AngleSharp.Dom.IElement? button = component.FindAll("button").FirstOrDefault(b => b.ClassList.Contains("simple-layout-standard-button"));
        Assert.Null(button);
    }

    [Fact]
    public async Task SimpleLayout_OnChange_CausesReRender()
    {
        // Arrange
        _ = RegisterSignedInSession();
        RegisterViewportService();
        Action onChangeCallback = null;
        var mockUiMode = new Mock<IUiModeService>();
        _ = mockUiMode.SetupGet(s => s.Mode).Returns("Simple");

        // Track whether OnChange has been raised to change IsLocked behavior
        bool hasChangeOccurred = false;
        _ = mockUiMode.SetupGet(s => s.IsLocked).Returns(() => hasChangeOccurred);

        _ = mockUiMode.Setup(s => s.InitializeAsync()).Returns(Task.CompletedTask);
        _ = mockUiMode.SetupAdd(s => s.OnChange += It.IsAny<Action>()).Callback<Action>(action =>
        {
            onChangeCallback = action;
        });
        _ = Services.AddScoped(_ => mockUiMode.Object);

        IRenderedComponent<SimpleLayout> component = Render<SimpleLayout>();
        // Precondition: the "Standard view" button should be rendered initially
        AngleSharp.Dom.IElement? initialButton = component.FindAll("button").FirstOrDefault(b => b.ClassList.Contains("simple-layout-standard-button"));
        Assert.NotNull(initialButton);

        // Act - simulate OnChange event (which sets IsLocked to true) and trigger re-render from dispatcher
        hasChangeOccurred = true;
        Assert.NotNull(onChangeCallback);
        await component.InvokeAsync(() => onChangeCallback.Invoke());

        // Assert - the "Standard view" button should no longer be rendered after OnChange with IsLocked=true
        IReadOnlyList<AngleSharp.Dom.IElement> buttonsAfterChange = component.FindAll("button");
        AngleSharp.Dom.IElement? standardButtonAfterChange = buttonsAfterChange.FirstOrDefault(b => b.ClassList.Contains("simple-layout-standard-button"));
        Assert.Null(standardButtonAfterChange);
    }
}