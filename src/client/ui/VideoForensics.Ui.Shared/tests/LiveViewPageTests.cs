namespace VideoForensics.Ui.Shared.Tests;

using System;
using System.Linq;
using System.Reactive.Subjects;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Moq;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Ui.Shared.Contracts;
using Xunit;

public class LiveViewPageTests : BunitContext
{
    private const string ExpectedModulePath = "./_content/VideoForensics.Ui.Shared/js/liveview.js";

    public LiveViewPageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddLocalization();
        Services.AddLogging();
        Services.AddScoped<VideoForensics.Ui.Shared.Services.RightPanelContentService>();

        var deviceRepository = new Mock<IDeviceRepository>();
        deviceRepository
            .Setup(r => r.ListAsync(It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(Array.Empty<Device>());

        var sessionService = new Mock<ILiveViewSessionService>();

        var sessionSource = new Mock<ILiveViewSessionSource>();
        sessionSource.SetupGet(s => s.SessionChanged).Returns(new Subject<LiveViewSession>());
        sessionSource.SetupGet(s => s.Telemetry).Returns(new Subject<LiveViewTelemetrySample>());
        sessionSource.SetupGet(s => s.IsConnected).Returns(false);

        Services.AddSingleton(deviceRepository.Object);
        Services.AddSingleton(sessionService.Object);
        Services.AddSingleton(sessionSource.Object);
    }

    [Fact]
    public void LiveView_ImportsModuleFromRclContentPath()
    {
        // Arrange / Act
        var cut = Render<VideoForensics.Ui.Shared.Pages.LiveView>();

        // Assert: the module must be requested from the Razor class library static asset path,
        // not "./js/liveview.js" (which resolves to /js/liveview.js and returns 404).
        cut.WaitForAssertion(() =>
        {
            var importCalls = JSInterop.Invocations
                .Where(i => i.Identifier == "import")
                .Select(i => i.Arguments.FirstOrDefault() as string)
                .ToList();

            Assert.Single(importCalls);
            Assert.Equal(ExpectedModulePath, importCalls[0]);
        });
    }
}
