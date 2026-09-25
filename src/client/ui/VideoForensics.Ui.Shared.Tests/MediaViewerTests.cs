namespace VideoForensics.Ui.Shared.Tests;

using System;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Xunit;
using VideoForensics.Ui.Shared.Components.Evidence;
using VideoForensics.Ui.Shared.Services;
using VideoForensics.Ui.Shared.Services.Evidence;
using VideoForensics.Data.Common.Entities;

public class MediaViewer_Image_Tests : BunitContext
{
    public MediaViewer_Image_Tests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private EvidenceItem CreateImageItem()
    {
        return new EvidenceItem
        {
            Key = "media:123",
            Kind = EvidenceKind.Snapshot,
            OccurredAtUtc = new DateTime(2024, 1, 15, 14, 30, 0, DateTimeKind.Utc),
            DeviceId = Guid.NewGuid(),
            DeviceName = "Front Door",
            Media = new MediaItem
            {
                Id = Guid.NewGuid(),
                FileName = "snapshot.jpg",
                MediaFormat = "image/jpeg",
                FilePath = "/media/snapshot.jpg",
                Resolution = "1920x1080",
                Sha256Hash = "abcdef1234567890abcdef1234567890",
                DeviceId = Guid.NewGuid(),
                RecordedAtUtc = DateTime.UtcNow,
                DownloadedAtUtc = DateTime.UtcNow
            }
        };
    }

    [Fact]
    public void Renders_Header_WithLabel()
    {
        // Arrange
        var item = CreateImageItem();

        // Act
        var component = Render<MediaViewer>(parameters => parameters
            .Add(p => p.Item, item)
            .Add(p => p.ContentUrl, "https://example.com/image.jpg")
            .Add(p => p.HasPrevious, false)
            .Add(p => p.HasNext, false));

        // Assert
        var header = component.Find(".viewer-header");
        Assert.NotNull(header);
        Assert.Contains("Snapshot", header.TextContent);
        Assert.Contains("Front Door", header.TextContent);
    }

    [Fact]
    public void Renders_Header_WithFormattedDate()
    {
        // Arrange
        var item = CreateImageItem();

        // Act
        var component = Render<MediaViewer>(parameters => parameters
            .Add(p => p.Item, item)
            .Add(p => p.ContentUrl, "https://example.com/image.jpg")
            .Add(p => p.HasPrevious, false)
            .Add(p => p.HasNext, false));

        // Assert
        var header = component.Find(".viewer-header");
        Assert.Contains("2024-01-15 14:30:00 UTC", header.TextContent);
    }

    [Fact]
    public void Renders_Header_WithHashPreview()
    {
        // Arrange
        var item = CreateImageItem();

        // Act
        var component = Render<MediaViewer>(parameters => parameters
            .Add(p => p.Item, item)
            .Add(p => p.ContentUrl, "https://example.com/image.jpg")
            .Add(p => p.HasPrevious, false)
            .Add(p => p.HasNext, false));

        // Assert
        var header = component.Find(".viewer-hash");
        Assert.NotNull(header);
        Assert.Contains("abcdef123456", header.TextContent);
        // Check title attribute has full hash
        var titleAttr = header.GetAttribute("title");
        Assert.Equal("abcdef1234567890abcdef1234567890", titleAttr);
    }

    [Fact]
    public void Renders_Image_WithCorrectSrc()
    {
        // Arrange
        var item = CreateImageItem();
        var url = "https://example.com/image.jpg";

        // Act
        var component = Render<MediaViewer>(parameters => parameters
            .Add(p => p.Item, item)
            .Add(p => p.ContentUrl, url)
            .Add(p => p.HasPrevious, false)
            .Add(p => p.HasNext, false));

        // Assert
        var img = component.Find("img");
        Assert.NotNull(img);
        Assert.Equal(url, img.GetAttribute("src"));
    }

