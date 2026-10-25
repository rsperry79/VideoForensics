using Bunit;

using Microsoft.Extensions.DependencyInjection;

using Moq;

using System.Reactive.Subjects;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Client.Ui.Shared.Contracts;
using VideoForensics.Client.Ui.Shared.Pages;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

public class LiveViewPageTests : BunitContext
{
    private const string ExpectedModulePath = "./_content/VideoForensics.Client.Ui.Shared/scripts/liveview.js";

    public LiveViewPageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _ = Services.AddLocalization();
        ScopeOwnershipTestDoubles.RegisterEmpty(Services);
        _ = Services.AddLogging();
        _ = Services.AddScoped<VideoForensics.Client.Ui.Shared.Services.RightPanelContentService>();

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
}
