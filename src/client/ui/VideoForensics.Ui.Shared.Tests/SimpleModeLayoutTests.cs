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
using VideoForensics.Ui.Shared.Services.Inspector;
using VideoForensics.Api.Contracts;
using Microsoft.Extensions.Localization;
using VideoForensics.Ui.Shared.Resources;
using VideoForensics.Ui.Shared.Services.Cases;
using VideoForensics.Ui.Shared.Services.Scope;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Providers.Common.Contracts;

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

        // Register TimeProvider for ScopeRail component
        Services.AddSingleton(TimeProvider.System);

        // Register state classes (no-dependency, real instances)
        Services.AddScoped<InspectorState>();
        Services.AddScoped<ScopeState>();
        Services.AddScoped<RightPanelContentService>();
        Services.AddScoped(sp => new LayoutPreferencesState(sp.GetRequiredService<IJSRuntime>()));

        // Register PairedSessionState for SimpleLayout and other components
        var session = new PairedSessionState(JSInterop.JSRuntime);
        Services.AddScoped(_ => session);

        // Register IChatService mock for ChatPanel component
        var mockChatService = new Mock<IChatService>();
        Services.AddScoped(_ => mockChatService.Object);

        // Register IStringLocalizer<SharedResources> mock
        var localizerMock = new Mock<IStringLocalizer<SharedResources>>();
        localizerMock
            .Setup(l => l[It.IsAny<string>()])
            .Returns((string key) => new LocalizedString(key, key));
        Services.AddScoped(_ => localizerMock.Object);

        // Register IAppLockPreferencesStore mock
        var appLockMock = new Mock<IAppLockPreferencesStore>();
        appLockMock.SetupGet(a => a.IsSupported).Returns(false);
        Services.AddScoped(_ => appLockMock.Object);

        // Register IServerConnectivityState mock
        var connectivityMock = new Mock<IServerConnectivityState>();
        connectivityMock.SetupGet(c => c.IsUnreachable).Returns(false);
        Services.AddScoped(_ => connectivityMock.Object);

        // Register INoticeRepository mock
        var noticeRepoMock = new Mock<INoticeRepository>();
        noticeRepoMock
            .Setup(r => r.CountUndismissedForOperatorAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        noticeRepoMock
            .Setup(r => r.ListForOperatorAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Notice>());
        Services.AddScoped(_ => noticeRepoMock.Object);

        // Register INoticeDismissalRepository mock
        var dismissalRepoMock = new Mock<INoticeDismissalRepository>();
        Services.AddScoped(_ => dismissalRepoMock.Object);

        // Register ILegalHoldRepository and IEvidenceValidationService mocks (needed by InspectorPanel)
        Services.AddScoped(_ => new Mock<ILegalHoldRepository>().Object);
        Services.AddScoped(_ => new Mock<VideoForensics.Client.Common.Contracts.IEvidenceValidationService>().Object);

        // Register ICultureSwitcher mock
        var cultureSwitcherMock = new Mock<ICultureSwitcher>();
        Services.AddScoped(_ => cultureSwitcherMock.Object);

        // Register IOperatorPreferencesRepository mock
        var opPrefRepoMock = new Mock<IOperatorPreferencesRepository>();
        opPrefRepoMock
            .Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OperatorPreferences?)null);
        Services.AddScoped(_ => opPrefRepoMock.Object);

        // Register default IUiModeService mock (can be overridden by individual tests)
        var uiModeMock = new Mock<IUiModeService>();
        uiModeMock.SetupGet(u => u.Mode).Returns("Standard");
        uiModeMock.SetupGet(u => u.IsLocked).Returns(false);
        uiModeMock.Setup(u => u.InitializeAsync()).Returns(Task.CompletedTask);
        uiModeMock.Setup(u => u.SetModeAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        uiModeMock.SetupAdd(u => u.OnChange += It.IsAny<Action>());
        Services.AddScoped(_ => uiModeMock.Object);

        // Register ICaseRepository mock
        var caseRepoMock = new Mock<ICaseRepository>();
        caseRepoMock
            .Setup(r => r.ListAsync(It.IsAny<CaseStatus>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ForensicCase>());
        Services.AddScoped(_ => caseRepoMock.Object);

        // Register services needed by RightPanel components (AccountSwitcher, ThemeLanguagePicker)
        Services.AddScoped(_ => new Mock<IProviderAccountRepository>().Object);
        Services.AddScoped(_ => new Mock<IUserRepository>().Object);

        // Note: AccountSwitcher requires IProviderAuthService which violates client/server architecture.
        // This is registered here for testing purposes only.
        // TODO: Refactor AccountSwitcher to use remote service calls instead of direct provider access.
        var authServiceMock = new Mock<IProviderAuthService>();
        Services.AddScoped(_ => authServiceMock.Object);

        // Register IForensicsConfiguration mock
        var forensicsConfigMock = new Mock<IForensicsConfiguration>();
        forensicsConfigMock.SetupGet(f => f.ActiveProviderAccountId).Returns((Guid?)null);
        Services.AddScoped(_ => forensicsConfigMock.Object);

        // Register IForensicsConfigurationService mock
        Services.AddScoped(_ => new Mock<VideoForensics.Client.Common.Contracts.IForensicsConfigurationService>().Object);

        // Register ThemePreferenceService with all its dependencies
        Services.AddScoped(sp =>
            new ThemePreferenceService(
                sp.GetRequiredService<IJSRuntime>(),
                sp.GetRequiredService<IOperatorPreferencesRepository>(),
                sp.GetRequiredService<PairedSessionState>(),
                sp.GetRequiredService<ICultureSwitcher>()));

        // Register CaseState which depends on ICaseRepository and ScopeState
        Services.AddScoped(sp =>
            new CaseState(
                sp.GetRequiredService<ICaseRepository>(),
                sp.GetRequiredService<ScopeState>()));
    }

    protected PairedSessionState RegisterSignedInSession()
    {
        var session = new PairedSessionState(JSInterop.JSRuntime);
        Services.AddScoped(_ => session);
        return session;
    }

    protected void RegisterUiModeService(string mode = "Standard")
    {
        var mockService = new Mock<IUiModeService>();
        mockService.SetupGet(s => s.Mode).Returns(mode);
        mockService.Setup(s => s.InitializeAsync()).Returns(Task.CompletedTask);
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
        Assert.Equal("/signin", new Uri(nav.Uri).AbsolutePath);
        Assert.False(session.IsSignedIn);
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
        var mockUiMode = new Mock<IUiModeService>();
        mockUiMode.SetupGet(s => s.Mode).Returns("Standard");
        mockUiMode.Setup(s => s.InitializeAsync()).Returns(Task.CompletedTask);
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
public class SimpleLayout_StandardViewButton_Tests : SimpleModeLayoutTestBase
{
    [Fact]
    public void SimpleLayout_WhenUnlocked_RendersStandardViewButton()
    {
        // Arrange
        RegisterSignedInSession();
        RegisterViewportService();
        RegisterUiModeService("Simple");
        var mockUiMode = new Mock<IUiModeService>();
        mockUiMode.SetupGet(s => s.Mode).Returns("Simple");
        mockUiMode.SetupGet(s => s.IsLocked).Returns(false);
        mockUiMode.Setup(s => s.InitializeAsync()).Returns(Task.CompletedTask);
        mockUiMode.Setup(s => s.SetModeAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        mockUiMode.SetupAdd(s => s.OnChange += It.IsAny<Action>());
        Services.AddScoped(_ => mockUiMode.Object);

        // Act
        var component = Render<SimpleLayout>();

        // Assert
        var button = component.FindAll("button").FirstOrDefault(b =>
            b.ClassList.Contains("simple-layout-standard-button") &&
            b.TextContent.Contains("Standard view"));
        Assert.NotNull(button);
    }

    [Fact]
    public async Task SimpleLayout_ClickingStandardViewButton_CallsSetModeAsync()
    {
        // Arrange
        RegisterSignedInSession();
        RegisterViewportService();
        var mockUiMode = new Mock<IUiModeService>();
        mockUiMode.SetupGet(s => s.Mode).Returns("Simple");
        mockUiMode.SetupGet(s => s.IsLocked).Returns(false);
        mockUiMode.Setup(s => s.InitializeAsync()).Returns(Task.CompletedTask);
        mockUiMode.Setup(s => s.SetModeAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        mockUiMode.SetupAdd(s => s.OnChange += It.IsAny<Action>());
        Services.AddScoped(_ => mockUiMode.Object);

        var component = Render<SimpleLayout>();
        var button = component.FindAll("button").First(b => b.ClassList.Contains("simple-layout-standard-button"));

        // Act
        await component.InvokeAsync(() => button.Click());

        // Assert
        mockUiMode.Verify(s => s.SetModeAsync("Standard"), Times.Once);
    }

    [Fact]
    public void SimpleLayout_WhenLocked_HidesStandardViewButton()
    {
        // Arrange
        RegisterSignedInSession();
        RegisterViewportService();
        var mockUiMode = new Mock<IUiModeService>();
        mockUiMode.SetupGet(s => s.Mode).Returns("Simple");
        mockUiMode.SetupGet(s => s.IsLocked).Returns(true);
        mockUiMode.Setup(s => s.InitializeAsync()).Returns(Task.CompletedTask);
        mockUiMode.SetupAdd(s => s.OnChange += It.IsAny<Action>());
        Services.AddScoped(_ => mockUiMode.Object);

        // Act
        var component = Render<SimpleLayout>();

        // Assert
        var button = component.FindAll("button").FirstOrDefault(b => b.ClassList.Contains("simple-layout-standard-button"));
        Assert.Null(button);
    }

    [Fact]
    public async Task SimpleLayout_OnChange_CausesReRender()
        {
            // Arrange
            RegisterSignedInSession();
            RegisterViewportService();
            Action onChangeCallback = null;
            var mockUiMode = new Mock<IUiModeService>();
            mockUiMode.SetupGet(s => s.Mode).Returns("Simple");

            // Track whether OnChange has been raised to change IsLocked behavior
            bool hasChangeOccurred = false;
            mockUiMode.SetupGet(s => s.IsLocked).Returns(() => hasChangeOccurred);

            mockUiMode.Setup(s => s.InitializeAsync()).Returns(Task.CompletedTask);
            mockUiMode.SetupAdd(s => s.OnChange += It.IsAny<Action>()).Callback<Action>(action =>
            {
                onChangeCallback = action;
            });
            Services.AddScoped(_ => mockUiMode.Object);

            var component = Render<SimpleLayout>();
            // Precondition: the "Standard view" button should be rendered initially
            var initialButton = component.FindAll("button").FirstOrDefault(b => b.ClassList.Contains("simple-layout-standard-button"));
            Assert.NotNull(initialButton);

            // Act - simulate OnChange event (which sets IsLocked to true) and trigger re-render from dispatcher
            hasChangeOccurred = true;
            Assert.NotNull(onChangeCallback);
            await component.InvokeAsync(() => onChangeCallback.Invoke());

            // Assert - the "Standard view" button should no longer be rendered after OnChange with IsLocked=true
            var buttonsAfterChange = component.FindAll("button");
            var standardButtonAfterChange = buttonsAfterChange.FirstOrDefault(b => b.ClassList.Contains("simple-layout-standard-button"));
            Assert.Null(standardButtonAfterChange);
        }
}