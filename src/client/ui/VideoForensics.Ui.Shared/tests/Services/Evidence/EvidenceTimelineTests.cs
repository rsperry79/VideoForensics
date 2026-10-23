namespace VideoForensics.Ui.Shared.Tests.Services.Evidence;

using Xunit;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Ui.Shared.Services.Evidence;

public class EvidenceTimeline_Group_Tests
{
    [Fact]
    public void Group_WithMultipleDays_GroupsByLocalDay()
    {
        // Arrange
        var tz = TimeZoneInfo.CreateCustomTimeZone("T-5", TimeSpan.FromHours(-5), "T-5", "T-5");

        var event1 = new Event
        {
            Id = Guid.NewGuid(),
            EventType = "Motion",
            OccurredAtUtc = new DateTime(2026, 9, 20, 4, 0, 0, DateTimeKind.Utc), // 2026-09-19 23:00 in T-5
            DiscoveredAtUtc = DateTime.UtcNow,
            ProviderEventId = "e1",
            MetadataJson = "{}"
        };

        var event2 = new Event
        {
            Id = Guid.NewGuid(),
            EventType = "Person",
            OccurredAtUtc = new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc), // 2026-09-20 07:00 in T-5
            DiscoveredAtUtc = DateTime.UtcNow,
            ProviderEventId = "e2",
            MetadataJson = "{}"
        };

        var item1 = new EvidenceItem
        {
            Key = "event:1",
            Kind = EvidenceKind.Event,
            OccurredAtUtc = event1.OccurredAtUtc,
            DeviceId = Guid.NewGuid(),
            DeviceName = "Front Door",
            Event = event1,
            Media = null
        };

        var item2 = new EvidenceItem
        {
            Key = "event:2",
            Kind = EvidenceKind.Event,
            OccurredAtUtc = event2.OccurredAtUtc,
            DeviceId = Guid.NewGuid(),
            DeviceName = "Front Door",
            Event = event2,
            Media = null
        };

        // Act
        var groups = EvidenceTimeline.Group(new[] { item1, item2 }, tz);

        // Assert
        Assert.Equal(2, groups.Count);
        // First group should be the later date
        Assert.Equal(new DateOnly(2026, 9, 20), groups[0].Day);
        Assert.Equal(new DateOnly(2026, 9, 19), groups[1].Day);
    }

    [Fact]
    public void Group_WithMultipleDevices_GroupsByDeviceWithinDay()
    {
        // Arrange
        var tz = TimeZoneInfo.Utc;
        var device1Id = Guid.NewGuid();
        var device2Id = Guid.NewGuid();

        var event1 = new Event
        {
            Id = Guid.NewGuid(),
            EventType = "Motion",
            OccurredAtUtc = new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc),
            DiscoveredAtUtc = DateTime.UtcNow,
            ProviderEventId = "e1",
            MetadataJson = "{}"
        };

        var event2 = new Event
        {
            Id = Guid.NewGuid(),
            EventType = "Person",
            OccurredAtUtc = new DateTime(2026, 9, 20, 13, 0, 0, DateTimeKind.Utc),
            DiscoveredAtUtc = DateTime.UtcNow,
            ProviderEventId = "e2",
            MetadataJson = "{}"
        };

        var item1 = new EvidenceItem
        {
            Key = "event:1",
            Kind = EvidenceKind.Event,
            OccurredAtUtc = event1.OccurredAtUtc,
            DeviceId = device1Id,
            DeviceName = "Front Door",
            Event = event1,
            Media = null
        };

        var item2 = new EvidenceItem
        {
            Key = "event:2",
            Kind = EvidenceKind.Event,
            OccurredAtUtc = event2.OccurredAtUtc,
            DeviceId = device2Id,
            DeviceName = "Back Yard",
            Event = event2,
            Media = null
        };

        // Act
        var groups = EvidenceTimeline.Group(new[] { item1, item2 }, tz);

        // Assert
        Assert.Single(groups);
        var dayGroup = groups[0];
        Assert.Equal(new DateOnly(2026, 9, 20), dayGroup.Day);
        Assert.Equal(2, dayGroup.Devices.Count);
        // Devices should be ordered by name
        Assert.Equal("Back Yard", dayGroup.Devices[0].DeviceName);
        Assert.Equal("Front Door", dayGroup.Devices[1].DeviceName);
    }

    [Fact]
    public void Group_ItemsWithinDay_SortedDescending()
    {
        // Arrange
        var tz = TimeZoneInfo.Utc;
        var deviceId = Guid.NewGuid();

        var event1 = new Event
        {
            Id = Guid.NewGuid(),
            EventType = "Motion",
            OccurredAtUtc = new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc),
            DiscoveredAtUtc = DateTime.UtcNow,
            ProviderEventId = "e1",
            MetadataJson = "{}"
        };

        var event2 = new Event
        {
            Id = Guid.NewGuid(),
            EventType = "Person",
            OccurredAtUtc = new DateTime(2026, 9, 20, 14, 0, 0, DateTimeKind.Utc),
            DiscoveredAtUtc = DateTime.UtcNow,
            ProviderEventId = "e2",
            MetadataJson = "{}"
        };

        var item1 = new EvidenceItem
        {
            Key = "event:1",
            Kind = EvidenceKind.Event,
            OccurredAtUtc = event1.OccurredAtUtc,
            DeviceId = deviceId,
            DeviceName = "Front Door",
            Event = event1,
            Media = null
        };

        var item2 = new EvidenceItem
        {
            Key = "event:2",
            Kind = EvidenceKind.Event,
            OccurredAtUtc = event2.OccurredAtUtc,
            DeviceId = deviceId,
            DeviceName = "Front Door",
            Event = event2,
            Media = null
        };

        // Act
        var groups = EvidenceTimeline.Group(new[] { item1, item2 }, tz);

        // Assert
        Assert.Single(groups);
        var dayGroup = groups[0];
        Assert.Single(dayGroup.Devices);
        var deviceGroup = dayGroup.Devices[0];
        // Items should be descending (newer first)
        Assert.Equal(2, deviceGroup.Items.Count);
        Assert.Equal(event2.OccurredAtUtc, deviceGroup.Items[0].OccurredAtUtc);
        Assert.Equal(event1.OccurredAtUtc, deviceGroup.Items[1].OccurredAtUtc);
    }

    [Fact]
    public void Group_CalculatesTotalCount()
    {
        // Arrange
        var tz = TimeZoneInfo.Utc;
        var deviceId = Guid.NewGuid();

        var items = Enumerable.Range(0, 5)
            .Select(i => new EvidenceItem
            {
                Key = $"event:{i}",
                Kind = EvidenceKind.Event,
                OccurredAtUtc = new DateTime(2026, 9, 20, 12, i, 0, DateTimeKind.Utc),
                DeviceId = deviceId,
                DeviceName = "Front Door",
                Event = null,
                Media = null
            })
            .ToList();

        // Act
        var groups = EvidenceTimeline.Group(items, tz);

        // Assert
        Assert.Single(groups);
        var dayGroup = groups[0];
        Assert.Equal(5, dayGroup.TotalCount);
    }
}

