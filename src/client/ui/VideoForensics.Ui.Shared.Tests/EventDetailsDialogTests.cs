namespace VideoForensics.Ui.Shared.Tests;

using Bunit;
using Microsoft.AspNetCore.Components;
using Moq;
using Xunit;
using Syncfusion.Blazor;
using VideoForensics.Ui.Shared.Components;

public class EventDetailsDialog_Rendering_Tests : Bunit.TestContext
{
    public EventDetailsDialog_Rendering_Tests()
    {
        ComponentFactories.AddStub<Syncfusion.Blazor.Buttons.SfButton>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Renders_ImageFormat_WithContentUrl_RendersImgTag()
    {
        // Arrange
        var component = Render<EventDetailsDialog>(parameters => parameters
            .Add(p => p.MediaItemId, Guid.NewGuid())
            .Add(p => p.MediaFormat, "image/jpeg")
            .Add(p => p.ContentUrl, "https://example.com/image.jpg")
            .Add(p => p.EventType, "TestEvent")
            .Add(p => p.OccurredAtUtc, DateTime.UtcNow)
            .Add(p => p.DiscoveredAtUtc, DateTime.UtcNow)
            .Add(p => p.ProviderEventId, "test-event-id")
            .Add(p => p.OnClosed, EventCallback.Factory.Create(this, async () => { })));

        // Act & Assert
        var imgTag = component.Find("img");
        Assert.NotNull(imgTag);
        Assert.Equal("https://example.com/image.jpg", imgTag.GetAttribute("src"));
    }

    [Fact]
    public void Renders_VideoFormat_WithContentUrl_RendersVideoTag()
    {
        // Arrange
        var component = Render<EventDetailsDialog>(parameters => parameters
            .Add(p => p.MediaItemId, Guid.NewGuid())
            .Add(p => p.MediaFormat, "video/mp4")
            .Add(p => p.ContentUrl, "https://example.com/video.mp4")
            .Add(p => p.EventType, "TestEvent")
            .Add(p => p.OccurredAtUtc, DateTime.UtcNow)
            .Add(p => p.DiscoveredAtUtc, DateTime.UtcNow)
            .Add(p => p.ProviderEventId, "test-event-id")
            .Add(p => p.OnClosed, EventCallback.Factory.Create(this, async () => { })));

        // Act & Assert
        var videoTag = component.Find("video");
        Assert.NotNull(videoTag);
        Assert.Equal("https://example.com/video.mp4", videoTag.GetAttribute("src"));
        Assert.Equal("metadata", videoTag.GetAttribute("preload"));
    }

    [Fact]
    public void Renders_MediaItemWithoutContentUrl_ShowsUnavailableMessage()
    {
        // Arrange
        var component = Render<EventDetailsDialog>(parameters => parameters
            .Add(p => p.MediaItemId, Guid.NewGuid())
            .Add(p => p.MediaFormat, "image/jpeg")
            .Add(p => p.ContentUrl, (string?)null)
            .Add(p => p.EventType, "TestEvent")
            .Add(p => p.OccurredAtUtc, DateTime.UtcNow)
            .Add(p => p.DiscoveredAtUtc, DateTime.UtcNow)
            .Add(p => p.ProviderEventId, "test-event-id")
            .Add(p => p.OnClosed, EventCallback.Factory.Create(this, async () => { })));

        // Act & Assert
        var imgTag = component.FindAll("img");
        Assert.Empty(imgTag);

        var videoTag = component.FindAll("video");
        Assert.Empty(videoTag);

        var unavailableDiv = component.Find(".media-preview-unavailable");
        Assert.NotNull(unavailableDiv);
    }

    [Fact]
    public void Renders_WithoutMediaItemId_NoMediaPreview()
    {
        // Arrange
        var component = Render<EventDetailsDialog>(parameters => parameters
            .Add(p => p.MediaItemId, (Guid?)null)
            .Add(p => p.EventType, "TestEvent")
            .Add(p => p.OccurredAtUtc, DateTime.UtcNow)
            .Add(p => p.DiscoveredAtUtc, DateTime.UtcNow)
            .Add(p => p.ProviderEventId, "test-event-id")
            .Add(p => p.OnClosed, EventCallback.Factory.Create(this, async () => { })));

        // Act & Assert
        var imgTags = component.FindAll("img");
        Assert.Empty(imgTags);

        var videoTags = component.FindAll("video");
        Assert.Empty(videoTags);

        var unavailableDivs = component.FindAll(".media-preview-unavailable");
        Assert.Empty(unavailableDivs);
    }
}
