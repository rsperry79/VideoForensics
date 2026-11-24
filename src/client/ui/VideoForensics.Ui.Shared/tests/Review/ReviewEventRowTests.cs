using VideoForensics.Client.Ui.Shared.Contracts;
using VideoForensics.Client.Ui.Shared.Services.Review;
using VideoForensics.Client.Ui.Shared.Services.Signal;
using VideoForensics.Core.Api.Contracts;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Review;

/// <summary>The merge model behind the review Events tab: camera events, jamming, signal anomalies and bit-rate flags in one newest-first list.</summary>
public class ReviewEventRowTests
{
    private static readonly DateTime T0 = new(2026, 11, 20, 10, 0, 0, DateTimeKind.Utc);
    private static readonly Guid DeviceA = Guid.Parse("00000000-0000-0000-0000-00000000000a");
    private static readonly Guid DeviceB = Guid.Parse("00000000-0000-0000-0000-00000000000b");

    private static EventDto Event(Guid id, Guid deviceId, DateTime occurredAtUtc)
    {
        return new EventDto(
            Id: id,
            DeviceId: deviceId,
            ProviderEventId: "provider-" + id,
            EventType: "motion",
            OccurredAtUtc: occurredAtUtc,
            SnapshotUrl: null,
            MetadataJson: null,
            DiscoveredAtUtc: occurredAtUtc,
            DownloadedAtUtc: null,
            ApiSourceHash: null,
            EventIntegrityHash: null);
    }

    private static JammingIncidentDto Jamming(Guid id, Guid deviceId, DateTime startUtc, DateTime endUtc)
    {
        return new JammingIncidentDto(
            Id: id,
            DeviceId: deviceId,
            StartUtc: startUtc,
            EndUtc: endUtc,
            AffectedEventCount: 3,
            AverageDegradationDb: 4.5,
            Confidence: "High",
            DetectedAtUtc: startUtc,
            Notes: null,
            Source: "AutoDetected",
            CaseId: null);
    }

    private static ReviewAnomaly Anomaly(Guid deviceId, DateTime startUtc, DateTime endUtc)
    {
        return new ReviewAnomaly(deviceId, startUtc, endUtc, default(SignalDeviationLevel));
    }

    private static ReviewBitRateFlag BitRate(Guid flagId, Guid deviceId, DateTime startUtc, DateTime endUtc)
    {
        return new ReviewBitRateFlag(flagId, deviceId, startUtc, endUtc, "LowBitRate", null);
    }

    [Fact]
    public void ReviewEventRows_Merge_NoInputs_ReturnsEmpty()
    {
        // Act
        IReadOnlyList<ReviewEventRow> rows = ReviewEventRows.Merge(
            Array.Empty<EventDto>(),
            Array.Empty<JammingIncidentDto>(),
            Array.Empty<ReviewAnomaly>(),
            Array.Empty<ReviewBitRateFlag>());

        // Assert
        Assert.Empty(rows);
    }

    [Fact]
    public void ReviewEventRows_Merge_OneOfEachKind_ReturnsFourRowsNewestFirst()
    {
        // Arrange
        EventDto camera = Event(Guid.NewGuid(), DeviceA, T0);
        JammingIncidentDto jamming = Jamming(Guid.NewGuid(), DeviceA, T0.AddHours(1), T0.AddHours(1).AddMinutes(5));
        ReviewAnomaly anomaly = Anomaly(DeviceA, T0.AddHours(-1), T0.AddHours(-1).AddHours(1));
        ReviewBitRateFlag bitRate = BitRate(Guid.NewGuid(), DeviceA, T0.AddHours(2), T0.AddHours(2).AddMinutes(10));

        // Act
        IReadOnlyList<ReviewEventRow> rows = ReviewEventRows.Merge(
            new[] { camera },
            new[] { jamming },
            new[] { anomaly },
            new[] { bitRate });

        // Assert
        Assert.Equal(4, rows.Count);
        Assert.Equal(
            new[] { ReviewEventRowKind.BitRate, ReviewEventRowKind.Jamming, ReviewEventRowKind.CameraEvent, ReviewEventRowKind.SignalAnomaly },
            rows.Select(r => r.Kind).ToArray());
        Assert.Equal(
            new[] { bitRate.StartUtc, jamming.StartUtc, camera.OccurredAtUtc, anomaly.StartUtc },
            rows.Select(r => r.StartUtc).ToArray());
    }

    [Fact]
    public void ReviewEventRows_Merge_EqualStartUtc_OrdersByKindThenDeviceId()
    {
        // Arrange: four rows at the same instant, all on device A, passed in reverse kind order.
        DateTime same = T0;
        EventDto camera = Event(Guid.NewGuid(), DeviceA, same);
        JammingIncidentDto jamming = Jamming(Guid.NewGuid(), DeviceA, same, same.AddMinutes(1));
        ReviewAnomaly anomaly = Anomaly(DeviceA, same, same.AddHours(1));
        ReviewBitRateFlag bitRate = BitRate(Guid.NewGuid(), DeviceA, same, same.AddMinutes(1));

        // Act
        IReadOnlyList<ReviewEventRow> rows = ReviewEventRows.Merge(
            new[] { camera },
            new[] { jamming },
            new[] { anomaly },
            new[] { bitRate });

        // Assert
        Assert.Equal(
            new[] { ReviewEventRowKind.CameraEvent, ReviewEventRowKind.Jamming, ReviewEventRowKind.SignalAnomaly, ReviewEventRowKind.BitRate },
            rows.Select(r => r.Kind).ToArray());
    }