public class EvidenceTimeline_Neighbours_Tests
{
    private List<EvidenceItem> CreateOrderedItems(int count)
    {
        var deviceId = Guid.NewGuid();
        var items = new List<EvidenceItem>();
        for (int i = 0; i < count; i++)
        {
            items.Add(new EvidenceItem
            {
                Key = $"item:{i}",
                Kind = EvidenceKind.Event,
                OccurredAtUtc = new DateTime(2026, 9, 20, 12, count - i, 0, DateTimeKind.Utc), // Descending
                DeviceId = deviceId,
                DeviceName = "Front Door",
                Event = i % 2 == 0 ? new Event
                {
                    Id = Guid.NewGuid(),
                    EventType = "Motion",
                    OccurredAtUtc = new DateTime(2026, 9, 20, 12, count - i, 0, DateTimeKind.Utc),
                    DiscoveredAtUtc = DateTime.UtcNow,
                    ProviderEventId = $"e{i}",
                    MetadataJson = "{}"
                } : null,
                Media = null
            });
        }
        return items;
    }

    [Fact]
    public void Neighbours_AtStart_ReturnsPreviousNullAndNext()
    {
        // Arrange
        var items = CreateOrderedItems(3);

        // Act
        var (prev, next) = EvidenceTimeline.Neighbours(items, items[0].Key, false);

        // Assert
        Assert.Null(prev);
        Assert.Equal(items[1].Key, next?.Key);
    }

    [Fact]
    public void Neighbours_AtEnd_ReturnsPreviousAndNextNull()
    {
        // Arrange
        var items = CreateOrderedItems(3);

        // Act
        var (prev, next) = EvidenceTimeline.Neighbours(items, items[2].Key, false);

        // Assert
        Assert.Equal(items[1].Key, prev?.Key);
        Assert.Null(next);
    }

    [Fact]
    public void Neighbours_InMiddle_ReturnsBoth()
    {
        // Arrange
        var items = CreateOrderedItems(3);

        // Act
        var (prev, next) = EvidenceTimeline.Neighbours(items, items[1].Key, false);

        // Assert
        Assert.Equal(items[0].Key, prev?.Key);
        Assert.Equal(items[2].Key, next?.Key);
    }

