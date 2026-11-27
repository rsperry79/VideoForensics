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

    // Live view session mock state: one session per device, the same one for Start and for GetActiveSessionAsync.
    private readonly Mock<ILiveViewSessionService> _sessionService = new();
    private readonly Subject<LiveViewSessionDto> _sessionChanged = new();
    private readonly Dictionary<Guid, LiveViewSessionDto> _sessionsByDevice = new();

    // Calls the fake liveview.js module received, in order: the function name and the session it was given.
    private readonly List<(string Name, Guid SessionId, string? ElementId)> _liveViewCalls = new();

    private static readonly Guid Camera = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Snapshot = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Garage = Guid.Parse("33333333-3333-3333-3333-333333333333");    private static readonly Guid Flood = Guid.Parse("44444444-4444-4444-4444-444444444444");

    // Permissive session service: Start and GetActiveSessionAsync return the device's Active session, created on first use.
    private LiveViewSessionDto SessionFor(Guid deviceId)
    {
        if (!_sessionsByDevice.TryGetValue(deviceId, out LiveViewSessionDto? session))
        {
            session = new LiveViewSessionDto(
                Id: Guid.NewGuid(),
                DeviceId: deviceId,
                TriggerReason: "Manual",
                State: "Active",
                StartedAtUtc: DateTime.UtcNow,
                LastExtendedAtUtc: DateTime.UtcNow);
            _sessionsByDevice[deviceId] = session;
        }

        return session;
    }

    private void SetupSessionService()
    {
        _ = _sessionService
            .Setup(s => s.StartAsync(It.IsAny<Guid>(), It.IsAny<LiveViewTriggerReasonDto>(), It.IsAny<Guid?>(), It.IsAny<System.Threading.CancellationToken>()))
            .Returns((Guid deviceId, LiveViewTriggerReasonDto reason, Guid? operatorId, System.Threading.CancellationToken ct) => Task.FromResult(SessionFor(deviceId)));
        _ = _sessionService
            .Setup(s => s.GetActiveSessionAsync(It.IsAny<Guid>(), It.IsAny<System.Threading.CancellationToken>()))
            .Returns((Guid deviceId, System.Threading.CancellationToken ct) => Task.FromResult<LiveViewSessionDto?>(SessionFor(deviceId)));
        _ = _sessionService
            .Setup(s => s.StopAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<System.Threading.CancellationToken>()))
            .Returns(Task.CompletedTask);
        _ = _sessionService
            .Setup(s => s.ExtendAsync(It.IsAny<Guid>(), It.IsAny<System.Threading.CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    // Fake liveview.js: initializeHubConnection returns a reference, and startLiveView and stopLiveView are recorded.
    private void SetupFakeLiveViewModule()
    {
        BunitJSModuleInterop module = JSInterop.SetupModule(ExpectedModulePath);
        module.Mode = JSRuntimeMode.Loose;
        _ = module.SetupModule("initializeHubConnection", _ => true);
        module.SetupVoid("startLiveView", inv => RecordLiveViewCall(inv)).SetVoidResult();
        module.SetupVoid("stopLiveView", inv => RecordLiveViewCall(inv)).SetVoidResult();
    }

    private bool RecordLiveViewCall(JSRuntimeInvocation invocation)
    {
        // startLiveView(hub, sessionId, elementId) takes the session second; stopLiveView(sessionId) takes it first.
        int sessionIndex = invocation.Identifier == "stopLiveView" ? 0 : 1;
        object? raw = invocation.Arguments.Count > sessionIndex ? invocation.Arguments[sessionIndex] : null;
        Guid sessionId = raw is Guid id ? id : Guid.Parse(raw?.ToString() ?? Guid.Empty.ToString());
        string? elementId = invocation.Identifier == "startLiveView" && invocation.Arguments.Count > 2 ? invocation.Arguments[2] as string : null;
        _liveViewCalls.Add((invocation.Identifier, sessionId, elementId));
        return true;
    }

    private void AddCamera()
    {
        var device = new Device { Id = Camera, LocationId = Guid.NewGuid(), ProviderDeviceId = "porch", Name = "Porch", Type = "camera" }.ToDto();
        _ = _deviceRepository.Setup(r => r.ListAsync(It.IsAny<System.Threading.CancellationToken>())).ReturnsAsync([device]);
    }

    // Several cameras in the device list, named Cam 0, Cam 1, and so on.
    private void AddCameras(params Guid[] ids)
    {
        List<DeviceDto> devices = ids
            .Select((id, i) => new Device { Id = id, LocationId = Guid.NewGuid(), ProviderDeviceId = $"cam-{i}", Name = $"Cam {i}", Type = "camera" }.ToDto())
            .ToList();
        _ = _deviceRepository.Setup(r => r.ListAsync(It.IsAny<System.Threading.CancellationToken>())).ReturnsAsync(devices);
    }

    // Each live tile renders its player under its own element id, so a stream's element is found by device.
    private static string PlayerIdFor(Guid deviceId) => "liveViewPlayer-" + deviceId.ToString("D");

    private static void ClickTile(IRenderedComponent<LiveView> cut, Guid deviceId) =>
        cut.Find($"[data-testid='live-view-tile'][data-device-id='{deviceId:D}'] button").Click();

    private static void WaitForLive(IRenderedComponent<LiveView> cut, Guid deviceId) =>
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll($"#{PlayerIdFor(deviceId)}")));

    private void StartBoth(IRenderedComponent<LiveView> cut, Guid first, Guid second)
    {
        ClickTile(cut, first);
        WaitForLive(cut, first);
        ClickTile(cut, second);
        WaitForLive(cut, second);
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
    public void LiveView_CameraOffline_BlocksStartAndShowsWhy()
    {
        // Arrange: the camera reports it is offline (a dead battery looks like this), so a stream cannot connect
        var device = new Device { Id = Camera, LocationId = Guid.NewGuid(), ProviderDeviceId = "porch", Name = "Porch", Type = "camera", IsOnline = false }.ToDto();
        _ = _deviceRepository.Setup(r => r.ListAsync(It.IsAny<System.Threading.CancellationToken>())).ReturnsAsync([device]);

        // Act
        IRenderedComponent<LiveView> cut = Render<VideoForensics.Client.Ui.Shared.Pages.LiveView>();

        // Assert: Start is disabled and the tile says why, before any session is started
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='live-view-tile']")));
        Assert.True(cut.Find("[data-testid='live-view-tile'] button").HasAttribute("disabled"));
        Assert.False(string.IsNullOrWhiteSpace(cut.Find("[data-testid='live-view-offline']").TextContent));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(null)]
    public void LiveView_CameraOnlineOrUnknown_StartStaysEnabledWithNoReason(bool? isOnline)
    {
        // Arrange: online, or no state from the provider, is not a reason to block
        var device = new Device { Id = Camera, LocationId = Guid.NewGuid(), ProviderDeviceId = "porch", Name = "Porch", Type = "camera", IsOnline = isOnline }.ToDto();
        _ = _deviceRepository.Setup(r => r.ListAsync(It.IsAny<System.Threading.CancellationToken>())).ReturnsAsync([device]);

        // Act
        IRenderedComponent<LiveView> cut = Render<VideoForensics.Client.Ui.Shared.Pages.LiveView>();

        // Assert
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='live-view-tile']")));
        Assert.False(cut.Find("[data-testid='live-view-tile'] button").HasAttribute("disabled"));
        Assert.Empty(cut.FindAll("[data-testid='live-view-offline']"));
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

    [Fact]
    public void LiveView_StartOneStream_CallsStartLiveViewOnce()
    {
        // Arrange: one camera, and the fake module that records the stream calls
        AddCamera();
        SetupFakeLiveViewModule();
        IRenderedComponent<LiveView> cut = Render<VideoForensics.Client.Ui.Shared.Pages.LiveView>();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='live-view-tile']")));

        // Act: the camera's own Start button
        cut.Find("[data-testid='live-view-tile'] button").Click();

        // Assert: the stream is started exactly once, for the session the service returned
        cut.WaitForAssertion(() => Assert.Single(_liveViewCalls, c => c.Name == "startLiveView"));
        Assert.Equal(_sessionsByDevice[Camera].Id, Assert.Single(_liveViewCalls, c => c.Name == "startLiveView").SessionId);
    }

    [Fact]
    public void LiveView_TwoCameras_BothAreLiveAtOnce()
    {
        // Arrange
        AddCameras(Camera, Garage);
        SetupFakeLiveViewModule();
        IRenderedComponent<LiveView> cut = Render<VideoForensics.Client.Ui.Shared.Pages.LiveView>();
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("[data-testid='live-view-tile']").Count));

        // Act: start the first camera, then the second while the first stays live
        StartBoth(cut, Camera, Garage);

        // Assert: both streams started, each in its own player element
        cut.WaitForAssertion(() =>
        {
            List<(string Name, Guid SessionId, string? ElementId)> starts = _liveViewCalls.Where(c => c.Name == "startLiveView").ToList();
            Assert.Equal(2, starts.Count);
            Assert.Contains(starts, c => c.ElementId == PlayerIdFor(Camera) && c.SessionId == _sessionsByDevice[Camera].Id);
            Assert.Contains(starts, c => c.ElementId == PlayerIdFor(Garage) && c.SessionId == _sessionsByDevice[Garage].Id);
        });
    }

    [Fact]
    public void LiveView_ThirdStartFails_TheTwoLiveStreamsStay()
    {
        // Arrange: two cameras are live, and the third refuses to start
        AddCameras(Camera, Garage, Flood);
        SetupFakeLiveViewModule();
        _ = _sessionService
            .Setup(s => s.StartAsync(Flood, It.IsAny<LiveViewTriggerReasonDto>(), It.IsAny<Guid?>(), It.IsAny<System.Threading.CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Start refused"));
        IRenderedComponent<LiveView> cut = Render<VideoForensics.Client.Ui.Shared.Pages.LiveView>();
        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll("[data-testid='live-view-tile']").Count));
        StartBoth(cut, Camera, Garage);

        // Act: the third camera's Start fails
        ClickTile(cut, Flood);

        // Assert: the failure is shown, and the first two are still live with their players
        cut.WaitForAssertion(() => _sessionService.Verify(s => s.StartAsync(Flood, It.IsAny<LiveViewTriggerReasonDto>(), It.IsAny<Guid?>(), It.IsAny<System.Threading.CancellationToken>()), Times.Once));
        Assert.Single(cut.FindAll($"#{PlayerIdFor(Camera)}"));
        Assert.Single(cut.FindAll($"#{PlayerIdFor(Garage)}"));
        Assert.DoesNotContain(_liveViewCalls, c => c.Name == "stopLiveView");
    }

    [Fact]
    public void LiveView_StopOneCamera_KeepsTheOtherLive()
    {
        // Arrange: two cameras are live
        AddCameras(Camera, Garage);
        SetupFakeLiveViewModule();
        IRenderedComponent<LiveView> cut = Render<VideoForensics.Client.Ui.Shared.Pages.LiveView>();
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("[data-testid='live-view-tile']").Count));
        StartBoth(cut, Camera, Garage);
        Guid cameraSession = _sessionsByDevice[Camera].Id;
        Guid garageSession = _sessionsByDevice[Garage].Id;

        // Act: stop the first camera from its own tile
        ClickTile(cut, Camera);

        // Assert: only that camera's session is stopped and unsubscribed, and the other keeps its player
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll($"#{PlayerIdFor(Camera)}")));
        Assert.Equal(cameraSession, Assert.Single(_liveViewCalls, c => c.Name == "stopLiveView").SessionId);
        Assert.Single(cut.FindAll($"#{PlayerIdFor(Garage)}"));
        _sessionService.Verify(s => s.StopAsync(cameraSession, "ManualStop", It.IsAny<System.Threading.CancellationToken>()), Times.Once);
        _sessionService.Verify(s => s.StopAsync(garageSession, It.IsAny<string>(), It.IsAny<System.Threading.CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LiveView_DisposeWithTwoLive_StopsBothSessions()
    {
        // Arrange: two cameras are live
        AddCameras(Camera, Garage);
        SetupFakeLiveViewModule();
        IRenderedComponent<LiveView> cut = Render<VideoForensics.Client.Ui.Shared.Pages.LiveView>();
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("[data-testid='live-view-tile']").Count));
        StartBoth(cut, Camera, Garage);
        Guid cameraSession = _sessionsByDevice[Camera].Id;
        Guid garageSession = _sessionsByDevice[Garage].Id;

        // Act: the page is torn down while both streams run
        await cut.Instance.DisposeAsync();

        // Assert: every live session is stopped with the page-disposed reason and its player is closed in JavaScript
        _sessionService.Verify(s => s.StopAsync(cameraSession, "PageDisposed", It.IsAny<System.Threading.CancellationToken>()), Times.Once);
        _sessionService.Verify(s => s.StopAsync(garageSession, "PageDisposed", It.IsAny<System.Threading.CancellationToken>()), Times.Once);
        Assert.Contains(_liveViewCalls, c => c.Name == "stopLiveView" && c.SessionId == cameraSession);
        Assert.Contains(_liveViewCalls, c => c.Name == "stopLiveView" && c.SessionId == garageSession);
    }

    private static readonly Guid Yard = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid Shed = Guid.Parse("66666666-6666-6666-6666-666666666666");

    // The page's error alert lives in the right panel, so its markup is read from the panel slot the page pushed.
    private string ShownError()
    {
        VideoForensics.Client.Ui.Shared.Services.RightPanelContentService slot = Services.GetRequiredService<VideoForensics.Client.Ui.Shared.Services.RightPanelContentService>();
        return slot.Content is null ? string.Empty : Render(slot.Content).Markup;
    }

    // The page's localized text for a resource key, formatted with the given arguments.
    private string Localized(string key, params object[] args) =>
        Services.GetRequiredService<Microsoft.Extensions.Localization.IStringLocalizer<LiveView>>()[key, args].Value;

    [Fact]
    public void LiveView_FourLive_FifthStartIsDisabledWithTheCapReason()
    {
        // Arrange: five cameras, and the first four are live
        AddCameras(Camera, Garage, Flood, Yard, Shed);
        SetupFakeLiveViewModule();
        IRenderedComponent<LiveView> cut = Render<VideoForensics.Client.Ui.Shared.Pages.LiveView>();
        cut.WaitForAssertion(() => Assert.Equal(5, cut.FindAll("[data-testid='live-view-tile']").Count));
        foreach (Guid id in new[] { Camera, Garage, Flood, Yard })
        {
            ClickTile(cut, id);
            WaitForLive(cut, id);
        }

        // Act: nothing more; the fifth camera's tile renders at the cap

        // Assert: its Start is disabled, and its tile says why
        var fifth = cut.Find($"[data-testid='live-view-tile'][data-device-id='{Shed:D}']");
        Assert.True(fifth.QuerySelector("button")!.HasAttribute("disabled"));
        Assert.Equal(Localized("LiveViewCapReached", 4), fifth.QuerySelector("[data-testid='live-view-cap']")!.TextContent.Trim());
    }

    [Fact]
    public void LiveView_StartRefusedAtCap_ShowsTheCapMessageAndKeepsTheOthersLive()
    {
        // Arrange: two cameras are live, and the third is refused because the cap is reached
        AddCameras(Camera, Garage, Flood);
        SetupFakeLiveViewModule();
        _ = _sessionService
            .Setup(s => s.StartAsync(Flood, It.IsAny<LiveViewTriggerReasonDto>(), It.IsAny<Guid?>(), It.IsAny<System.Threading.CancellationToken>()))
            .ThrowsAsync(new LiveViewCapReachedException("Four streams are live."));
        IRenderedComponent<LiveView> cut = Render<VideoForensics.Client.Ui.Shared.Pages.LiveView>();
        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll("[data-testid='live-view-tile']").Count));
        StartBoth(cut, Camera, Garage);

        // Act: the third camera's Start is refused
        ClickTile(cut, Flood);

        // Assert: the localized cap message is shown, and the first two are still live with their players
        cut.WaitForAssertion(() => Assert.Contains(Localized("LiveViewCapReached", 4), ShownError()));
        Assert.Single(cut.FindAll($"#{PlayerIdFor(Camera)}"));
        Assert.Single(cut.FindAll($"#{PlayerIdFor(Garage)}"));
        Assert.DoesNotContain(_liveViewCalls, c => c.Name == "stopLiveView");
    }

    [Fact]
    public void LiveView_StartRefusedByBudget_ShowsTheBudgetMessage()
    {
        // Arrange: one camera whose start is refused because the provider's API budget is used up
        AddCamera();
        SetupFakeLiveViewModule();
        _ = _sessionService
            .Setup(s => s.StartAsync(Camera, It.IsAny<LiveViewTriggerReasonDto>(), It.IsAny<Guid?>(), It.IsAny<System.Threading.CancellationToken>()))
            .ThrowsAsync(new ProviderBudgetExceededException("Provider API budget exceeded for Ring."));
        IRenderedComponent<LiveView> cut = Render<VideoForensics.Client.Ui.Shared.Pages.LiveView>();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='live-view-tile']")));

        // Act
        ClickTile(cut, Camera);

        // Assert: the localized budget message is shown, and no stream started
        cut.WaitForAssertion(() => Assert.Contains(Localized("LiveViewBudgetExceeded"), ShownError()));
        Assert.DoesNotContain(_liveViewCalls, c => c.Name == "startLiveView");
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
        // The camera selector in the right panel needs the popup state when the panel fragment is rendered in a test.
        _ = Services.AddScoped<VideoForensics.Client.Ui.Shared.Services.Review.ReviewPopupState>();
        _ = Services.AddScoped(_ => new VideoForensics.Client.Ui.Shared.Services.Scope.ScopeState(TimeProvider.System));

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

        SetupSessionService();

        var sessionSource = new Mock<ILiveViewSessionSource>();
        _ = sessionSource.SetupGet(s => s.SessionChanged).Returns(_sessionChanged);
        _ = sessionSource.SetupGet(s => s.Telemetry).Returns(new Subject<LiveViewTelemetrySampleDto>());
        _ = sessionSource.SetupGet(s => s.IsConnected).Returns(false);

        _ = Services.AddSingleton(deviceRepository.Object);
        _ = Services.AddSingleton(_sessionService.Object);
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