    [Fact]
    public void Renders_Image_WithDefaultTransform()
    {
        // Arrange
        var item = CreateImageItem();

        // Act
        var component = Render<MediaViewer>(parameters => parameters
            .Add(p => p.Item, item)
            .Add(p => p.ContentUrl, "https://example.com/image.jpg")
            .Add(p => p.HasPrevious, false)
            .Add(p => p.HasNext, false));

        // Assert
        var img = component.Find("img");
        var style = img.GetAttribute("style");
        Assert.Contains("translate(0px, 0px) scale(1)", style);
    }

    [Fact]
    public void ZoomInButton_UpdatesImageScale()
    {
        // Arrange
        var item = CreateImageItem();
        var component = Render<MediaViewer>(parameters => parameters
            .Add(p => p.Item, item)
            .Add(p => p.ContentUrl, "https://example.com/image.jpg")
            .Add(p => p.HasPrevious, false)
            .Add(p => p.HasNext, false));

        // Act
        var zoomInBtn = component.Find("[data-testid='zoom-in']");
        zoomInBtn.Click();

        // Assert
        var img = component.Find("img");
        var style = img.GetAttribute("style");
        Assert.Contains("scale(1.25)", style);
    }

    [Fact]
    public void ZoomOutButton_DecreasesImageScale()
    {
        // Arrange
        var item = CreateImageItem();
        var component = Render<MediaViewer>(parameters => parameters
            .Add(p => p.Item, item)
            .Add(p => p.ContentUrl, "https://example.com/image.jpg")
            .Add(p => p.HasPrevious, false)
            .Add(p => p.HasNext, false));

        // Act - zoom in twice
        var zoomInBtn = component.Find("[data-testid='zoom-in']");
        zoomInBtn.Click();
        zoomInBtn.Click();

        // Then zoom out once
        var zoomOutBtn = component.Find("[data-testid='zoom-out']");
        zoomOutBtn.Click();

        // Assert - should be 1.25
        var img = component.Find("img");
        var style = img.GetAttribute("style");
        Assert.Contains("scale(1.25)", style);
    }

    [Fact]
    public void ZoomFitButton_ResetsZoom()
    {
        // Arrange
        var item = CreateImageItem();
        var component = Render<MediaViewer>(parameters => parameters
            .Add(p => p.Item, item)
            .Add(p => p.ContentUrl, "https://example.com/image.jpg")
            .Add(p => p.HasPrevious, false)
            .Add(p => p.HasNext, false));

        // Act - zoom in
        var zoomInBtn = component.Find("[data-testid='zoom-in']");
        zoomInBtn.Click();

        // Then click fit
        var fitBtn = component.Find("[data-testid='zoom-fit']");
        fitBtn.Click();

        // Assert
        var img = component.Find("img");
        var style = img.GetAttribute("style");
        Assert.Contains("translate(0px, 0px) scale(1)", style);
    }

    [Fact]
    public void WheelUp_OnImage_ZoomsIn()
    {
        // Arrange
        var item = CreateImageItem();
        var component = Render<MediaViewer>(parameters => parameters
            .Add(p => p.Item, item)
            .Add(p => p.ContentUrl, "https://example.com/image.jpg")
            .Add(p => p.HasPrevious, false)
            .Add(p => p.HasNext, false));

        // Act - wheel scroll up (negative deltaY) zooms in
        var img = component.Find("img");
        img.Wheel(new WheelEventArgs { DeltaY = -100 });

        // Assert
        var style = component.Find("img").GetAttribute("style");
        Assert.Contains("scale(1.25)", style);
    }

    [Fact]
    public void WheelDown_OnImage_ZoomsOut()
    {
        // Arrange
        var item = CreateImageItem();
        var component = Render<MediaViewer>(parameters => parameters
            .Add(p => p.Item, item)
            .Add(p => p.ContentUrl, "https://example.com/image.jpg")
            .Add(p => p.HasPrevious, false)
            .Add(p => p.HasNext, false));

        // Act - zoom in twice, then wheel down once
        var img = component.Find("img");
        img.Wheel(new WheelEventArgs { DeltaY = -100 });
        img.Wheel(new WheelEventArgs { DeltaY = -100 });
        img.Wheel(new WheelEventArgs { DeltaY = 100 });

        // Assert
        var style = component.Find("img").GetAttribute("style");
        Assert.Contains("scale(1.25)", style);
    }

