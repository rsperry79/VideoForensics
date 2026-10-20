namespace VideoForensics.Ui.Shared.Tests.Services.Evidence;

using Xunit;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Ui.Shared.Services.Evidence;

public class EvidenceTimelineStyle_ToneClass_Tests
{
    private static readonly DateTime OccurredAtUtc = new(2026, 9, 20, 10, 0, 0, DateTimeKind.Utc);

    private static EvidenceItem EventItem(string eventType, Guid? eventId = null, MediaItem? media = null)
    {
        var id = eventId ?? Guid.NewGuid();
        return new EvidenceItem
        {
            Key = $"event:{id}",
            Kind = EvidenceKind.Event,
            OccurredAtUtc = OccurredAtUtc,
            DeviceId = Guid.NewGuid(),
            DeviceName = "Front Door",
            Event = new Event
            {
                Id = id,
                EventType = eventType,
                OccurredAtUtc = OccurredAtUtc,
                DiscoveredAtUtc = OccurredAtUtc,
                ProviderEventId = "p1",
                MetadataJson = "{}"
            },
            Media = media
        };
    }

    private static EvidenceItem MediaOnlyItem(Guid mediaId) =>
        new()
        {
            Key = $"media:{mediaId}",
            Kind = EvidenceKind.Snapshot,
            OccurredAtUtc = OccurredAtUtc,
            DeviceId = Guid.NewGuid(),
            DeviceName = "Front Door",
            Media = new MediaItem
            {
                Id = mediaId,
                FileName = "still.jpg",
                FilePath = "still.jpg",
                MediaFormat = "image/jpeg",
                Sha256Hash = "abc",
                RecordedAtUtc = OccurredAtUtc,
                DeviceId = Guid.NewGuid(),
                IsPurged = false,
            }
        };

    private static readonly IReadOnlySet<Guid> NothingPinned = new HashSet<Guid>();

    [Fact]
    public void MediaOnly_NoEvent_IsGray()
    {
        var item = MediaOnlyItem(Guid.NewGuid());

        Assert.Equal("timeline-tone-none", EvidenceTimelineStyle.ToneClass(item, NothingPinned));
    }

    [Theory]
    [InlineData("motion")]
    [InlineData("Motion")]
    public void MotionEvent_IsGreen(string eventType)
    {
        Assert.Equal("timeline-tone-motion", EvidenceTimelineStyle.ToneClass(EventItem(eventType), NothingPinned));
    }

    [Theory]
    [InlineData("manual")]
    [InlineData("on_demand")]
    public void ManualEvent_IsYellow(string eventType)
    {
        Assert.Equal("timeline-tone-manual", EvidenceTimelineStyle.ToneClass(EventItem(eventType), NothingPinned));
    }

    [Fact]
    public void OtherEventType_IsGray()
    {
        Assert.Equal("timeline-tone-none", EvidenceTimelineStyle.ToneClass(EventItem("person"), NothingPinned));
    }

    [Fact]
    public void PinnedEvent_IsRed_EvenWhenMotion()
    {
        var eventId = Guid.NewGuid();
        var pinned = new HashSet<Guid> { eventId };

        Assert.Equal("timeline-tone-in-case", EvidenceTimelineStyle.ToneClass(EventItem("motion", eventId), pinned));
    }

    [Fact]
    public void PinnedMediaOnly_IsRed()
    {
        var mediaId = Guid.NewGuid();
        var pinned = new HashSet<Guid> { mediaId };

        Assert.Equal("timeline-tone-in-case", EvidenceTimelineStyle.ToneClass(MediaOnlyItem(mediaId), pinned));
    }

    [Fact]
    public void EventWithPinnedAttachedMedia_IsRed()
    {
        var media = MediaOnlyItem(Guid.NewGuid()).Media!;
        var pinned = new HashSet<Guid> { media.Id };

        Assert.Equal("timeline-tone-in-case", EvidenceTimelineStyle.ToneClass(EventItem("motion", media: media), pinned));
    }
}

public class EvidenceTimelineStyle_DisplayLabel_Tests
{
    [Theory]
    [InlineData("motion", "Motion")]
    [InlineData("Motion", "Motion")]
    [InlineData("Snapshot", "Snapshot")]
    [InlineData("", "")]
    public void DisplayLabel_CapitalizesFirstLetterOnly(string label, string expected)
    {
        var item = new EvidenceItem
        {
            Key = "media:x",
            Kind = EvidenceKind.Video,
            OccurredAtUtc = DateTime.UtcNow,
            DeviceId = Guid.NewGuid(),
            DeviceName = "Front Door",
            Media = null
        };

        Assert.Equal(expected, EvidenceTimelineStyle.Capitalize(label));
        Assert.Equal("Video", EvidenceTimelineStyle.DisplayLabel(item));
    }
}
