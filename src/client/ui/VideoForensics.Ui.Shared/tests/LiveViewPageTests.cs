using Bunit;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

using Moq;

using System.Reactive.Subjects;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Client.Ui.Shared.Contracts;
using VideoForensics.Client.Ui.Shared.Pages;
using VideoForensics.Client.Ui.Shared.Services;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

public class LiveViewPageTests : BunitContext
{
    private const string SessionToken = "session-token";
    private const string ExpectedModulePath = "./_content/VideoForensics.Client.Ui.Shared/scripts/liveview.js";

    public LiveViewPageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _ = Services.AddLocalization();
        ScopeOwnershipTestDoubles.RegisterEmpty(Services);
        _ = Services.AddLogging();
        _ = Services.AddScoped<VideoForensics.Client.Ui.Shared.Services.RightPanelContentService>();
        _ = Services.AddScoped<VideoForensics.Client.Ui.Shared.Services.LeftPanelContentService>();
        _ = Services.AddScoped<VideoForensics.Client.Ui.Shared.Services.Review.ReviewPageState>();

        var deviceRepository = new Mock<IDeviceRepository>();
        _ = deviceRepository
            .Setup(r => r.ListAsync(It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(Array.Empty<Device>());

        var sessionService = new Mock<ILiveViewSessionService>();

        var sessionSource = new Mock<ILiveViewSessionSource>();
        _ = sessionSource.SetupGet(s => s.SessionChanged).Returns(new Subject<LiveViewSession>());
        _ = sessionSource.SetupGet(s => s.Telemetry).Returns(new Subject<LiveViewTelemetrySample>());
        _ = sessionSource.SetupGet(s => s.IsConnected).Returns(false);

        _ = Services.AddSingleton(deviceRepository.Object);
        _ = Services.AddSingleton(sessionService.Object);
        _ = Services.AddSingleton(sessionSource.Object);

        // The hub authenticates the browser connection with the paired session token, passed to the module.
        var session = new Mock<PairedSessionState>(Mock.Of<IJSRuntime>());
        _ = session.SetupGet(s => s.SessionToken).Returns(SessionToken);
        _ = Services.AddScoped(_ => session.Object);
    }

    [Fact]
    public void LiveView_ImportsModuleFromRclContentPath()
    {
        // Arrange / Act
        IRenderedComponent<LiveView> cut = Render<VideoForensics.Client.Ui.Shared.Pages.LiveView>();

        // Assert: the module must be requested from the Razor class library static asset path,
        // not "./scripts/liveview.js" (which resolves to /scripts/liveview.js and returns 404).
        cut.WaitForAssertion(() =>
        {
            var importCalls = JSInterop.Invocations
                .Where(i => i.Identifier == "import")
                .Select(i => i.Arguments.FirstOrDefault() as string)
                .ToList();

            _ = Assert.Single(importCalls);
            Assert.Equal(ExpectedModulePath, importCalls[0]);
        });
    }

    [Fact]
    public void LiveView_Rendered_RegistersTheViewNavInTheLeftPanel()
    {
        // Act
        _ = Render<VideoForensics.Client.Ui.Shared.Pages.LiveView>();

        // Assert
        Assert.NotNull(Services.GetRequiredService<VideoForensics.Client.Ui.Shared.Services.LeftPanelContentService>().Content);
    }

    [Fact]
    public void LiveView_InitializesHubConnectionThroughTheModuleWithoutError()
    {
        // Arrange: the module is the one the page imports, so its calls can be inspected
        BunitJSModuleInterop module = JSInterop.SetupModule(ExpectedModulePath);
        module.Mode = JSRuntimeMode.Loose;

        // Act
        IRenderedComponent<LiveView> cut = Render<VideoForensics.Client.Ui.Shared.Pages.LiveView>();

        // Assert: the page calls functions the module exports, and nothing else it never defined
        cut.WaitForAssertion(() =>
        {
            var calls = module.Invocations.Select(i => i.Identifier).ToList();
            Assert.Contains("initializeHubConnection", calls);
            JSRuntimeInvocation init = module.Invocations.First(i => i.Identifier == "initializeHubConnection");
            Assert.EndsWith("/hubs/liveview-signaling", init.Arguments[0] as string);
            Assert.Equal(SessionToken, init.Arguments[1]);
            Assert.DoesNotContain("setupIceCandidateHandler", calls);
            Assert.Empty(cut.FindAll(".alert-danger"));
        });
    }
}