    [Fact]
    public void MouseDrag_AtScaleGreaterThan1_PansImage()
    {
        // Arrange
        var item = CreateImageItem();
        var component = Render<MediaViewer>(parameters => parameters
            .Add(p => p.Item, item)
            .Add(p => p.ContentUrl, "https://example.com/image.jpg")
            .Add(p => p.HasPrevious, false)
            .Add(p => p.HasNext, false));

        // Zoom in so panning has an effect
        component.Find("[data-testid='zoom-in']").Click();

        // Act - drag from (100, 100) to (140, 130)
        var img = component.Find("img");
        img.MouseDown(new MouseEventArgs { ClientX = 100, ClientY = 100 });
        img.MouseMove(new MouseEventArgs { ClientX = 140, ClientY = 130 });
        img.MouseUp(new MouseEventArgs { ClientX = 140, ClientY = 130 });

        // Assert - offset moved by the drag delta (40, 30)
        var style = component.Find("img").GetAttribute("style");
        Assert.Contains("translate(40px, 30px) scale(1.25)", style);
    }

    [Fact]
    public void MouseDrag_AtScale1_DoesNotPan()
    {
        // Arrange
        var item = CreateImageItem();
        var component = Render<MediaViewer>(parameters => parameters
            .Add(p => p.Item, item)
            .Add(p => p.ContentUrl, "https://example.com/image.jpg")
            .Add(p => p.HasPrevious, false)
            .Add(p => p.HasNext, false));

        // Act - drag without zooming in first
        var img = component.Find("img");
        img.MouseDown(new MouseEventArgs { ClientX = 100, ClientY = 100 });
        img.MouseMove(new MouseEventArgs { ClientX = 140, ClientY = 130 });
        img.MouseUp(new MouseEventArgs { ClientX = 140, ClientY = 130 });

        // Assert - no pan at scale 1
        var style = component.Find("img").GetAttribute("style");
        Assert.Contains("translate(0px, 0px) scale(1)", style);
    }

    [Fact]
    public void PreviousButton_DisabledWhenHasPreviousFalse()
    {
        // Arrange
        var item = CreateImageItem();

        // Act
        var component = Render<MediaViewer>(parameters => parameters
            .Add(p => p.Item, item)
            .Add(p => p.ContentUrl, "https://example.com/image.jpg")
            .Add(p => p.HasPrevious, false)
            .Add(p => p.HasNext, true));

        // Assert
        var prevBtn = component.Find("[data-testid='viewer-prev']");
        Assert.NotNull(prevBtn.GetAttribute("disabled"));
    }

    [Fact]
    public void NextButton_DisabledWhenHasNextFalse()
    {
        // Arrange
        var item = CreateImageItem();

        // Act
        var component = Render<MediaViewer>(parameters => parameters
            .Add(p => p.Item, item)
            .Add(p => p.ContentUrl, "https://example.com/image.jpg")
            .Add(p => p.HasPrevious, true)
            .Add(p => p.HasNext, false));

        // Assert
        var nextBtn = component.Find("[data-testid='viewer-next']");
        Assert.NotNull(nextBtn.GetAttribute("disabled"));
    }
}

public class MediaViewer_NoUrl_Tests : BunitContext
{
    public MediaViewer_NoUrl_Tests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Renders_UnavailableMessage_WhenUrlNull()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var item = new EvidenceItem
        {
            Key = "media:123",
            Kind = EvidenceKind.Snapshot,
            OccurredAtUtc = DateTime.UtcNow,
            DeviceId = deviceId,
            DeviceName = "Camera",
            Media = new MediaItem
            {
                Id = Guid.NewGuid(),
                FileName = "test.jpg",
                MediaFormat = "image/jpeg",
                FilePath = "/media/test.jpg",
                Sha256Hash = "test",
                DeviceId = deviceId,
                RecordedAtUtc = DateTime.UtcNow,
                DownloadedAtUtc = DateTime.UtcNow
            }
        };

        // Act
        var component = Render<MediaViewer>(parameters => parameters
            .Add(p => p.Item, item)
            .Add(p => p.ContentUrl, null));