    [Fact]
    public void Neighbours_WithViewableOnlyAndAllViewable_ReturnsBoth()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var items = new List<EvidenceItem>
        {
            new EvidenceItem
            {
                Key = "media:1",
                Kind = EvidenceKind.Snapshot,
                OccurredAtUtc = new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc),
                DeviceId = deviceId,
                DeviceName = "Front Door",
                Event = null,
                Media = new MediaItem
                {
                    Id = Guid.NewGuid(),
                    DeviceId = deviceId,
                    FileName = "snap1.jpg",
                    FilePath = "/snap1.jpg",
                    MediaFormat = "image/jpeg",
                    Sha256Hash = "hash1",
                    RecordedAtUtc = DateTime.UtcNow,
                    DownloadedAtUtc = DateTime.UtcNow
                }
            },
            new EvidenceItem
            {
                Key = "media:2",
                Kind = EvidenceKind.Snapshot,
                OccurredAtUtc = new DateTime(2026, 9, 20, 12, 1, 0, DateTimeKind.Utc),
                DeviceId = deviceId,
                DeviceName = "Front Door",
                Event = null,
                Media = new MediaItem
                {
                    Id = Guid.NewGuid(),
                    DeviceId = deviceId,
                    FileName = "snap2.jpg",
                    FilePath = "/snap2.jpg",
                    MediaFormat = "image/jpeg",
                    Sha256Hash = "hash2",
                    RecordedAtUtc = DateTime.UtcNow,
                    DownloadedAtUtc = DateTime.UtcNow
                }
            },
            new EvidenceItem
            {
                Key = "media:3",
                Kind = EvidenceKind.Snapshot,
                OccurredAtUtc = new DateTime(2026, 9, 20, 12, 2, 0, DateTimeKind.Utc),
                DeviceId = deviceId,
                DeviceName = "Front Door",
                Event = null,
                Media = new MediaItem
                {
                    Id = Guid.NewGuid(),
                    DeviceId = deviceId,
                    FileName = "snap3.jpg",
                    FilePath = "/snap3.jpg",
                    MediaFormat = "image/jpeg",
                    Sha256Hash = "hash3",
                    RecordedAtUtc = DateTime.UtcNow,
                    DownloadedAtUtc = DateTime.UtcNow
                }
            }
        };

        // Act
        var (prev, next) = EvidenceTimeline.Neighbours(items, items[1].Key, viewableOnly: true);

        // Assert
        Assert.Equal(items[0].Key, prev?.Key);
        Assert.Equal(items[2].Key, next?.Key);
    }

    [Fact]
    public void Neighbours_WithViewableOnlyAndNonViewable_SkipsNonViewable()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var items = new List<EvidenceItem>
        {
            new EvidenceItem
            {
                Key = "media:1",
                Kind = EvidenceKind.Snapshot,
                OccurredAtUtc = new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc),
                DeviceId = deviceId,
                DeviceName = "Front Door",
                Event = null,
                Media = new MediaItem
                {
                    Id = Guid.NewGuid(),
                    DeviceId = deviceId,
                    FileName = "snap1.jpg",
                    FilePath = "/snap1.jpg",
                    MediaFormat = "image/jpeg",
                    Sha256Hash = "hash1",
                    RecordedAtUtc = DateTime.UtcNow,
                    DownloadedAtUtc = DateTime.UtcNow
                }
            },
            new EvidenceItem
            {
                Key = "event:nonviewable",
                Kind = EvidenceKind.Event,
                OccurredAtUtc = new DateTime(2026, 9, 20, 12, 1, 0, DateTimeKind.Utc),
                DeviceId = deviceId,
                DeviceName = "Front Door",
                Event = new Event
                {
                    Id = Guid.NewGuid(),
                    EventType = "Motion",
                    OccurredAtUtc = new DateTime(2026, 9, 20, 12, 1, 0, DateTimeKind.Utc),
                    DiscoveredAtUtc = DateTime.UtcNow,
                    ProviderEventId = "e1",
                    MetadataJson = "{}"
                },
                Media = null // No media = not viewable
            },
            new EvidenceItem
            {
                Key = "media:3",
                Kind = EvidenceKind.Snapshot,
                OccurredAtUtc = new DateTime(2026, 9, 20, 12, 2, 0, DateTimeKind.Utc),
                DeviceId = deviceId,
                DeviceName = "Front Door",
                Event = null,
                Media = new MediaItem
                {
                    Id = Guid.NewGuid(),
                    DeviceId = deviceId,
                    FileName = "snap3.jpg",
                    FilePath = "/snap3.jpg",
                    MediaFormat = "image/jpeg",
                    Sha256Hash = "hash3",
                    RecordedAtUtc = DateTime.UtcNow,
                    DownloadedAtUtc = DateTime.UtcNow
                }
            }
        };

        // Act
        var (prev, next) = EvidenceTimeline.Neighbours(items, items[1].Key, viewableOnly: true);

        // Assert
        Assert.Equal(items[0].Key, prev?.Key);
        Assert.Equal(items[2].Key, next?.Key);
    }
}