    [Fact]
    public void ReviewEventRows_Merge_EqualStartUtcAndKind_OrdersByDeviceId()
    {
        // Arrange: two anomalies at the same instant, given device B first.
        DateTime same = T0;
        ReviewAnomaly onB = Anomaly(DeviceB, same, same.AddHours(1));
        ReviewAnomaly onA = Anomaly(DeviceA, same, same.AddHours(1));

        // Act
        IReadOnlyList<ReviewEventRow> rows = ReviewEventRows.Merge(
            Array.Empty<EventDto>(),
            Array.Empty<JammingIncidentDto>(),
            new[] { onB, onA },
            Array.Empty<ReviewBitRateFlag>());

        // Assert
        Assert.Equal(new[] { DeviceA, DeviceB }, rows.Select(r => r.DeviceId).ToArray());
    }

    [Fact]
    public void ReviewEventRows_Merge_CameraEvent_CarriesEventIdAndTheSourceDto()
    {
        // Arrange
        EventDto camera = Event(Guid.NewGuid(), DeviceA, T0);

        // Act
        IReadOnlyList<ReviewEventRow> rows = ReviewEventRows.Merge(
            new[] { camera },
            Array.Empty<JammingIncidentDto>(),
            Array.Empty<ReviewAnomaly>(),
            Array.Empty<ReviewBitRateFlag>());

        // Assert
        ReviewEventRow row = Assert.Single(rows);
        Assert.Equal(camera.Id, row.EventId);
        Assert.Same(camera, row.Source);
        Assert.Equal(camera.OccurredAtUtc, row.StartUtc);
        Assert.Equal(DeviceA, row.DeviceId);
    }

    [Fact]
    public void ReviewEventRows_Merge_NonCameraKinds_HaveNoEventId()
    {
        // Arrange
        JammingIncidentDto jamming = Jamming(Guid.NewGuid(), DeviceA, T0, T0.AddMinutes(5));
        ReviewAnomaly anomaly = Anomaly(DeviceA, T0.AddHours(1), T0.AddHours(2));
        ReviewBitRateFlag bitRate = BitRate(Guid.NewGuid(), DeviceA, T0.AddHours(3), T0.AddHours(3).AddMinutes(1));

        // Act
        IReadOnlyList<ReviewEventRow> rows = ReviewEventRows.Merge(
            Array.Empty<EventDto>(),
            new[] { jamming },
            new[] { anomaly },
            new[] { bitRate });

        // Assert
        Assert.All(rows, r => Assert.Null(r.EventId));
    }

    [Fact]
    public void ReviewEventRows_Merge_CameraEvent_HasNullEndUtc()
    {
        // Arrange
        EventDto camera = Event(Guid.NewGuid(), DeviceA, T0);

        // Act
        IReadOnlyList<ReviewEventRow> rows = ReviewEventRows.Merge(
            new[] { camera },
            Array.Empty<JammingIncidentDto>(),
            Array.Empty<ReviewAnomaly>(),
            Array.Empty<ReviewBitRateFlag>());

        // Assert
        Assert.Null(Assert.Single(rows).EndUtc);
    }

    [Fact]
    public void ReviewEventRows_Merge_NonCameraKinds_TakeEndUtcFromTheirSource()
    {
        // Arrange
        JammingIncidentDto jamming = Jamming(Guid.NewGuid(), DeviceA, T0, T0.AddMinutes(5));
        ReviewAnomaly anomaly = Anomaly(DeviceA, T0.AddHours(1), T0.AddHours(2));
        ReviewBitRateFlag bitRate = BitRate(Guid.NewGuid(), DeviceA, T0.AddHours(3), T0.AddHours(3).AddMinutes(1));

        // Act
        IReadOnlyList<ReviewEventRow> rows = ReviewEventRows.Merge(
            Array.Empty<EventDto>(),
            new[] { jamming },
            new[] { anomaly },
            new[] { bitRate });

        // Assert
        Assert.All(rows, r => Assert.NotNull(r.EndUtc));
        Assert.Equal(jamming.EndUtc, rows.Single(r => r.Kind == ReviewEventRowKind.Jamming).EndUtc);
        Assert.Equal(anomaly.EndUtc, rows.Single(r => r.Kind == ReviewEventRowKind.SignalAnomaly).EndUtc);
        Assert.Equal(bitRate.EndUtc, rows.Single(r => r.Kind == ReviewEventRowKind.BitRate).EndUtc);
    }
}