        // Assert
        var unavailable = component.Find(".viewer-unavailable");
        Assert.NotNull(unavailable);
        Assert.Contains("Media preview unavailable", unavailable.TextContent);
    }

    [Fact]
    public void Renders_UnavailableMessage_WhenUrlEmpty()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var item = new EvidenceItem
        {
            Key = "media:123",
            Kind = EvidenceKind.Snapshot,
            OccurredAtUtc = DateTime.UtcNow,
            DeviceId = deviceId,
            DeviceName = "Camera",
            Media = new MediaItem
            {
                Id = Guid.NewGuid(),
                FileName = "test.jpg",
                MediaFormat = "image/jpeg",
                FilePath = "/media/test.jpg",
                Sha256Hash = "test",
                DeviceId = deviceId,
                RecordedAtUtc = DateTime.UtcNow,
                DownloadedAtUtc = DateTime.UtcNow
            }
        };

        // Act
        var component = Render<MediaViewer>(parameters => parameters
            .Add(p => p.Item, item)
            .Add(p => p.ContentUrl, ""));

        // Assert
        var unavailable = component.Find(".viewer-unavailable");
        Assert.NotNull(unavailable);
    }
}

public class MediaViewer_Video_Tests : BunitContext
{
    private readonly Bunit.BunitJSModuleInterop _module;

