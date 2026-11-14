using Bunit;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

using Moq;

using System.Reactive.Subjects;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Core.Api.Contracts.Repositories;
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

    private readonly Mock<IDeviceRepository> _deviceRepository;
    private readonly Mock<IMediaItemRepository> _mediaItems;
    private readonly Mock<IMediaContentUrlProvider> _contentUrls;
    private readonly Subject<EvidenceChangedDto> _evidence;

    private static readonly Guid Camera = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Snapshot = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Garage = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private void AddCamera()
    {
        var device = new Device { Id = Camera, LocationId = Guid.NewGuid(), ProviderDeviceId = "porch", Name = "Porch", Type = "camera" }.ToDto();
        _ = _deviceRepository.Setup(r => r.ListAsync(It.IsAny<System.Threading.CancellationToken>())).ReturnsAsync([device]);
    }

    private void ServeStill(string url)
    {
        var media = new MediaItem
        {
            Id = Snapshot,
            DeviceId = Camera,
            FileName = "a.jpg",
            FilePath = "a.jpg",
            MediaFormat = "jpg",
            RecordedAtUtc = DateTime.UtcNow,
            Sha256Hash = "h",
        };
        _ = _mediaItems
            .Setup(m => m.GetSnapshotsByDeviceAndDateRangeAsync(Camera, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync([media.ToDto()]);
        _ = _contentUrls
            .Setup(c => c.GetContentUrlsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [Snapshot] = url });
    }

    [Fact]
    public void LiveView_NotLive_ShowsTheLatestStillForTheSelectedCamera()
    {
        // Arrange
        AddCamera();
        ServeStill("https://example.test/still-1.jpg");

        // Act
        IRenderedComponent<LiveView> cut = Render<VideoForensics.Client.Ui.Shared.Pages.LiveView>();

        // Assert: with nothing started, the camera shows its newest snapshot, not a player
        cut.WaitForAssertion(() => Assert.Equal("https://example.test/still-1.jpg", cut.Find("[data-testid='live-view-still']").GetAttribute("src")));
        Assert.Empty(cut.FindAll("#liveViewPlayer"));
    }

    [Fact]
    public void LiveView_NewSnapshotAnnounced_ReloadsTheStill()
    {
        // Arrange
        AddCamera();
        ServeStill("https://example.test/still-1.jpg");
        IRenderedComponent<LiveView> cut = Render<VideoForensics.Client.Ui.Shared.Pages.LiveView>();
        cut.WaitForAssertion(() => Assert.Equal("https://example.test/still-1.jpg", cut.Find("[data-testid='live-view-still']").GetAttribute("src")));

        // Act: the camera saves a newer snapshot and the evidence feed announces it
        ServeStill("https://example.test/still-2.jpg");
        _evidence.OnNext(new EvidenceChangedDto(Camera));

        // Assert
        cut.WaitForAssertion(() => Assert.Equal("https://example.test/still-2.jpg", cut.Find("[data-testid='live-view-still']").GetAttribute("src")));
    }

    [Fact]
    public void LiveView_StoredCameraSelection_RestoresOnLoad()
    {
        // Arrange: two cameras, and the operator last chose only the second one
        var flood = Guid.Parse("33333333-3333-3333-3333-333333333333");
        _deviceRepository.Setup(r => r.ListAsync(It.IsAny<System.Threading.CancellationToken>())).ReturnsAsync(
        [
            new Device { Id = Camera, LocationId = Guid.NewGuid(), ProviderDeviceId = "porch", Name = "Porch", Type = "camera" }.ToDto(),
            new Device { Id = flood, LocationId = Guid.NewGuid(), ProviderDeviceId = "flood", Name = "Flood", Type = "camera" }.ToDto(),
        ]);
        var media = new MediaItem { Id = Snapshot, DeviceId = flood, FileName = "f.jpg", FilePath = "f.jpg", MediaFormat = "jpg", RecordedAtUtc = DateTime.UtcNow, Sha256Hash = "h" };
        _mediaItems
            .Setup(m => m.GetSnapshotsByDeviceAndDateRangeAsync(flood, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync([media.ToDto()]);
        _contentUrls
            .Setup(c => c.GetContentUrlsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [Snapshot] = "https://example.test/flood.jpg" });
        JSInterop.Setup<string?>("vfLayoutPrefs.get", "vf.filters.live-view")
            .SetResult($"{{\"providers\":null,\"account\":null,\"cameras\":[\"{flood:D}\"]}}");

        // Act
        IRenderedComponent<LiveView> cut = Render<VideoForensics.Client.Ui.Shared.Pages.LiveView>();

        // Assert: only the stored camera has a tile
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='live-view-tile']")));
        Assert.Equal(flood.ToString("D"), cut.Find("[data-testid='live-view-tile']").GetAttribute("data-device-id"));
    }

    [Fact]
    public async Task LiveView_CameraChosenOnSharedReviewState_IsTheCameraShown()
    {
        // Arrange: two cameras; Live View starts on the first one
        var porch = new Device { Id = Camera, LocationId = Guid.NewGuid(), ProviderDeviceId = "porch", Name = "Porch", Type = "camera" }.ToDto();
        var garage = new Device { Id = Garage, LocationId = Guid.NewGuid(), ProviderDeviceId = "garage", Name = "Garage", Type = "camera" }.ToDto();
        _ = _deviceRepository.Setup(r => r.ListAsync(It.IsAny<System.Threading.CancellationToken>())).ReturnsAsync([porch, garage]);
        IRenderedComponent<LiveView> cut = Render<VideoForensics.Client.Ui.Shared.Pages.LiveView>();
        cut.WaitForAssertion(() => Assert.Equal(Camera.ToString("D"), cut.Find("[data-testid='live-view-tile']").GetAttribute("data-device-id")));

        // Act: the camera is chosen on the shared review state, the same one the Review page writes to
        var shared = Services.GetRequiredService<VideoForensics.Client.Ui.Shared.Services.Review.ReviewPageState>();
        await cut.InvokeAsync(() => shared.SetCameras([Garage]));

        // Assert: Live View shows the chosen camera
        cut.WaitForAssertion(() => Assert.Equal(Garage.ToString("D"), cut.Find("[data-testid='live-view-tile']").GetAttribute("data-device-id")));
    }

    public LiveViewPageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _ = Services.AddScoped<VideoForensics.Client.Ui.Shared.Services.RightPanelFilterStore>();
        _ = Services.AddLocalization();
        _ = Services.AddSingleton<VideoForensics.Client.Ui.Shared.Services.IMediaPlaybackPolicy, VideoForensics.Client.Ui.Shared.Services.DefaultMediaPlaybackPolicy>();
        ScopeOwnershipTestDoubles.RegisterEmpty(Services);
        _ = Services.AddLogging();
        _ = Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        _ = Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();
        _ = Services.AddScoped<VideoForensics.Client.Ui.Shared.Services.RightPanelContentService>();
        _ = Services.AddScoped<VideoForensics.Client.Ui.Shared.Services.LeftPanelContentService>();
        _ = Services.AddScoped<VideoForensics.Client.Ui.Shared.Services.Review.ReviewPageState>();

        _deviceRepository = new Mock<IDeviceRepository>();
        _ = _deviceRepository
            .Setup(r => r.ListAsync(It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(Array.Empty<VideoForensics.Core.Api.Contracts.DeviceDto>());
        Mock<IDeviceRepository> deviceRepository = _deviceRepository;

        _mediaItems = new Mock<IMediaItemRepository>();
        _contentUrls = new Mock<IMediaContentUrlProvider>();
        _evidence = new Subject<EvidenceChangedDto>();
        var evidenceSource = new Mock<IEvidenceChangedSource>();
        _ = evidenceSource.SetupGet(s => s.EvidenceChanged).Returns(_evidence);
        _ = Services.AddSingleton(_mediaItems.Object);
        _ = Services.AddSingleton(_contentUrls.Object);
        _ = Services.AddSingleton(evidenceSource.Object);

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

            // The page's MediaPlayer imports its own module too, so only the live view module is counted.
            Assert.Equal(ExpectedModulePath, Assert.Single(importCalls, c => c == ExpectedModulePath));
        });
    }

    [Fact]
    public void LiveView_Rendered_LeavesTheLeftPanelToTheMainMenu()
    {
        // Act
        _ = Render<VideoForensics.Client.Ui.Shared.Pages.LiveView>();

        // Assert - the Media views, Live included, are items of the main left menu
        Assert.Null(Services.GetRequiredService<VideoForensics.Client.Ui.Shared.Services.LeftPanelContentService>().Content);
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