    public MediaViewer_Video_Tests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _module = JSInterop.SetupModule("./_content/VideoForensics.Ui.Shared/js/media-viewer.js");
        _module.SetupVoid("setPlaybackRate", _ => true);
        _module.SetupVoid("stepFrame", _ => true);
        _module.SetupVoid("togglePlay", _ => true);
        _module.SetupVoid("focusElement", _ => true);
    }

    private EvidenceItem CreateVideoItem(decimal? frameRate = null)
    {
        return new EvidenceItem
        {
            Key = "media:456",
            Kind = EvidenceKind.Video,
            OccurredAtUtc = new DateTime(2024, 1, 15, 15, 0, 0, DateTimeKind.Utc),
            DeviceId = Guid.NewGuid(),
            DeviceName = "Garage",
            Media = new MediaItem
            {
                Id = Guid.NewGuid(),
                FileName = "video.mp4",
                MediaFormat = "video/mp4",
                FilePath = "/media/video.mp4",
                FrameRate = frameRate,
                Resolution = "1280x720",
                Sha256Hash = "1111111111111111111111111111111111111111",
                DeviceId = Guid.NewGuid(),
                RecordedAtUtc = DateTime.UtcNow,
                DownloadedAtUtc = DateTime.UtcNow
            }
        };
    }

    [Fact]
    public void Renders_Video_WithPreloadMetadata()
    {
        // Arrange
        var item = CreateVideoItem();

        // Act
        var component = Render<MediaViewer>(parameters => parameters
            .Add(p => p.Item, item)
            .Add(p => p.ContentUrl, "https://example.com/video.mp4")
            .Add(p => p.HasPrevious, false)
            .Add(p => p.HasNext, false));

        // Assert
        var video = component.Find("video");
        Assert.NotNull(video);
        Assert.Equal("metadata", video.GetAttribute("preload"));
        Assert.Equal("https://example.com/video.mp4", video.GetAttribute("src"));
    }

    [Fact]
    public void SpeedSelect_RendersAllOptions()
    {
        // Arrange
        var item = CreateVideoItem();

        // Act
        var component = Render<MediaViewer>(parameters => parameters
            .Add(p => p.Item, item)
            .Add(p => p.ContentUrl, "https://example.com/video.mp4")
            .Add(p => p.HasPrevious, false)
            .Add(p => p.HasNext, false));

        // Assert
        var select = component.Find("[data-testid='speed']");
        Assert.NotNull(select);
        var options = component.FindAll("select option");
        Assert.Equal(5, options.Count);
        Assert.Equal("0.25", options[0].GetAttribute("value"));
        Assert.Equal("0.5", options[1].GetAttribute("value"));
        Assert.Equal("1", options[2].GetAttribute("value"));
        Assert.Equal("1.5", options[3].GetAttribute("value"));
        Assert.Equal("2", options[4].GetAttribute("value"));
    }

    [Fact]
    public void SpeedSelect_DefaultsTo1()
    {
        // Arrange
        var item = CreateVideoItem();

        // Act
        var component = Render<MediaViewer>(parameters => parameters
            .Add(p => p.Item, item)
            .Add(p => p.ContentUrl, "https://example.com/video.mp4")
            .Add(p => p.HasPrevious, false)
            .Add(p => p.HasNext, false));

        // Assert
        var select = component.Find("select[data-testid='speed']");
        Assert.NotNull(select);
        // The select should have value="1" selected
        var selectedOption = component.Find("select option[value='1']");
        Assert.NotNull(selectedOption);
    }

    [Fact]
    public void SpeedSelect_ChangedTo2_InvokesSetPlaybackRateWith2()
    {
        // Arrange
        var item = CreateVideoItem();
        var component = Render<MediaViewer>(parameters => parameters
            .Add(p => p.Item, item)
            .Add(p => p.ContentUrl, "https://example.com/video.mp4")
            .Add(p => p.HasPrevious, false)
            .Add(p => p.HasNext, false));

        // Act
        var select = component.Find("[data-testid='speed']");
        select.Change("2");

        // Assert
        var invocation = _module.VerifyInvoke("setPlaybackRate");
        Assert.Equal(2d, Assert.IsType<double>(invocation.Arguments[1]));
    }

    [Fact]
    public void FrameForwardButton_WithFrameRate25_InvokesStepFrameWith0_04()
    {
        // Arrange
        var item = CreateVideoItem(frameRate: 25);
        var component = Render<MediaViewer>(parameters => parameters
            .Add(p => p.Item, item)
            .Add(p => p.ContentUrl, "https://example.com/video.mp4")
            .Add(p => p.HasPrevious, false)
            .Add(p => p.HasNext, false));

        // Act
        component.Find("[data-testid='frame-forward']").Click();

        // Assert
        var invocation = _module.VerifyInvoke("stepFrame");
        var seconds = Assert.IsType<double>(invocation.Arguments[1]);
        Assert.Equal(1.0 / 25, seconds, precision: 6);
    }

    [Fact]
    public void FrameBackButton_WithNullFrameRate_InvokesStepFrameWithNegativeOneThirtieth()
    {
        // Arrange
        var item = CreateVideoItem(frameRate: null);
        var component = Render<MediaViewer>(parameters => parameters
            .Add(p => p.Item, item)
            .Add(p => p.ContentUrl, "https://example.com/video.mp4")
            .Add(p => p.HasPrevious, false)
            .Add(p => p.HasNext, false));

        // Act
        component.Find("[data-testid='frame-back']").Click();

        // Assert
        var invocation = _module.VerifyInvoke("stepFrame");
        var seconds = Assert.IsType<double>(invocation.Arguments[1]);
        Assert.Equal(-1.0 / 30, seconds, precision: 6);
    }

    [Fact]
    public void FrameForwardButton_WithFrameRateZero_InvokesStepFrameWithOneThirtieth()
    {
        // Arrange - regression test: FrameRate of 0 must not divide by zero, and falls back to 30fps
        var item = CreateVideoItem(frameRate: 0);
        var component = Render<MediaViewer>(parameters => parameters
            .Add(p => p.Item, item)
            .Add(p => p.ContentUrl, "https://example.com/video.mp4")
            .Add(p => p.HasPrevious, false)
            .Add(p => p.HasNext, false));

        // Act
        component.Find("[data-testid='frame-forward']").Click();

        // Assert
        var invocation = _module.VerifyInvoke("stepFrame");
        var seconds = Assert.IsType<double>(invocation.Arguments[1]);
        Assert.Equal(1.0 / 30, seconds, precision: 6);
    }

    [Fact]
    public void FrameBackButton_WithFrameRateZero_InvokesStepFrameWithNegativeOneThirtieth()
    {
        // Arrange - regression test: FrameRate of 0 must not divide by zero, and falls back to 30fps
        var item = CreateVideoItem(frameRate: 0);
        var component = Render<MediaViewer>(parameters => parameters
            .Add(p => p.Item, item)
            .Add(p => p.ContentUrl, "https://example.com/video.mp4")
            .Add(p => p.HasPrevious, false)
            .Add(p => p.HasNext, false));

        // Act
        component.Find("[data-testid='frame-back']").Click();

        // Assert
        var invocation = _module.VerifyInvoke("stepFrame");
        var seconds = Assert.IsType<double>(invocation.Arguments[1]);
        Assert.Equal(-1.0 / 30, seconds, precision: 6);
    }

    [Fact]
    public void PlayToggleButton_Click_InvokesTogglePlay()
    {
        // Arrange
        var item = CreateVideoItem();
        var component = Render<MediaViewer>(parameters => parameters
            .Add(p => p.Item, item)
            .Add(p => p.ContentUrl, "https://example.com/video.mp4")
            .Add(p => p.HasPrevious, false)
            .Add(p => p.HasNext, false));

        // Act
        component.Find("[data-testid='play-toggle']").Click();

        // Assert
        _module.VerifyInvoke("togglePlay");
    }
}

public class MediaViewer_KeyboardNavigation_Tests : BunitContext
{
    private readonly Bunit.BunitJSModuleInterop _module;

    public MediaViewer_KeyboardNavigation_Tests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _module = JSInterop.SetupModule("./_content/VideoForensics.Ui.Shared/js/media-viewer.js");
        _module.SetupVoid("setPlaybackRate", _ => true);
        _module.SetupVoid("stepFrame", _ => true);
        _module.SetupVoid("togglePlay", _ => true);
        _module.SetupVoid("focusElement", _ => true);
    }

    [Fact]
    public void ArrowRight_InvokesOnNext_WhenHasNextTrue()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var item = new EvidenceItem
        {
            Key = "media:1",
            Kind = EvidenceKind.Video,
            OccurredAtUtc = DateTime.UtcNow,
            DeviceId = deviceId,
            DeviceName = "Camera",
            Media = new MediaItem
            {
                Id = Guid.NewGuid(),
                FileName = "video.mp4",
                MediaFormat = "video/mp4",
                FilePath = "/media/video.mp4",
                Sha256Hash = "test",
                DeviceId = deviceId,
                RecordedAtUtc = DateTime.UtcNow,
                DownloadedAtUtc = DateTime.UtcNow
            }
        };
        var callbackInvoked = false;
        var component = Render<MediaViewer>(parameters => parameters
            .Add(p => p.Item, item)
            .Add(p => p.ContentUrl, "https://example.com/video.mp4")
            .Add(p => p.HasPrevious, false)
            .Add(p => p.HasNext, true)
            .Add(p => p.OnNext, EventCallback.Factory.Create(this, () => { callbackInvoked = true; })));

        // Act
        var viewer = component.Find("[data-testid='media-viewer']");
        viewer.KeyDown("ArrowRight");

        // Assert
        Assert.True(callbackInvoked);
    }

    [Fact]
    public void ArrowRight_DoesNotInvokeOnNext_WhenHasNextFalse()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var item = new EvidenceItem
        {
            Key = "media:1",
            Kind = EvidenceKind.Video,
            OccurredAtUtc = DateTime.UtcNow,
            DeviceId = deviceId,
            DeviceName = "Camera",
            Media = new MediaItem
            {
                Id = Guid.NewGuid(),
                FileName = "video.mp4",
                MediaFormat = "video/mp4",
                FilePath = "/media/video.mp4",
                Sha256Hash = "test",
                DeviceId = deviceId,
                RecordedAtUtc = DateTime.UtcNow,
                DownloadedAtUtc = DateTime.UtcNow
            }
        };
        var callbackInvoked = false;
        var component = Render<MediaViewer>(parameters => parameters
            .Add(p => p.Item, item)
            .Add(p => p.ContentUrl, "https://example.com/video.mp4")
            .Add(p => p.HasPrevious, false)
            .Add(p => p.HasNext, false)
            .Add(p => p.OnNext, EventCallback.Factory.Create(this, () => { callbackInvoked = true; })));

        // Act
        var viewer = component.Find("[data-testid='media-viewer']");
        viewer.KeyDown("ArrowRight");

        // Assert
        Assert.False(callbackInvoked);
    }

    [Fact]
    public void ArrowLeft_InvokesOnPrevious_WhenHasPreviousTrue()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var item = new EvidenceItem
        {
            Key = "media:1",
            Kind = EvidenceKind.Video,
            OccurredAtUtc = DateTime.UtcNow,
            DeviceId = deviceId,
            DeviceName = "Camera",
            Media = new MediaItem
            {
                Id = Guid.NewGuid(),
                FileName = "video.mp4",
                MediaFormat = "video/mp4",
                FilePath = "/media/video.mp4",
                Sha256Hash = "test",
                DeviceId = deviceId,
                RecordedAtUtc = DateTime.UtcNow,
                DownloadedAtUtc = DateTime.UtcNow
            }
        };
        var callbackInvoked = false;
        var component = Render<MediaViewer>(parameters => parameters
            .Add(p => p.Item, item)
            .Add(p => p.ContentUrl, "https://example.com/video.mp4")
            .Add(p => p.HasPrevious, true)
            .Add(p => p.HasNext, false)
            .Add(p => p.OnPrevious, EventCallback.Factory.Create(this, () => { callbackInvoked = true; })));

        // Act
        var viewer = component.Find("[data-testid='media-viewer']");
        viewer.KeyDown("ArrowLeft");

        // Assert
        Assert.True(callbackInvoked);
    }

    [Fact]
    public void Escape_InvokesOnClose()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var item = new EvidenceItem
        {
            Key = "media:1",
            Kind = EvidenceKind.Video,
            OccurredAtUtc = DateTime.UtcNow,
            DeviceId = deviceId,
            DeviceName = "Camera",
            Media = new MediaItem
            {
                Id = Guid.NewGuid(),
                FileName = "video.mp4",
                MediaFormat = "video/mp4",
                FilePath = "/media/video.mp4",
                Sha256Hash = "test",
                DeviceId = deviceId,
                RecordedAtUtc = DateTime.UtcNow,
                DownloadedAtUtc = DateTime.UtcNow
            }
        };
        var callbackInvoked = false;
        var component = Render<MediaViewer>(parameters => parameters
            .Add(p => p.Item, item)
            .Add(p => p.ContentUrl, "https://example.com/video.mp4")
            .Add(p => p.HasPrevious, false)
            .Add(p => p.HasNext, false)
            .Add(p => p.OnClose, EventCallback.Factory.Create(this, () => { callbackInvoked = true; })));

        // Act
        var viewer = component.Find("[data-testid='media-viewer']");
        viewer.KeyDown("Escape");

        // Assert
        Assert.True(callbackInvoked);
    }

    private static EvidenceItem CreateVideoItemForKeyboard(decimal? frameRate = null)
    {
        var deviceId = Guid.NewGuid();
        return new EvidenceItem
        {
            Key = "media:1",
            Kind = EvidenceKind.Video,
            OccurredAtUtc = DateTime.UtcNow,
            DeviceId = deviceId,
            DeviceName = "Camera",
            Media = new MediaItem
            {
                Id = Guid.NewGuid(),
                FileName = "video.mp4",
                MediaFormat = "video/mp4",
                FilePath = "/media/video.mp4",
                FrameRate = frameRate,
                Sha256Hash = "test",
                DeviceId = deviceId,
                RecordedAtUtc = DateTime.UtcNow,
                DownloadedAtUtc = DateTime.UtcNow
            }
        };
    }

    [Fact]
    public void SpaceKey_OnVideo_InvokesTogglePlay()
    {
        // Arrange
        var item = CreateVideoItemForKeyboard();
        var component = Render<MediaViewer>(parameters => parameters
            .Add(p => p.Item, item)
            .Add(p => p.ContentUrl, "https://example.com/video.mp4")
            .Add(p => p.HasPrevious, false)
            .Add(p => p.HasNext, false));

        // Act
        component.Find("[data-testid='media-viewer']").KeyDown(" ");

        // Assert
        _module.VerifyInvoke("togglePlay");
    }

    [Fact]
    public void PeriodKey_OnVideo_InvokesStepFrameForward()
    {
        // Arrange
        var item = CreateVideoItemForKeyboard(frameRate: 25);
        var component = Render<MediaViewer>(parameters => parameters
            .Add(p => p.Item, item)
            .Add(p => p.ContentUrl, "https://example.com/video.mp4")
            .Add(p => p.HasPrevious, false)
            .Add(p => p.HasNext, false));

        // Act
        component.Find("[data-testid='media-viewer']").KeyDown(".");

        // Assert
        var invocation = _module.VerifyInvoke("stepFrame");
        var seconds = Assert.IsType<double>(invocation.Arguments[1]);
        Assert.True(seconds > 0);
        Assert.Equal(1.0 / 25, seconds, precision: 6);
    }

    [Fact]
    public void CommaKey_OnVideo_InvokesStepFrameBack()
    {
        // Arrange
        var item = CreateVideoItemForKeyboard(frameRate: 25);
        var component = Render<MediaViewer>(parameters => parameters
            .Add(p => p.Item, item)
            .Add(p => p.ContentUrl, "https://example.com/video.mp4")
            .Add(p => p.HasPrevious, false)
            .Add(p => p.HasNext, false));

        // Act
        component.Find("[data-testid='media-viewer']").KeyDown(",");

        // Assert
        var invocation = _module.VerifyInvoke("stepFrame");
        var seconds = Assert.IsType<double>(invocation.Arguments[1]);
        Assert.True(seconds < 0);
        Assert.Equal(-1.0 / 25, seconds, precision: 6);
    }
}

public class MediaViewer_ItemChange_Tests : BunitContext
{
    public MediaViewer_ItemChange_Tests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void ChangingItem_ResetsZoom()
    {
        // Arrange
        var deviceId1 = Guid.NewGuid();
        var deviceId2 = Guid.NewGuid();
        var item1 = new EvidenceItem
        {
            Key = "media:1",
            Kind = EvidenceKind.Snapshot,
            OccurredAtUtc = DateTime.UtcNow,
            DeviceId = deviceId1,
            DeviceName = "Camera1",
            Media = new MediaItem
            {
                Id = Guid.NewGuid(),
                FileName = "img1.jpg",
                MediaFormat = "image/jpeg",
                FilePath = "/media/img1.jpg",
                Sha256Hash = "test1",
                DeviceId = deviceId1,
                RecordedAtUtc = DateTime.UtcNow,
                DownloadedAtUtc = DateTime.UtcNow
            }
        };
        var item2 = new EvidenceItem
        {
            Key = "media:2",
            Kind = EvidenceKind.Snapshot,
            OccurredAtUtc = DateTime.UtcNow,
            DeviceId = deviceId2,
            DeviceName = "Camera2",
            Media = new MediaItem
            {
                Id = Guid.NewGuid(),
                FileName = "img2.jpg",
                MediaFormat = "image/jpeg",
                FilePath = "/media/img2.jpg",
                Sha256Hash = "test2",
                DeviceId = deviceId2,
                RecordedAtUtc = DateTime.UtcNow,
                DownloadedAtUtc = DateTime.UtcNow
            }
        };

        var component = Render<MediaViewer>(parameters => parameters
            .Add(p => p.Item, item1)
            .Add(p => p.ContentUrl, "https://example.com/img1.jpg")
            .Add(p => p.HasPrevious, false)
            .Add(p => p.HasNext, false));

        // Act - zoom in
        var zoomInBtn = component.Find("[data-testid='zoom-in']");
        zoomInBtn.Click();
        var imgAfterZoom = component.Find("img");
        var styleAfterZoom = imgAfterZoom.GetAttribute("style");
        Assert.Contains("scale(1.25)", styleAfterZoom);

        // Change item - render with new parameters
        component.Render(parameters => parameters
            .Add(p => p.Item, item2)
            .Add(p => p.ContentUrl, "https://example.com/img2.jpg"));

        // Assert - zoom should be reset
        var imgAfterChange = component.Find("img");
        var styleAfterChange = imgAfterChange.GetAttribute("style");
        Assert.Contains("translate(0px, 0px) scale(1)", styleAfterChange);
    }
}
